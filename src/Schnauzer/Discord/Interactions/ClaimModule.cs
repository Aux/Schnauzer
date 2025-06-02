using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Schnauzer.Services;

namespace Schnauzer.Discord.Interactions;

[RequireContext(ContextType.Guild)]
public class ClaimModule(
    LocalizationProvider localizer,
    GracePeriodService gracePeriod,
    ConfigCache configs,
    ChannelManager manager,
    ChannelCache cache
    ) : InteractionModuleBase<SocketInteractionContext>
{
    private Locale _locale;

    public override void BeforeExecute(ICommandInfo command)
    {
        _locale = localizer.GetLocale(Context.Interaction.UserLocale);
    }

    [SlashCommand("claim", "Claim ownership of an abandoned channel.")]
    public Task SlashClaimAsync(IVoiceChannel channel)
    {
        return ClaimAsync(channel);
    }

    [ComponentInteraction("claim_channel:*")]
    public Task ButtonClaimAsync(IVoiceChannel channel)
    {
        return ClaimAsync(channel);
    }

    private async Task ClaimAsync(IVoiceChannel voice)
    {
        var user = Context.User as IGuildUser;

        // Must be in a channel to claim it
        if (user.VoiceChannel?.Id != voice.Id)
        {
            await RespondAsync(_locale.Get("claim:not_in_channel_error"), ephemeral: true);
            return;
        }

        var config = await configs.GetAsync(Context.Guild.Id);

        // Don't allow deafened users to claim a channel
        if ((config.DenyDeafenedOwnership ?? true) && user.IsDeafened)
        {
            await RespondAsync(_locale.Get("claim:server_deafened_error"), ephemeral: true);
            return;
        }

        // Don't allow muted users to claim a channel
        if ((config.DenyMutedOwnership ?? true) && user.IsMuted)
        {
            await RespondAsync(_locale.Get("claim:server_muted_error"), ephemeral: true);
            return;
        }

        // Must have a configured ownership role
        if (config.CanOwnRoleIds is not null && config.CanOwnRoleIds.Count > 0)
        {
            var roles = user.RoleIds.Intersect(config.CanOwnRoleIds);

            if (!roles.Any())
            {
                await RespondAsync(_locale.Get("claim:no_owner_role_error"), ephemeral: true);
                return;
            }
        }

        // Must not already be owner of another channel
        var ownedChannel = await cache.GetByOwnerAsync(user.Id);
        if (ownedChannel != null)
        {
            await RespondAsync(_locale.Get("claim:already_owner_error", MentionUtils.MentionChannel(ownedChannel.Id)), ephemeral: true);
            return;
        }

        // Get the channel config
        var channel = await cache.GetAsync(voice.Id);
        var previousOwner = Context.Guild.GetUser(channel.OwnerId);

        // Check if the owner is in the channel
        var members = await voice.GetUsersAsync().FlattenAsync();
        if (members.Any(x => x.Id == previousOwner.Id))
        {
            if (Context.Interaction is SocketMessageComponent component)
            {
                await component.Message.DeleteAsync(new RequestOptions { 
                    AuditLogReason = _locale.Get("claim:delete_claim_msg")
                });
            }

            await RespondAsync(_locale.Get("claim:not_abandoned_error"), ephemeral: true);
            return;
        }

        channel.OwnerId = user.Id;

        // Stop the grace period timer if one is active
        gracePeriod.TryStopTimer(voice, previousOwner);

        // Remove previous owner and add new owner permissions
        await voice.RemovePermissionOverwriteAsync(previousOwner,
            new RequestOptions { AuditLogReason = _locale.Get("log:transfer_to", user.Username, user.Id) });
        await voice.AddPermissionOverwriteAsync(user, new OverwritePermissions(
            moveMembers: PermValue.Allow, muteMembers: PermValue.Allow, deafenMembers: PermValue.Allow,
            prioritySpeaker: PermValue.Allow, useVoiceActivation: PermValue.Allow),
            new RequestOptions { AuditLogReason = _locale.Get("log:transfer_from", previousOwner.Username, previousOwner.Id) });

        // Save changes to the cache and database
        cache.Remove(previousOwner.Id);
        channel.OwnerId = user.Id;
        await cache.ModifyAsync(channel);

        // Update the panel message with new owner information
        await manager.CreateOrModifyPanelAsync(channel, user, _locale);

        await RespondAsync(_locale.Get("claim:success", user.Mention), allowedMentions: AllowedMentions.None);
    }
}
