using Discord;
using Discord.Interactions;

namespace Schnauzer.Discord.Interactions;

public partial class ConfigModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("automod-toggle", "Enable or disable AutoMod checks in channel names")]
    public async Task ToggleAsync(
        [Choice("Enable", 1), Choice("Disable", 0)]
        [Summary(description: "Either enable or disable AutoMod checks")]
        int toggle)
    {
        var config = await configs.GetAsync(Context.Guild.Id);
        config.IsAutoModEnabled = toggle == 1;
        await configs.ModifyAsync(config);

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
        var config = await configs.GetAsync(Context.Guild.Id);
        config.AutoModLogChannelId = channel?.Id;
        await configs.ModifyAsync(config);

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
        var config = await configs.GetAsync(Context.Guild.Id);
        if (config.AutomodRuleIds?.Contains(rule.Id) ?? false)
        {
            await RespondAsync(_locale.Get("config:add_automod_rule_duplicate_error", rule.Name), ephemeral: true);
            return;
        }

        if (config.AutomodRuleIds is null)
            config.AutomodRuleIds?.Add(rule.Id);
        else
            config.AutomodRuleIds = [rule.Id];

        await configs.ModifyAsync(config);

        await RespondAsync(_locale.Get("config:add_automod_rule_success", rule.Name), ephemeral: true);
    }

    [SlashCommand("automod-remove", "Remove AutoMod rule")]
    public async Task RemoveRuleAsync(IAutoModRule rule)
    {
        var config = await configs.GetAsync(Context.Guild.Id);

        // The rule isn't configured
        if (config.AutomodRuleIds is null || !config.AutomodRuleIds.Contains(rule.Id))
        {
            await RespondAsync(_locale.Get("config:remove_automod_rule_missing_error"), ephemeral: true);
            return;
        }

        await configs.ModifyAsync(config);

        await RespondAsync(_locale.Get("config:remove_automod_rule_success", rule.Name), ephemeral: true);
    }
}
