using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Schnauzer.Data;
using Schnauzer.Data.Models;

namespace Schnauzer.Services;

public class VoiceStateService(
    ILogger<VoiceStateService> logger,
    DiscordSocketClient discord,
    SchnauzerDb db
    ) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        discord.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;

        logger.LogInformation("Started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        discord.UserVoiceStateUpdated -= OnUserVoiceStateUpdatedAsync;

        logger.LogInformation("Stopped");
        return Task.CompletedTask;
    }

    private async Task OnUserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        // Ignore not in a guild
        if (user is not IGuildUser guildUser)
            return;

        var config = await db.Guilds
            .Include(x => x.DynamicChannels)
            .SingleOrDefaultAsync(x => x.Id == guildUser.GuildId);

        // No config or no create channel is set
        if (config is null || config.CreateChannelId is null)
            return;

        // User joined a voice channel
        if (before.VoiceChannel == null && after.VoiceChannel != null)
        {
            // The joined channel is Create
            if (after.VoiceChannel.Id == config.CreateChannelId)
                await CreateDynamicAsync(config, guildUser);
            
            return;
        }

        // User left or changed voice channels
        if (before.VoiceChannel != null && after.VoiceChannel == null ||
            before.VoiceChannel.Id != after.VoiceChannel?.Id)
        {
            var dynchan = config.DynamicChannels?.SingleOrDefault(x => x.Id == before.VoiceChannel.Id);

            // Ignore non-dynamic channels
            if (dynchan == null)
                return;

            // The dynamic channel is empty
            if (before.VoiceChannel.ConnectedUsers.Count == 0)
            {
                await before.VoiceChannel.DeleteAsync(new()
                {
                    AuditLogReason = "Dynamic voice channel is empty."
                });

                db.Remove(dynchan);
                await db.SaveChangesAsync();

                return;
            }

            // The dynamic channel is orphaned
            if (dynchan.OwnerId == guildUser.Id)
            {
                await before.VoiceChannel.SendMessageAsync($"Looks like the channel owner has left. Normally I would " +
                    $"mention that someone could take ownership, but that hasn't been implemented yet 😳");
            }
        }
    }

    private async Task CreateDynamicAsync(Guild config, IGuildUser user)
    {
        // Get the create channel's info
        var create = await user.Guild.GetVoiceChannelAsync(config.CreateChannelId.Value);
        var category = await create.GetCategoryAsync();

        var dynamicPerms = new List<Overwrite>(category.PermissionOverwrites)
        {
            new(user.Id, PermissionTarget.User, new OverwritePermissions(
                moveMembers: PermValue.Allow, muteMembers: PermValue.Allow, deafenMembers: PermValue.Allow,
                prioritySpeaker: PermValue.Allow, useVoiceActivation: PermValue.Allow, manageMessages: PermValue.Allow))
        };

        // Create the channel
        var dynvoice = await user.Guild.CreateVoiceChannelAsync($"{user.DisplayName}'s channel", p =>
        {
            p.CategoryId = create.CategoryId;
            p.Position = create.Position + 1;
            p.PermissionOverwrites = dynamicPerms;
        },
        new RequestOptions { AuditLogReason = $"Created dynamic channel for @{user.Username} ({user.Id})" });

        // Move the user to the new channel
        await user.ModifyAsync(x => x.ChannelId = dynvoice.Id);

        // Create channel owner panel
        var embed = new EmbedBuilder()
            .WithTitle("Dynamic Voice Channel Controls")
            .WithDescription($"Owner: {user.Mention}");

        var renameButton = new ButtonBuilder()
            .WithCustomId("rename_button:" + dynvoice.Id)
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(new Emoji("✏️"))
            .WithLabel("Rename");
        var limitButton = new ButtonBuilder()
            .WithCustomId("limit_button:" + dynvoice.Id)
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(new Emoji("💺"))
            .WithLabel("User Limit");
        //var manageButton = new ButtonBuilder()
        //    .WithCustomId("manage_button:" + dynvoice.Id)
        //    .WithStyle(ButtonStyle.Secondary)
        //    .WithEmote(new Emoji("🛠️"))
        //    .WithLabel("Manage Users");

        var components = new ComponentBuilder()
            .WithButton(renameButton)
            .WithButton(limitButton);

        var panelMsg = await dynvoice.SendMessageAsync(embed: embed.Build(), components: components.Build());

        // Save the channel to db
        var dynchan = new DynamicChannel
        {
            Id = dynvoice.Id,
            GuildId = user.GuildId,
            CreatorId = user.Id,
            OwnerId = user.Id,
            PanelMessageId = panelMsg.Id
        };

        await db.AddAsync(dynchan);
        await db.SaveChangesAsync();
    }
}
