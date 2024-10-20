add members to this folder

```yaml
apiVersion: lab.dev/v1
kind: Member
metadata:
  name: dbones
  namespace: {{namespace}}
  labels:
    lab.dev/verison: 1
spec:
  account: dbones
  role: Owner # Member, Owner, Guest
```