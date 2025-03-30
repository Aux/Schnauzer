using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Schnauzer.Data;
using Schnauzer.Discord;

namespace Schnauzer.Interactions;

public class ModalData : IModal {
    public string Title { get; set; }

    [ModalTextInput("new_name")]
    public string NewName { get; set; }

    [ModalTextInput("new_limit")]
    public string NewLimit { get; set; }
}

public class VoicePanelModule(
    SchnauzerDb db
    ) : InteractionModuleBase<SocketInteractionContext>
{
    [ComponentInteraction("claim_channel:*")]
    public async Task ClaimChannelAsync(IVoiceChannel channel)
    {
        if (Context.User is not IGuildUser guildUser)
            return;

        var voiceMembers = await channel.GetUsersAsync().FlattenAsync();
        if (!voiceMembers.Any(x => x.Id == guildUser.Id))
        {
            await RespondAsync("You can't claim a channel you aren't currently in.", ephemeral: true);
            return;
        }

        var config = await db.Guilds
            .Include(x => x.DynamicChannels.Where(x => x.Id == channel.Id))
            .SingleOrDefaultAsync(x => x.Id == Context.Guild.Id);
        var dynchan = config.DynamicChannels.SingleOrDefault();

        if (dynchan.OwnerId == Context.User.Id)
        {
            await RespondAsync("You can't claim a channel you already own.", ephemeral: true);
            return;
        }

        if (config.CanOwnRoleIds != null && !guildUser.RoleIds.Intersect(config.CanOwnRoleIds).Any())
        {
            await RespondAsync("You do not have a role that allows you to claim ownership of a channel.", ephemeral: true);
            return;
        }

        await channel.AddPermissionOverwriteAsync(guildUser,
            new OverwritePermissions(moveMembers: PermValue.Allow, muteMembers: PermValue.Allow,
            deafenMembers: PermValue.Allow, prioritySpeaker: PermValue.Allow, useVoiceActivation: PermValue.Allow));
        await channel.RemovePermissionOverwriteAsync(Context.Guild.GetUser(dynchan.OwnerId),
            new RequestOptions { AuditLogReason = $"Transferring ownership to @{guildUser.Username} ({guildUser.Id})" });

        dynchan.OwnerId = guildUser.Id;
        db.Update(dynchan);
        await db.SaveChangesAsync();

        var allCommands = await Context.Client.GetGlobalApplicationCommandsAsync();
        var cmds = allCommands.SingleOrDefault(x => x.Name.StartsWith("voice"));

        // Create channel owner panel
        var panelMsg = (IUserMessage)await channel.GetMessageAsync(dynchan.PanelMessageId.Value);
        var embed = new EmbedBuilder()
            .WithTitle("Voice Channel Controls")
            .AddField("Owner", guildUser.Mention)
            .AddField("Commands", string.Join(" ", cmds.Options.Select(x => $"</{cmds.Name} {x.Name}:{cmds.Id}>")));

        await panelMsg.ModifyAsync(x => x.Embed = embed.Build());

        await RespondAsync($"{guildUser.Mention} is now the owner of this channel.", allowedMentions: AllowedMentions.None);
    }

    [RequireChannelOwner]
    [ComponentInteraction("rename_button:*")]
    public async Task RenameChannelAsync(IVoiceChannel channel)
    {
        var modal = new ModalBuilder()
            .WithCustomId("rename_modal:" + channel.Id)
            .WithTitle("Rename Voice Channel")
            .AddTextInput("New Name", "new_name", placeholder: channel.Name, 
                minLength: 1, maxLength: 50, required: true);

        await RespondWithModalAsync(modal.Build());
    }

    [RequireChannelOwner]
    [ModalInteraction("rename_modal:*")]
    public async Task RenameChannelModalAsync(IVoiceChannel channel, ModalData modal)
    {
        await channel.ModifyAsync(x => x.Name = modal.NewName, 
            new RequestOptions { AuditLogReason = $"Updated by @{Context.User.Username} ({Context.User.Id})" });
        await RespondAsync($"{Context.User.Mention} updated the channel's name to `{modal.NewName}`", allowedMentions: AllowedMentions.None);
    }

    [RequireChannelOwner]
    [ComponentInteraction("limit_button:*")]
    public async Task LimitChannelAsync(IVoiceChannel channel)
    {
        var modal = new ModalBuilder()
            .WithCustomId("limit_modal:" + channel.Id)
            .WithTitle("Change Voice Channel Limit")
            .AddTextInput("New Limit", "new_limit", placeholder: channel.UserLimit?.ToString() ?? "unlimited",
                minLength: 0, maxLength: 2);

        await RespondWithModalAsync(modal.Build());
    }

    [RequireChannelOwner]
    [ModalInteraction("limit_modal:*")]
    public async Task LimitChannelModalAsync(IVoiceChannel channel, ModalData modal)
    {
        if (!int.TryParse(modal.NewLimit, out var limit))
        {
            await RespondAsync($"`{modal.NewLimit}` is not a valid number, please try again.", ephemeral: true);
            return;
        }

        await channel.ModifyAsync(x => x.UserLimit = limit,
            new RequestOptions { AuditLogReason = $"Updated by @{Context.User.Username} ({Context.User.Id})" });
        await RespondAsync($"{Context.User.Mention} updated the channel limit to `{limit}`", allowedMentions: AllowedMentions.None);
    }
}
