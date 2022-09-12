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
using v1.Platform.Rancher.External.Fleet;

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
        
        var candidateZones = await _kubernetesClient.List<Zone>(entity.Metadata.NamespaceProperty, serverQueryList);
        
        //anything we cannot do as a selector, we do in mem :(
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

        //setup create the service role for the tenancies fleet (this is in the GIT sub folder)
        
        var github = await _kubernetesClient.GetGithub(entity.Metadata.NamespaceProperty);
        //setup the fleet for the one tenancy.
        var local = "fleet-local";
        var @default = "fleet-default";
        var githubToken = "github-token";
        var repo = $"https://github.com/{github.Spec.Organisation}/{entity.Name()}.git";

        await _kubernetesClient.Ensure(() => new GitRepo
        {
            Spec = new GitRepoSpec()
            {
                Branch = "main",
                Repo = repo,
                ClientSecretName = githubToken,
                Paths = new List<string>() { "members", "services", "libraries" },
                TargetNamespace = entity.Name(),
                Targets = new List<ClusterSelector>()
                {
                    new ClusterSelector()
                    {
                        ClusterGroup = "control"
                    }
                }
            }
        }, entity.Name(), local);
        
        await _kubernetesClient.Ensure(() => new GitRepo
        {
            Spec = new GitRepoSpec()
            {
                Branch = "main",
                Repo = repo,
                ClientSecretName = githubToken,
                Paths = new List<string>() { "cd" },
                ServiceAccount = entity.Name(),
                //TargetNamespace = entity.Name(),
                Targets = new List<ClusterSelector>()
                {
                    new ClusterSelector()
                    {
                        ClusterGroup = "delivery"
                    }
                }
            }
        }, entity.Name(), @default);
        
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
        
        var local = "fleet-local";
        var @default = "fleet-default";       
        
        await _kubernetesClient.Delete<GitRepo>(entity.Name(), local);
        await _kubernetesClient.Delete<GitRepo>(entity.Name(), @default);
    }
}