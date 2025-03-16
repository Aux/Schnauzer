using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Schnauzer.Data;
using Schnauzer.Data.Models;

namespace Schnauzer.Interactions;

[Group("config", "A collection of admin-only configuration commands")]
[RequireUserPermission(GuildPermission.Administrator)]
public class AdminModule(
    SchnauzerDb db
    ): InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("setcreatechannel", "Set the channel users will join to have a voice channel automatically created.")]
    public async Task SetCreateChannelAsync(IVoiceChannel channel)
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
}
