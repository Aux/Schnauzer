using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Schnauzer.Data.Models;
using Schnauzer.Utility;

namespace Schnauzer.Services;

/// <summary>
///     Dynamic channel manager
/// </summary>
public class ChannelManager(
    ILogger<ChannelManager> logger,
    DiscordSocketClient discord,
    LocalizationProvider localizer,
    GracePeriodService gracePeriod,
    ChannelCache channels)
{
    /// <summary>
    ///     Actions to be taken when a user joins a voice channel
    /// </summary>
    public async Task HandleChannelJoinAsync(Guild config, SocketGuildUser user, SocketVoiceState state)
    {
        // Don't do anything for non-create channel joins
        if (state.VoiceChannel.Id != config.CreateChannelId)
            return;

        // Get the server's preferred language
        var locale = localizer.GetLocale(config.PreferredLocale);
        
        // Don't allow deafened users to own a channel
        if ((config.DenyDeafenedOwnership ?? true) && state.IsDeafened)
        {
            await user.ModifyAsync(x => x.ChannelId = null,
                new RequestOptions() { AuditLogReason = locale.Get("log:deny_deafened_ownership") });
            return;
        }

        // Don't allow muted users to own a channel
        if ((config.DenyMutedOwnership ?? true) && state.IsMuted)
        {
            await user.ModifyAsync(x => x.ChannelId = null,
                new RequestOptions() { AuditLogReason = locale.Get("log:deny_muted_ownership") });
            return;
        }

        logger.LogInformation("User {UserId} joined create channel {ChannelId} in {GuildId}", 
            user.Id, state.VoiceChannel.Id, user.Guild.Id);

        // Create channel joins should be managed by permissions, but
        // just in case that doesn't happen check if the user has owner roles
        if (config.CanOwnRoleIds is not null && config.CanOwnRoleIds.Count > 0)
        {
            var roles = user.Roles.Select(x => x.Id)
                .Intersect(config.CanOwnRoleIds);

            if (!roles.Any())
                await user.ModifyAsync(x => x.ChannelId = null,
                    new RequestOptions() { AuditLogReason = locale.Get("log:no_owner_roles") });
        }

        // Get the user's existing dynamic channel or create one
        var channel = await channels.GetByOwnerAsync(user.Id);
        if (channel is null)
        {
            // Check if the user's name contains automod blocked terms
            if (config.IsAutoModEnabled ?? false && config.AutomodRuleIds?.Count != 0)
            {
                var result = AutoModHelper.IsBlocked(user.DisplayName, user, config);
                if (result.IsBlocked)
                {
                    await user.KickAsync(locale.Get("log:blocked_channel_create", result.Rule.Name, result.Keyword));
                    return;
                }
            }

            var create = user.Guild.GetVoiceChannel(config.CreateChannelId.Value);
            var perms = new List<Overwrite>(create.Category.PermissionOverwrites)
            {
                new(user.Id, PermissionTarget.User, new OverwritePermissions(
                    moveMembers: PermValue.Allow, muteMembers: PermValue.Allow, deafenMembers: PermValue.Allow,
                    prioritySpeaker: PermValue.Allow, useVoiceActivation: PermValue.Allow))
            };

            // Create the channel in discord
            var voice = await user.Guild.CreateVoiceChannelAsync($"{user.DisplayName}'s channel", p =>
            {
                p.CategoryId = create.CategoryId;
                p.Position = create.Position + 1;
                p.PermissionOverwrites = perms;
                p.UserLimit = config.DefaultLobbySize ?? 4;
            },
            new RequestOptions { AuditLogReason = locale.Get("log:create_new_channel", user.Username, user.Id) });

            // Save the channel to the database
            channel = new Channel
            {
                Id = voice.Id,
                GuildId = user.Guild.Id,
                CreatorId = user.Id,
                OwnerId = user.Id
            };

            await channels.TryCreateAsync(channel);
        }

        // Can't move someone to a voice channel in another server, so disconnect them
        if (channel.GuildId != state.VoiceChannel.Guild.Id)
        {
            await user.ModifyAsync(x => x.ChannelId = null, 
                new RequestOptions() { AuditLogReason = locale.Get("log:other_server_channel", channel.GuildId) });
        }

        // Move the user (owner) to the dynamic channel
        await user.ModifyAsync(x =>  x.ChannelId = channel.Id);

        // Remove the abandoned timer if one exists
        gracePeriod.TryStopTimer(state.VoiceChannel, user);

        // Create the owner panel message if it doesn't exist
        if (channel.PanelMessageId == null)
            await CreateOrModifyPanelAsync(channel, user, locale);
    }

    /// <summary>
    ///     Actions to be taken when a user leaves a voice channel
    /// </summary>
    public async Task HandleChannelLeaveAsync(Guild config, SocketGuildUser user, SocketVoiceState state)
    {
        // Ignore if it's not a dynamic channel
        if (!await channels.ExistsAsync(state.VoiceChannel.Id))
            return;

        logger.LogInformation("User {UserId} left dynamic channel {ChannelId} in {GuildId}",
            user.Id, state.VoiceChannel.Id, user.Guild.Id);

        // Get the server's preferred language
        var locale = localizer.GetLocale(config.PreferredLocale);

        // Get channel config
        var channel = await channels.GetByOwnerAsync(user.Id);

        // Channel is empty
        if (state.VoiceChannel.ConnectedUsers.Count == 0)
        {
            // Clear any orphan timers before deletion
            gracePeriod.TryStopTimer(state.VoiceChannel, user);

            await state.VoiceChannel.DeleteAsync(new()
            {
                AuditLogReason = locale.Get("log:delete_empty_channel")
            });

            await channels.DeleteAsync(user.Id);
            return;
        }

        // The dynamic channel is orphaned
        if (channel.OwnerId == user.Id)
        {
            gracePeriod.TryStartTimer(state.VoiceChannel, user, locale, config.AbandonedGracePeriod ?? TimeSpan.FromSeconds(30));
            return;
        }
    }

    /// <summary>
    ///     Create or modify a dynamic channel panel message
    /// </summary>
    public async Task CreateOrModifyPanelAsync(Channel channel, IGuildUser user, Locale locale)
    {
        // Get voice commands to mention
        var commands = await user.Guild.GetApplicationCommandsAsync();
        var voiceCmds = commands.SingleOrDefault(x => x.Name.StartsWith("voice"));
        
        // Create interaction buttons
        var renameButton = new ButtonBuilder()
            .WithCustomId("rename_button:" + channel.Id)
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(new Emoji("✏️"))
            .WithLabel(locale.Get("voicepanel:rename_button_name"));
        var limitButton = new ButtonBuilder()
            .WithCustomId("limit_button:" + channel.Id)
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(new Emoji("💺"))
            .WithLabel(locale.Get("voicepanel:limit_button_name"));
        var localeButton = new ButtonBuilder()
            .WithCustomId("locale_button:" + channel.Id)
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(new Emoji("🌎"))
            .WithLabel(locale.Get("voicepanel:locale_button_name"));

        var kickButton = new ButtonBuilder()
            .WithCustomId("kick_button:" + channel.Id)
            .WithStyle(ButtonStyle.Danger)
            .WithEmote(new Emoji("🍃"))
            .WithLabel("Kick");
        var blockButton = new ButtonBuilder()
            .WithCustomId("block_button:" + channel.Id)
            .WithStyle(ButtonStyle.Danger)
            .WithEmote(new Emoji("🔨"))
            .WithLabel("Block");
        var unblockButton = new ButtonBuilder()
            .WithCustomId("unblock_button:" + channel.Id)
            .WithStyle(ButtonStyle.Danger)
            .WithEmote(new Emoji("🙏"))
            .WithLabel("Unblock");
        var transferButton = new ButtonBuilder()
            .WithCustomId("transfer_button:" + channel.Id)
            .WithStyle(ButtonStyle.Success)
            .WithEmote(new Emoji("🥏"))
            .WithLabel("Transfer");

        // Create components panel
        var builder = new ComponentBuilderV2()
            .WithContainer(new ContainerBuilder()
                .WithSection(new SectionBuilder()
                    .WithAccessory(new ThumbnailBuilder(new UnfurledMediaItemProperties(user.GetGuildAvatarUrl() ?? user.GetAvatarUrl())))
                    .WithTextDisplay($"## {locale.Get("voicepanel:panel_title")}\n" +
                                     $"**{locale.Get("voicepanel:owner_field")}:** {user.Mention}\n" +
                                     $"**{locale.Get("voicepanel:locale_field")}:** {locale.Culture.DisplayName}\n" +
                                     $"### {locale.Get("voicepanel:commands_field")}\n" +
                            $"{string.Join(" ", voiceCmds?.Options.Select(x => $"</{voiceCmds.Name} {x.Name}:{voiceCmds.Id}>")) ?? "*none*"}"))
                .WithSeparator()
                .WithActionRow(new ActionRowBuilder()
                    .WithButton(renameButton)
                    .WithButton(limitButton)
                    .WithButton(localeButton))
                .WithActionRow(new ActionRowBuilder()
                    .WithButton(kickButton)
                    .WithButton(blockButton)
                    .WithButton(unblockButton)
                    .WithButton(transferButton))
            );
        
        // Send or modify panel message
        var voice = (IVoiceChannel)await discord.GetChannelAsync(channel.Id);
        if (channel.PanelMessageId == null)
        {
            var msg = await voice.SendMessageAsync(components: builder.Build(), allowedMentions: AllowedMentions.None);

            channel.PanelMessageId = msg.Id;
            await channels.ModifyAsync(channel);
        } else
        {
            var msg = await voice.GetMessageAsync(channel.PanelMessageId.Value) as IUserMessage;
            await msg.ModifyAsync(x => x.Components = builder.Build());
        }
    }
}
