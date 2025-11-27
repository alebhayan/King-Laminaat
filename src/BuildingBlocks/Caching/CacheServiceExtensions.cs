namespace FSH.Framework.Caching;
public static class CacheServiceExtensions
{
    public static T? GetOrSet<T>(this ICacheService cache, string key, Func<T?> getItemCallback, TimeSpan? slidingExpiration = null)
    {
        ArgumentNullException.ThrowIfNull(cache);

        T? value = cache.GetItem<T>(key);

        if (value is not null)
        {
            return value;
        }

        ArgumentNullException.ThrowIfNull(getItemCallback);
        value = getItemCallback();

        if (value is not null)
        {
            cache.SetItem(key, value, slidingExpiration);
        }

        return value;
    }

    public static async Task<T?> GetOrSetAsync<T>(this ICacheService cache, string key, Func<Task<T>> task, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);

        T? value = await cache.GetItemAsync<T>(key, cancellationToken);

        if (value is not null)
        {
            return value;
        }

        ArgumentNullException.ThrowIfNull(task);
        value = await task();

        if (value is not null)
        {
            await cache.SetItemAsync(key, value, slidingExpiration, cancellationToken);
        }

        return value;
    }
}