namespace Schnauzer.Data.Models;
public class User
{
    public required ulong Id { get; set; }
    public bool? IsBanned { get; set; }

    public string PreferredLocale { get; set; }

    public Channel DynamicChannel { get; set; }
}
