add library yamls to this folder

```yaml
apiVersion: lab.dev/v1
kind: Package
metadata:
  name: auditable
  namespace: {{namespace}}
  labels:
    lab.dev/verison: 1
spec:
  visibility: internal
```