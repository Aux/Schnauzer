using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Schnauzer.Services;

namespace Schnauzer.Discord;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public class RequireChannelOwnerAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var localizer = services.GetRequiredService<LocalizationProvider>();
        var locale = localizer.GetLocale(context.Interaction.UserLocale);

        // This is just to get the GuildUser cast
        if (context.User is not IGuildUser user)
            return PreconditionResult.FromError(locale.Get("errors:not_guilduser"));

        // Ignore commands if the user isn't in a voice channel
        if (user.VoiceChannel is null)
            return PreconditionResult.FromError(locale.Get("errors:not_in_voice"));

        // Don't bother checking if the user is owner if they have enough guild permissions
        if (user.GuildPermissions.Administrator || user.GuildPermissions.ManageChannels)
            return PreconditionResult.FromSuccess();

        // Check if the user is the channel owner
        var cache = services.GetRequiredService<ChannelCache>();
        var channel = await cache.GetByOwnerAsync(context.User.Id);

        if (channel.Id == context.Channel.Id)
            return PreconditionResult.FromSuccess();
        return PreconditionResult.FromError(locale.Get("errors:not_owner"));
    }
}
