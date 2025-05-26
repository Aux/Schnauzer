using Discord;
using Discord.Interactions;
using Schnauzer.Services;

namespace Schnauzer.Discord.Interactions;

[Group("config", "A collection of admin-only configuration commands")]
[RequireUserPermission(GuildPermission.Administrator)]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public class ConfigLobbyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ConfigCache _config;
    private readonly ChannelCache _cache;
    private readonly Locale _locale;

    public ConfigLobbyModule(LocalizationProvider localizer, ConfigCache config, ChannelCache cache)
    {
        _config = config;
        _cache = cache;

        _locale = localizer.GetLocale(Context.Interaction.UserLocale);
    }

    [SlashCommand("create-channel", "Set the voice channel users will join to create a dynamic voice channel")]
    public async Task SetCreateChannelAsync(
        [Summary(description: "The voice channel users will join")]
        IVoiceChannel channel)
    {
        // Can't set a dynamic channel as create
        if (await _cache.ExistsAsync(channel.Id))
        {
            await RespondAsync(_locale.Get("config:set_dynamic_error"), ephemeral: true);
            return;
        }

        var config = await _config.GetAsync(Context.Guild.Id);
        config.CreateChannelId = channel.Id;
        await _config.ModifyAsync(config);

        await RespondAsync(_locale.Get("config:set_dynamic_success", channel.Mention), ephemeral: true);
    }

    [SlashCommand("default-lobby-size", "Set the default user limit when a new dynamic channel is created")]
    public async Task SetDefaultLobbySizeAsync(
        [MinValue(0)]
        [Summary(description : "The default number of users allowed to join a channel, set to 0 for unlimited.")]
        int size = 4)
    {
        var config = await _config.GetAsync(Context.Guild.Id);
        config.DefaultLobbySize = size > 0 ? size : null;
        await _config.ModifyAsync(config);

        await RespondAsync(_locale.Get("config:set_default_lobby_size_success", 
            config.DefaultLobbySize?.ToString() ?? "∞"), ephemeral: true);
    }

    [SlashCommand("max-lobby-size", "Set the maximum number of users that can join a dynamic channel")]
    public async Task SetMaxLobbySizeAsync(
        [MinValue(0)]
        [Summary(description: "The max number of users allowed to join a channel, set to 0 for unlimited.")]
        int size = 0)
    {
        var config = await _config.GetAsync(Context.Guild.Id);
        config.MaxLobbySize = size > 0 ? size : null;
        await _config.ModifyAsync(config);

        await RespondAsync(_locale.Get("config:set_max_lobby_size_success", 
            config.MaxLobbySize?.ToString() ?? "∞"), ephemeral: true);
    }

    [SlashCommand("max-lobby-count", "Set the maximum number of dynamic channels that can exist in your server")]
    public async Task SetMaxLobbyCountAsync(
        [MinValue(0)]
        [Summary(description: "The max number of dynamic channels, set to 0 for unlimited.")]
        int count = 0)
    {
        var config = await _config.GetAsync(Context.Guild.Id);
        config.MaxLobbyCount = count > 0 ? count : null;
        await _config.ModifyAsync(config);

        await RespondAsync(_locale.Get("config:set_max_lobby_count_success", 
            config.MaxLobbyCount?.ToString() ?? "∞"), ephemeral: true);
    }

    [SlashCommand("deafened-toggle", "Enable or disable allowing server deafened users to own channels")]
    public async Task ToggleDeafenedAsync(
        [Choice("Enable", 1), Choice("Disable", 0)]
        [Summary(description: "Either enable or disable AutoMod checks")]
        int toggle)
    {
        var config = await _config.GetAsync(Context.Guild.Id);
        config.IsAutoModEnabled = toggle == 1;
        await _config.ModifyAsync(config);

        if (config.IsAutoModEnabled ?? true)
            await RespondAsync(_locale.Get("config:toggle_deny_deafened_ownership"), ephemeral: true);
        else
            await RespondAsync(_locale.Get("config:toggle_allow_deafened_ownership"), ephemeral: true);
    }

    [SlashCommand("muted-toggle", "Enable or disable allowing server muted users to own channels")]
    public async Task ToggleMutedAsync(
        [Choice("Allow", 1), Choice("Deny", 0)]
        [Summary(description: "Either allow or deny server muted ownership")]
        int toggle)
    {
        var config = await _config.GetAsync(Context.Guild.Id);
        config.DenyMutedOwnership = toggle == 1;
        await _config.ModifyAsync(config);

        if (config.IsAutoModEnabled ?? true)
            await RespondAsync(_locale.Get("config:toggle_deny_muted_ownership"), ephemeral: true);
        else
            await RespondAsync(_locale.Get("config:toggle_allow_muted_ownership"), ephemeral: true);
    }

    [SlashCommand("add-ownership-role", "Add a role for users that are allowed to own a dynamic channel")]
    public async Task AddOwnershipRoleAsync(
        [Summary(description: "The default number of users allowed to join a channel, set to 0 for unlimited.")]
        IRole role)
    {
        var config = await _config.GetAsync(Context.Guild.Id);

        if (config.CanOwnRoleIds is null)
            config.CanOwnRoleIds = [role.Id];
        else
            config.CanOwnRoleIds.Add(role.Id);

        await _config.ModifyAsync(config);

        await RespondAsync(_locale.Get("config:add_ownership_roles_success", role.Mention, 
            string.Join(" ", config.CanOwnRoleIds.Select(MentionUtils.MentionRole))), 
            ephemeral: true, allowedMentions: AllowedMentions.None);
    }

    [SlashCommand("remove-ownership-role", "Remove a role for users that are allowed to own a dynamic channel")]
    public async Task RemoveOwnershipRoleAsync(IRole role)
    {
        var config = await _config.GetAsync(Context.Guild.Id);

        if (config.CanOwnRoleIds is null || !config.CanOwnRoleIds.Remove(role.Id))
        {
            await RespondAsync(_locale.Get("config:remove_ownership_roles_error", role.Mention),
                ephemeral: true, allowedMentions: AllowedMentions.None);
            return;
        } 

        await _config.ModifyAsync(config);

        await RespondAsync(_locale.Get("config:remove_ownership_roles_success", role.Mention,
            string.Join(" ", config.CanOwnRoleIds.Select(MentionUtils.MentionRole))),
            ephemeral: true, allowedMentions: AllowedMentions.None);
    }
}
