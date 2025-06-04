using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Schnauzer.Data;
using Schnauzer.Discord;
using Schnauzer.Discord.Interactions;

namespace Schnauzer.Services;

public class InteractionsHost(
        DiscordSocketClient discord,
        InteractionService interactions,
        IServiceProvider services,
        ILogger<InteractionsHost> logger
    ) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        interactions.Log += msg => LogHelper.OnLogAsync(logger, msg);
        discord.Ready += async () =>
        {
            await discord.SetCustomStatusAsync("🎙️ Serving up dynamic voice channels");
        };
        discord.GuildAvailable += guild => interactions.RegisterCommandsToGuildAsync(guild.Id, true);
        discord.InteractionCreated += OnInteractionAsync;

        interactions.AddComponentTypeConverter<StringTime>(new StringTimeComponentConverter());
        interactions.AddTypeConverter<StringTime>(new StringTimeConverter());
        interactions.AddTypeReader<StringTime>(new StringTimeTypeReader());
        interactions.AddComponentTypeConverter<IAutoModRule>(new AutoModRuleComponentConverter());
        interactions.AddTypeConverter<IAutoModRule>(new AutoModRuleConverter());
        interactions.AddTypeReader<IAutoModRule>(new AutoModRuleTypeReader());

        await interactions.AddModuleAsync<AboutModule>(services);
        await interactions.AddModuleAsync<ConfigModule>(services);
        await interactions.AddModuleAsync<VoiceModule>(services);
        await interactions.AddModuleAsync<ClaimModule>(services);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        interactions.Dispose();
        return Task.CompletedTask;
    }

    private async Task OnInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(discord, interaction);
            var result = await interactions.ExecuteCommandAsync(context, services);

            if (!result.IsSuccess)
                await interaction.RespondAsync(result.ToString(), ephemeral: true);
        } catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(msg => msg.Result.DeleteAsync());
            }
        }
    }
}