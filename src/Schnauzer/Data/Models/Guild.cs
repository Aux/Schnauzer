namespace Schnauzer.Data.Models;

public class Guild
{
    public required ulong Id { get; set; }
    public bool? IsBanned { get; set; }
    public ulong? CreateChannelId { get; set; }
    public string PreferredLocale { get; set; }

    public List<ulong> CanOwnRoleIds { get; set; }
    public int? DefaultLobbySize { get; set; }
    public int? MaxLobbySize { get; set; }

    public bool? IsAutoModEnabled { get; set; }
    public ulong? AutoModLogChannelId { get; set; }
    public List<ulong> AutomodRuleIds { get; set; }

    public List<Channel> DynamicChannels { get; set; }
}
