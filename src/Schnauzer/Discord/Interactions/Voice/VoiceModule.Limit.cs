using Discord;
using Discord.Interactions;

namespace Schnauzer.Discord.Interactions;

public class LimitModal : IModal
{
    public string Title { get; }
    [ModalTextInput("new_limit")]
    public string NewLimit { get; set; }
}

// VoiceModule section for channel user limit commands
public partial class VoiceModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("limit", "Change the user limit for a voice channel you own.")]
    public Task SlashLimitAsync(
        [MinValue(0), MaxValue(99)]
        [Summary(description: "The max number of users that can join this channel, must be between 1 and 99, set to 0 for infinite.")]
        int limit)
    {
        return LimitAsync(limit, Context.Channel as IVoiceChannel);
    }

    [ModalInteraction("limit_modal:*", ignoreGroupNames: true)]
    public async Task ModalLimitAsync(IVoiceChannel channel, LimitModal data)
    {
        if (!int.TryParse(data.NewLimit, out var limit))
        {
            await RespondAsync(_locale.Get("voice:limit:not_a_number_error", data.NewLimit), ephemeral: true);
            return;
        }

        if (limit < 0 || limit > 99)
        {
            await RespondAsync(_locale.Get("voice:limit:out_of_range_error", limit), ephemeral: true);
            return;
        }

        await LimitAsync(limit, channel);
    }

    [ComponentInteraction("limit_button:*", ignoreGroupNames: true)]
    public async Task ButtonLimitAsync(IVoiceChannel channel)
    {
        var modal = new ModalBuilder()
            .WithCustomId("limit_modal:" + channel.Id)
            .WithTitle(_locale.Get("voice:limit:modal_title"))
            .AddTextInput(_locale.Get("voice:limit:modal_input"),
                "new_limit", placeholder: channel.UserLimit?.ToString() ?? "∞",
                minLength: 0, maxLength: 2, required: true);

        await RespondWithModalAsync(modal.Build());
    }

    private async Task LimitAsync(int input, IVoiceChannel channel)
    {
        await channel.ModifyAsync(x => x.UserLimit = input,
            new RequestOptions { AuditLogReason = _locale.Get("log:updated_channel", Context.User.Username, Context.User.Id) });
        await RespondAsync(_locale.Get("voice:limit:success", Context.User.Mention, input), allowedMentions: AllowedMentions.None);
    }
}
