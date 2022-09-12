﻿namespace Dev.Controllers.Rancher;

using DotnetKubernetesClient;
using Github.Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Components.Kubernetes;
using v1.Core;
using v1.Core.Services;
using v1.Platform.Github;
using v1.Platform.Rancher;
using v1.Platform.Rancher.External.Fleet;

[EntityRbac(typeof(Rancher), Verbs = RbacVerb.All)]
public class RancherController : IResourceController<Rancher>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<RancherController> _logger;

    public RancherController(
        IKubernetesClient kubernetesClient,
        ILogger<RancherController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Rancher? entity)
    {
        if (entity == null) return null;
        if (entity.Metadata.Name != "rancher") throw new Exception("please call the Rancher Resource - 'rancher'");

        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");
        var orgNs = org.Metadata.NamespaceProperty;

        var gitRepoBase = "https://github.com/{0}/{1}.git";

        var local = "fleet-local";
        var @default = "fleet-default";
        var githubToken = "github-token";

        var github = await _kubernetesClient.GetGithub(orgNs);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        if (token == null) throw new Exception("missing the Github api token");

        var localGitRepo = string.Format(gitRepoBase, github.Spec.Organisation, local);
        var defaultGitRepo = string.Format(gitRepoBase, github.Spec.Organisation, @default);
        var orgGitRepo = string.Format(gitRepoBase, github.Spec.Organisation, github.Metadata.NamespaceProperty);


        //setup the gitops for the control/local cluster
        await _kubernetesClient.Ensure(() => new ClusterGroup()
        {
            Spec = new ClusterGroupSpec
            {
                Selector = new GroupSelector()
                {
                    MatchExpressions = new List<MatchSelector>()
                    {
                        new MatchSelector()
                        {
                            Key = "provider.cattle.io",
                            Operator = "NotIn",
                            Values = new List<string>() { "harvester" }
                        }
                    }
                }
            }
        }, "control", local);

        await _kubernetesClient.Ensure(() => new Repository()
        {
            Spec = new()
            {
                EnforceCollaborators = false,
                State = State.Active,
                Type = Type.System,
                Visibility = Visibility.Private,
                OrganizationNamespace = orgNs
            }
        }, local, orgNs);

        await _kubernetesClient.Ensure(() => new V1Secret
        {
            Type = "kubernetes.io/basic-auth",
            StringData = new Dictionary<string, string>()
            {
                { "username", github.Spec.TechnicalUser },
                { "password", token }
            }
        }, githubToken, local);

        //the main "org" repo
        await _kubernetesClient.Ensure(() => new GitRepo
        {
            Spec = new GitRepoSpec()
            {
                Branch = "main",
                Repo = localGitRepo,
                ClientSecretName = githubToken,
                Targets = new List<ClusterSelector>()
                {
                    new ClusterSelector()
                    {
                        ClusterGroup = "control"
                    }
                }
            }
        }, local, local);


        //setup the groups (to help with downstream deployments)
        await _kubernetesClient.Ensure(() => new ClusterGroup()
        {
            Spec = new ClusterGroupSpec
            {
                Selector = new GroupSelector()
                {
                    MatchExpressions = new List<MatchSelector>()
                    {
                        new()
                        {
                            Key = Zone.EnvironmentTypeLabel(),
                            Operator = "NotIn",
                            Values = new List<string> { EnvironmentType.Engineering.ToString() }
                        }
                    }
                }
            }
        }, "delivery", @default);

        await _kubernetesClient.Ensure(() => new ClusterGroup()
        {
            Spec = new ClusterGroupSpec
            {
                Selector = new GroupSelector()
                {
                    MatchExpressions = new List<MatchSelector>()
                    {
                        new()
                        {
                            Key = Zone.EnvironmentTypeLabel(),
                            Operator = "In",
                            Values = new List<string> { EnvironmentType.Engineering.ToString() }
                        }
                    }
                }
            }
        }, "engineering", @default);


        //needed to store the the gitops for handling access for downstream repo's
        await _kubernetesClient.Ensure(() => new Repository()
        {
            Spec = new()
            {
                EnforceCollaborators = false,
                State = State.Active,
                Type = Type.System,
                Visibility = Visibility.Private,
                OrganizationNamespace = orgNs
            }
        }, @default, orgNs);

        await _kubernetesClient.Ensure(() => new V1Secret
        {
            Type = "kubernetes.io/basic-auth",
            StringData = new Dictionary<string, string>()
            {
                { "username", github.Spec.TechnicalUser },
                { "password", token }
            }
        }, githubToken, @default);

        await _kubernetesClient.Ensure(() => new GitRepo
        {
            Spec = new GitRepoSpec()
            {
                Branch = "main",
                Repo = defaultGitRepo,
                ClientSecretName = githubToken,
                Targets = new List<ClusterSelector>()
                {
                    new ClusterSelector()
                    {
                        ClusterGroup = "delivery"
                    }
                }
            }
        }, @default, @default);


        //register the Control Env (local rancher cluster)
        await _kubernetesClient.Ensure(() => new Environment()
        {
            Spec = new EnvironmentSpec()
            {
                Type = EnvironmentType.Production
            }
        }, "control", orgNs);

        await _kubernetesClient.Ensure(() => new Zone()
        {
            Spec = new ZoneSpec
            {
                Cloud = "control",
                Environment = "control",
                Region = "control",
                IsControl = true
            }
        }, "control", orgNs);

        await _kubernetesClient.Ensure(() => new Kubernetes()
        {
            Spec = new KubernetesSpec()
            {
                IsPrimary = true
            }
        }, "local", "control");
        
        await _kubernetesClient.Ensure(() => new GitRepo
        {
            Spec = new GitRepoSpec()
            {
                Branch = "main",
                Repo = orgGitRepo,
                ClientSecretName = githubToken,
                Targets = new List<ClusterSelector>()
                {
                    new ClusterSelector()
                    {
                        ClusterGroup = "control"
                    }
                }
            }
        }, github.Metadata.NamespaceProperty, local);


        //Engineering defaults
        await _kubernetesClient.Ensure(() => new Environment()
        {
            Spec = new EnvironmentSpec()
            {
                Type = EnvironmentType.Engineering
            }
        }, org.Spec.Engineering, orgNs);

        await _kubernetesClient.Ensure(() => new Repository()
        {
            Spec = new()
            {
                EnforceCollaborators = false,
                State = State.Active,
                Type = Type.System,
                Visibility = Visibility.Internal,
                OrganizationNamespace = orgNs
            }
        }, "engineering", orgNs);

        await _kubernetesClient.Ensure(() => new GitRepo
        {
            Spec = new GitRepoSpec()
            {
                Branch = "main",
                Repo = defaultGitRepo,
                ClientSecretName = githubToken,
                Targets = new List<ClusterSelector>()
                {
                    new ClusterSelector()
                    {
                        ClusterGroup = "engineering"
                    }
                }
            }
        }, "engineering", @default);

        return null;
    }
}