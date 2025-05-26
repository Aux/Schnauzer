using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Schnauzer;

public class LocalizationProvider
{
    private const string LocaleFolder = @".\locales";

    private readonly ILogger _logger;

    private List<Locale> _locales = [];

    public IReadOnlyCollection<Locale> Locales => _locales.AsReadOnly();

    public LocalizationProvider(ILogger<LocalizationProvider> logger)
    {
        _logger = logger;
        ReloadLocales();
    }

    public Locale GetLocale(CultureInfo culture)
    {
        /* Need to include parent match so that 'en' and 'en-US' and 'en-UK'
           can all return each other if another isn't present. */
        var locale = _locales.Find(x => x.Culture.Equals(culture)) 
            ?? _locales.Find(x => 
                x.Culture.Equals(culture.Parent) || 
                x.Culture.Parent.Equals(culture) ||
                x.Culture.Parent.Equals(culture.Parent));

        return locale ?? GetLocale(CultureInfo.GetCultureInfo("en"));
    }

    public Locale GetLocale(string code)
    {
        if (CultureHelper.TryCreate(code, out var culture))
            return GetLocale(culture);
        return null;
    }

    public void ReloadLocales()
    {
        var reloadedLocales = new List<Locale>();
        var filePaths = Directory.GetFiles(LocaleFolder, "messages.*.json");

        foreach (var filePath in filePaths)
        {
            string contents = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(contents))
            {
                _logger.LogError("The file at `{filePath}` was empty, skipping.", filePath);
                continue;
            }

            var node = JsonNode.Parse(contents);
            if (node == null)
            {
                _logger.LogError("The file at `{filePath}` has contents not in json format, skipping.", filePath);
                continue;
            }

            var cultureCode = node["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(cultureCode))
            {
                _logger.LogError("The file at `{filePath}` had an empty culture code, skipping.", filePath);
                continue;
            }

            if (!CultureHelper.TryCreate(cultureCode, out var culture))
            {
                _logger.LogError("The file at `{filePath}` did not have a valid culture id, skipping.", filePath);
                continue;
            }

            if (_locales.Any(x => x.Culture == culture))
            {
                _logger.LogError("The file at `{filePath}` is a duplicate culture id, skipping.", filePath);
                continue;
            }

            reloadedLocales.Add(new Locale(culture, node["entries"]));
            _logger.LogInformation("Loaded a locale for `{culture.Name}`", culture.Name);
        }

        _locales = reloadedLocales;
        _logger.LogInformation("Loaded {_locales.Count} locale(s)", _locales.Count);
    }
}
