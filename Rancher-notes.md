
# Fleet

needs to be multi-tenancy across multiple clusters

making local part of the main workspace

https://fleet.rancher.io/troubleshooting/#migrate-the-local-cluster-to-the-fleet-default-cluster


### org repo

```yaml
apiVersion: fleet.cattle.io/v1alpha1
kind: GitRepo
metadata:
  name: lab
  annotations:
    field.cattle.io/description: org repository
  namespace: fleet-local
spec:
  branch: main
  clientSecretName: gitrepo-auth-vs2qb
  insecureSkipTLSVerify: false
  repo: https://github.com/fox-in-the-lab/lab.git
```

```yaml
apiVersion: fleet.cattle.io/v1alpha1
kind: GitRepo
metadata:
  name: lab-ds
  namespace: fleet-default
spec:
  branch: main
  serviceAccount: lab # this is how we lock it down
  clientSecretName: gitrepo-auth-kl6vg
  insecureSkipTLSVerify: false
  paths: []
  repo: https://github.com/fox-in-the-lab/lab.git
  targets:
  - clusterGroup: downstream
```


secret

```yaml
apiVersion: v1
data:
  password: Z2hwX2RHd3Q0d0pkNElxQ1dmMWNxTUhCSWF0dVQ1OHE0NDA2aXZqZQ==
  username: ZGV2LXR1
kind: Secret
metadata:
  generateName: gitrepo-auth-
  name: gitrepo-auth-vs2qb
  namespace: fleet-local
type: kubernetes.io/basic-auth
```

### tenancy repo


## Clusters

labels

- `lab.dev/cluster` - `downstream`, `local`
- `lab.dev/environment` - `production` etc
- `lab.dev/region` - a location
- `lab.dev/cloud` - aws, azure, on-prem, etc
- `lab.dev/zone` - name of the zone


## Groups

groups

- downstream
- local is a differnt fleet project



```yaml
apiVersion: fleet.cattle.io/v1alpha1
kind: ClusterGroup
metadata:
  name: development
  namespace: fleet-default
spec:
  selector:
    matchExpressions: []
    matchLabels:
      lab.dev/cluster: downstream
```

```yaml
apiVersion: fleet.cattle.io/v1alpha1
kind: ClusterGroup
metadata:
  name: eu-on-prem-development
  namespace: fleet-default
spec:
  selector:
    matchLabels:
      lab.dev/cloud: on-prem
      lab.dev/environment: development
      lab.dev/region: eu
#      key: string
    matchExpressions:
      - key: lab.dev/cluster
        operator: Exists
#      - key: string
#        operator: string
#        values:
#          - string
```

## Cluster Service Account

### roles

per cluster

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: lab-dev-tenancy
  namespace: default
rules:
- apiGroups:
  - '*'
  resources:
  - '*'
  verbs:
  - get
  - watch
  - list
- apiGroups:
  - '*'
  resources:
  - secrets
  verbs:
  - create
  - delete
```




### serviceacount

per tenancy, per cluster

### bindings

per tenancy, per cluster


```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: lab
  namespace: default
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: lab-dev-tenancy 
subjects:
- kind: ServiceAccount
  name: lab
  namespace: cattle-fleet-system

---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: lab-dev-tenancy
rules:
- apiGroups:
  - '*'
  resources:
  - namespaces
  verbs:
  - create
  - delete
  - list
---

apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: lab
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: lab-dev-tenancy
subjects:
- kind: ServiceAccount
  name: lab
  namespace: cattle-fleet-system
```