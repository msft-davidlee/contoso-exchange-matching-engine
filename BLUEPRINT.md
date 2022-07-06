[Main](README.md) | [Customer Service Web App](APP.md) | [Real-time API Data Ingestion](AKS.md)

# Introduction
Azure Blueprint will create resource and resource groups for the demo on Contoso Exchange.

# Requirements
1. All resources must be located in specific regions in US
2. The Contoso IT team would like to follow the least privilege principle. Developers should only be allowed access to specific resource groups. For example, there is a resource group for shared resources such as Azure Key Vault. The service principal used for deployment should only have read access to secrets but the developers should have both read/write access to secrets. 

# Setup
Please follow the steps below to deploy the Azure Blueprint into your Azure Subscription. Note that you will either need to use CloudShell or ensure Azure CLI is installed locally.

1. [Fork](https://docs.github.com/en/get-started/quickstart/fork-a-repo) this git repo.
2. Next, we will execute a blueprint deployment to create our environment which consists of shared resources, networking and application resource groups and take care of RBAC. The first step is to create a Service Principal which is assigned into each resource group. Take note of the tenant Id, appId and password.
```
az ad sp create-for-rbac -n "Contoso Exchange"
```
3. We need to get the Object Id for the Service principal we have created. This is used as input to our Blueprint deployment later.
```
(az ad sp show --id <appId from the previous command> | ConvertFrom-Json).id
```
4. We need to get the Object Id for our user. This is used as input to our Blueprint deployment later so we can grant oursleves access to shared resources such as Azure Key Vault.
```
(az ad signed-in-user show | ConvertFrom-Json).id
```
5. Clone this git repo locally.
6. We should cd into the Governance directory and execute our blueprint.bicep with the following command. Note that we are just building out two environments, one for dev and one for prod.
```
DeployBlueprint.ps1 -BUILD_ENV dev -SVC_PRINCIPAL_ID <Object Id for Contoso Exchange GitHub Service Principal> -MY_PRINCIPAL_ID <Object Id for your user>
DeployBlueprint.ps1 -BUILD_ENV prod -SVC_PRINCIPAL_ID <Object Id for Contoso Exchange GitHub Service Principal> -MY_PRINCIPAL_ID <Object Id for your user>
```
