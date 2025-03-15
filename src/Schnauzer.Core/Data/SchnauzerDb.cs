using Microsoft.EntityFrameworkCore;
using Schnauzer.Data.Models;

namespace Schnauzer.Data;

public class SchnauzerDb(
    DbContextOptions<SchnauzerDb> options
    ) : DbContext(options)
{
    public DbSet<Guild> Guilds { get; set; }
    public DbSet<DynamicChannel> Channels { get; set; }
}
