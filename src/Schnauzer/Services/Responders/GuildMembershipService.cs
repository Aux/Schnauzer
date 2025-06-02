using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Schnauzer.Services;

public class GuildMembershipService(
    ILogger<GuildMembershipService> logger,
    DiscordSocketClient discord,
    ConfigCache configs
    ) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        discord.JoinedGuild += OnGuildJoinedAsync;
        discord.LeftGuild += OnGuildLeftAsync;
        discord.GuildAvailable += OnGuildAvailableAsync;
        discord.GuildUpdated += OnGuildUpdatedAsync;

        logger.LogInformation("Started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        discord.JoinedGuild -= OnGuildJoinedAsync;
        discord.LeftGuild -= OnGuildLeftAsync;
        discord.GuildAvailable -= OnGuildAvailableAsync;
        discord.GuildUpdated -= OnGuildUpdatedAsync;

        logger.LogInformation("Stopped");
        return Task.CompletedTask;
    }

    private async Task OnGuildJoinedAsync(SocketGuild guild)
    {
        var config = await configs.GetAsync(guild.Id);

        if (config is not null)
        {
            logger.LogWarning("Joined a guild that already has a config {GuildName} ({GuildId})", guild.Name, guild.Id);

            if (config?.IsBanned ?? false)
            {
                logger.LogWarning("Leaving banned guild {GuildName} ({GuildId})", guild.Name, guild.Id);
                await guild.LeaveAsync();
            }

            return;
        }
        
        await configs.TryCreateAsync(new() 
        { 
            Id = guild.Id, 
            PreferredLocale = guild.PreferredCulture.TwoLetterISOLanguageName 
        });

        logger.LogInformation("Created config for {GuildName} ({GuildId})", guild.Name, guild.Id);
    }

    private async Task OnGuildLeftAsync(SocketGuild guild)
    {
        if (!await configs.ExistsAsync(guild.Id))
        {
            logger.LogInformation("Left a guild that didn't have a config {GuildName} ({GuildId})", guild.Name, guild.Id);
            return;
        }

        // Need to delete guild config and dynamic channels from database
    }

    private async Task OnGuildAvailableAsync(SocketGuild guild)
    {
        await guild.GetAutoModRulesAsync();
        if (!await configs.ExistsAsync(guild.Id))
        {
            await configs.TryCreateAsync(new()
            {
                Id = guild.Id,
                PreferredLocale = guild.PreferredCulture.TwoLetterISOLanguageName
            });

            logger.LogInformation("Loaded config for a skipped guild {GuildName} ({GuildId})", guild.Name, guild.Id);
        }
    }

    private async Task OnGuildUpdatedAsync(SocketGuild before, SocketGuild after)
    {
        if (before.PreferredLocale == after.PreferredLocale)
            return;

        var config = await configs.GetAsync(after.Id);
        config.PreferredLocale = after.PreferredCulture.TwoLetterISOLanguageName;
        await configs.ModifyAsync(config);

        logger.LogInformation("Updated preferred locale {Before} -> {After} for {GuildName} ({GuildId})", 
            before.PreferredCulture.TwoLetterISOLanguageName, after.PreferredCulture.TwoLetterISOLanguageName, after.Name, after.Id);
    }
}
