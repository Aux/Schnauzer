using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Schnauzer.Data;
using Schnauzer.Data.Models;

namespace Schnauzer.Services;

public class GuildMembershipService(
    ILogger<GuildMembershipService> logger,
    DiscordSocketClient discord,
    SchnauzerDb db
    ) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        discord.JoinedGuild += OnGuildJoinedAsync;
        discord.LeftGuild += OnGuildLeftAsync;
        discord.GuildAvailable += OnGuildAvailableAsync;

        logger.LogInformation("Started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        discord.JoinedGuild -= OnGuildJoinedAsync;
        discord.LeftGuild -= OnGuildLeftAsync;
        discord.GuildAvailable -= OnGuildAvailableAsync;

        logger.LogInformation("Stopped");
        return Task.CompletedTask;
    }

    private async Task OnGuildJoinedAsync(SocketGuild guild)
    {
        var config = await db.Guilds.SingleOrDefaultAsync(x => x.Id == guild.Id);
        if (config is not null)
        {
            logger.LogInformation($"Joined a guild that already has a config {guild.Name} ({guild.Id})");
            return;
        }

        if (config?.IsBanned ?? false)
        {
            logger.LogInformation($"Leaving banned guild {guild.Name} ({guild.Id})");
            await guild.LeaveAsync();
        }

        config = new Guild { Id = guild.Id };
        await db.AddAsync(config);
        await db.SaveChangesAsync();

        logger.LogInformation($"Config created for {guild.Name} ({guild.Id})");
    }

    private async Task OnGuildLeftAsync(SocketGuild guild)
    {
        var config = await db.Guilds.SingleOrDefaultAsync(x => x.Id == guild.Id);
        if (config is null)
        {
            logger.LogInformation($"Left a guild that didn't have a config {guild.Name} ({guild.Id})");
            return;
        }

        db.Remove(config);
        await db.SaveChangesAsync();

        logger.LogInformation($"Config deleted for {guild.Name} ({guild.Id})");
    }

    private async Task OnGuildAvailableAsync(SocketGuild guild)
    {
        var exists = await db.Guilds.AnyAsync(x => x.Id == guild.Id);
        if (!exists)
        {
            var config = new Guild { Id = guild.Id };
            await db.AddAsync(config);
            await db.SaveChangesAsync();

            logger.LogInformation($"Added a config for a missed guild {guild.Name} ({guild.Id})");
            return;
        }
    }
}
