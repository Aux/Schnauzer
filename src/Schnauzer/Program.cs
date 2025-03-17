using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Schnauzer;
using Schnauzer.Data;
using Schnauzer.Services;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureLogging(logging =>
    {
        logging.AddSimpleConsole();
    })
    .AddDiscord()
    .ConfigureServices(services =>
    {
        services.AddDbContextPool<SchnauzerDb>((provider, options) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            options.UseNpgsql($"" +
                $"Host={config["PGHOST"]};" +
                $"Username={config["PGUSER"]};" +
                $"Password={config["PGPASSWORD"]};" +
                $"Database={config["PGDATABASE"]};");
        });

        services.AddSingleton<GracePeriodService>();

        services.AddHostedService<DiscordStartupService>();
        services.AddHostedService<InteractionHandlingService>();

        services.AddHostedService<GuildMembershipService>();
        services.AddHostedService<VoiceStateService>();
    })
    .Build();

host.Services.GetRequiredService<SchnauzerDb>().Database.EnsureCreated();

await host.RunAsync();