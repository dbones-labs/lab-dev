namespace Dev.Controllers.Rancher;

using System.Text.RegularExpressions;
using v1.Core;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Github.Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core.Services;

/// <summary>
/// figures up the tenancies across the zones (which zone components will act on)
/// </summary>
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
        //note currently storing this in the zone ns
        
        //see what we have setup
        var alreadyTenancyInZones = await _kubernetesClient
            .List<TenancyInZone>(
                null,
                new EqualsSelector(Tenancy.TenancyLabel(), @namespace));

        var removalCandidates = alreadyTenancyInZones.ToDictionary(zone => zone.Metadata.Name);
        
        //setup the filters
        //we run as much as we can using K8s Selectors
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
        
        //anything we cannot do as a selector, we do in mem :(
        var inMemoryQueryList = entity.Spec.ZoneFilter
            .Where(x => x.Operator == Operator.Pattern)
            .Where(x => x.Operator == Operator.StartsWith)
            .Select(x =>
            {
                //TODO REFACTOR
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

        //no filter, all access is allowed. 
        if (!inMemoryQueryList.Any())
        {
            inMemoryQueryList = new List<Func<Zone, bool>> { _ => true };
        }
        
        var candidateZones = await _kubernetesClient.List<Zone>(entity.Metadata.NamespaceProperty, serverQueryList);
        
        candidateZones = candidateZones
            .Where(x => entity.Spec.Where(x))
            .ToList();
        
        //we need to figure out
        //create
        //skip (exists)
        //delete
        
        foreach (var candidateZone in candidateZones)
        {
            var zoneInTenancyName = $"{candidateZone.Metadata.Name}-{entity.Name()}";
            if (removalCandidates.ContainsKey(zoneInTenancyName))
            {
                //already have, skip
                removalCandidates.Remove(zoneInTenancyName);
                continue;
            }
            
            //easy we create
            var zoneName = candidateZone.Metadata.Name;
            await _kubernetesClient.Ensure(() => new TenancyInZone()
            {
                Metadata = new()
                {
                    Labels = new Dictionary<string, string>
                    {
                        { Tenancy.TenancyLabel(), @namespace }
                    }
                },
                Spec = new()
                {
                    Tenancy = @namespace
                }
            }, $"{zoneName}-{@namespace}", zoneName);
        }

        //anything we have left is now considered obsolete
        foreach (var removalCandidate in removalCandidates.Values)
        {
            await _kubernetesClient.Delete(removalCandidate);
        }

        //setup create the service role for the tenancies fleet
        
        //setup the fleet for the one tenancy.

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
                null, 
                new EqualsSelector(Tenancy.TenancyLabel(), @namespace));
        
        await _kubernetesClient.Delete(zones);
    }
}