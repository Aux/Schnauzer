using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Schnauzer.Discord.Interactions;

// VoiceModule section for ownership transfer commands
public partial class VoiceModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("transfer", "Transfer ownership of a voice channel you own to another user.")]
    public async Task SlashTransferAsync(
        [Summary(description: "Select a user in your voice channel to transfer ownership to")]
        IGuildUser user)
    {
        var channel = await channels.GetByOwnerAsync(Context.User.Id);

        var owner = Context.User as SocketGuildUser;
        if (owner.VoiceChannel is null || owner.VoiceChannel?.Id != channel.Id)
        {
            await RespondAsync(_locale.Get("voice:transfer:not_in_voice_error"), ephemeral: true);
            return;
        }

        var components = new ComponentBuilderV2()
            .WithSection(new SectionBuilder()
                .WithTextDisplay(_locale.Get("voice:transfer:confirm_text", user.Mention))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId($"confirm_transfer_button:{channel.Id},{user.Id}")
                    .WithEmote(new Emoji("❗"))
                    .WithStyle(ButtonStyle.Danger)
                    .WithLabel(_locale.Get("voice:transfer:confirm_label"))));

        await RespondAsync(components: components.Build(), 
            ephemeral: true,
            allowedMentions: AllowedMentions.None);
    }

    [ComponentInteraction("transfer_select:*", ignoreGroupNames: true)]
    public async Task SelectTransferAsync(IVoiceChannel channel)
    {
        var interaction = Context.Interaction as SocketMessageComponent;
        var user = interaction.Data.Members.SingleOrDefault();

        var components = new ComponentBuilderV2()
            .WithSection(new SectionBuilder()
                .WithTextDisplay(_locale.Get("voice:transfer:confirm_text", user.Mention))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId($"confirm_transfer_button:{channel.Id},{user.Id}")
                    .WithEmote(new Emoji("❗"))
                    .WithStyle(ButtonStyle.Danger)
                    .WithLabel(_locale.Get("voice:transfer:confirm_label"))));

        await RespondAsync(components: components.Build(), 
            ephemeral: true,
            allowedMentions: AllowedMentions.None);
    }

    [ComponentInteraction("transfer_button:*", ignoreGroupNames: true)]
    public async Task ButtonTransferAsync(IVoiceChannel channel)
    {
        var owner = Context.User as SocketGuildUser;
        if (owner.VoiceChannel is null)
        {
            await RespondAsync(_locale.Get("voice:transfer:not_in_voice_error"), ephemeral: true);
            return;
        }

        var builder = new ComponentBuilder()
            .AddRow(new ActionRowBuilder(
                new SelectMenuBuilder("transfer_select:" + channel.Id)
                    .WithPlaceholder(_locale.Get("voice:transfer:select_placeholder"))
                    .WithType(ComponentType.UserSelect)));

        await RespondAsync(components: builder.Build(), ephemeral: true);
    }

    [ComponentInteraction("confirm_transfer_button:*,*", ignoreGroupNames: true)]
    public async Task ConfirmTransferAsync(IVoiceChannel channel, IUser user)
    {
        var owner = Context.User as SocketGuildUser;
        if (owner.VoiceChannel is null)
        {
            await RespondAsync(_locale.Get("voice:transfer:not_in_voice_error"), ephemeral: true);
            return;
        }

        await TransferAsync(channel, user.Id);
    }

    private async Task TransferAsync(IVoiceChannel voiceChannel, ulong userId)
    {
        var voice = voiceChannel as SocketVoiceChannel;
        var owner = Context.User as SocketGuildUser;
        var user = voice.GetUser(userId);

        // Target user must be in the channel
        if (user is null)
        {
            await RespondAsync(_locale.Get("voice:transfer:target_not_in_voice_error"), ephemeral: true);
            return;
        }

        var config = await configs.GetAsync(Context.Guild.Id);

        // Don't allow deafened users to own a channel
        if ((config.DenyDeafenedOwnership ?? true) && user.IsDeafened)
        {
            await RespondAsync(_locale.Get("voice:transfer:server_deafened_error"), ephemeral: true);
            return;
        }

        // Don't allow muted users to own a channel
        if ((config.DenyMutedOwnership ?? true) && user.IsMuted)
        {
            await RespondAsync(_locale.Get("voice:transfer:server_muted_error"), ephemeral: true);
            return;
        }

        // Must have a configured ownership role
        if (config.CanOwnRoleIds is not null && config.CanOwnRoleIds.Count > 0)
        {
            var roles = user.Roles
                .Select(x => x.Id)
                .Intersect(config.CanOwnRoleIds);

            if (!roles.Any())
            {
                await RespondAsync(_locale.Get("voice:transfer:no_owner_role_error"), ephemeral: true);
                return;
            }
        }

        // Must not already be owner of another channel
        var ownedChannel = await channels.GetByOwnerAsync(user.Id);
        if (ownedChannel != null)
        {
            await RespondAsync(_locale.Get("voice:transfer:already_owner_error"), ephemeral: true);
            return;
        }

        // Get the channel config
        var channel = await channels.GetAsync(voice.Id);
        channel.OwnerId = user.Id;

        // Remove previous owner and add new owner permissions
        await voice.RemovePermissionOverwriteAsync(owner,
            new RequestOptions { AuditLogReason = _locale.Get("log:transfer_to", user.Username, user.Id) });
        await voice.AddPermissionOverwriteAsync(user, new OverwritePermissions(
            moveMembers: PermValue.Allow, muteMembers: PermValue.Allow, deafenMembers: PermValue.Allow,
            prioritySpeaker: PermValue.Allow, useVoiceActivation: PermValue.Allow),
            new RequestOptions { AuditLogReason = _locale.Get("log:transfer_from", owner.Username, owner.Id) });

        // Save changes to the cache and database
        channels.Remove(owner.Id);
        channel.OwnerId = user.Id;
        await channels.ModifyAsync(channel);

        // Update the panel message with new owner information
        await manager.CreateOrModifyPanelAsync(channel, user, _locale);

        await RespondAsync(_locale.Get("voice:transfer:success", owner.Mention, user.Mention), allowedMentions: AllowedMentions.None);
    }
}
