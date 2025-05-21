using Discord;
using Discord.WebSocket;
using Schnauzer.Data.Models;
using System.Data;
using System.Text.RegularExpressions;

namespace Schnauzer.Utility;

public static class AutoModHelper
{
    public static (bool IsBlocked, SocketAutoModRule Rule, string Keyword) IsBlocked(string input, SocketGuildUser user, Guild config)
    {
        // Exclude disabled, non keyword, or rules exempt by user roles
        var rules = user.Guild.AutoModRules.Where(x =>
            config.AutomodRuleIds.Contains(x.Id) &&         // Rule is configured
            x.Enabled &&                                    // Rule is enabled
            x.TriggerType == AutoModTriggerType.Keyword &&  // Rule is keywords
            !x.ExemptRoles.Intersect(user.Roles).Any());    // User doesn't have an exempt role

        // Return if no rules found
        if (!rules.Any())
            return (false, null, null);

        foreach (var rule in rules)
        {
            // Get blocked text from the input, if any
            string match = GetMatch(input, rule.KeywordFilter);
            if (match is not null)
            {
                // Check if the blocked text is in the allowlist
                string allow = GetMatch(match, rule.AllowList);
                if (allow is null)
                    return (true, rule, match);
            }
        }

        return (false, null, null);
    }

    private static string GetMatch(string input, IEnumerable<string> filter)
    {
        foreach (var keyword in filter)
        {
            bool wildStart = keyword.StartsWith('*');
            bool wildEnd = keyword.EndsWith('*');

            string pattern = keyword;       // *word*

            if (!wildStart && !wildEnd)     // word
                pattern = @$"(\s+|^){keyword}(\s+|$)";
            else
            if (wildStart && !wildEnd)      // *word
                pattern = @$"{keyword}(\s+|$)";
            else
            if (!wildStart && wildEnd)      // word*
                pattern = @$"(\s+|^){keyword}";

            var match = Regex.Match(input, pattern);
            if (match.Success)
                return match.Value;
        }

        return null;
    }
}
