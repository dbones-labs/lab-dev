namespace Dev.Infrastructure;

using Caching;
using k8s;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;

public abstract class ResourceController<TEntity> : IResourceController<TEntity>
    where TEntity : IKubernetesObject<V1ObjectMeta>
{
    private readonly ResourceCache _cache;

    public ResourceController(ResourceCache cache)
    {
        _cache = cache;
    }
    
    public async Task<ResourceControllerResult?> ReconcileAsync(TEntity? entity)
    {
        if (entity == null) return null;
        if (!_cache.ShouldReconcile<TEntity>(entity.Name(), entity.Namespace()))
        {
            return null;
        }

        var result = await InternalReconcileAsync(entity);
        
        _cache.MarkAsReconciled<TEntity>(entity.Name(), entity.Namespace());
        return result;
    }

    public async Task StatusModifiedAsync(TEntity? entity)
    {
        if (entity == null) return;
        await InternalStatusModifiedAsync(entity);
        _cache.MarkToReconcile<TEntity>(entity.Name(), entity.Namespace());
    }


    public async Task DeletedAsync(TEntity? entity)
    {
        if (entity == null) return;
        await InternalDeletedAsync(entity);
        _cache.MarkToRemove<TEntity>(entity.Name(), entity.Namespace());
    }


    protected virtual Task<ResourceControllerResult?> InternalReconcileAsync(TEntity entity)
    {
        return Task.FromResult((ResourceControllerResult?)null);
    }

    protected virtual Task InternalStatusModifiedAsync(TEntity entity)
    {
        return Task.CompletedTask;
    }
    
    protected virtual Task InternalDeletedAsync(TEntity entity)
    {
        return Task.CompletedTask;
    }
}