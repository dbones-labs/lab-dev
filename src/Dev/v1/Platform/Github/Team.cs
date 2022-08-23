namespace Dev.v1.Platform.Github;

using k8s.Models;
using KubeOps.Operator.Entities;

[KubernetesEntity(Group = "github.internal.lab.dev", ApiVersion = "v1")]
public class Team : CustomKubernetesEntity<TeamSpec, TeamStatus>
{
    public static string GetTeamName(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName)) throw new Exception(nameof(teamName));
        return IsGuestTeamName(teamName)
            ? teamName.Replace("-guest", "")
            : teamName;
    }
    
    public static string GetGuestTeamName(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName)) throw new Exception(nameof(teamName));
        return $"{GetTeamName(teamName)}-guest";
    }

    public static bool IsGuestTeamName(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName)) throw new Exception(nameof(teamName));
        return teamName.EndsWith("-guest");
    }

    public static string PlatformLabel() => "github.lab.dev/platformTeam";
    public static string TenancyLabel() => "github.lab.dev/tenancy";
    public static string GuestLabel() => "github.lab.dev/guest";
}

public class TeamSpec
{
    // /// <summary>
    // /// the name of the team
    // /// </summary>
    // [Required] public string Name { get; set; }
    
    /// <summary>
    /// who should be able to see the team (some teams you may not want to be viewable)
    /// </summary>
    public Visibility Visibility { get; set; } = Visibility.Private;
    
    /// <summary>
    /// what is the context of the team, system teams are around automations and control
    /// </summary>
    public Type Type { get; set; } = Type.Normal;

    public string Description { get; set; } = string.Empty;
}

public class TeamStatus
{
    public int? Id { get; set; }
}