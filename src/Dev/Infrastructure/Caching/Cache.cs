namespace Dev.Infrastructure.Caching;

using Microsoft.Extensions.Caching.Memory;

public interface ICache
{
    void Delete<T>(string id) where T : class;
    void Set<T>(string id, T instance) where T : class;
    void Set<T>(string id, T instance, TimeSpan timeSpan) where T : class;
    T Get<T>(string id) where T : class;
}
    
public class InMemoryCache : ICache
{
    public MemoryCache Cache { get; set; }

    public InMemoryCache()
    {
        Cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100000
        });
    }

    private string GetId<T>(string id) where T : class
    {
        var key = string.Join(":", typeof(T).Name, id);
        return key;
    }

    public void Delete<T>(string id) where T : class
    {
        var fullId = GetId<T>(id);
        Cache.Remove(fullId);
    }

    public void Set<T>(string id, T instance) where T : class
    {
        Set(id, instance, TimeSpan.FromHours(48));
    }

    public void Set<T>(string id, T instance, TimeSpan timeSpan) where T : class
    {
        var fullId = GetId<T>(id);

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetAbsoluteExpiration(timeSpan);

        Cache.Set(fullId, instance, cacheEntryOptions);
    }

    public T Get<T>(string id) where T : class
    {
        var fullId = GetId<T>(id);
        if (!Cache.TryGetValue(fullId, out var settingsCached)) return null;
        return (T)settingsCached;
    }
}