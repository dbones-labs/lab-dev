namespace Dev.Controllers.Rancher.Git;

using System.Security.Cryptography;
using DotnetKubernetesClient;
using Github.Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using v1.Core;
using v1.Platform.Github;
using v1.Platform.Rancher;
using Repository = v1.Platform.Github.Repository;

[EntityRbac(typeof(Rancher), Verbs = RbacVerb.All)]
public class RancherController : IResourceController<Rancher>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitService _gitService;
    private readonly ILogger<RancherController> _logger;

    public RancherController( 
        IKubernetesClient kubernetesClient,
        GitService gitService,
        ILogger<RancherController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _gitService = gitService;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Rancher? entity)
    {
        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");

        var orgNs = org.Metadata.NamespaceProperty;

        using (var gitScope = await _gitService.BeginScope("fleet-setup", orgNs))
        {
            gitScope.Clone();
            gitScope.Fetch();
            
            //write
            //org files
            //todo: this.
            gitScope.EnsureFile("./org/org-permissions.yaml", "yaml content");
            gitScope.EnsureFile("./org/org-permissions.yaml", "yaml content");

            gitScope.Commit("updated the org");
            gitScope.Push("main");
        }
        
        return null;
    }
}

public class GitService
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<GitService> _logger;
    private volatile bool _scopeInUse;
    private string _repositoryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "fleet-repos");

    public GitService(
        IKubernetesClient kubernetesClient,
        ILogger<GitService> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<GitScope> BeginScope(string name, string organisationNamespace)
    {
        if (_scopeInUse) throw new Exception($"git scope is in use for {name}");
        _scopeInUse = true;
        
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

internal record GitContext(
    Github Github, 
    Repository Repository, 
    string RepositoryFolder, 
    string Token, 
    Action Unlock);

public class GitScope : IDisposable
{
    private readonly GitContext _context;
    private readonly ILogger _logger;
    private readonly string _gitBaseUrl;
    private readonly string _repoLocally;
    private LibGit2Sharp.Repository _repository; 

    internal GitScope(GitContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
        _gitBaseUrl = $"https://github.com/{context.Github.Spec.Organisation}/{context.Repository.Name()}.git";
        _repoLocally = Path.Join(context.RepositoryFolder, context.Repository.Name());
    }

    public void Clone()
    {
        if (!Directory.Exists(_repoLocally))
        {
            Directory.CreateDirectory(_repoLocally);
        }

        if (FolderHelpers.HasDirectoryHaveFiles(_repoLocally))
        {
            return;
        }
        
        CloneOptions co = new CloneOptions();
        co.CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials
        {
            Username = "dev-tu", //TODO add to the resource
            Password = _context.Token
        };
        
        LibGit2Sharp.Repository.Clone(_gitBaseUrl, _repoLocally, co);
    }

    public void Fetch(string branch = "main")
    {
        using var repo = new LibGit2Sharp.Repository(_repoLocally);
        
        var remote = repo.Network.Remotes["origin"];
        var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
        
        Commands.Fetch(repo, remote.Name, refSpecs, null, "");
        
        var inConflict = repo.RetrieveStatus(new StatusOptions()).Any(x => x.State == FileStatus.Conflicted);
        if (inConflict)
        {
            throw new Exception("cannot handle in conflict");
        }
    }

    public void Commit(string message)
    {
        using var repo = new LibGit2Sharp.Repository(_repoLocally);
        
        foreach (var item in repo.RetrieveStatus(new LibGit2Sharp.StatusOptions()))
        {
            _logger.LogDebug("{path} {state}", item.FilePath, item.State);
        }
        
        //TODO: tech user
        // Create the committer's signature and commit
        var author = new Signature("dev-tu", "@techuser", DateTime.Now);
        var committer = author;

        // Commit to the repository
        var commit = repo.Commit(message, author, committer);
    }


    public void AddFile(string filePath)
    {
        using var repo = new LibGit2Sharp.Repository(_repoLocally);
        repo.Index.Add(filePath);
    }

    public void Push(string branchName)
    {
        using var repo = new LibGit2Sharp.Repository(_repoLocally);
        
        LibGit2Sharp.PushOptions options = new LibGit2Sharp.PushOptions();
        options.CredentialsProvider = new CredentialsHandler(
            (url, usernameFromUrl, types) =>
                new UsernamePasswordCredentials()
                {
                    Username = "dev-tu", //TODO add to the resource
                    Password = _context.Token
                });
       
        repo.Network.Push(repo.Branches[branchName], options);
    }
 
    
    public void EnsureFile(string filePath, string content)
    {
        FolderHelpers.EnsureFile(_repoLocally, filePath, content);
        AddFile(Path.Combine(_repoLocally, filePath));
    }
    
    
    public void Dispose()
    {
        //if(Directory.Exists(_repoLocally)) Directory.Delete(_repoLocally);
        _context.Unlock();
    }
    
   
    
    
}

public static class FolderHelpers 
{
    public static string CalculateMD5FromFile(string filename)
    {
        using var stream = File.OpenRead(filename);
        
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    
    public static string CalculateMD5FromString(string content)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;
        
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static void EnsureFile(string basePath, string filePath, string content)
    {
        var fullPath = Path.Combine(basePath, filePath);

        //file exists
        var newFile = File.Exists(fullPath);
        if (newFile)
        {
            //content is the same
            var onFileMd5 = CalculateMD5FromFile(fullPath);
            var desiredFileMd5 = CalculateMD5FromString(content);
            if (onFileMd5 == desiredFileMd5)
            {
                //no change
                return;
            }
            
            File.Delete(fullPath);
        }
        else
        {
            //ensure direct exists
            var parent = Directory.GetParent(fullPath);
            Directory.CreateDirectory(parent.FullName);
        }
        
        using var fileStream = File.OpenWrite(fullPath);
        using var streamWriter = new StreamWriter(fileStream);
        
        streamWriter.Write(content);
        streamWriter.Flush();
    }
    
    public static bool HasDirectoryHaveFiles(string path)
    {
        return Directory.EnumerateFileSystemEntries(path).Any();
    }
    
} 