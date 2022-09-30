namespace Dev.Controllers.Rancher.Internal;

using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Github.Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Platform.Rancher;
using v1.Platform.Rancher.External;
using User = v1.Platform.Rancher.User;

[EntityRbac(typeof(UserAttribute), Verbs = RbacVerb.All)]
public class UserAttributeController : IResourceController<UserAttribute>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<UserAttributeController> _logger;

    public UserAttributeController(
        IKubernetesClient kubernetesClient,
        ILogger<UserAttributeController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }
    
    
    public async Task<ResourceControllerResult?> ReconcileAsync(UserAttribute? entity)
    {
        if (entity == null) return null;
        
        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");
        var orgNs = org.Metadata.NamespaceProperty;

        //as Rancher ports users in via Github, we use this as the glue to map it back to our Account
        if (!entity.ExtraByProvider.TryGetValue("github", out var principal))
        {
            _logger.LogWarning("user {Name} has not Github account", entity.Name());
            return null;
        }
        
        var githubLoginSelector = new EqualsSelector(v1.Platform.Github.User.LoginLabel(), principal.Username);
        var githubLogins = await _kubernetesClient.List<v1.Platform.Github.User>(orgNs, githubLoginSelector);
        var githubLogin = githubLogins.FirstOrDefault();
        if (githubLogin == null)
        {
            throw new Exception($"user {entity.Name()} has not Github account");
        }

        await _kubernetesClient.Ensure(() => new User()
        {
            Metadata = new()
            {
                Name = githubLogin.Name(),
                NamespaceProperty = orgNs,
                Labels = new Dictionary<string, string>()
                {
                    { User.LoginLabel(), entity.Name()},
                    { User.AccountLabel(), githubLogin.Name() }
                }
            },

            Spec = new UserSpec()
            {
                Login = entity.Name()
            }
        }, githubLogin.Name(), orgNs);

        return null;
    }
    
    public async Task DeletedAsync(UserAttribute? entity)
    {
        if (entity == null) return;

        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");
        var orgNs = org.Metadata.NamespaceProperty;    
        
        await _kubernetesClient.Delete<V1Namespace>(entity.Metadata.Name, entity.Metadata.NamespaceProperty);

        var userLoginSelector = new EqualsSelector(User.LoginLabel(), entity.Name());
        var users = await _kubernetesClient.List<User>(orgNs, userLoginSelector);
        var user = users.FirstOrDefault();
        if (user != null)
        {
            await _kubernetesClient.Delete(user);
        }
    }
}