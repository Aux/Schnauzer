using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Schnauzer.Data;
using Schnauzer.Data.Models;

namespace Schnauzer.Services;

/// <summary>
///     
/// </summary>
public class ConfigCache(
    ILogger<ConfigCache> logger,
    IMemoryCache cache,
    SchnauzerDb db)
{
    /// <summary>
    ///     
    /// </summary>
    public Task<bool> ExistsAsync(ulong guildId)
    {
        return SchnauzerDb.ConfigExistsAsync(db, guildId);
    }

    /// <summary>
    ///     
    /// </summary>
    public async Task<Guild> GetAsync(ulong guildId)
    {
        if (cache.TryGetValue<Guild>($"config:{guildId}", out var config))
        {
            logger.LogDebug("Returned `config:{Id}` from cache", guildId);
            return config;
        }

        config = await SchnauzerDb.GetConfigAsync(db, guildId);
        if (config is not null)
        {
            cache.Set($"config:{guildId}", config);
            logger.LogDebug("Added `config:{Id}` to cache", guildId);
            return config;
        }

        logger.LogDebug("`config:{Id}` was not in the cache or db", guildId);
        return null;
    }

    /// <summary>
    ///     
    /// </summary>
    public async Task<bool> TryCreateAsync(Guild guild)
    {
        var existing = await GetAsync(guild.Id);
        if (existing is not null)
        {
            logger.LogDebug("Did not create `config:{Id}`, already exists", guild.Id);
            return false;
        }

        await db.AddAsync(guild);
        await db.SaveChangesAsync();
        cache.Set($"config:{guild.Id}", guild);

        logger.LogDebug("Created and added `config:{Id}` to cache and db", guild.Id);
        return true;
    }

    /// <summary>
    ///     
    /// </summary>
    public async Task ModifyAsync(Guild guild)
    {
        db.Update(guild);
        await db.SaveChangesAsync();
        cache.Set($"config:{guild.Id}", guild);

        logger.LogDebug("Modified `config:{Id}` in cache and db", guild.Id);
    }

    /// <summary>
    ///     Delete a dynamic channel from both the cache and database.
    /// </summary>
    public async Task DeleteAsync(ulong guildId)
    {
        if (!cache.TryGetValue<Guild>($"config:{guildId}", out var config))
        {
            logger.LogDebug("Did not delete `config:{Id}` from cache and db, does not exist.", guildId);
            return;
        }

        db.Remove(config);
        await db.SaveChangesAsync();
        cache.Remove($"config:{guildId}");

        logger.LogDebug("Deleted `config:{Id}` from cache and db", guildId);
    }
}
