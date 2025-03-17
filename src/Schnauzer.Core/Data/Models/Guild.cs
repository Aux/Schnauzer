namespace Schnauzer.Data.Models;

public class Guild
{
    public required ulong Id { get; set; }
    public ulong? CreateChannelId { get; set; }
    public bool? IsBanned { get; set; }
    public List<ulong> CanOwnRoleIds { get; set; }

    public List<DynamicChannel> DynamicChannels { get; set; }
}
