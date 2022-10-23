namespace Dev.Infrastructure.Git;

using k8s.Models;
using LibGit2Sharp;

public class GitScope : IDisposable
{
    private readonly GitContext _context;
    private readonly ILogger _logger;
    private readonly string _gitBaseUrl;
    private readonly string _repoLocally;

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
            Username = _context.Github.Spec.TechnicalUser,
            Password = _context.Token
        };
        
        Repository.Clone(_gitBaseUrl, _repoLocally, co);
    }

    public void Fetch()
    {
        using var repository = new Repository(_repoLocally);
        
        var remote = repository.Network.Remotes["origin"];
        var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
        var fops = new FetchOptions();
        fops.CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials
        {
            Username = _context.Github.Spec.TechnicalUser,
            Password = _context.Token
        };
        
        Commands.Fetch(repository, remote.Name, refSpecs, fops, "");
        
        var inConflict = repository.RetrieveStatus(new StatusOptions()).Any(x => x.State == FileStatus.Conflicted);
        if (inConflict)
        {
            throw new Exception("cannot handle in conflict");
        }
    }

    public void Commit(string message)
    {
        using var repository = new Repository(_repoLocally);

        var status = repository.RetrieveStatus(new StatusOptions());
        if (!status.IsDirty)
        {
            _logger.LogInformation("no changes for {Repo}", _context.Repository.Name());
            return;
        }
        
        foreach (var item in status)
        {
            _logger.LogDebug("{Path} {State}", item.FilePath, item.State);
        }
        
        // Create the committer's signature and commit
        var author = new Signature(_context.Github.Spec.TechnicalUser, "@techuser", DateTime.Now);
        var committer = author;

        // Commit to the repository
        repository.Commit(message, author, committer);
    }


    public void AddFile(string filePath)
    {
        using var repository = new Repository(_repoLocally);
        var formattedFilePath = filePath.PathFormat();
        repository.Index.Add(formattedFilePath);
        Commands.Stage(repository, formattedFilePath);
    }

    public void Push(string branchName)
    {
        using var repository = new Repository(_repoLocally);
        
        PushOptions options = new PushOptions();
        options.CredentialsProvider = (url, usernameFromUrl, types) =>
            new UsernamePasswordCredentials()
            {
                Username = _context.Github.Spec.TechnicalUser,
                Password = _context.Token
            };
       
        repository.Network.Push(repository.Branches[branchName], options);
    }
 
    
    public void EnsureFile(string filePath, string content)
    {
        FolderHelpers.EnsureFile(_repoLocally, filePath, content);
        AddFile(filePath);
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
        if(Directory.Exists(_repoLocally)) Directory.Delete(_repoLocally, true);
    }
}