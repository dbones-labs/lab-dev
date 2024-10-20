# lab-dev
OrgOps - Enable DevOps with GitOps and KubeOps


! this repo is a complete work in progess !

## Simple overview (initial thoughts)

- no one should have god access, all config and setup should be by Disired state
- networking and components should be installed by purpose built CRD (i.e. RabbitMQ has better than what can be done here)

### team side structure

- an Org, has many teams/tenancies
- a Tenancy has members
- a Tenancy has many services or Library

### deployment side structure

- an environment (production, pre-production) is made of several zones (usa, asia, europe)
- a zone is made up of several componets (Compute-clusters, Rabbit, Postgres)
- a service is deployed accross several components
- production is a default, and key environment the others can be defined

### Access

#### Envonments/zones

- tenancys has access to the environments, for prod read only to compute and telemetry, but not data (that needs elevated access), for other env's access is granted to the tnancy thats owns the data for that service
- elevated access is via Gihub issues, using tags


#### github

- repo's are owned by a team
- people can be a guess to private repos, allowed pull access
- everyone is part of an org team, which can be allowed pull access
- each tenancy gets a tenacny repo, allowing then to handle issues and also self serve
- a platform teams owns zones and org

### tenancy transfering

- a service must allow for it to be remvoed from 1 tenancy and added (as a new resource, see notes about namespaces)

### CORE

Github and Rancher are the core to this design

the design needs to cater for replaceable parts

- Discrod
- Postgres
- Rabbit

## example setup

### desired state

possible repo's


```yaml

# /lab [REPO] <- manually created, but then this will assume ownership
#   dbones-labs.yaml
#
#   /platform-services
#     github.yaml
#     rancher.yaml
#     discord.yaml
#      
#   /users
#     dbones.yaml
#     bob.yaml
#     sammi.yaml
#
#   /tenencies
#     platform.yaml
#     galaxy.yaml
#     open-sourcerors.yaml
#
#   /zones
#     apex.yaml
#     frontier.yaml


# /tenency-galaxy [REPO] <- this is created from the above org repo
#   /members
#     dbones.yaml
#   /services
#     billing.yaml
#  /libraries
#    core.yaml


# /tenency-open-sourcerors [REPO]
#   /members
#     dbones.yaml
#     sammi.yaml
#  /libraries
#    auditable.yaml


# /zone-frontier this is production [REPO] <- this is created from the above org repo
#   cluster-aqua.yaml
#   postgres-spike.yaml
#   postgres-goku.yaml
#   rabbitmq-asuna.yaml

# /zone-apex  this is development [REPO]
#   cluster-saber.yaml
#   postgres-kirito.yaml
#   rabbitmq-levi.yaml

```

possible setup



