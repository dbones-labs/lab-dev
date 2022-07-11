namespace Dev.v1.Platform.Github;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "github.internal.lab.dev", ApiVersion = "v1")]
public class TeamMember : CustomKubernetesEntity<TeamMemberSpec, TeamMemberStatus>
{
    public static string GetName(string team, string login)
    {
        return $"{team}-{login}";
    }
}

public class TeamMemberSpec
{
    [Required] public string Login { get; set; } = String.Empty;
    [Required] public string Team { get; set; } = String.Empty;
}

public class TeamMemberStatus { }