using Microsoft.EntityFrameworkCore;
using Schnauzer.Data.Models;

namespace Schnauzer.Data;

public class SchnauzerDb(
    DbContextOptions<SchnauzerDb> options
    ) : DbContext(options)
{
    public DbSet<Guild> Guilds { get; set; }
    public DbSet<Channel> Channels { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Guild>()
            .HasMany(x => x.DynamicChannels)
            .WithOne(x => x.Guild)
            .HasForeignKey(x => x.GuildId)
            .HasPrincipalKey(x => x.Id);
    }

    public static readonly Func<SchnauzerDb, ulong, Task<bool>> ConfigExistsAsync =
        EF.CompileAsyncQuery((SchnauzerDb db, ulong guildId) =>
            db.Guilds.Any(x => x.Id == guildId));

    public static readonly Func<SchnauzerDb, ulong, Task<Guild>> GetConfigAsync =
        EF.CompileAsyncQuery((SchnauzerDb db, ulong guildId) =>
            db.Guilds.SingleOrDefault(x => x.Id == guildId));


    public static readonly Func<SchnauzerDb, ulong, Task<bool>> ChannelExistsAsync =
        EF.CompileAsyncQuery((SchnauzerDb db, ulong channelId) =>
            db.Channels.Any(x => x.Id == channelId));

    public static readonly Func<SchnauzerDb, ulong, Task<Channel>> GetChannelAsync =
        EF.CompileAsyncQuery((SchnauzerDb db, ulong channelId) =>
            db.Channels.SingleOrDefault(x => x.Id == channelId));

    public static readonly Func<SchnauzerDb, ulong, Task<Channel>> GetChannelByOwnerAsync =
        EF.CompileAsyncQuery((SchnauzerDb db, ulong userId) =>
            db.Channels.SingleOrDefault(x => x.OwnerId == userId));

    public static readonly Func<SchnauzerDb, ulong, ulong, Task<bool>> IsChannelOwnerAsync =
        EF.CompileAsyncQuery((SchnauzerDb db, ulong channelId, ulong userId) =>
            db.Channels.Any(x => x.Id == channelId && x.OwnerId == userId));
}
