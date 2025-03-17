using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Schnauzer.Data;

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

        var config = await db.Guilds
            .Include(x => x.DynamicChannels.Where(x => x.Id == channel.Id))
            .SingleOrDefaultAsync(x => x.Id == Context.Guild.Id);
        var dynchan = config.DynamicChannels.SingleOrDefault();

        if (dynchan.OwnerId == Context.User.Id)
        {
            await RespondAsync("You can't claim a channel you already own.", ephemeral: true);
            return;
        }

        if (!guildUser.RoleIds.Intersect(config.CanOwnRoleIds).Any())
        {
            await RespondAsync("You do not have a role that allows you to claim ownership of a channel.", ephemeral: true);
            return;
        }

        var category = await channel.GetCategoryAsync();

        var dynamicPerms = new List<Overwrite>(category.PermissionOverwrites)
        {
            new(guildUser.Id, PermissionTarget.User, new OverwritePermissions(
                moveMembers: PermValue.Allow, muteMembers: PermValue.Allow, deafenMembers: PermValue.Allow,
                prioritySpeaker: PermValue.Allow, useVoiceActivation: PermValue.Allow, manageMessages: PermValue.Allow))
        };

        await channel.ModifyAsync(x =>
        {
            x.PermissionOverwrites = dynamicPerms;
        });

        dynchan.OwnerId = guildUser.Id;
        db.Update(dynchan);
        await db.SaveChangesAsync();

        var panelMsg = (IUserMessage)await channel.GetMessageAsync(dynchan.PanelMessageId.Value);
        var embed = new EmbedBuilder()
            .WithTitle("Dynamic Voice Channel Controls")
            .WithDescription($"Owner: {guildUser.Mention}");

        await panelMsg.ModifyAsync(x => x.Embed = embed.Build());

        await RespondAsync($"{guildUser.Mention} is now the owner of this channel.", allowedMentions: AllowedMentions.None);
    }

    [ComponentInteraction("rename_button:*")]
    public async Task RenameChannelAsync(IVoiceChannel channel)
    {
        if (!await IsOwnerAsync(channel))
            return;

        var modal = new ModalBuilder()
            .WithCustomId("rename_modal:" + channel.Id)
            .WithTitle("Rename Voice Channel")
            .AddTextInput("New Name", "new_name", placeholder: channel.Name, 
                minLength: 1, maxLength: 50, required: true);

        await RespondWithModalAsync(modal.Build());
    }

    [ModalInteraction("rename_modal:*")]
    public async Task RenameChannelModalAsync(IVoiceChannel channel, ModalData modal)
    {
        await channel.ModifyAsync(x => x.Name = modal.NewName, 
            new RequestOptions { AuditLogReason = $"Updated by @{Context.User.Username} ({Context.User.Id})" });
        await RespondAsync($"{Context.User.Mention} updated the name of {channel.Mention} to `{modal.NewName}`", allowedMentions: AllowedMentions.None);
    }

    [ComponentInteraction("limit_button:*")]
    public async Task LimitChannelAsync(IVoiceChannel channel)
    {
        if (!await IsOwnerAsync(channel))
            return;

        var modal = new ModalBuilder()
            .WithCustomId("limit_modal:" + channel.Id)
            .WithTitle("Change Voice Channel Limit")
            .AddTextInput("New Limit", "new_limit", placeholder: channel.UserLimit?.ToString() ?? "unlimited",
                minLength: 0, maxLength: 2);

        await RespondWithModalAsync(modal.Build());
    }

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
        await RespondAsync($"{Context.User.Mention} updated {channel.Mention} to a limit of `{limit}`", allowedMentions: AllowedMentions.None);
    }

    [ComponentInteraction("manage_button:*")]
    public async Task ManageChannelAsync(IVoiceChannel channel)
    {
        if (!await IsOwnerAsync(channel))
            return;

        var menu = new SelectMenuBuilder()
            .WithCustomId("manage_menu:" + channel.Id)
            .AddOption("Block User", "block_user", "Prevent a user from joining the channel", new Emoji("🔨"))
            .AddOption("Give Owner", "give_owner", "Give ownership of the channel to another user", new Emoji("❗"));

    }

    private Task<bool> IsOwnerAsync(IVoiceChannel channel)
        => db.Channels.AnyAsync(x => x.Id == channel.Id && x.OwnerId == Context.User.Id);
}
