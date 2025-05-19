using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Schnauzer.Services;

public class VoiceStateService(
    ILogger<VoiceStateService> logger,
    DiscordSocketClient discord,
    DynamicChannelManager manager,
    ConfigCache configs
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
        if (user is not SocketGuildUser guildUser)
            return;

        var config = await configs.GetAsync(guildUser.Guild.Id);

        // No config or no create channel is set
        if (config is null || config.CreateChannelId is null)
            return;

        // User joined a voice channel
        if (before.VoiceChannel == null && after.VoiceChannel != null)
        {
            await manager.HandleChannelJoinAsync(config, guildUser, after);
            return;
        }

        // User left or changed voice channels
        if (before.VoiceChannel != null && after.VoiceChannel == null ||
            before.VoiceChannel.Id != after.VoiceChannel?.Id)
        {
            await manager.HandleChannelLeaveAsync(config, guildUser, before);
            return;
        }
    }
}
