using Discord;
using Discord.Interactions;
using Schnauzer.Services;

namespace Schnauzer.Discord.Interactions;

[Group("config", "A collection of admin-only configuration commands")]
[RequireUserPermission(GuildPermission.Administrator)]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public class ConfigAutomodModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ConfigCache _config;
    private readonly Locale _locale;

    public ConfigAutomodModule(LocalizationProvider localizer, ConfigCache config)
    {
        _config = config;
        _locale = localizer.GetLocale(Context.Interaction.UserLocale);
    }

    [SlashCommand("automod-toggle", "Enable or disable AutoMod checks in channel names")]
    public async Task ToggleAsync(
        [Choice("Enable", 1), Choice("Disable", 0)]
        [Summary(description: "Either enable or disable AutoMod checks")]
        int toggle)
    {
        var config = await _config.GetAsync(Context.Guild.Id);
        config.IsAutoModEnabled = toggle == 1;
        await _config.ModifyAsync(config);

        if (config.IsAutoModEnabled ?? true)
            await RespondAsync(_locale.Get("config:toggle_automod_enabled"), ephemeral: true);
        else
            await RespondAsync(_locale.Get("config:toggle_automod_disabled"), ephemeral: true);
    }

    [SlashCommand("automod-logto", "Set the channel to log AutoMod violations to")]
    public async Task SetLogChannelAsync(
        [Summary(description: "The channel to send messages to, omit to disable")]
        ITextChannel channel = null)
    {
        var config = await _config.GetAsync(Context.Guild.Id);
        config.AutoModLogChannelId = channel?.Id;
        await _config.ModifyAsync(config);

        if (channel is null)
            await RespondAsync(_locale.Get("config:set_automod_logto_disabled"), ephemeral: true);
        else
            await RespondAsync(_locale.Get("config:set_automod_logto_success", channel.Mention), ephemeral: true);
    }

    [SlashCommand("automod-add", "Add AutoMod rule")]
    public async Task AddRuleAsync(IAutoModRule rule)
    {
        // Only custom keyword rules provide the list of words to block
        if (rule.TriggerType != AutoModTriggerType.Keyword)
        {
            await RespondAsync(_locale.Get("config:add_automod_rule_keyword_error"), ephemeral: true);
            return;
        }

        // The rule is already configured
        var config = await _config.GetAsync(Context.Guild.Id);
        if (config.AutomodRuleIds?.Contains(rule.Id) ?? false)
        {
            await RespondAsync(_locale.Get("config:add_automod_rule_duplicate_error", rule.Name), ephemeral: true);
            return;
        }

        if (config.AutomodRuleIds is null)
            config.AutomodRuleIds?.Add(rule.Id);
        else
            config.AutomodRuleIds = [rule.Id];

        await _config.ModifyAsync(config);

        await RespondAsync(_locale.Get("config:add_automod_rule_success", rule.Name), ephemeral: true);
    }

    [SlashCommand("automod-remove", "Remove AutoMod rule")]
    public async Task RemoveRuleAsync(IAutoModRule rule)
    {
        var config = await _config.GetAsync(Context.Guild.Id);

        // The rule isn't configured
        if (config.AutomodRuleIds is null || !config.AutomodRuleIds.Contains(rule.Id))
        {
            await RespondAsync(_locale.Get("config:remove_automod_rule_missing_error"), ephemeral: true);
            return;
        }

        await _config.ModifyAsync(config);

        await RespondAsync(_locale.Get("config:remove_automod_rule_success", rule.Name), ephemeral: true);
    }
}
