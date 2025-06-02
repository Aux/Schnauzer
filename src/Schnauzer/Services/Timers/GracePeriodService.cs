using Discord;
using System.Collections.Concurrent;

namespace Schnauzer.Services;

public class GracePeriodService
{
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(30);

    private ConcurrentDictionary<ulong, Timer> _gracePeriods = new();

    public bool TryStartTimer(IVoiceChannel channel, IGuildUser owner, Locale locale, TimeSpan duration)
    {
        var timer = new Timer(OnTimerTick, (channel, owner, locale),
            duration, Timeout.InfiniteTimeSpan);

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
