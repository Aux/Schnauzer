using Discord.Interactions;
using Schnauzer.Services;

namespace Schnauzer.Discord.Interactions;

[RequireChannelOwner]
[RequireContext(ContextType.Guild)]
[Group("voice", "Management commands for channel owners.")]
public partial class VoiceModule(
    LocalizationProvider localizer,
    ConfigCache configs,
    ChannelCache channels)
    : InteractionModuleBase<SocketInteractionContext>
{
    private Locale _locale;

    public override void BeforeExecute(ICommandInfo command)
    {
        _locale = localizer.GetLocale(Context.Interaction.UserLocale);
    }
}
