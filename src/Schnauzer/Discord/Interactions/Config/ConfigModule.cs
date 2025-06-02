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
        string noValue = _locale.Get("no_value");
        string enabled = _locale.Get("enabled");
        string disabled = _locale.Get("disabled");

        string canOwnRoleValues = config.CanOwnRoleIds is not null && config.CanOwnRoleIds?.Count > 0
            ? string.Join(',', config.CanOwnRoleIds) 
            : noValue;
        string automodRuleValues = config.AutomodRuleIds is not null && config.AutomodRuleIds?.Count > 0
            ? string.Join(',', config.AutomodRuleIds)
            : noValue;
        string gracePeriodValue = $"{config.AbandonedGracePeriod?.TotalMinutes ?? GracePeriodService.DefaultDuration.TotalMinutes} minute(s)";

        string configDisplay = $"" +
            $"**{nameof(config.CreateChannelId)}**: {config.CreateChannelId?.ToString() ?? noValue}\n" +
            $"**{nameof(config.CanOwnRoleIds)}**: {canOwnRoleValues}\n" +
            $"**{nameof(config.DenyDeafenedOwnership)}**: {(config.DenyDeafenedOwnership != false ? enabled : disabled)}\n" +
            $"**{nameof(config.DenyMutedOwnership)}**: {(config.DenyMutedOwnership != false ? enabled : disabled)}\n" +
            $"**{nameof(config.DefaultLobbySize)}**: {config.DefaultLobbySize?.ToString() ?? "4"}\n" +
            $"**{nameof(config.MaxLobbySize)}**: {config.MaxLobbySize?.ToString() ?? "∞"}\n" +
            $"**{nameof(config.MaxLobbyCount)}**: {config.MaxLobbyCount?.ToString() ?? "∞"}\n" +
            $"**{nameof(config.AbandonedGracePeriod)}**: {gracePeriodValue}\n" +
            $"**{nameof(config.IsAutoModEnabled)}**: {(config.IsAutoModEnabled != false ? enabled : disabled)}\n" +
            $"**{nameof(config.AutoModLogChannelId)}**: {config.AutoModLogChannelId?.ToString() ?? noValue}\n" +
            $"**{nameof(config.AutomodRuleIds)}**: {automodRuleValues}\n";

        var embed = new EmbedBuilder()
            .WithTitle(_locale.Get("config:show:embed_title"))
            .WithDescription(configDisplay);

        await RespondAsync(embeds: [embed.Build()], ephemeral: true);
    }
}
