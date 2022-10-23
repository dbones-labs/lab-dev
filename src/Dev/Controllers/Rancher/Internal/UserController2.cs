namespace Dev.Controllers.Rancher.Internal;

using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Github.Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using v1.Core;
using v1.Platform.Rancher;
using v1.Platform.Rancher.External;
using User = v1.Platform.Rancher.External.User;
using InternalUser = Dev.v1.Platform.Rancher.User;

[EntityRbac(typeof(User), Verbs = RbacVerb.All)]
public class UserController : IResourceController<User>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IKubernetesClient kubernetesClient,
        ILogger<UserController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(User? entity)
    {
        if (entity == null) return null;

        var org = await _kubernetesClient.GetOrganization();
        var orgNs = org.Namespace();

        var githubIdEntry = entity.PrincipalIds.FirstOrDefault(x => x.StartsWith("github_user://"));
        if (githubIdEntry == null)
        {
            return null;
        }

        var githubId = githubIdEntry.Replace("github_user://", "");

        var users = await _kubernetesClient.List<v1.Platform.Github.User>(orgNs,
            new EqualsSelector(v1.Platform.Github.User.IdLabel(), githubId));
        var user = users.FirstOrDefault();
        if (user == null)
        {
            throw new Exception($"github user does not exist yet for {entity.DisplayName} - {entity.Name()}");
        }

        
        await _kubernetesClient.Ensure(() => new InternalUser()
        {
            Metadata = new()
            {
                Name = user.Name(),
                NamespaceProperty = orgNs,
                Labels = new Dictionary<string, string>()
                {
                    { InternalUser.LoginLabel(), entity.Name() },
                    { InternalUser.AccountLabel(), user.Name() }
                }
            },

            Spec = new UserSpec()
            {
                Login = entity.Name()
            }
        }, user.Name(), orgNs);

        return null;


    }
}