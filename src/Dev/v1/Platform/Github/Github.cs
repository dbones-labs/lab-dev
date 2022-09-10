namespace Dev.v1.Platform.Github;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Github : CustomKubernetesEntity<GithubSpec, GithubStatus>
{
    public bool IsGlobal(string team)
    {
        return team == Spec.GlobalTeam || team == Spec.ArchiveTeam;
    }

}

public class GithubSpec
{
    /// <summary>
    /// the github organisation
    /// </summary>
    [Required] 
    [AdditionalPrinterColumn] 
    public string Organisation { get; set; } = string.Empty;
    
    /// <summary>
    /// API key to call the Github API with
    /// </summary>
    [Required]
    public string Credentials { get; set; } = String.Empty;
    
    /// <summary>
    /// when services are deleted should the code be archived (Default: true)
    /// </summary>
    public bool Archive { get; set; } = true;

    /// <summary>
    /// the default visibility of all repositories (Default: private)
    /// </summary>
    public Visibility Visibility { get; set; } = Visibility.Private;
    
    /// <summary>
    /// the global team which everyone will be part of, this is so
    /// everyone can get access to private repo's
    /// </summary>
    [AdditionalPrinterColumn] 
    public string GlobalTeam { get; set; } = "org";

    /// <summary>
    /// the team which is used to admin archived projects
    /// (to make github as clean as it can be for others)
    /// </summary>
    public string ArchiveTeam { get; set; } = "archive";

    /// <summary>
    /// remove all managed items when deleted.
    /// (remove teams, repos, including archived)
    /// </summary>
    public bool CleanUp { get; set; } = false;

    /// <summary>
    /// the technical user which will be used to modify github with
    /// </summary>
    [Required] public string TechnicalUser { get; set; }
}

public class GithubStatus
{
    public V1SecretReference? CredentialsReference { get; set; }
    public int? Id { get; set; }
}