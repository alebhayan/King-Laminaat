namespace FSH.Framework.Caching;

public sealed class CachingOptions
{
    /// <summary>Redis connection string. If empty, falls back to in-memory.</summary>
    public string Redis { get; set; } = string.Empty;

    /// <summary>Default sliding expiration if caller doesn't specify.</summary>
    public TimeSpan? DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Default absolute expiration (cap).</summary>
    public TimeSpan? DefaultAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Optional prefix (env/tenant/app) applied to all keys.</summary>
    public string? KeyPrefix { get; set; } = "fsh_";
}
