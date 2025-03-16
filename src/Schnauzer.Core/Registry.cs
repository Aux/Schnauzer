using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Schnauzer;

public static class Registry
{
    public static IHostBuilder AddDiscord(this IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddTransient<DiscordRestClient>();
            services.AddSingleton(_ => new DiscordSocketClient(new()
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
                LogLevel = LogSeverity.Verbose,
                MessageCacheSize = 0,
                SuppressUnknownDispatchWarnings = true
            }));
            services.AddSingleton(services =>
            {
                var discord = services.GetRequiredService<DiscordSocketClient>();
                return new InteractionService(discord, new()
                {
                    LogLevel = LogSeverity.Verbose,
                    //LocalizationManager = new JsonLocalizationManager("locales/_interactions", "interactions")
                });
            });
        });

        return builder;
    }
}