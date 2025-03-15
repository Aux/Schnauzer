using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Schnauzer.Data;

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
        // User joined a voice channel
        if (before.VoiceChannel == null && after.VoiceChannel != null)
        {
            // Check if joined channel is the configured Create channel
        }

        // User left a voice channel
        if (before.VoiceChannel != null && after.VoiceChannel == null)
        {
            // Check if left channel is a dynamic channel
            // Check if left channel has 0 members
        }

        // User moved voice channels
        if (before.VoiceChannel?.Id != after.VoiceChannel?.Id)
        {
            // Check if left channel is a dynamic channel
            // Check if left channel has 0 members
        }

        await Task.Delay(0);
    }
}
