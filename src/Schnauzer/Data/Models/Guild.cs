using System.Text.Json.Serialization;

namespace Schnauzer.Data.Models;

public class Guild
{
    public required ulong Id { get; set; }
    [JsonIgnore]
    public bool? IsBanned { get; set; }
    [JsonIgnore]
    public string PreferredLocale { get; set; }
    public ulong? CreateChannelId { get; set; }

    public List<ulong> CanOwnRoleIds { get; set; }
    public bool? DenyDeafenedOwnership { get; set; }
    public bool? DenyMutedOwnership { get; set; }

    public int? DefaultLobbySize { get; set; }
    public int? MaxLobbySize { get; set; }
    public int? MaxLobbyCount { get; set; }
    public TimeSpan? AbandonedGracePeriod { get; set; }

    public bool? IsAutoModEnabled { get; set; }
    public ulong? AutoModLogChannelId { get; set; }
    public List<ulong> AutomodRuleIds { get; set; }

    [JsonIgnore]
    public List<Channel> DynamicChannels { get; set; }
}
