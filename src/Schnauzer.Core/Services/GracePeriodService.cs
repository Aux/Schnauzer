using Discord;
using System.Collections.Concurrent;

namespace Schnauzer.Services;

public class GracePeriodService
{
    private ConcurrentDictionary<ulong, Timer> _gracePeriods = new();

    public bool TryStartTimer(IVoiceChannel channel, IGuildUser owner)
    {
        var timer = new Timer(OnTimerTick, (channel, owner), 
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
        var state = ((IVoiceChannel Channel, IGuildUser Owner))stateobj;

        var claimButton = new ButtonBuilder()
            .WithCustomId("claim_channel:" + state.Channel.Id)
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(new Emoji("❗"))
            .WithLabel("Claim Ownership");
        var componenets = new ComponentBuilder()
            .WithButton(claimButton);

        state.Channel.SendMessageAsync($"Looks like the channel owner, {state.Owner.Mention}, has abandoned the channel. " +
            $"Anyone can click this button to claim ownership.",
            components: componenets.Build(), allowedMentions: AllowedMentions.None)
            .GetAwaiter().GetResult();

        TryStopTimer(state.Channel, state.Owner);
    }
}
