using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Schnauzer.Utility;

namespace Schnauzer.Discord.Interactions;

public class RenameModal : IModal
{
    public string Title { get; }
    [ModalTextInput("new_name")]
    public string NewName { get; set; }
}

public partial class VoiceModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("rename", "Rename a voice channel you own.")]
    public Task SlashRenameAsync(
        [MinLength(1), MaxLength(50)]
        [Summary(description: "The new name of your channel, must be between 1 and 50 characters long.")]
        string newName)
    {
        return RenameAsync(newName, Context.Channel as IVoiceChannel);
    }

    [ModalInteraction("rename_modal:*", ignoreGroupNames: true)]
    public Task ModalRenameAsync(IVoiceChannel channel, RenameModal data)
    {
        return RenameAsync(data.NewName, channel);
    }

    [ComponentInteraction("rename_button:*", ignoreGroupNames: true)]
    public async Task ButtonRenameAsync(IVoiceChannel channel)
    {
        var modal = new ModalBuilder()
            .WithCustomId("rename_modal:" + channel.Id)
            .WithTitle(_locale.Get("voicepanel:rename_modal_title"))
            .AddTextInput(_locale.Get("voicepanel:rename_modal_input"), 
                "new_name", placeholder: channel.Name,
                minLength: 1, maxLength: 50, required: true);

        await RespondWithModalAsync(modal.Build());
    }

    private async Task RenameAsync(string input, IVoiceChannel channel)
    {
        var config = await configs.GetAsync(Context.Guild.Id);
        var user = Context.User as SocketGuildUser;

        var result = AutoModHelper.IsBlocked(input, user, config);
        if (result.IsBlocked)
        {
            if (config.AutoModLogChannelId is not null)
            {
                var logTo = Context.Guild.GetTextChannel(config.AutoModLogChannelId.Value);
                if (logTo is not null)
                {
                    var embed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Blocked Channel Update")
                        .WithThumbnailUrl(Context.User.GetAvatarUrl())
                        .WithDescription($"> **User:** {Context.User.Mention} (@{Context.User.Username})\n" +
                                         $"> **Channel:** {channel.Mention}\n" +
                                         $"> **Blocked Text:** `{input}`\n" +
                                         $"> **Rule:** {result.Rule.Name}\n" +
                                         $"> **Keyword:** `{result.Keyword}`")
                        .WithCurrentTimestamp();
                    await logTo.SendMessageAsync(embed: embed.Build());
                }
            }

            await RespondAsync(_locale.Get("voice:rename_blocked_term_error"), ephemeral: true);
            return;
        }

        await channel.ModifyAsync(x => x.Name = input,
            new RequestOptions { AuditLogReason = _locale.Get("log:updated_channel", Context.User.Username, Context.User.Id) });
        await RespondAsync(_locale.Get("voice:rename_success", Context.User.Mention, input), allowedMentions: AllowedMentions.None);
    }
}
