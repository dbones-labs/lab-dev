namespace Dev.Controllers.Github.Internal;

using System.Text;
using Dev.v1.Platform.Github;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using k8s;
using k8s.Models;
using KubeOps.Operator.Entities;
using Marvin.JsonPatch;
using Newtonsoft.Json;
using Octokit;
using v1.Core;
using Organization = Octokit.Organization;
using Repository = v1.Platform.Github.Repository;
using User = v1.Platform.Github.User;

[Obsolete("need to refactor", false)]
public static class Collab
{
    public static string GetCollabName(string repositoryName, string teamName)
    {
        return $"{repositoryName}-{teamName}";
    }

}

public static class KubernetesClientExtensions
{
    public static async Task<Dev.v1.Core.Organization> GetOrganization(this IKubernetesClient kubernetesClient)
    {
        var config = await kubernetesClient.Get<V1ConfigMap>("lab.dev");
        if (config == null) throw new Exception("ensure you have added an organisation");
        var name = config.Data["name"];
        var @namespace = config.Data["namespace"];

        var org = await kubernetesClient.Get<Dev.v1.Core.Organization>(name, @namespace);
        if (org == null) throw new Exception("confirm the lab.dev config has the correct settings");
        return org;
    }

    public static async Task<Github> GetGithub(this IKubernetesClient kubernetesClient, string organisationNamespace)
    {
        var targetNamespace = organisationNamespace;
        var context = await kubernetesClient.Get<TenancyContext>(TenancyContext.GetName(), organisationNamespace);
        if (context != null)
        {
            targetNamespace = context.Spec.OrganizationNamespace;
        }
        
        var github = await kubernetesClient.Get<Github>("github", targetNamespace);
        if (github == null) throw new Exception("cannot find 'github' resource");
        return github;
    }

    public static async Task<v1.Core.Organization> GetOrganization(this IKubernetesClient kubernetesClient, string @namespace)
    {
        var orgs = await kubernetesClient.List<v1.Core.Organization>(@namespace);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");
        return org;
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

    public static async Task UpsertCrdLabel<T>(this IKubernetesClient client, T entity, Dictionary<string, string?> newLables) where T: class, IKubernetesObject<V1ObjectMeta>
    {
        //https://github.com/kubernetes-client/csharp/issues/78#issuecomment-372048262
        //https://github.com/kubernetes-client/csharp/issues/528
        var patch = new JsonPatchDocument<T>();
        patch.Replace(e => e.Metadata.Labels, newLables);

        var crdMeta = typeof(T).GetCrdMeta();
        if (crdMeta == null) throw new Exception($"cannot find CRD info for {typeof(T)}");

        var serializedItemToUpdate = JsonConvert.SerializeObject(patch);
        
       
        await client.ApiClient.PatchNamespacedCustomObjectAsync(
            new V1Patch(serializedItemToUpdate, V1Patch.PatchType.JsonPatch),
            crdMeta.Group,
            crdMeta.ApiVersion,
            entity.Metadata.NamespaceProperty ?? "default",
            crdMeta.PluralName,
            entity.Metadata.Name);
       

    }
    
    public static V1Namespace SetProject(this V1Namespace ns, string project, string? cluster = null)
    {
        ns.Metadata ??= new V1ObjectMeta();
        var metadata = ns.Metadata;

        var key = "field.cattle.io/projectId";
        cluster ??= "local";
        var value = $"{cluster}:{project}";

        //set the label
        metadata.Labels ??= new Dictionary<string, string>();
        metadata.Labels.Update(key, project);

        //set the annotation
        metadata.Annotations ??= new Dictionary<string, string>();
        metadata.Annotations.Update(key, value);
        return ns;
    }

    public static async Task<T> Ensure<T>(
        this IKubernetesClient client, 
        Func<T> newItem, 
        string name, 
        string? @namespace = null) 
        where T : class, IKubernetesObject<V1ObjectMeta>
    {
        var resource = await client.Get<T>(name, @namespace);
        if (resource != null) return resource;

        resource = await client.Create(newItem, name, @namespace);
        return resource;
    }
    
    public static async Task<T> Create<T>(
        this IKubernetesClient client, 
        Func<T> newItem, 
        string? name = null, 
        string? @namespace = null) 
        where T : class, IKubernetesObject<V1ObjectMeta>
    {
        var resource = newItem();
        resource.Metadata ??= new V1ObjectMeta();
        var meta = resource.Metadata;

        meta.Name ??= name;
        meta.NamespaceProperty ??= @namespace;

        if (meta.Name == null && meta.GenerateName == null)
            throw new Exception("no name passed in, or generatedName");
        
        meta.Labels ??= new Dictionary<string, string>();
        meta.Labels.Add("lab.dev/creator", "lab");

        var hasApiVersion = resource.ApiVersion != null;
        if (!hasApiVersion)
        {
            var entity = typeof(T).GetCrdMeta();
            if (entity != null)
            {
                resource.ApiVersion = $"{entity.Group}/{entity.ApiVersion}";
            }
        }
        
        var hasKindVersion = resource.Kind != null;
        if (!hasKindVersion)
        {
            var entity = typeof(T).GetCrdMeta();
            if (entity != null)
            {
                resource.Kind = entity.Kind ?? typeof(T).Name;
            }
        }
        
        resource = await client.Create(resource);
        return resource;
    }
    
}

public static class TypeExtensions
{
    public static KubernetesEntityAttribute? GetCrdMeta(this System.Type type)
    {
        return type.GetAttr<KubernetesEntityAttribute>();
    }
    public static T? GetAttr<T>(this System.Type type) where T : Attribute
    {
        var entityAttrType = typeof(T);
        var attr = type
            .GetCustomAttributes(entityAttrType, false)
            .Cast<T>()
            .FirstOrDefault();

        return attr;
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

public static class HttpAssist
{
    public static async Task<T?> Get<T>(Func<Task<T>> call) where T : class
    {
        try
        {
            var result = await call.Invoke();
            return result;
        }
        catch (NotFoundException notFound)
        {
            return null;
        }
        
    }
}

public static class DictionaryExtensions
{
    public static IDictionary<TKey, TValue> Update<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary.TryGetValue(key, out var entry))
        {
            if (entry.Equals(value))
            {
                dictionary[key] = value;
            }
        }
        else
        {
            dictionary.Add(key, value);
        }

        return dictionary;
    }

    public static IDictionary<TKey, TValue> UpdateRange<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IDictionary<TKey, TValue> source)
    {
        foreach (var value in source)
        {
            dictionary.Update(value.Key, value.Value);
        }

        return dictionary;
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