using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Schnauzer.Services;

public class DiscordHost(
        DiscordSocketClient discord,
        IConfiguration config,
        ILogger<DiscordHost> logger
    ) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        discord.Log += msg => LogHelper.OnLogAsync(logger, msg);

#if RELEASE
        string discordToken = config["SCHNAUZER_DISCORD"];
#elif DEBUG
        string discordToken = config["TEST_DISCORD"];
#endif

        if (string.IsNullOrWhiteSpace(discordToken))
            throw new Exception("No discord bot token was provided.");
        
        await discord.LoginAsync(TokenType.Bot, discordToken);
        await discord.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await discord.LogoutAsync();
        await discord.StopAsync();
    }
}