using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schnauzer.Data;

namespace Schnauzer.Discord;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public class RequireChannelOwnerAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        // This is just to get the GuildUser cast
        if (context.User is not IGuildUser user)
            return PreconditionResult.FromError("It shouldn't be possible for this to happen, but you know...");

        // Don't bother checking if the user is owner if they have enough guild permissions
        if (user.GuildPermissions.Administrator || user.GuildPermissions.ManageChannels)
            return PreconditionResult.FromSuccess();

        // Check if the user is the channel owner
        var db = services.GetRequiredService<SchnauzerDb>();
        var allowed = await db.Channels.AnyAsync(x => x.Id == context.Channel.Id && x.OwnerId == context.User.Id);

        if (allowed)
            return PreconditionResult.FromSuccess();
        return PreconditionResult.FromError("You must be the channel owner to perform this action.");
    }
}
