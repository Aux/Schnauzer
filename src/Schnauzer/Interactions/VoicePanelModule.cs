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
            .AddOption("Block User", "block_user")
            .AddOption("Give Owner", "give_owner");

    }

    private Task<bool> IsOwnerAsync(IVoiceChannel channel)
        => db.Channels.AnyAsync(x => x.Id == channel.Id && x.OwnerId == Context.User.Id);
}
