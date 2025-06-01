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

        string canOwnRoleValues = config.CanOwnRoleIds is not null && config.CanOwnRoleIds?.Count > 0
            ? string.Join(',', config.CanOwnRoleIds) 
            : "*none set*";
        string automodRuleValues = config.AutomodRuleIds is not null && config.AutomodRuleIds?.Count > 0
            ? string.Join(',', config.AutomodRuleIds)
            : "*none set*";
        string gracePeriodValue = $"{config.AbandonedGracePeriod?.TotalMinutes ?? GracePeriodService.DefaultDuration.TotalMinutes} minute(s)";

        string configDisplay = $"" +
            $"**{nameof(config.CreateChannelId)}**: {config.CreateChannelId?.ToString() ?? "*none set*"}\n" +
            $"**{nameof(config.CanOwnRoleIds)}**: {canOwnRoleValues}\n" +
            $"**{nameof(config.DenyDeafenedOwnership)}**: {config.DenyDeafenedOwnership?.ToString() ?? "True"}\n" +
            $"**{nameof(config.DenyMutedOwnership)}**: {config.DenyMutedOwnership?.ToString() ?? "True"}\n" +
            $"**{nameof(config.DefaultLobbySize)}**: {config.DefaultLobbySize?.ToString() ?? "4"}\n" +
            $"**{nameof(config.MaxLobbySize)}**: {config.MaxLobbySize?.ToString() ?? "∞"}\n" +
            $"**{nameof(config.MaxLobbyCount)}**: {config.MaxLobbyCount?.ToString() ?? "∞"}\n" +
            $"**{nameof(config.AbandonedGracePeriod)}**: {gracePeriodValue}\n" +
            $"**{nameof(config.IsAutoModEnabled)}**: {config.IsAutoModEnabled?.ToString() ?? "False"}\n" +
            $"**{nameof(config.AutoModLogChannelId)}**: {config.AutoModLogChannelId?.ToString() ?? "*none set*"}\n" +
            $"**{nameof(config.AutomodRuleIds)}**: {automodRuleValues}\n";

        var embed = new EmbedBuilder()
            .WithTitle("Current Configuration")
            .WithDescription(configDisplay);

        await RespondAsync(embeds: [embed.Build()], ephemeral: true);
    }
}
