using Discord;
using Discord.Interactions;

namespace Schnauzer.Interactions;

// We'll place these commands as mentions inside of the voice owner panel

[Group("voice", "Management commands for channel owners.")]
public class VoiceOwnerModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("rename", "Rename a voice channel you own.")]
    public async Task RenameAsync(string name)
    {
        await Task.Delay(0);
    }

    [SlashCommand("limit", "Change the user limit of a voice channel you own.")]
    public async Task RenameAsync(int limit)
    {
        await Task.Delay(0);
    }

    [SlashCommand("kick", "Kick a user from a voice channel you own.")]
    public async Task KickAsync(IGuildUser user, string reason)
    {
        await Task.Delay(0);
    }

    [SlashCommand("block", "Block a user from accessing a voice channel you own.")]
    public async Task BlockAsync(IGuildUser user, string reason)
    {
        await Task.Delay(0);
    }

    [SlashCommand("give", "Give ownership of a voice channel to another user.")]
    public async Task GiveAsync(IGuildUser user)
    {
        await Task.Delay(0);
    }
}
