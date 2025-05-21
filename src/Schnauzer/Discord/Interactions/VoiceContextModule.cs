using Discord;
using Discord.Interactions;
using Schnauzer.Data;
using Schnauzer.Discord;

namespace Schnauzer.Interactions;

[RequireChannelOwner]
public class VoiceContextModule(
    SchnauzerDb db
    ) : InteractionModuleBase<SocketInteractionContext>
{
    [UserCommand("Voice Kick")]
    public async Task KickAsync(IUser user)
    {
        await Task.Delay(0);
    }

    [UserCommand("Voice Transfer")]
    public async Task TransferAsync(IUser user)
    {
        await Task.Delay(0);
    }
}
