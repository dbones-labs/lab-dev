namespace Dev.v1.Platform.Discord;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Discord : CustomKubernetesEntity<DiscordSpec, DiscordStatus>
{
    
}

public class DiscordSpec
{
    public long Guild { get; set; }

    [Required] public string Credentials { get; set; } = string.Empty;
}

public class DiscordStatus { }
