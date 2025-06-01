using Discord;
using Discord.Interactions;

namespace Schnauzer.Discord;

public class AutoModRuleAutocompleter : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        if (context.Guild is null)
            return AutocompletionResult.FromError(InteractionCommandError.UnmetPrecondition, "AutoMod rules can only be found in a guild context.");

        var value = autocompleteInteraction.Data.Current.Value?.ToString();
        var rules = await context.Guild.GetAutoModRulesAsync();

        var results = new List<AutocompleteResult>();
        if (string.IsNullOrWhiteSpace(value))
            results.AddRange(rules.Select(x => new AutocompleteResult(x.Name, x.Id.ToString())));
        else
            results.AddRange(rules
                .Where(x => x.Id.ToString() == value || x.Name.Contains(value, StringComparison.InvariantCultureIgnoreCase))
                .Select(x => new AutocompleteResult(x.Name, x.Id.ToString())));

        return AutocompletionResult.FromSuccess(results);
    }
}