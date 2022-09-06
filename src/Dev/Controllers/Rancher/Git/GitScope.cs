namespace Dev.Controllers.Rancher.Git;

using k8s.Models;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

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

    public void RemoveFile(string filePath)
    {
        var path = Path.Combine(_repoLocally, filePath);
        if (File.Exists(path))
        {
            File.Delete(filePath);
        }
    }
    
    
    public void Dispose()
    {
        //where possible try not to delete the local repo.
        //if(Directory.Exists(_repoLocally)) Directory.Delete(_repoLocally);
        _context.Unlock();
    }


    public void CleanUp()
    {
        //TODO: test the delete logic here.
        //if(Directory.Exists(_repoLocally)) Directory.Delete(_repoLocally);
    }
}