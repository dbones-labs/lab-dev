namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using Infrastructure;
using Internal;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Core.Tenancies;
using v1.Platform.Github;

[EntityRbac(typeof(Member), Verbs = RbacVerb.All)]
public class MemberController : IResourceController<Member>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<MemberController> _logger;

    public MemberController(
        IKubernetesClient kubernetesClient,
        ILogger<MemberController> logger
        )
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Member? entity)
    {
        if (entity == null) return null;

        var baseName = entity.Metadata.NamespaceProperty;
        var teamName = entity.Spec.Role == MemberRole.Guest
            ? Team.GetGuestTeamName(baseName)
            : Team.GetTeamName(baseName);

        var entryName = TeamMember.GetName(teamName, entity.Spec.Account);

        var contextName = TenancyContext.GetName();
        var context = await _kubernetesClient.Get<TenancyContext>(contextName, entity.Metadata.NamespaceProperty);
        if (context == null) throw new Exception($"missing context {entity.Metadata.NamespaceProperty}.{contextName}");
        
        var login = await _kubernetesClient.Get<User>(entity.Spec.Account, context.Spec.OrganizationNamespace);
        if (login == null) throw new Exception($"missing login for account: {entity.Spec.Account}");

        await _kubernetesClient.Ensure(() => new TeamMember
        {
            Spec = new()
            {
                Login = login.Spec.Login,
                Team = teamName
            }
        }, entryName, entity.Metadata.NamespaceProperty);

        return null;
    }

    public async Task DeletedAsync(Member? entity)
    {
        if (entity == null) return;
        
        var baseName = entity.Metadata.NamespaceProperty;
        var teamName = entity.Spec.Role == MemberRole.Guest
            ? Team.GetGuestTeamName(baseName)
            : Team.GetTeamName(baseName);

        var entryName = TeamMember.GetName(teamName, entity.Spec.Account);

        var contextName = TenancyContext.GetName();
        var context = await _kubernetesClient.Get<TenancyContext>(contextName, entity.Metadata.NamespaceProperty);
        if (context == null) throw new Exception($"missing context {entity.Metadata.NamespaceProperty}.{contextName}");
        
        var login = await _kubernetesClient.Get<User>(entity.Spec.Account, context.Spec.OrganizationNamespace);
        if (login == null) throw new Exception($"missing login for account: {entity.Spec.Account}");

        await _kubernetesClient.Delete<TeamMember>(entryName, entity.Metadata.NamespaceProperty);
    }
}

