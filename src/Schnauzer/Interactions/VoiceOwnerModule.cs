using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Schnauzer.Data;
using Schnauzer.Discord;
using Schnauzer.Services;

namespace Schnauzer.Interactions;

[RequireChannelOwner]
[Group("voice", "Management commands for channel owners.")]
public class VoiceOwnerModule(
    SchnauzerDb db
    ) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("rename", "Rename a voice channel you own.")]
    public async Task RenameAsync(
        [MinLength(1), MaxLength(50)]
        [Summary(description: "The new name of your channel, must be between 1 and 50 characters long.")]
        string name)
    {
        if (Context.Channel is not IVoiceChannel channel)
            return;

        await channel.ModifyAsync(x => x.Name = name,
            new RequestOptions { AuditLogReason = $"Updated by @{Context.User.Username} ({Context.User.Id})" });
        await RespondAsync($"{Context.User.Mention} updated the channel's name to `{name}`", allowedMentions: AllowedMentions.None);
    }

    [SlashCommand("limit", "Change the user limit of a voice channel you own.")]
    public async Task LimitAsync(
        [MinValue(0), MaxValue(99)]
        [Summary(description: "The max number of users that can join this channel, must be between 1 and 99, set to 0 for infinite.")]
        int limit)
    {
        if (Context.Channel is not IVoiceChannel channel)
            return;

        await channel.ModifyAsync(x => x.UserLimit = limit,
            new RequestOptions { AuditLogReason = $"Updated by @{Context.User.Username} ({Context.User.Id})" });
        await RespondAsync($"{Context.User.Mention} updated the channel limit to `{limit}`", allowedMentions: AllowedMentions.None);
    }

    [SlashCommand("kick", "Kick a user from a voice channel you own.")]
    public async Task KickAsync(
        [Summary(description: "The user to be kicked")]
        IGuildUser user, 
        [MinLength(1)]
        [Summary(description: "A short description of why this user needed to be kicked")]
        string reason)
    {
        if (Context.Channel is not IVoiceChannel channel)
            return;

        if (Context.User.Id == user.Id)
        {
            await RespondAsync($"You can't kick yourself 🙄", ephemeral: true);
            return;
        }

        await user.ModifyAsync(x => x.Channel = null,
            new RequestOptions { AuditLogReason = $"Kicked by @{Context.User.Username} ({Context.User.Id})\nReason: {reason}" });
        await RespondAsync($"{user.Mention} has been removed from the channel.", ephemeral: true);
    }

    [SlashCommand("block", "Block a user from accessing a voice channel you own.")]
    public async Task BlockAsync(
        [Summary(description: "The user to be blocked")]
        IGuildUser user, 
        [MinLength(1)]
        [Summary(description: "A short description of why this user needed to be blocked")]
        string reason)
    {
        if (Context.Channel is not IVoiceChannel channel)
            return;

        if (Context.User.Id == user.Id)
        {
            await RespondAsync($"You can't block yourself 🙄", ephemeral: true);
            return;
        }

        if (channel.PermissionOverwrites.Any(x => x.TargetId == user.Id))
        {
            await RespondAsync($"{user.Mention} is already blocked from this channel.", ephemeral: true);
            return;
        }

        if (user.GuildPermissions.Administrator || user.GuildPermissions.ManageChannels || user.GuildPermissions.BanMembers)
        {
            await RespondAsync($"You can't block server moderators.", ephemeral: true);
            return;
        }

        await channel.AddPermissionOverwriteAsync(user,
            new OverwritePermissions(connect: PermValue.Deny, sendMessages: PermValue.Deny),
            new RequestOptions { AuditLogReason = $"Blocked by @{Context.User.Username} ({Context.User.Id})\nReason: {reason}" });

        await RespondAsync($"{user.Mention} can no longer join this voice channel.", ephemeral: true);
    }

    [SlashCommand("unblock", "Unblock a user from a voice channel you own.")]
    public async Task UnblockAsync(
        [Summary(description: "The user to be unblocked")]
        IGuildUser user,
        [MinLength(1)]
        [Summary(description: "A short description of why this user is being unblocked")]
        string reason)
    {
        if (Context.Channel is not IVoiceChannel channel)
            return;

        if (Context.User.Id == user.Id)
        {
            await RespondAsync($"You can't unblock yourself 🙄", ephemeral: true);
            return;
        }

        if (!channel.PermissionOverwrites.Any(x => x.TargetId == user.Id))
        {
            await RespondAsync($"{user.Mention} isn't blocked from this channel.", ephemeral: true);
            return;
        }

        await channel.RemovePermissionOverwriteAsync(user, 
            new RequestOptions { AuditLogReason = $"Unblocked by @{Context.User.Username} ({Context.User.Id})\nReason: {reason}" });

        await RespondAsync($"{user.Mention} is no longer blocked from this channel.", ephemeral: true);
    }

    [SlashCommand("transfer", "Transfer ownership of a voice channel to another user.")]
    public async Task TransferAsync(
        [Summary(description: "The user to transfer ownership to.")]
        IGuildUser user, 
        [Summary(description: "Type CONFIRM to verify you aren't doing this by accident.")] 
        string confirm)
    {
        // User has to type CONFIRM to prevent any accidents
        if (confirm != "CONFIRM")
        {
            await RespondAsync($"You did not `CONFIRM` your ownership transfer.", ephemeral: true);
            return;
        }

        // Owner has to be in a voice channel
        if (Context.Channel is not IVoiceChannel channel)
        {
            await RespondAsync($"You must be in a voice channel to transfer ownership.", ephemeral: true);
            return;
        }

        // Get config and dynamic channel from the db
        var config = await db.Guilds
            .Include(x => x.DynamicChannels.Where(x => x.Id == Context.Channel.Id))
            .SingleOrDefaultAsync(x => x.Id == Context.Guild.Id);
        var dynchan = config.DynamicChannels.SingleOrDefault();

        // Target user needs to be in the channel being transferred
        if (user.VoiceChannel is null || user.VoiceChannel.Id != dynchan.Id)
        {
            await RespondAsync($"{user.Mention} must be in the channel you're transferring ownership of.", ephemeral: true);
            return;
        }

        // Target user needs to have configured allow roles
        if (config.CanOwnRoleIds != null && !user.RoleIds.Intersect(config.CanOwnRoleIds).Any())
        {
            await RespondAsync($"{user.Mention} does not have a role that allows them to have ownership of a channel.", ephemeral: true);
            return;
        }

        await channel.AddPermissionOverwriteAsync(user, 
            new OverwritePermissions(moveMembers: PermValue.Allow, muteMembers: PermValue.Allow,
            deafenMembers: PermValue.Allow, prioritySpeaker: PermValue.Allow, useVoiceActivation: PermValue.Allow));
        await channel.RemovePermissionOverwriteAsync(Context.Guild.GetUser(dynchan.OwnerId),
            new RequestOptions { AuditLogReason = $"Transferring ownership to @{user.Username} ({user.Id})" });

        dynchan.OwnerId = user.Id;
        db.Update(dynchan);
        await db.SaveChangesAsync();

        var allCommands = await Context.Client.GetGlobalApplicationCommandsAsync();
        var cmds = allCommands.SingleOrDefault(x => x.Name.StartsWith("voice"));

        // Create channel owner panel
        var panelMsg = (IUserMessage)await channel.GetMessageAsync(dynchan.PanelMessageId.Value);
        var embed = new EmbedBuilder()
            .WithTitle("Voice Channel Controls")
            .AddField("Owner", user.Mention)
            .AddField("Commands", string.Join(" ", cmds.Options.Select(x => $"</{cmds.Name} {x.Name}:{cmds.Id}>")));

        await panelMsg.ModifyAsync(x => x.Embed = embed.Build());

        await RespondAsync($"{user.Mention} is now the owner of this channel.", allowedMentions: AllowedMentions.None);
    }
}
