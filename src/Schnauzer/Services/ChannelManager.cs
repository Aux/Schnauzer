﻿using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Schnauzer.Data.Models;
using Schnauzer.Utility;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;

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

        logger.LogInformation("User {userName} ({userId}) joined create channel {createId} in guild {guildName} ({guildId})",
            user.Username, user.Id, config.CreateChannelId, user.Guild.Name, user.Guild.Id);

        // Get the server's preferred language
        var locale = localizer.GetLocale(config.PreferredLocale);
        logger.LogInformation("Got locale {cultureCode} for guild {guildName} ({guildId})", 
            locale.Culture.TwoLetterISOLanguageName, user.Guild.Name, user.Guild.Id);
        
        // Don't allow deafened users to own a channel
        if ((config.DenyDeafenedOwnership ?? true) && state.IsDeafened)
        {
            await user.ModifyAsync(x => x.ChannelId = null,
                new RequestOptions() { AuditLogReason = locale.Get("log:deny_deafened_ownership") });
            logger.LogInformation("Removed user {userName} ({userId}) in guild {guildName} ({guildId}), reason: Deny deafened user ownership",
                user.Username, user.Id, user.Guild.Name, user.Guild.Id);
            return;
        }

        // Don't allow muted users to own a channel
        if ((config.DenyMutedOwnership ?? true) && state.IsMuted)
        {
            await user.ModifyAsync(x => x.ChannelId = null,
                new RequestOptions() { AuditLogReason = locale.Get("log:deny_muted_ownership") });
            logger.LogInformation("Removed user {userName} ({userId}) in guild {guildName} ({guildId}), reason: Deny muted user ownership",
                user.Username, user.Id, user.Guild.Name, user.Guild.Id);
            return;
        }

        // Create channel joins should be managed by permissions, but
        // just in case that doesn't happen check if the user has owner roles
        if (config.CanOwnRoleIds is not null && config.CanOwnRoleIds.Count > 0)
        {
            var roles = user.Roles.Select(x => x.Id)
                .Intersect(config.CanOwnRoleIds);

            if (!roles.Any())
            {
                await user.ModifyAsync(x => x.ChannelId = null,
                    new RequestOptions() { AuditLogReason = locale.Get("log:no_owner_roles") });
                logger.LogInformation("Removed user {userName} ({userId}) in guild {guildName} ({guildId}), reason: Does not have ownership roles",
                    user.Username, user.Id, user.Guild.Name, user.Guild.Id);
                return;
            }
        }

        // Get the user's dynamic channel if it exists
        var channel = await channels.GetByOwnerAsync(user.Id);
        if (channel is not null)
        {
            var voice = user.Guild.GetVoiceChannel(channel.Id);

            // If we can't find the voice channel it's probably a
            // forgotten channel, remove it from the cache and db
            if (voice is null)  
            {
                await channels.DeleteAsync(user.Id);
                logger.LogInformation("Removed an orphaned dynamic channel ({channelId}) from the db in guild {guildName} ({guildId})",
                    channel.Id, user.Guild.Name, user.Guild.Id);
                channel = null;
            }
        }

        // Create a new channel for the user
        if (channel is null)
        {
            // Check if the user's name contains automod blocked terms
            if (config.IsAutoModEnabled ?? false && config.AutomodRuleIds?.Count != 0)
            {
                var result = AutoModHelper.IsBlocked(user.DisplayName, user, config);
                if (result.IsBlocked)
                {
                    await user.ModifyAsync(x => x.Channel = null,
                        new RequestOptions { AuditLogReason = locale.Get("log:blocked_channel_create", result.Rule.Name, result.Keyword) });
                    logger.LogInformation("Removed user {userName} ({userId}) in guild {guildName} ({guildId}), reason: Blocked by automod rule {ruleName} ({ruleId})",
                        user.Username, user.Id, user.Guild.Name, user.Guild.Id, result.Rule.Name, result.Rule.Id);

                    if (config.AutoModLogChannelId is not null)
                    {
                        var logTo = user.Guild.GetTextChannel(config.AutoModLogChannelId.Value);
                        if (logTo is not null)
                        {
                            var embed = new EmbedBuilder()
                                .WithColor(Color.Red)
                                .WithTitle("Blocked Channel Create")
                                .WithThumbnailUrl(user.GetAvatarUrl())
                                .WithDescription($"> **User:** {user.Mention} (@{user.Username})\n" +
                                                 $"> **Channel:** {state.VoiceChannel.Mention} ({state.VoiceChannel.Name})\n" +
                                                 $"> **Rule:** {result.Rule.Name}\n" +
                                                 $"> **Keyword:** `{result.Keyword}`\n" +
                                                 $"> **Blocked Text:** `{user.DisplayName}`")
                                .WithCurrentTimestamp();
                            await logTo.SendMessageAsync(embed: embed.Build());
                        }
                    }

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

        try
        {
            // Move the user (owner) to the dynamic channel
            await user.ModifyAsync(x => x.ChannelId = channel.Id);
        } catch (HttpException ex)
        {
            // User left create before the process finished.
            if (ex.DiscordCode == DiscordErrorCode.TargetUserNotInVoice)
            {
                logger.LogInformation("A user {userName} ({userId}) in guild {guildName} ({guildId}) left the create channel before the process finished.",
                    user.Username, user.Id, user.Guild.Name, user.Guild.Id);
                return;
            }
        }

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

        logger.LogInformation("User {userName} ({userId}) left dynamic channel {channelName} ({channelId}) in guild {guildName} ({guildId})",
            user.Username, user.Id, state.VoiceChannel.Name, state.VoiceChannel.Id, user.Guild.Name, user.Guild.Id);

        // Get the server's preferred language
        var locale = localizer.GetLocale(config.PreferredLocale);
        logger.LogInformation("Got locale {cultureCode} for guild {guildName} ({guildId})",
            locale.Culture.TwoLetterISOLanguageName, user.Guild.Name, user.Guild.Id);

        // Get channel config
        var channel = await channels.GetAsync(state.VoiceChannel.Id);

        // Channel is empty
        if (state.VoiceChannel.ConnectedUsers.Count == 0)
        {
            // Clear any orphan timers before deletion
            gracePeriod.TryStopTimer(state.VoiceChannel, user);

            try
            {
                await state.VoiceChannel.DeleteAsync(new()
                {
                    AuditLogReason = locale.Get("log:delete_empty_channel")
                });

                await channels.DeleteAsync(user.Id);
                logger.LogInformation("Deleting empty dynamic channel {channelName} ({channelId}) in guild {guildName} ({guildId})",
                    state.VoiceChannel.Name, state.VoiceChannel.Id, user.Guild.Name, user.Guild.Id);
            } catch (HttpException ex)
            {
                // Channel was manually deleted, handled in VoiceStateService
                if (ex.DiscordCode == DiscordErrorCode.UnknownChannel)
                    return;
            }
            return;
        }

        // The dynamic channel is orphaned
        if (channel.OwnerId == user.Id)
        {
            gracePeriod.TryStartTimer(state.VoiceChannel, user, locale, config.AbandonedGracePeriod ?? GracePeriodService.DefaultDuration);
            logger.LogInformation("Started grace period timer for dynamic channel {channelName} ({channelId}) in guild {guildName} ({guildId})",
                state.VoiceChannel.Name, state.VoiceChannel.Id, user.Guild.Name, user.Guild.Id);
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
            .WithLabel(locale.Get("panel:rename_button_name"));
        var limitButton = new ButtonBuilder()
            .WithCustomId("limit_button:" + channel.Id)
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(new Emoji("💺"))
            .WithLabel(locale.Get("panel:limit_button_name"));
        var localeButton = new ButtonBuilder()
            .WithCustomId("locale_button:" + channel.Id)
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(new Emoji("🌎"))
            .WithLabel(locale.Get("panel:locale_button_name"));

        var kickButton = new ButtonBuilder()
            .WithCustomId("kick_button:" + channel.Id)
            .WithStyle(ButtonStyle.Danger)
            .WithEmote(new Emoji("🍃"))
            .WithLabel(locale.Get("panel:kick_button_name"));
        var blockButton = new ButtonBuilder()
            .WithCustomId("block_button:" + channel.Id)
            .WithStyle(ButtonStyle.Danger)
            .WithEmote(new Emoji("🔨"))
            .WithLabel(locale.Get("panel:block_button_name"));
        var unblockButton = new ButtonBuilder()
            .WithCustomId("unblock_button:" + channel.Id)
            .WithStyle(ButtonStyle.Danger)
            .WithEmote(new Emoji("🙏"))
            .WithLabel(locale.Get("panel:unblock_button_name"));
        var transferButton = new ButtonBuilder()
            .WithCustomId("transfer_button:" + channel.Id)
            .WithStyle(ButtonStyle.Success)
            .WithEmote(new Emoji("🥏"))
            .WithLabel(locale.Get("panel:transfer_button_name"));

        // Create components panel
        var builder = new ComponentBuilderV2()
            .WithContainer(new ContainerBuilder()
                .WithSection(new SectionBuilder()
                    .WithAccessory(new ThumbnailBuilder(new UnfurledMediaItemProperties(user.GetGuildAvatarUrl() ?? user.GetAvatarUrl())))
                    .WithTextDisplay($"## {locale.Get("panel:title")}\n" +
                                     $"**{locale.Get("panel:owner_field")}:** {user.Mention}\n" +
                                     $"**{locale.Get("panel:locale_field")}:** {locale.Culture.DisplayName}\n" +
                                     $"### {locale.Get("panel:commands_field")}\n" +
                            $"{string.Join(" ", voiceCmds?.Options.Select(x => $"</{voiceCmds.Name} {x.Name}:{voiceCmds.Id}>")) ?? "*none*"}"))
                .WithSeparator()
                .WithActionRow(new ActionRowBuilder()
                    .WithButton(renameButton)
                    .WithButton(limitButton)
                    .WithButton(localeButton))
                .WithActionRow(new ActionRowBuilder()
                    //.WithButton(kickButton)
                    //.WithButton(blockButton)
                    //.WithButton(unblockButton)
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
