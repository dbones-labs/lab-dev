add library yamls to this folder

```yaml
# clusters represent pockets of compute, controlled by rancher
# the cluster will be stood up before this
# rancher local is the cluster-local, and does not need to be created.

apiVersion: lab.dev/v1
kind: Kubernetes
metadata:
  name: aqua
  namespace: {{namespace}}
  labels:
    lab.dev/verison: 1

---

apiVersion: lab.dev/v1
kind: Postgres
metadata:
  name: spike
  namespace: {{namespace}}
  labels:
    lab.dev/verison: 1
spec:
  credentials: spike

---

apiVersion: lab.dev/v1
kind: Rabbitmq
metadata:
  name: asuna
  namespace: {{namespace}}
  labels:
    lab.dev/verison: 1
spec:
  credentials: asuna

```