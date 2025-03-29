using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            await discord.SetCustomStatusAsync("🎙️ Serving up dynamic voice channels 🎙️");
            await interactions.RegisterCommandsGloballyAsync(true);
        };
        discord.InteractionCreated += OnInteractionAsync;

        await interactions.AddModuleAsync<ConfigModule>(services);

        await interactions.AddModuleAsync<VoicePanelModule>(services);
        await interactions.AddModuleAsync<VoiceOwnerModule>(services);
        //await interactions.AddModuleAsync<VoiceContextModule>(services);
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
        } catch
        {
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(msg => msg.Result.DeleteAsync());
            }
        }
    }
}