```yaml
---

apiVersion: lab.dev/v1
kind: Organization
metadata:
  name: dbones-labs
  namespace: lab # sets the org namespace
  labels:
    lab.dev/verison: 1
spec:
  service:
    retainFor: 300 # in seconds, default is 1 week, allow for a service to transfer tenancies
    

---

# =========================================================
#     platform services
# =========================================================
# this that are setup before hand

apiVersion: lab.dev/v1
kind: Github
metadata:
  name: github
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  archive: true
  credentials: github-account
  globalTeam: in-the-lab
  organisation: fox-in-the-lab
  technicalUser: dev-tu

# github acc to call its api's with
# need one for Rancher, Vault, RabbitMq, Discord, etc

---

apiVersion: lab.dev/v1
kind: Discord
metadata:
  name: discord
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  guild: 123412432432
  credentials: discord-account


---

apiVersion: lab.dev/v1
kind: Rancher
metadata:
  name: rancher
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  technicalUser: user-kg5zd

---

# =========================================================
#     Accounts (users)
# =========================================================


apiVersion: lab.dev/v1
kind: Account
metadata:
  name: sammi
  namespace: lab
spec:
  externalAccounts:
  - id: 5ammi-b
    provider: github

---

apiVersion: lab.dev/v1
kind: Account
metadata:
  name: bob
  namespace: lab
spec:
  externalAccounts:
  - id: b0b-b
    provider: github

---

apiVersion: lab.dev/v1
kind: Account
metadata:
  name: dbones
  namespace: lab
spec:
  externalAccounts:
  - id: dbones
    provider: github
    

# each user/login, we will need to keep some ids for different accounts (which they create)
# the scripts will create rancher, databases, vault, etc

---

# =========================================================
#     zones clusters and shared services
# =========================================================


apiVersion: lab.dev/v1
kind: Environment
metadata:
  name: production
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  isProduction: true

---

apiVersion: lab.dev/v1
kind: Environment
metadata:
  name: development
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  isProduction: false


---


# /frontier <--- zone repo
#   kubernetes-aqua.yaml
#   postgres-spike.yaml
#   postgres-goku.yaml
#   rabbitmq-asuna.yaml


apiVersion: lab.dev/v1
kind: Zone
metadata:
  name: frontier
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  environment: production #development , see Environment
  cloud: on-prem
  region: uk

---

apiVersion: lab.dev/v1
kind: Zone
metadata:
  name: apex
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  environment: development
  cloud: on-prem
  region: uk

---

# clusters represent pockets of compute, controlled by rancher
# the cluster will be stood up before this
# rancher local is the cluster-local, and does not need to be created.

apiVersion: lab.dev/v1
kind: Kubernetes
metadata:
  name: aqua
  namespace: frontier
  labels:
    lab.dev/verison: 1

---

apiVersion: lab.dev/v1
kind: Postgres
metadata:
  name: spike
  namespace: frontier
  labels:
    lab.dev/verison: 1
spec:
  credentials: spike

---

apiVersion: lab.dev/v1
kind: Rabbitmq
metadata:
  name: asuna
  namespace: frontier
  labels:
    lab.dev/verison: 1
spec:
  credentials: asuna

---

# =========================================================
#     Tenencies
# =========================================================


# /galaxy [REPO] <- this is created from the above org repo - Tenancy
#   /members
#     dbones.yaml
#   /services
#     billing.yaml
#  /libraries
#    core.yaml

apiVersion: lab.dev/v1
kind: Tenancy
metadata:
  name: platform
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  isPlatform: true

---

# setup Rancher Project, Github Team, Postgres Roles, Discord
# rabbit does not seem to care

apiVersion: lab.dev/v1
kind: Tenancy
metadata:
  name: galaxy
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  isPlatform: false

---

apiVersion: lab.dev/v1
kind: Tenancy
metadata:
  name: pinoneers
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  isPlatform: true
  zoneFilter:
    - key: "lab.dev/environment"
      operator: StartsWith
      value: hi

---

apiVersion: lab.dev/v1
kind: Member
metadata:
  name: dbones
  namespace: platform
  labels:
    lab.dev/verison: 1
spec:
  account: dbones
  role: Owner # Member, Owner, Guest

---
apiVersion: lab.dev/v1
kind: Member
metadata:
  name: bob
  namespace: galaxy
  labels:
    lab.dev/verison: 1
spec:
  account: bob
  role: Owner

---

apiVersion: lab.dev/v1
kind: Member
metadata:
  name: sammi
  namespace: platform
  labels:
    lab.dev/verison: 1
spec:
  account: sammi
  role: Member # Member, Owner, Guest

---
apiVersion: lab.dev/v1
kind: Member
metadata:
  name: sammi
  namespace: galaxy
  labels:
    lab.dev/verison: 1
spec:
  account: sammi
  role: Guest # Member, Owner, Guest


# Github Team update, postgres roles, rabbitmq

---

# =========================================================
#     services and libraries
# =========================================================

apiVersion: lab.dev/v1
kind: Service
metadata:
  name: billing
  namespace: galaxy
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

---

apiVersion: lab.dev/v1
kind: Package
metadata:
  name: auditable
  namespace: libraries
  labels:
    lab.dev/verison: 1
spec:
  github: internal

---
```



## NOTES

- A CRD (or any resource), can be observed by more than one controller. this is in use here note the Core CRD's will be listened by multiple controllers
- A Core CRD `lab.dev/v1`, will be listed to by a `top level controller` which in turn creates a `internal.lab.dev/v1` resource, the internal resoruces will have the `internal level controller` do the actual work

in other words 
TLC will orchastraste several ILC's -> therefore: Tenency create a repo, teams, etc (the TLC does not modfiy anything but internal CRD's)
ILC will update 1 (or as little) actual things -> therefore: team create an actual team on the souce system (github) - it tries to do the least

- namepaces are not changable, this means the location of resouces can be impacted if incorrectly places
