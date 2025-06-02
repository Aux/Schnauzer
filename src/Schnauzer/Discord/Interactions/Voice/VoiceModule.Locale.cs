using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Schnauzer.Discord.Interactions;

// VoiceModule section for locale commands
public partial class VoiceModule
{
    [SlashCommand("locale", "Set the locale for a voice channel you own")]
    public Task SlashLocaleAsync(
        [Autocomplete]
        [Summary("locale", "The locale to set your voice channel responses to")]
        string localeCode)
    {
        return SetLocaleAsync(localeCode);
    }

    [AutocompleteCommand("locale", "locale")]
    public async Task AutofillLocaleAsync()
    {
        var interaction = Context.Interaction as SocketAutocompleteInteraction;
        string userInput = interaction.Data.Current.Value.ToString();

        var results = new List<AutocompleteResult>();
        var matches = localizer.Locales.Where(x =>
            x.Culture.TwoLetterISOLanguageName == userInput ||
            x.Culture.DisplayName.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
            x.Culture.NativeName.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Culture.TwoLetterISOLanguageName);

        foreach (var match in matches)
            results.Add(new AutocompleteResult(match.Culture.NativeName, match.Culture.TwoLetterISOLanguageName));

        await interaction.RespondAsync(results);
    }

    [ComponentInteraction("locale_select:*", ignoreGroupNames: true)]
    public Task SelectLocaleAsync(IVoiceChannel channel)
    {
        var interaction = Context.Interaction as SocketMessageComponent;
        string userInput = interaction.Data.Values.SingleOrDefault();
        return SetLocaleAsync(userInput);
    }

    [ComponentInteraction("locale_button:*", ignoreGroupNames: true)]
    public async Task ButtonLocaleAsync(IVoiceChannel channel)
    {
        var menu = new SelectMenuBuilder("locale_select:" + channel.Id)
            .WithPlaceholder(_locale.Get("voice:locale:select_placeholder"));

        foreach (var locale in localizer.Locales)
            menu.AddOption(locale.Culture.DisplayName, locale.Culture.TwoLetterISOLanguageName, locale.Culture.NativeName);

        var builder = new ComponentBuilder()
            .AddRow(new(menu));

        await RespondAsync(components: builder.Build(), ephemeral: true);
    }

    private async Task SetLocaleAsync(string localeCode)
    {
        var locale = localizer.GetLocale(localeCode);
        if (locale is null)
        {
            await RespondAsync(_locale.Get("voice:locale:no_matches_error"), ephemeral: true);
            return;
        }

        var channel = await channels.GetByOwnerAsync(Context.User.Id);

        if (channel.PreferredLocale == locale.Culture.TwoLetterISOLanguageName)
        {
            await RespondAsync(_locale.Get("voice:locale:already_set_error"), ephemeral: true);
            return;
        }

        channel.PreferredLocale = locale.Culture.TwoLetterISOLanguageName;
        await channels.ModifyAsync(channel);

        await RespondAsync(_locale.Get("voice:locale:success", locale.Culture.DisplayName), ephemeral: true);
    }
}
