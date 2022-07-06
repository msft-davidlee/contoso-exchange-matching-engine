# Disclaimer
The information contained in this README.md file and any accompanying materials (including, but not limited to, scripts, sample codes, etc.) are provided "AS-IS" and "WITH ALL FAULTS." Any estimated pricing information is provided solely for demonstration purposes and does not represent final pricing and Microsoft assumes no liability arising from your use of the information. Microsoft makes NO GUARANTEES OR WARRANTIES OF ANY KIND, WHETHER EXPRESSED OR IMPLIED, in providing this information, including any pricing information.

![CI workflow](/contoso-exchange-matching-engine/actions/workflows/ci.yml/badge.svg)
![CD Build Azure Environment workflow](/contoso-exchange-matching-engine/actions/workflows/cdenvironment.yml/badge.svg)
![CD Apps workflow](/contoso-exchange-matching-engine/actions/workflows/cdapps.yml/badge.svg)

# Introduction
This repo contains a Exchange Matching engine demo on New Single Order made via FIX protocol with multicast broadcast to Market Data Recipients running on Azure VMs (Infrastruture-as-a-service). It is build to suit our specific use case today around New Single Order (buy and sell) via FIX 4.4 protocol and there are 2 market data recipients are listening for the executed order over UDP multicast. The architecture of the demo is NOT representative of how a trading platform should be designed and run on Azure and does not speak to how we would recommand a cloud native architecture

![Architecture](/docs/TradingPlatformDemo.png)

# Getting Started
As a developer, you can run build this solution locally with the following commands within the root directory. Configure your solution to debug the following projects. Note that you wouldn't be able to debug the MarketDataRecipient unless you have the Multicast appliance configured.

* Demo.CustomerOrder
* Demo.FIXMessageProcessor
* Demo.MatchingEngine

# Setting up the Demo Environment
Follow the steps below to create the demo environment in your own Azure Subscription. Be sure to review prerequisites first!

## Prerequisites
1. Azure Subscription:
    * Owner Access to the Subscription where the solution will be running in.
    * Access to create App registrations in Azure Active Directory (AAD) which is associated with that Azure Subscription.
2. Azure CLI installed locally or Azure CloudShell configured in your Azure Subscription.
3. A GitHub account as we are planning to use GitHub Actions to drive CI/CD with it.

