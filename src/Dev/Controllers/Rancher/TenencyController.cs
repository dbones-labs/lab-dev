namespace Dev.Controllers.Rancher;

using System.Text.RegularExpressions;
using Dev.v1.Core;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Github.Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core.Services;

[EntityRbac(typeof(Tenancy), Verbs = RbacVerb.All)]
public class TenancyController : IResourceController<Tenancy>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<TenancyController> _logger;

    public TenancyController(
        IKubernetesClient kubernetesClient,
        ILogger<TenancyController> logger
        )
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Tenancy? entity)
    {
        if (entity == null) return null;

        var contextName = TenancyContext.GetName();
        var @namespace = entity.Metadata.Name; 
        
        //a place to store Tenancy information
        var ns = await _kubernetesClient.Ensure(() => new V1Namespace(), @namespace);
        var context = await _kubernetesClient.Ensure(() => new TenancyContext
        {
            Spec = new()
            {
                OrganizationNamespace = entity.Metadata.NamespaceProperty
            }
        }, contextName, @namespace);
        
        //figure out which zones this tenancy has access too
        //note currently storing this in the lab ns
        var serverQueryList = entity.Spec.ZoneFilter
            .Where(x => x.Operator != Operator.Pattern)
            .Where(x => x.Operator != Operator.StartsWith)
            .Select(x =>
            {
                ILabelSelector selector = x.Operator == Operator.Equals
                    ? new EqualsSelector(x.Key, new[] { x.Value })
                    : new NotEqualsSelector(x.Key, new[] { x.Value });
                return selector;
            })
            .ToArray();

        var candidateZones = await _kubernetesClient.List<Zone>(entity.Metadata.NamespaceProperty, serverQueryList);
        
        var inMemoryQueryList = entity.Spec.ZoneFilter
            .Where(x => x.Operator == Operator.Pattern)
            .Where(x => x.Operator == Operator.StartsWith)
            .Select(x =>
            {
                Func<Zone, bool> where = zone =>
                {
                    var key = x.Key;
                    var filterForValue = x.Value;
                    var hasLabel = zone.Metadata.Labels.TryGetValue(key, out var value);
                    if (!hasLabel) return false;

                    return (x.Operator == Operator.StartsWith)
                        ? value.StartsWith(filterForValue)
                        : Regex.IsMatch(value, filterForValue);
                };

                return where;
            } )
            .ToList();

        candidateZones = candidateZones
            .Where(x => inMemoryQueryList.All(filter => filter(x)))
            .ToList();

        foreach (var candidateZone in candidateZones)
        {
            var zoneName = candidateZone.Metadata.Name;
            await _kubernetesClient.Ensure(() => new TenancyInZone()
            {
                Metadata = new()
                {
                    Labels = new Dictionary<string, string>
                    {
                        { TenancyInZone.TenancyLabel(), @namespace }
                    }
                },
                Spec = new()
                {
                    Tenancy = zoneName
                }
            }, $"{zoneName}-{@namespace}", entity.Metadata.NamespaceProperty);
        }

        return null;
    }


    public async Task DeletedAsync(Tenancy? entity)
    {
        if (entity == null) return;
        
        var contextName = TenancyContext.GetName();
        var @namespace = entity.Metadata.Name;

        await _kubernetesClient.Delete<TenancyContext>(contextName, @namespace);
        await _kubernetesClient.Delete<V1Namespace>(@namespace);

        var zones = await _kubernetesClient
            .List<TenancyInZone>(
                entity.Metadata.NamespaceProperty, 
                new EqualsSelector(TenancyInZone.TenancyLabel(), entity.Metadata.Name));
        
        await _kubernetesClient.Delete(zones);
    }
}