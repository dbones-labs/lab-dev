namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using Infrastructure;
using Infrastructure.Caching;
using k8s.Models;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core.Services;
using v1.Platform.Github;

[EntityRbac(typeof(Service), Verbs = RbacVerb.All)]
public class ServiceController : ResourceController<Service>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<ServiceController> _logger;

    public ServiceController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        ILogger<ServiceController> logger
    ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Service entity)
    {
        var serviceName = entity.Metadata.Name; //Tenancy.GetNamespaceName(entity.Metadata.Name);
        var tenancyName = entity.Metadata.NamespaceProperty;
        var teamName = Team.GetTeamName(tenancyName);
        var guestTeamName = Team.GetGuestTeamName(tenancyName);
        var organization = await _kubernetesClient.GetOrganization();
        var organisationNamespace = organization.Namespace();
        
        var ns = await _kubernetesClient.Get<V1Namespace>(serviceName);
        if (ns == null) throw new Exception($"requires service namespace {serviceName}");

        //REPO
        var tenancyRepo = await _kubernetesClient.Get<Repository>(serviceName, serviceName);
        if (tenancyRepo == null)
        {
            await _kubernetesClient.Create(() =>
            {
                var repo =  new Repository
                {
                    Spec = new()
                    {
                        EnforceCollaborators = false,
                        State = State.Active,
                        Type = Type.Normal,
                        Visibility = Visibility.Internal,
                        OrganizationNamespace = organisationNamespace
                    }
                };
                
                SetLabels(entity, repo);
                return repo;
                
            }, serviceName, serviceName);
        }
        else
        {
            SetLabels(entity, tenancyRepo);
            var spec = tenancyRepo.Spec;
            
            //restoring a team from archive (just re-apply the correct settings)
            spec.EnforceCollaborators = false;
            spec.State = State.Active;
            spec.Type = Type.Normal;
            spec.Visibility = Visibility.Internal;
            spec.OrganizationNamespace = organisationNamespace;
            
            
            await _kubernetesClient.Update(tenancyRepo);
        }

        

        //COLLB
        var collab = Collaborator.Init(serviceName, teamName, organisationNamespace, Membership.Push);
        await _kubernetesClient.Ensure(() => collab, collab.Metadata.Name, serviceName);
        
        var guestCollab = Collaborator.Init(serviceName, guestTeamName, organisationNamespace, Membership.Pull);
        await _kubernetesClient.Ensure(() => guestCollab, guestCollab.Metadata.Name, serviceName);
        
        return null;
    }

    private static void SetLabels(Service entity, Repository repository)
    {
        //not pretty :(
        Func<IDictionary<string, string>, IDictionary<string, string>> filterOutDefaults = input =>
        {
            return input
                .Where(x => !x.Key.Contains("cattle.io"))
                .Where(x => !x.Key.Contains("kubernetes.io"))
                .ToDictionary(x => x.Key, x => x.Value);
        };
        
        Func<IDictionary<string, string>, IDictionary<string, string>> filterForDefaults = input =>
        {
            return input
                .Where(x => x.Key.Contains("cattle.io"))
                .Where(x => x.Key.Contains("kubernetes.io"))
                .ToDictionary(x => x.Key, x => x.Value);
        };
        
        repository.Metadata.Labels ??= new Dictionary<string, string>();

        var labels = filterOutDefaults(repository.Metadata.Labels);
        labels.Update(Repository.OwnerLabel(), entity.Metadata.NamespaceProperty);
        labels.Update(Repository.TypeLabel(), "service");
        labels.UpdateRange(filterOutDefaults(entity.Metadata.Labels));

        //keep the default set by k8s and rancher and apply the custom ones
        repository.Metadata.Labels = filterForDefaults(repository.Metadata.Labels);
        repository.Metadata.Labels.UpdateRange(labels);
    }

    protected override async Task InternalDeletedAsync(Service entity)
    {
        var serviceName = entity.Metadata.Name;
        var tenancyName = entity.Metadata.Name;
        var teamName = Team.GetTeamName(tenancyName);
        var guestTeamName = Team.GetGuestTeamName(tenancyName);
        
        var collabName = Collab.GetCollabName(serviceName, teamName);
        var guestCollabName = Collab.GetCollabName(serviceName, guestTeamName);
        
        await _kubernetesClient.Delete<Collaborator>(collabName, serviceName);
        await _kubernetesClient.Delete<Collaborator>(guestCollabName, serviceName);
        
        //archive repo (for audit)
        var repository = await _kubernetesClient.Get<Repository>(serviceName, serviceName);
        if (repository != null)
        {
            var spec = repository.Spec;
            spec.State = State.Archived;
            await _kubernetesClient.Update(repository);
        }
    }
}