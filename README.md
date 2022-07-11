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

# /org [REPO] <- manually created, but then this will assume ownership
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
kind: Organisation
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
  org: platform
  visibility: internal
  globalTeam: in-the-lab
  credentials: github-account
  archive: true

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
  credentials: rancher-account

---

# =========================================================
#     users
# =========================================================

apiVersion: lab.dev/v1
kind: user
metadata:
  name: dbones
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  externalAccounts:
    - provider: github
      id: d_bones
    - provider: discord
      id: 726638408860172328

# each user/login, we will need to keep some ids for different accounts (which they create)
# the scripts will create rancher, databases, vault, etc

---

# =========================================================
#     zones clusters and shared services
# =========================================================



# /frontier
#   cluster-aqua.yaml
#   postgres-spike.yaml
#   postgres-goku.yaml
#   rabbitmq-asuna.yaml


apiVersion: lab.dev/v1
kind: zone
metadata:
  name: frontier
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  environment: production #development , or custom

---

# clusters represent pockets of compute, controlled by rancher
# the cluster will be stood up before this
# rancher local is the cluster-local, and does not need to be created.

apiVersion: lab.dev/v1
kind: cluster
metadata:
  name: aqua
  namespace: zone-frontier
  labels:
    lab.dev/verison: 1

---

apiVersion: lab.dev/v1
kind: postgres
metadata:
  name: postgres-spike
  namespace: zone-frontier
  labels:
    lab.dev/verison: 1
spec:
  credentials: postgres-spike

---

apiVersion: lab.dev/v1
kind: rabbitmq
metadata:
  name: rabbitmq-asuna
  namespace: zone-frontier
  labels:
    lab.dev/verison: 1
spec:
  credentials: rabbitmq-asuna

---

# =========================================================
#     Tenencies
# =========================================================

apiVersion: lab.dev/v1
kind: tenancy
metadata:
  name: galaxy
  namespace: lab
  labels:
    lab.dev/verison: 1
spec:
  isPlatform: true # signals this is a platform team
  clusterFilter: regex-of-allowed-clusters # default all

# setup Rancher Project, Github Team, Postgres Roles, Discord
# rabbit does not seem to care

---

apiVersion: lab.dev/v1
kind: member
metadata:
  name: member-platform-dbones
  namespace: tenency-platform
  labels:
    lab.dev/verison: 1
spec:
  tenancy: platform
  user: dbones
  role: owner # member, owner, guest

# Github Team update, postgres roles, rabbitmq

---

# =========================================================
#     services and libraries
# =========================================================

apiVersion: lab.dev/v1
kind: service
metadata:
  name: billing
  namespace: tenancy-galaxy
  labels:
    lab.dev/verison: 1
spec:
  zones:
    - name: frontier
      postgres: postgres-spike
      rabbit: rabbitmq-asuna
    - name: zone-apex
      # entries here.
  visibility: internal # public and private
  
# Github repo and add to Team, postgres roles/db, rabbitmq login

# note that state items should not delete their state directly
# we may be moving ownership (git, postgres, rabbit, vault etc should wait for x days)

---

apiVersion: lab.dev/v1
kind: library
metadata:
  name: auditable
  namespace: tenancy-libraries
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
