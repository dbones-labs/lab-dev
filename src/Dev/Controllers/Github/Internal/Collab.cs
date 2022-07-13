namespace Dev.Controllers.Github;

using System.Text;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using k8s.Models;
using Octokit;
using v1.Platform.Github;
using User = v1.Platform.Github.User;

[Obsolete("need to refactor", false)]
public static class Collab
{
    public static string GetCollabName(string repositoryName, string teamName)
    {
        return $"{repositoryName}-{teamName}";
    }

    public static Collaborator Create(string repositoryName, string teamName, string organizationNamespace, Membership membership)
    {
        var name = GetCollabName(repositoryName, teamName);
        return new Collaborator()
        {
            Metadata = new()
            {
                Name = name,
                NamespaceProperty = organizationNamespace
            },

            Spec = new()
            {
                Repository = repositoryName,
                Team = teamName,
                OrganizationNamespace = organizationNamespace,
                Membership = membership
            }
        };
    }
}

public static class KubernetesClientExtensions
{
    public static async Task<Github> GetGithub(this IKubernetesClient kubernetesClient, string organisationNamespace)
    {
        var github = await kubernetesClient.Get<Github>("github", organisationNamespace);
        if (github == null) throw new Exception("cannot find 'github' resource");
        return github;
    }

    public static async Task<Dev.v1.Core.Account?> GetAccountByGithubUser(this IKubernetesClient kubernetesClient, string @namespace, string login)
    {
        var users = await kubernetesClient.List<User>(
            @namespace,
            new EqualsSelector(User.LoginLabel(), login));
        
        var user = users.FirstOrDefault();
        if (user == null) return null;
        
        var account =
            await kubernetesClient.Get<Dev.v1.Core.Account>(user.Metadata.Name, @namespace);

        return account;
    }

    public static async Task<string?> GetSecret(this IKubernetesClient client, string @namespace, string name)
    {
        var secret = await client.Get<V1Secret>(name, @namespace);
        if (secret == null)
        {
            return null;
        }

        if (!secret.Data.TryGetValue("token", out var raw))
        {
            return null;
        }
        
        var value = Encoding.UTF8.GetString(raw);
        return value;
    }
    
}

public static class GithubClientExtensions
{
    public static void Auth(this GitHubClient client, string token)
    {
        var tokenAuth = new Credentials(token);
        client.Credentials = tokenAuth;
    }
}

public static class SystemDateTime
{
    private static Func<DateTime> _get = () => DateTime.UtcNow;

    public static DateTime UtcNow => _get();

    public static void Set(Func<DateTime> getDateTime)
    {
        _get = getDateTime;
    }

    public static void Reset()
    {
        _get = () => DateTime.UtcNow;
    }
}