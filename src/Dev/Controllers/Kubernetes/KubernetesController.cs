namespace Dev.Controllers.Kubernetes;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Rbac;
using Cluster = Dev.v1.Components.Kubernetes.Kubernetes;

[EntityRbac(typeof(Cluster), Verbs = RbacVerb.All)]
public class KubernetesController : IResourceController<Cluster>
{
    
}

/*
Part of a zone

cluster ID

Projects (filtered by zone)


*/