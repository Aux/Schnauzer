using Discord;
using Discord.Interactions;
using Humanizer;
using Humanizer.Localisation;
using Octokit;
using System.Diagnostics;
using System.Reflection;

namespace Schnauzer.Discord.Interactions;

public class AboutModule(
    LocalizationProvider localizer,
    GitHubClient github
    ) : InteractionModuleBase<SocketInteractionContext>
{
    private const string BaseUrl = "https://github.com/";
    private const string RepoOwner = "Aux";
    private const string RepoName = "Schnauzer";
    private const string RepoUrl = BaseUrl + RepoOwner + "/" + RepoName;

    [SlashCommand("about", "Get some information about Schnauzer")]
    public async Task AboutAsync()
    {
        var locale = localizer.GetLocale(Context.Interaction.UserLocale);
        string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        var commits = (await github.Repository.Commit.GetAll(RepoOwner, RepoName, 
            new CommitRequest { Sha = "dev" }, 
            new ApiOptions() { PageSize = 5, PageCount = 1 })).Take(5);
        var commitSection = string.Join("\n", commits.Select(x => $"[`{x.Sha[..7]}`]({x.HtmlUrl}) {x.Commit.Message}"));

        string latency = $"{Context.Client.Latency}ms";
        var uptime = (DateTime.Now - Process.GetCurrentProcess().StartTime)
            .Humanize(culture: Context.Guild.PreferredCulture, minUnit: TimeUnit.Second);
        var uptimeTag = new TimestampTag(Process.GetCurrentProcess().StartTime, TimestampTagStyles.Relative);

        var components = new ComponentBuilderV2()
            .WithContainer(new ContainerBuilder()
                .WithTextDisplay($"## [Schnauzer]({RepoUrl}) v{version}\n**{locale.Get("about:recent_changes")}**\n{commitSection}")
                .WithSeparator()
                .WithTextDisplay($"{locale.Get("about:latency")}: {latency}\n" +
                                 $"{locale.Get("about:uptime")}: {uptime} or {uptimeTag}")
                );

        await RespondAsync(components: components.Build(), ephemeral: true);
    }
}
