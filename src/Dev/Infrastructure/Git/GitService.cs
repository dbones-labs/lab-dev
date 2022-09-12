namespace Dev.Controllers.Rancher.Git;

using DotnetKubernetesClient;
using Github.Internal;
using v1.Platform.Github;

public class GitService
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<GitService> _logger;
    private volatile bool _scopeInUse;
    private string _repositoryDirectory = Path.Combine(FolderHelpers.BaseDirectory, "fleet-repos");
    private object _lock = new object();

    public GitService(
        IKubernetesClient kubernetesClient,
        ILogger<GitService> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<GitScope> BeginScope(string name, string organisationNamespace)
    {
        lock (_lock)
        {
            if (_scopeInUse) throw new Exception($"git scope is in use for {name}");
            _scopeInUse = true;
        }
        
        Repository? repoMeta;
        string? token;
        Github github;
        
        if (!Directory.Exists(_repositoryDirectory))
        {
            Directory.CreateDirectory(_repositoryDirectory);
        }
        
        try
        {
            
            github = await _kubernetesClient.GetGithub(organisationNamespace);
            token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
            repoMeta = await _kubernetesClient.Get<Repository>(name, organisationNamespace);
            repoMeta ??= await _kubernetesClient.Get<Repository>(name, name); //refactor please
            
            if (repoMeta == null)
            {
                throw new Exception($"cannot find {name} repo");
            }
        }
        catch (Exception e)
        {
            _scopeInUse = false;
            throw;
        }

        var context = new GitContext(
            github, 
            repoMeta, 
            _repositoryDirectory, 
            token, 
            () => _scopeInUse = false);

        return new GitScope(context, _logger);
    }
    
}