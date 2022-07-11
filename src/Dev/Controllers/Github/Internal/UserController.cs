﻿namespace Dev.Controllers.Github.Internal;

using DotnetKubernetesClient;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using v1.Platform.Github;
using User = v1.Platform.Github.User;

[EntityRbac(typeof(User), Verbs = RbacVerb.All)]
public class UserController :  IResourceController<User>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly IGitHubClient _gitHubClient;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IKubernetesClient kubernetesClient, 
        IGitHubClient gitHubClient,
        ILogger<UserController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _gitHubClient = gitHubClient;
        _logger = logger;
    }
    
    public async Task<ResourceControllerResult?> ReconcileAsync(User? entity)
    {
        if (entity == null) return null;
        if (entity.Status.OrganisationStatus == OrganisationStatus.Member) return null;
        
        var github = await _kubernetesClient.Get<Github>("github", entity.Metadata.NamespaceProperty);
        if (github == null)
        {
            throw new Exception("cannot find 'github' resource");
        }
        
        //find the user (their account should already exist)
        if (string.IsNullOrWhiteSpace(entity.Status.GithubId))
        {
            var login = entity.Spec.Login;
            var githubUser = await _gitHubClient.User.Get(login);
            if (githubUser == null) throw new Exception($"cannot find Github user - {login}");

            _logger.LogInformation("account: {name} - setup complete", entity.Metadata.Name);
            entity.Status.GithubId = githubUser.Id.ToString();
        }
        
        //confirm the invite state
        var isMember = await _gitHubClient.Organization.Member.CheckMember(
            github.Spec.Organisation, 
            entity.Spec.Login);

        if (isMember)
        {
            entity.Status.OrganisationStatus = OrganisationStatus.Member;
            return null;
        }

        //ok we will need so send an invite
        await _gitHubClient.Organization.Member.AddOrUpdateOrganizationMembership(
            github.Spec.Organisation,
            entity.Spec.Login,
            new OrganizationMembershipUpdate { Role = MembershipRole.Member });

        entity.Status.OrganisationStatus = OrganisationStatus.Invited;

        return null;
    }
    
    public async Task DeletedAsync(User? entity)
    {
        if (entity == null) return;
        
        var github = await _kubernetesClient.Get<Github>("github");
        if (github == null) throw new Exception("cannot find 'github' resource");
        
        
        var isMember = await _gitHubClient.Organization.Member.CheckMember(
            github.Spec.Organisation, 
            entity.Spec.Login);

        if (!isMember)
        {
            return;
        }

        await _gitHubClient.Organization.Member.Delete(
            github.Spec.Organisation,
            entity.Spec.Login);
    }
}