## Steps
1. As an Azure Subscription Owner, we will need to configure our Azure Subscription with Azure Blueprint to create the necessary resources and resource groups. This will also create the right tags on your resources and resource groups which our scripts used in CI/CD rely on.
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
    5. Clone this git repo locally and create a new branch.
    6. We should cd into the Governance directory and execute our blueprint.bicep with the following command. Note that we are just building out two environments, one for dev and one for prod.
    ```
    DeployBlueprint.ps1 -BUILD_ENV dev -SVC_PRINCIPAL_ID <Object Id for Contoso Exchange GitHub Service Principal> -MY_PRINCIPAL_ID <Object Id for your user>
    DeployBlueprint.ps1 -BUILD_ENV prod -SVC_PRINCIPAL_ID <Object Id for Contoso Exchange GitHub Service Principal> -MY_PRINCIPAL_ID <Object Id for your us
2. If you check your local repo, it contains several yaml files located in the Deployment directory and they rely on GitHub Secrets for accessing the Service Principal which is assigned access to the Azure Subscription from the previous step. From a code scanning perspective, the workflows/codeql-analysis.yml contains the code language known as Code QL that specify the scanning parameters such as language and scanning triggers. 
    1. Create an environment called dev and prod in GitHub secrets. 
    2. Create the following secrets as shown in thr secrets section below which are populate with some of the same values as in the shared Azure Key Vault.
    3. The client Id comes from the **Contoso Exchange** app registration.
    4. The client secret can be generated from the  **Contoso Exchange***.
    5. Use the subscription Id of your Azure Subscription.
    6. Use the tenant Id of your Azure AAD.
3. On your GitHub forked repo, go to settings, then Actions on the left blade, scroll down to the bottom and under Workflow permissions check the read and write permissions option.
4. Push into your git remote repo to kick off the CI process. You will also notice the CD process might have kicked off. This is because this is needed to create the initial workflow that will appear in the workflow screen. 
5. Check to make sure the workflow(s) completes successfully.
6. Navigate to the keyvault in your shared resource resource group and add a secret named vmpassword. This would be the password you would use to login to your VMs. 
7. Now, we can trigger the Deploy Azure Resources and Environment workflow from the workflow screen which will create the VMs. Use the following script and run from the root directory:
```
.\TriggerWorkflow.ps1 -DeplyEnvironment -Owner <org name> -Branch <name of your branch> -PersonalAccessToken (ConvertTo-SecureString -String "<personal access token generated from your personal profile>" -AsPlainText -Force)
```
8. If the Deploy Azure Resources and Environment workflow is successful, create a trial cloudSwXtch appliance in the matchingengine resource group. We are using cloudSwXtch for our multicast needs. Please create an instance of it in your VNET. Note that subnetDataName should be configured to the switchiodata subnet and subnetCtrlName should be configured to matchingengine subnet.
9. Now we are ready to kick off the CI process. Open Demo.CustomerOrder project and add some comments in Program.cs.
10. The Build, Test, Publish apps workflow should kick off.
11. If the Build, Test, Publish apps workflow is successful, we can now enable JIT for the trading platform VM. Follow the link for more info: https://docs.microsoft.com/en-us/azure/defender-for-cloud/just-in-time-access-usage.
    1. Launch the trading platform VM.
    2. Click on Configuration tab.
    3. Click Enable Just in time button.
    4. Once enabled, a link should show up: Open Microsoft Defender for Cloud
    5. Click on the link and request for access.
    6. Select the Remote Desktop Port. 
12. RDP into the trading platform VM. Remember that your VM password is stored in Azure Key Vault in the shared resource group.
    1. Execute ``` ssh-keygen ``` to generate your ssh keys. 
    2. Use the value from id_rsa.pub and use this for each client VM install so you can ssh into the client VMs. To get value, first, ``` C:\Users\devuser\.ssh ```, then run ``` Get-Content id_rsa.pub ```.
    3. Save this file into the Storage account inside of the container certs and name the file ``` pub.txt ``` in the shared resource group.
13. Now, we can trigger the Deploy apps into VMs workflow from the workflow screen which will deploy the apps into the VMs. Use the following script and run from the root directory:
```
.\TriggerWorkflow.ps1 -DeplyApps -Owner <org name> -Branch <name of your branch> -PersonalAccessToken (ConvertTo-SecureString -String "<personal access token generated from your personal profile>" -AsPlainText -Force)
```
14. Copy the 2 powershell scripts from the VM folder into the Desktop of the trading platform VM. 
15. Edit the 2 powershell scripts and configure the prefix which refers to the prefix + environment as part of the name of the VMs.
16. We can now run the LocalRun.ps1 file which will run the trading platform as well as ssh into the other VMs and launch the processes there. Refer to the Demo section for more information on what you can run.
    1. The FIX message processor and matching engine should be running locally. 
    2. When prompted whether to trust the ssh connections, say yes.
    3. In the 2 Market Data Recipients consoles, start with the following: ``` .\Demo.MarketDataRecipient.exe ```
    4. In the client 1 console, start with the following: ``` .\Demo.CustomerOrder.exe 10 ```
    5. In the client 2 console, start with the following: ``` .\Demo.CustomerOrder.exe 100000 ```

### Secrets
| Name | Value |
| --- | --- |
| AZURE_CREDENTIALS | <pre>{<br/>&nbsp;&nbsp;&nbsp;&nbsp;"clientId": "",<br/>&nbsp;&nbsp;&nbsp;&nbsp;"clientSecret": "", <br/>&nbsp;&nbsp;&nbsp;&nbsp;"subscriptionId": "",<br/>&nbsp;&nbsp;&nbsp;&nbsp;"tenantId": "" <br/>}</pre> |
3. Now we are ready to execute our first GitHub Workflow for creating our networking environments.

# Demo
1. Note there is a buyer and a seller client. To run a sell of 50 MSFT stocks at $150 use the following syntax on the seller client.
```
sell 50 MSFT@150
```
2. To run a buy of 25 MSFT stocks at $175, use the following syntax on the buyer client.
```
buy 25 MSFT@175
```
3. To run a sell simulation you can use the following command where 0 means sell, 15 means every 15 seconds, and 10 means for the next 10 minutes.
```
sim 0 15 10
```
4. To run a buy simulation you can use the following command where 1 means buy, 15 means every 15 seconds, and 10 means for the next 10 minutes.
```
sim 1 15 10
```
5. You can review Application Insights which will show the Application Map and be able to drill down on each order.
6. You should also take note of the Market Data Recipients where when a trade is made, it will be the receiver of the broadcast event.

# References
The following are libraries used in this repo.

* FIX Library: https://github.com/connamara/quickfixn
* Matching Engine: https://github.com/ArjunVachhani/order-matcher
* Multicast on Azure via a virtual network switch with [cloudSwXtch](https://docs.swxtch.io/). Use the following link to create an instance: https://azuremarketplace.microsoft.com/en-us/marketplace/apps/swxtchiollc1614108926893.sdmc-1_1?tab=Overview

## Have an issue?
You are welcome to create an issue if you need help but please note that there is no timeline to answer or resolve any issues you have with the contents of this project. Use the contents of this project at your own risk! If you are interested to volunteer to maintain this, please feel free to reach out to be added as a contributor and send Pull Requests (PR).
