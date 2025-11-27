namespace FSH.Framework.Caching;
public interface ICacheService
{
    Task<T?> GetItemAsync<T>(string key, CancellationToken ct = default);
    Task SetItemAsync<T>(string key, T value, TimeSpan? sliding = default, CancellationToken ct = default);
    Task RemoveItemAsync(string key, CancellationToken ct = default);
    Task RefreshItemAsync(string key, CancellationToken ct = default);
    T? GetItem<T>(string key);
    void SetItem<T>(string key, T value, TimeSpan? sliding = default);
    void RemoveItem(string key);
    void RefreshItem(string key);
}