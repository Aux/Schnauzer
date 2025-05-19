namespace Schnauzer.Data.Models;

public class DynamicChannel
{
    public required ulong Id { get; set; }
    public required ulong GuildId { get; set; }
    public required ulong CreatorId { get; set; }
    public ulong OwnerId { get; set; }
    public ulong? PanelMessageId { get; set; }
    public string PreferredLocale { get; set; }

    public Guild Guild { get; set; }
}
