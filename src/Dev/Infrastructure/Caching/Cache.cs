namespace Dev.Infrastructure.Caching;

using k8s.Models;
using Microsoft.Extensions.Caching.Memory;

public interface ICache
{
    void Delete<T>(string id) where T : class;
    void Set<T>(string id, T instance) where T : class;
    void Set<T>(string id, T instance, TimeSpan timeSpan) where T : class;
    T? Get<T>(string id) where T : class;
}
    
public class InMemoryCache : ICache
{
    public MemoryCache Cache { get; set; }

    public InMemoryCache()
    {
        Cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000000
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

    public T? Get<T>(string id) where T : class
    {
        var fullId = GetId<T>(id);
        if (!Cache.TryGetValue(fullId, out var settingsCached)) return null;
        return (T)settingsCached;
    }
}


public class ResourceCache
{
    private readonly ICache _cache;
    private readonly ILogger<ResourceCache> _logger;

    public ResourceCache(
        ICache cache, 
        ILogger<ResourceCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool ShouldReconcile<T>(string id, string @namespace = "default")
    {
        
        var entryId = GetId<T>(@namespace, id);
        var entry = _cache.Get<ReconciledResource>(entryId);
        var shouldCheck = entry == null;
        
        if (shouldCheck)
        {
            _logger.LogInformation("not cached {Id}, and should reconcile", entryId);
        }

        return shouldCheck;
    }
    
    public void MarkAsReconciled<T>(string id, string @namespace = "default")
    {
        var entryId = GetId<T>(@namespace, id);

        var entry = new ReconciledResource(entryId, typeof(T), SystemDateTime.UtcNow);
        var retain = Rng.Between(5, 15);

        _cache.Set(entryId, entry, TimeSpan.FromMinutes(retain));
        _logger.LogInformation("reconcile cached {Id} for {Timespan} minutes", entryId, retain);
    }

    public void MarkToReconcile<T>(string id, string @namespace = "default")
    {
        var entryId = GetId<T>(@namespace, id);
        _cache.Delete<ReconciledResource>(entryId);
        _logger.LogInformation("cleared cached {Id}", entryId);
    }
    
    
    public void MarkToRemove<T>(string id, string @namespace = "default")
    {
        var entryId = GetId<T>(@namespace, id);
        _cache.Delete<ReconciledResource>(entryId);
        _logger.LogInformation("cleared cached {Id}", entryId);
    }
    
    

    private string GetId<T>( string? @namespace, string id)
    {
        var meta = typeof(T).GetCrdMeta();
        var kind = string.IsNullOrEmpty(meta.Kind) ? typeof(T).Name : meta.Kind;
        var ns = string.IsNullOrEmpty(@namespace) ? "default" : @namespace;
        return $"[{meta.Group}]-[{kind}]-[{ns}]-[{id}]";
    }
}

public record ReconciledResource(string Id, Type Type, DateTime DateTime);


