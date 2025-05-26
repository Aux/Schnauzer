using Discord;
using Discord.Interactions;
using Schnauzer.Data.Models;
using System.Text.Json;

namespace Schnauzer.Discord.Interactions;

public partial class ConfigModule : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [SlashCommand("export", "Export this server's config as a json file.")]
    public async Task ExportAsync()
    {
        var config = await configs.GetAsync(Context.Guild.Id);

        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions);

        await RespondWithFileAsync(stream, $"{Context.Guild.Name} config.json", 
            _locale.Get("config:export_json_success", Context.Guild.Name), ephemeral: true);
    }

    [SlashCommand("import", "Modify this server's configuration by uploading a json config file.")]
    public async Task ImportAsync(IAttachment attachment)
    {
        // Block oversized files
        if (attachment.Size > 1024 || attachment.Size == 0)
        {
            await RespondAsync(_locale.Get("config:import_json_oversize_error"), ephemeral: true);
            return;
        }

        // Block non-json files
        if (!attachment.Filename.EndsWith(".json"))
        {
            await RespondAsync(_locale.Get("config:import_json_nonjson_error"), ephemeral: true);
            return;
        }

        // Block empty files
        var content = await HttpHelper.DownloadStringAsync(attachment.Url);
        if (string.IsNullOrWhiteSpace(content))
        {
            await RespondAsync(_locale.Get("config:import_json_empty_error"), ephemeral: true);
            return;
        }

        // Block invalid json
        Guild imported = null;
        try
        {
            imported = JsonSerializer.Deserialize<Guild>(content);
        } catch
        {
            await RespondAsync(_locale.Get("config:import_json_invalid_error"), ephemeral: true);
            return;
        }

        var config = await configs.GetAsync(Context.Guild.Id);

        var changes = new List<string>();
        if (config.CreateChannelId != imported.CreateChannelId)
        {
            changes.Add($"{nameof(config.CreateChannelId)}: {imported.CreateChannelId}");
            config.CreateChannelId = imported.CreateChannelId;
        }
        if (!config.CanOwnRoleIds?.SequenceEqual(imported.CanOwnRoleIds ?? []) ?? imported.CanOwnRoleIds is not null)
        {
            changes.Add($"{nameof(config.CanOwnRoleIds)}: {string.Join(',', imported.CanOwnRoleIds)}");
            config.CanOwnRoleIds = imported.CanOwnRoleIds;
        }
        if (config.DenyDeafenedOwnership != imported.DenyDeafenedOwnership)
        {
            changes.Add($"{nameof(config.DenyDeafenedOwnership)}: {imported.DenyDeafenedOwnership}");
            config.DenyDeafenedOwnership = imported.DenyDeafenedOwnership;
        }
        if (config.DenyMutedOwnership != imported.DenyMutedOwnership)
        {
            changes.Add($"{nameof(config.DenyMutedOwnership)}: {imported.DenyMutedOwnership}");
            config.DenyMutedOwnership = imported.DenyMutedOwnership;
        }
        if (config.DefaultLobbySize != imported.DefaultLobbySize)
        {
            changes.Add($"{nameof(config.DefaultLobbySize)}: {imported.DefaultLobbySize}");
            config.DefaultLobbySize = imported.DefaultLobbySize;
        }
        if (config.MaxLobbySize != imported.MaxLobbySize)
        {
            changes.Add($"{nameof(config.MaxLobbySize)}: {imported.MaxLobbySize}");
            config.MaxLobbySize = imported.MaxLobbySize;
        }
        if (config.MaxLobbyCount != imported.MaxLobbyCount)
        {
            changes.Add($"{nameof(config.MaxLobbyCount)}: {imported.MaxLobbyCount}");
            config.MaxLobbyCount = imported.MaxLobbyCount;
        }
        if (config.IsAutoModEnabled != imported.IsAutoModEnabled)
        {
            changes.Add($"{nameof(config.IsAutoModEnabled)}: {imported.IsAutoModEnabled}");
            config.IsAutoModEnabled = imported.IsAutoModEnabled;
        }
        if (config.AutoModLogChannelId != imported.AutoModLogChannelId)
        {
            changes.Add($"{nameof(config.AutoModLogChannelId)}: {imported.AutoModLogChannelId}");
            config.AutoModLogChannelId = imported.AutoModLogChannelId;
        }
        if (!config.AutomodRuleIds?.SequenceEqual(imported.AutomodRuleIds ?? []) ?? imported.AutomodRuleIds is not null)
        {
            changes.Add($"{nameof(config.AutomodRuleIds)}: {string.Join(',', imported.AutomodRuleIds)}");
            config.AutomodRuleIds = imported.AutomodRuleIds;
        }

        // Ignore if no changes made
        if (changes.Count == 0)
        {
            await RespondAsync(_locale.Get("config:import_json_no_changes"), ephemeral: true);
            return;
        }

        await configs.ModifyAsync(config);
        await RespondAsync(_locale.Get("config:import_json_success", string.Join('\n', changes)), ephemeral: true);
    }
}
