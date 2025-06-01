using Discord;
using Discord.Interactions;
using Schnauzer.Services;

namespace Schnauzer.Discord.Interactions;

[RequireUserPermission(GuildPermission.Administrator)]
[DefaultMemberPermissions(GuildPermission.Administrator)]
[Group("config", "A collection of admin-only configuration commands")]
public partial class ConfigModule(
    LocalizationProvider localizer,
    ConfigCache configs,
    ChannelCache cache) : InteractionModuleBase<SocketInteractionContext>
{
    private Locale _locale;

    public override void BeforeExecute(ICommandInfo command)
    {
        _locale = localizer.GetLocale(Context.Interaction.UserLocale);
    }

    [SlashCommand("show", "Show this server's current configuration")]
    public async Task ShowAsync()
    {
        var config = await configs.GetAsync(Context.Guild.Id);

        string configDisplay = $"" +
            $"{nameof(config.CreateChannelId)}: {config.CreateChannelId}" +
            $"{nameof(config.CanOwnRoleIds)}: {string.Join(',', config.CanOwnRoleIds)}" +
            $"{nameof(config.DenyDeafenedOwnership)}: {config.DenyDeafenedOwnership}" +
            $"{nameof(config.DenyMutedOwnership)}: {config.DenyMutedOwnership}" +
            $"{nameof(config.DefaultLobbySize)}: {config.DefaultLobbySize}" +
            $"{nameof(config.MaxLobbySize)}: {config.MaxLobbySize}" +
            $"{nameof(config.MaxLobbyCount)}: {config.MaxLobbyCount}" +
            $"{nameof(config.AbandonedGracePeriod)}: {config.AbandonedGracePeriod?.TotalMinutes} minute(s)" +
            $"{nameof(config.IsAutoModEnabled)}: {config.IsAutoModEnabled}" +
            $"{nameof(config.AutoModLogChannelId)}: {config.AutoModLogChannelId}" +
            $"{nameof(config.AutomodRuleIds)}: {string.Join(',', config.AutomodRuleIds)}";

        var embed = new EmbedBuilder()
            .WithTitle("Current Configuration")
            .WithDescription(configDisplay);

        await RespondAsync(embeds: [embed.Build()], ephemeral: true);
    }
}
