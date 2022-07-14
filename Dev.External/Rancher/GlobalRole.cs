﻿namespace Dev.External.Rancher;

using k8s.Models;
using KubeOps.Operator.Entities;

[KubernetesEntity(Group = "cattle.test", ApiVersion = "v3")]
public class GlobalRole : CustomKubernetesEntity
{
    
}