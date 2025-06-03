using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octokit;
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
        services.AddMemoryCache();
        services.AddDbContextPool<SchnauzerDb>((provider, options) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            options.EnableSensitiveDataLogging(true);
            options.UseNpgsql($"" +
                $"Host={config["PGHOST"]};" +
                $"Username={config["PGUSER"]};" +
                $"Password={config["PGPASSWORD"]};" +
                $"Database={config["PGDATABASE"]};");
        });

        services.AddSingleton(new GitHubClient(new ProductHeaderValue("Schnauzer")));
        services.AddSingleton<LocalizationProvider>();
        services.AddSingleton<GracePeriodService>();

        services.AddTransient<ChannelManager>();
        services.AddTransient<ConfigCache>();
        services.AddTransient<ChannelCache>();

        services.AddHostedService<DiscordHost>();
        services.AddHostedService<InteractionsHost>();
        services.AddHostedService<GuildMembershipService>();
        services.AddHostedService<VoiceStateService>();
        //services.AddHostedService<CleanupService>();
    })
    .Build();

// Ensure db is created and on latest migration
var db = host.Services.GetRequiredService<SchnauzerDb>();
await db.Database.MigrateAsync();

// Preload locale files
host.Services.GetRequiredService<LocalizationProvider>();

await host.RunAsync();