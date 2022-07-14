namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using Internal;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Platform.Github;
using Account = v1.Core.Account;
using User = v1.Platform.Github.User;

[EntityRbac(typeof(Account), Verbs = RbacVerb.All)]
public class AccountController : IResourceController<Account>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IKubernetesClient kubernetesClient,
        ILogger<AccountController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }
    
    public async Task<ResourceControllerResult?> ReconcileAsync(Account? entity)
    {
        if (entity == null) return null;
        
        //lets confirm if we have the github login
        var login = entity.Spec.ExternalAccounts.FirstOrDefault(x => x.Provider == "github")?.Id;
        if (login == null) throw new Exception($"we require a github login for {entity.Metadata.Name}");

        var name = entity.Metadata.Name;
        var @namespace = entity.Metadata.NamespaceProperty;
        
        var user = await _kubernetesClient.Get<User>(name, @namespace);
        
  
        if (user == null)
        {
            //create user
            user = new User
            {
                ApiVersion = "github.internal.lab.dev/v1", 
                Kind = "User",
                Metadata = new()
                {
                    Name = name,
                    NamespaceProperty = @namespace,
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
            };

            await _kubernetesClient.Create(user);
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
            member = new TeamMember()
            {
                ApiVersion = "github.internal.lab.dev/v1",
                Kind = "TeamMember",
                Metadata = new()
                {
                    Name = name,
                    NamespaceProperty = entity.Metadata.NamespaceProperty
                },
                Spec = new()
                {
                    Login = login,
                    Team = github.Spec.GlobalTeam
                }
            };
            
            await _kubernetesClient.Create(member);
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
    
    public async Task DeletedAsync(Account? entity)
    {
        if (entity == null) return;
        
        var name = entity.Metadata.Name;
        var @namespace = entity.Metadata.NamespaceProperty;

        await _kubernetesClient.Delete<User>(name, @namespace);
    }
}