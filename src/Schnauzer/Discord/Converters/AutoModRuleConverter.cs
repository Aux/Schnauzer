using Discord;
using Discord.Interactions;

namespace Schnauzer.Discord;

public class AutoModRuleConverter : TypeConverter<IAutoModRule>
{
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
        => AutoModRuleTypeReader.ReadAsync(context, option.Value?.ToString());
}

public class AutoModRuleComponentConverter : ComponentTypeConverter<IAutoModRule>
{
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IComponentInteractionData option, IServiceProvider services)
        => AutoModRuleTypeReader.ReadAsync(context, option.Value?.ToString());
}

public class AutoModRuleTypeReader : TypeReader<IAutoModRule>
{
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, string option, IServiceProvider services)
        => ReadAsync(context, option);

    public static async Task<TypeConverterResult> ReadAsync(IInteractionContext context, string option)
    {
        if (context.Guild is null)
            return TypeConverterResult.FromError(InteractionCommandError.UnmetPrecondition, "AutoMod rules can only be found in a guild context.");
        if (!ulong.TryParse(option, out var ruleId))
            return TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, $"Value `{option}` was not a valid automod rule id");

        var rule = await context.Guild.GetAutoModRuleAsync(ruleId);
        if (rule is null)
            return TypeConverterResult.FromError(InteractionCommandError.Unsuccessful, $"No automod rules with the id `{ruleId}` were found.");
        else
            return TypeConverterResult.FromSuccess(rule);
    }
}
