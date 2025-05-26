using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Schnauzer.Discord.Interactions;
using Schnauzer.Interactions;

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
            //await interactions.RegisterCommandsGloballyAsync(true);
            await discord.SetCustomStatusAsync("🎙️ Serving up dynamic voice channels");
        };
        discord.GuildAvailable += guild => interactions.RegisterCommandsToGuildAsync(guild.Id, true);
        discord.InteractionCreated += OnInteractionAsync;

        await interactions.AddModuleAsync<AboutModule>(services);

        //await interactions.AddModuleAsync<ConfigAutomodModule>(services);
        await interactions.AddModuleAsync<ConfigModule>(services);
        await interactions.AddModuleAsync<VoiceModule>(services);
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