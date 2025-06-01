using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Schnauzer.Data;
using Schnauzer.Data.Models;

namespace Schnauzer.Services;

/// <summary>
///     
/// </summary>
public class ChannelCache(
    ILogger<ChannelCache> logger,
    IMemoryCache cache,
    SchnauzerDb db)
{
    /// <summary>
    ///     Check if a voice channel is a dynamic channel
    /// </summary>
    public Task<bool> ExistsAsync(ulong channelId)
    {
        return SchnauzerDb.ChannelExistsAsync(db, channelId);
    }

    /// <summary>
    ///     Get the dynamic channel settings for a voice channel
    /// </summary>
    public Task<Channel> GetAsync(ulong channelId)
    {
        return SchnauzerDb.GetChannelAsync(db, channelId);
    }

    /// <summary>
    ///     Get the dynamic channel settings for a voice channel by owner id
    /// </summary>
    public async Task<Channel> GetByOwnerAsync(ulong ownerId)
    {
        if (cache.TryGetValue<Channel>($"channel:{ownerId}", out var channel))
        {
            logger.LogDebug("Returned `channel:{Id}` from cache", ownerId);
            return channel;
        }

        channel = await SchnauzerDb.GetChannelByOwnerAsync(db, ownerId);
        if (channel is not null)
        {
            cache.Set($"channel:{ownerId}", channel);
            logger.LogDebug("Added `channel:{Id}` to cache", ownerId);
            return channel;
        }

        logger.LogDebug("`channel:{Id}` was not in the cache or db", ownerId);
        return null;
    }

    /// <summary>
    ///     
    /// </summary>
    public async Task<bool> TryCreateAsync(Channel channel)
    {
        var existing = await GetByOwnerAsync(channel.OwnerId);
        if (existing is not null)
        {
            logger.LogDebug("Did not create `channel:{Id}`, already exists", channel.OwnerId);
            return false;
        }

        await db.AddAsync(channel);
        await db.SaveChangesAsync();
        cache.Set($"channel:{channel.OwnerId}", channel);

        logger.LogDebug("Created and added `channel:{Id}` to cache and db", channel.OwnerId);
        return true;
    }

    /// <summary>
    ///     
    /// </summary>
    public async Task ModifyAsync(Channel channel)
    {
        db.Update(channel);
        await db.SaveChangesAsync();
        cache.Set($"channel:{channel.OwnerId}", channel);

        logger.LogDebug("Modified `channel:{Id}` in cache and db", channel.OwnerId);
    }

    /// <summary>
    ///     Delete a dynamic channel from both the cache and database.
    /// </summary>
    public async Task DeleteAsync(ulong ownerId)
    {
        var channel = Remove(ownerId);

        db.Remove(channel);
        await db.SaveChangesAsync();

        logger.LogDebug("Deleted `channel:{Id}` from cache and db", ownerId);
    }

    /// <summary>
    ///     Remove a dynamic channel from the cache
    /// </summary>
    public Channel Remove(ulong ownerId)
    {
        if (!cache.TryGetValue<Channel>($"channel:{ownerId}", out var channel))
        {
            logger.LogDebug("Did not delete `channel:{Id}` from cache and db, does not exist.", ownerId);
            return null;
        }

        cache.Remove($"channel:{ownerId}");
        return channel;
    }
}
