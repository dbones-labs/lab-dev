namespace Dev.Controllers.Github.FireFighting;

using DotnetKubernetesClient;
using Internal;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using v1.Core.Tenancies;
using v1.Platform.Github;
using Repository = Octokit.Repository;

[EntityRbac(typeof(Github), Verbs = RbacVerb.All)]
public class GithubController //: IResourceController<Github>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitHubClient _gitHubClient;
    private readonly ILogger<GithubController> _logger;

    public GithubController(
        IKubernetesClient kubernetesClient,
        GitHubClient gitHubClient,
        ILogger<GithubController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _gitHubClient = gitHubClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Github? entity)
    {
        if (entity == null) return null;
        
        var github = await _kubernetesClient.GetGithub(entity.Metadata.NamespaceProperty);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        if(!string.IsNullOrWhiteSpace(token)) _gitHubClient.Auth(token);
        
        var recently = new IssueRequest
        {
            Filter = IssueFilter.All,
            State = ItemStateFilter.All,
            SortDirection = SortDirection.Descending,
            Labels = { FireFighter.Requested(), FireFighter.Approved(), FireFighter.Activated() }
        };
        
        var issues = await _gitHubClient.Issue.GetAllForOrganization(github.Spec.Organisation, recently);

        foreach (var issue in issues)
        {
            var approved = issue.Labels.Any(x => x.Name == FireFighter.Approved());
            var activated = issue.Labels.Any(x => x.Name == FireFighter.Activated());
            
            if (activated)
            {
                await CancelFireFighter(issue.Repository, issue);
            }
            else if (approved)
            {
                await RaiseFireFighter(github, issue.Repository, issue);
            }
            else
            {
                await CleanUpOldRequest(issue.Repository, issue);
            }
        }

        return null;
    }


    private async Task CancelFireFighter(Repository repository, Issue issue)
    {
        //note this is only checked at this level, to reduce API calls to Github
        if (issue.State.Value == ItemState.Open) return;
        
        var name = $"issue-{issue.Number}";
        var @namespace = repository.Name;
        
        await _kubernetesClient.Delete<FireFighter>(name, @namespace);
    }

    private async Task RaiseFireFighter(Github github, Repository repository, Issue issue)
    {
        //find who is asking for access
        //confirm if access has been provided
        //provide access
        //clean up ticket
        
        var events = await _gitHubClient.Issue.Events.GetAllForIssue(repository.Id, issue.Number);
        var requestedEvent = events.LastOrDefault(x => x.Label.Name == FireFighter.Requested());
        var approvedEvent = events.LastOrDefault(x => x.Label.Name == FireFighter.Approved());

        if (requestedEvent == null) return;
        if (approvedEvent == null) return;

        if (requestedEvent.Actor.Login == approvedEvent.Actor.Login)
        {
            var userName = approvedEvent.Actor.Name ?? approvedEvent.Actor.Login;
            await _gitHubClient.Issue.Comment.Create(repository.Id, issue.Number,
                $"Sorry {userName}, you cannot approve your own access");
            
            return;
        }
        
        var login = requestedEvent.Actor.Login;
        var requesterAccount = await _kubernetesClient.GetAccountByGithubUser(github.Metadata.NamespaceProperty, login);
        if (requesterAccount == null)
        {
            _logger.LogError("cannot find an account for login {Login}, against issue {Number} in {Repo}",
                login, issue.Number, repository.Name);
            return;
        }
        
        var name = $"issue-{issue.Number}";
        var @namespace = repository.Name;

        var ticket = await _kubernetesClient.Get<FireFighter>(name, @namespace);
        if (ticket == null)
        {
            ticket = new()
            {
                Metadata = new()
                {
                    Name = name,
                    NamespaceProperty = @namespace,
                },
                
                Spec = new() 
                {
                    Account = requesterAccount.Metadata.Name,
                    Number = issue.Number,
                    RepositoryId = repository.Id,
                    Tenancy = @namespace,
                    Start = SystemDateTime.UtcNow,
                    Finish = SystemDateTime.UtcNow.AddHours(8)
                }
            };
            
            await _kubernetesClient.Create(ticket);
        }

        if (issue.Labels.Any(x=> x.Name == FireFighter.Requested()))
        {
            await _gitHubClient.Issue.Labels.RemoveFromIssue(repository.Id, issue.Number, FireFighter.Requested());
        }
        
        if (issue.Labels.Any(x=> x.Name == FireFighter.Approved()))
        {
            await _gitHubClient.Issue.Labels.RemoveFromIssue(repository.Id, issue.Number, FireFighter.Approved());
        }
    }
    
    
    private async Task CleanUpOldRequest(Repository repository, Issue issue)
    {
        //see how long the issue has been waiting for since
        //access was requested
        //if its has been a while, then remove the request (they can always request it again)
        
        if (!issue.UpdatedAt.HasValue)
        {
            return;
        }
        
        var timeout = issue.UpdatedAt.Value.UtcDateTime.AddMinutes(30);
        if (SystemDateTime.UtcNow > timeout)
        {
            //be sure there is a lable.
            if (issue.Labels.Any(x=> x.Name == FireFighter.Requested()))
            {
                await _gitHubClient.Issue.Labels.RemoveFromIssue(repository.Id, issue.Number, FireFighter.Requested());
            }
        }
    }
}