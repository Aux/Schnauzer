using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Schnauzer.Data;

namespace Schnauzer.Services;

/// <summary>
///     Due to caching, race conditions, or whatever, sometimes a channel can be missed
///     when it should be deleted. This service cleans up those channels if they exist.
/// </summary>
public class CleanupService(
    ILogger<CleanupService> logger,
    LocalizationProvider localizer,
    DiscordSocketClient discord,
    ChannelCache cache,
    SchnauzerDb db
    ) : IHostedService
{
    private readonly TimeSpan _cleanupRate = TimeSpan.FromSeconds(300);

    private Timer _timer;
    private uint _iterations = 0;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new(OnTimerTick, default, _cleanupRate, _cleanupRate);

        logger.LogInformation("Started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _timer.DisposeAsync();
        _timer = null;
        _iterations = 0;

        logger.LogInformation("Stopped");
    }

    private void OnTimerTick(object state)
    {
        _iterations++;

        var channels = db.Channels.ToList();

        foreach (var channel in channels)
        {
            // Doesn't matter if guild is null here, should be handled in GuildMembershipService
            var guild = discord.GetGuild(channel.GuildId);
            if (guild is null)
                continue;

            // If the channel isn't found, or has no connected users, delete it
            var voice = guild.GetVoiceChannel(channel.Id);
            if (voice is null || voice.ConnectedUsers.Count == 0)
            {
                var locale = localizer.GetLocale(guild.PreferredLocale);
                cache.DeleteAsync(channel.OwnerId).GetAwaiter().GetResult();
                voice?.DeleteAsync(new() { AuditLogReason = locale.Get("log:forgotten_channel") }).GetAwaiter().GetResult();

                logger.LogInformation("Cleaned up a forgotten channel {ChannelId} in {GuildId}, check #{Iter}", channel.Id, guild.Id, _iterations);
            }
        }
    }
}
