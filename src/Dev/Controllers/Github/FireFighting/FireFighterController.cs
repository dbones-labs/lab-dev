namespace Dev.Controllers.Github.FireFighting;

using DotnetKubernetesClient;
using Infrastructure;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using v1.Core.Tenancies;

[EntityRbac(typeof(FireFighter), Verbs = RbacVerb.All)]
public class FireFighterController : IResourceController<FireFighter>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitHubClient _gitHubClient;
    private readonly ILogger<FireFighterController> _logger;

    public FireFighterController(        
        IKubernetesClient kubernetesClient,
        GitHubClient gitHubClient,
        ILogger<FireFighterController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _gitHubClient = gitHubClient;
        _logger = logger;
    }
    
    public async Task<ResourceControllerResult?> ReconcileAsync(FireFighter? entity)
    {
        if (entity == null) return null;
        var spec = entity.Spec;
        var stat = entity.Status;

        if (stat.State == State.Approved)
        {
            await _gitHubClient.Issue.Labels.AddToIssue(spec.RepositoryId, spec.Number, new[] { FireFighter.Activated() });
            stat.State = State.Activated;
            return null;
        }
        
        if (stat.State == State.Activated)
        {
            if (spec.Finish >= SystemDateTime.UtcNow) return null;
            
            await _gitHubClient.Issue.Comment.Create(spec.RepositoryId, spec.Number, "FireFighter access timed-out");
            await _kubernetesClient.Delete<FireFighter>(entity.Metadata.Name, entity.Metadata.NamespaceProperty);
        }
        
        return null;
    }

    public async Task DeletedAsync(FireFighter? entity)
    {
        if (entity == null) return;
        var spec = entity.Spec;
        await _gitHubClient.Issue.Labels.RemoveFromIssue(spec.RepositoryId, spec.Number,  FireFighter.Activated() );
    }
}