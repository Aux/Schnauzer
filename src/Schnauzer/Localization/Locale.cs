using System.Globalization;
using System.Text.Json.Nodes;

namespace Schnauzer;

public record class Locale
{
    public CultureInfo Culture { get; init; }

    private readonly JsonNode _node;

    public Locale(CultureInfo culture, JsonNode node)
    {
        Culture = culture;
        _node = node;
    }

    public string Get(string target)
    {
        var keys = target.Split(':');
        var previous = _node;

        foreach (var key in keys)
        {
            var current = previous?[key];

            if (current == null)
                return previous.GetValue<string>();

            previous = current;
        }

        return previous.GetValue<string>();
    }

    public string Get(string target, params object[] args)
    {
        return string.Format(Get(target), args);
    }
}
