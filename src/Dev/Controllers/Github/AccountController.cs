namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using Account = v1.Core.Account;
using User = v1.Platform.Github.User;

[EntityRbac(typeof(Account), Verbs = RbacVerb.All)]
public class AccountController : IResourceController<Account>
{
    private readonly KubernetesClient _kubernetesClient;
    private readonly GitHubClient _gitHubClient;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        KubernetesClient kubernetesClient,
        GitHubClient gitHubClient,
        ILogger<AccountController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _gitHubClient = gitHubClient;
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
        
        //update the user, if needed.
        if (user != null)
        {
            if (user.Spec.Login == login) return null;
            
            user.Spec.Login = login;
            await _kubernetesClient.Update(user);

            return null;
        }

        //create user
        user = new User
        {
            Metadata = new()
            {
                Name = name,
                NamespaceProperty = @namespace
            },

            Spec = new()
            {
                Login = login
            }
        };

        await _kubernetesClient.Create(user);
        
        //todo add membership to default teams.
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