add service yamls to this folder

```yaml
apiVersion: lab.dev/v1
kind: Service
metadata:
  name: billing
  namespace: {{namespace}}
  labels:
    lab.dev/verison: 1
spec:
  zones:
    - name: frontier
      components:
        - name: spike # this will create a db and credentials
          provider: postgres
        - name: asuna
          provier: rabbitmq
    - name: zone-apex
      # entries here.
  visibility: internal # public and private
  
# Github repo and add to Team, postgres roles/db, rabbitmq login

# note that state items should not delete their state directly
# we may be moving ownership (git, postgres, rabbit, vault etc should wait for x days)
```