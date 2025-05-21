using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Schnauzer.Data;
using Schnauzer.Data.Models;

namespace Schnauzer.Interactions;

[Group("config", "A collection of admin-only configuration commands")]
[RequireUserPermission(GuildPermission.Administrator)]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public class ConfigModule(
    SchnauzerDb db
    ): InteractionModuleBase<SocketInteractionContext>
{
    private async Task<Guild> EnsureConfigCreatedAsync()
    {
        var config = await db.Guilds
            .Include(x => x.DynamicChannels)
            .SingleOrDefaultAsync(x => x.Id == Context.Guild.Id);

        if (config == null)
        {
            config = new Guild { Id = Context.Guild.Id };
            await db.AddAsync(config);
            await db.SaveChangesAsync();
        }

        return config;
    }

    [SlashCommand("show", "Show this server's current config options.")]
    public async Task ConfigAsync()
    {
        var config = await EnsureConfigCreatedAsync();

        string createOption = config.CreateChannelId is null 
            ? "*none set*" 
            : MentionUtils.MentionChannel(config.CreateChannelId.Value);

        var embed = new EmbedBuilder()
            .WithTitle("Dynamic Channels Config")
            .WithDescription($"Create Channel: {createOption}\n" +
                             $"Roles that can own channels:\n");

        await RespondAsync(embeds: [embed.Build()], ephemeral: true);
    }

    [SlashCommand("set-create-channel", "Set the channel users will join to have a voice channel automatically created.")]
    public async Task SetCreateChannelAsync(IVoiceChannel channel)
    {
        var config = await EnsureConfigCreatedAsync();

        if (channel.Id == config?.CreateChannelId)
        {
            await RespondAsync($"{channel.Mention} is already set as the current Create Channel.", ephemeral: true);
            return;
        }

        if (config.DynamicChannels?.Any(x => x.Id == channel.Id) ?? false)
        {
            await RespondAsync("You can't set a dynamic channel as the Create Channel.", ephemeral: true);
            return;
        }

        config.CreateChannelId = channel.Id;
        db.Update(config);
        await db.SaveChangesAsync();

        await RespondAsync($"{channel.Mention} has been set as the Create Channel.", ephemeral: true);
    }

    [SlashCommand("add-ownership-roles", "Set the roles that are allowed to claim ownership of a channel.")]
    public async Task AddOwnershipRolesAsync(IRole role1, IRole role2 = null, IRole role3 = null, IRole role4 = null, IRole role5 = null)
    {
        var config = await EnsureConfigCreatedAsync();
        config.CanOwnRoleIds ??= [];

        if (role1 is not null)
            config.CanOwnRoleIds.Add(role1.Id);
        if (role2 is not null)
            config.CanOwnRoleIds.Add(role2.Id);
        if (role3 is not null)
            config.CanOwnRoleIds.Add(role3.Id);
        if (role4 is not null)
            config.CanOwnRoleIds.Add(role4.Id);
        if (role5 is not null)
            config.CanOwnRoleIds.Add(role5.Id);

        config.CanOwnRoleIds = config.CanOwnRoleIds.Distinct().ToList();

        db.Update(config);
        await db.SaveChangesAsync();

        await RespondAsync($"These roles are now allowed to claim ownership of a dynamic channel: " +
            $"{string.Join(" ", config.CanOwnRoleIds.Select(MentionUtils.MentionRole))}", ephemeral: true);
    }
}
