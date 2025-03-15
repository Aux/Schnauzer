using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Schnauzer.Services;

public class DiscordStartupService(
        DiscordSocketClient discord,
        IConfiguration config,
        ILogger<DiscordStartupService> logger
    ) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        discord.Log += msg => LogHelper.OnLogAsync(logger, msg);
        await discord.LoginAsync(TokenType.Bot, config["schnauzer_discord"]);
        await discord.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await discord.LogoutAsync();
        await discord.StopAsync();
    }
}