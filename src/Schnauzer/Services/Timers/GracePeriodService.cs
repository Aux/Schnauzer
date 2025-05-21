using Discord;
using System.Collections.Concurrent;

namespace Schnauzer.Services;

public class GracePeriodService
{
    private ConcurrentDictionary<ulong, Timer> _gracePeriods = new();

    public bool TryStartTimer(IVoiceChannel channel, IGuildUser owner, Locale locale)
    {
        var timer = new Timer(OnTimerTick, (channel, owner, locale), 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        return _gracePeriods.TryAdd(channel.Id, timer);
    }

    public bool TryStopTimer(IVoiceChannel channel, IGuildUser owner)
    {
        if (_gracePeriods.TryRemove(channel.Id, out var timer))
        {
            timer.Dispose();
            return true;
        }
        return false;
    }

    private void OnTimerTick(object stateobj)
    {
        var state = ((IVoiceChannel Channel, IGuildUser Owner, Locale Locale))stateobj;

        var builder = new ComponentBuilderV2()
            .WithSection(new SectionBuilder()
                .WithTextDisplay(state.Locale.Get("graceperiod:claim_msg", state.Owner.Mention))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId("claim_channel:" + state.Channel.Id)
                    .WithStyle(ButtonStyle.Primary)
                    .WithEmote(new Emoji("❗"))
                    .WithLabel(state.Locale.Get("graceperiod:claim_button_name"))));

        state.Channel.SendMessageAsync(components: builder.Build(), allowedMentions: AllowedMentions.None)
            .GetAwaiter().GetResult();

        TryStopTimer(state.Channel, state.Owner);
    }
}
