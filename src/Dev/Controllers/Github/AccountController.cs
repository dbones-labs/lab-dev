namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using Infrastructure;
using Infrastructure.Caching;
using Internal;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Platform.Github;
using Account = v1.Core.Account;
using User = v1.Platform.Github.User;

[EntityRbac(typeof(Account), Verbs = RbacVerb.All)]
public class AccountController : ResourceController<Account>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        ILogger<AccountController> logger
        ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }
    
    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Account entity)
    {
        //lets confirm if we have the github login
        var login = entity.Spec.ExternalAccounts.FirstOrDefault(x => x.Provider == "github")?.Id;
        if (login == null) throw new Exception($"we require a github login for {entity.Metadata.Name}");

        var name = entity.Metadata.Name;
        var @namespace = entity.Metadata.NamespaceProperty;
        
        var user = await _kubernetesClient.Get<User>(name, @namespace);
        if (user == null)
        {
            //create user
            await _kubernetesClient.Create(() => new User
            {
                Metadata = new()
                {
                    Labels = new Dictionary<string, string>()
                    {
                        { User.AccountLabel(), name },
                        { User.LoginLabel(), login }
                    }
                },

                Spec = new()
                {
                    Login = login
                }
            }, name, @namespace);
        }
        else
        {
            //update the user, if needed.
            if (user.Spec.Login != login)
            {
                user.Spec.Login = login;
                user.Metadata.Labels[User.LoginLabel()] = login;
                await _kubernetesClient.Update(user);
            }
        }

        //add membership to default team.
        var github = await _kubernetesClient.GetGithub(entity.Metadata.NamespaceProperty);
        
        
        var member = await _kubernetesClient.Get<TeamMember>(name, @namespace);
        if (member == null)
        {
            await _kubernetesClient.Create(() => new TeamMember
            {
                Spec = new()
                {
                    Login = login,
                    Team = github.Spec.GlobalTeam
                }
            }, name, @namespace);
            
        }
        else
        {
            if (member.Spec.Login != login)
            {
                member.Spec.Login = login;
                await _kubernetesClient.Update(member);
            }
        }

        return null;
    }
    
    protected override async Task InternalDeletedAsync(Account entity)
    {
        var name = entity.Metadata.Name;
        var @namespace = entity.Metadata.NamespaceProperty;

        await _kubernetesClient.Delete<TeamMember>(name, @namespace);
        await _kubernetesClient.Delete<User>(name, @namespace);
    }
}