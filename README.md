# Disclaimer
The information contained in this README.md file and any accompanying materials (including, but not limited to, scripts, sample codes, etc.) are provided "AS-IS" and "WITH ALL FAULTS." Any estimated pricing information is provided solely for demonstration purposes and does not represent final pricing and Microsoft assumes no liability arising from your use of the information. Microsoft makes NO GUARANTEES OR WARRANTIES OF ANY KIND, WHETHER EXPRESSED OR IMPLIED, in providing this information, including any pricing information.

![CI workflow](/contoso-exchange-matching-engine/actions/workflows/ci.yml/badge.svg)
![CD Build Azure Environment workflow](/contoso-exchange-matching-engine/actions/workflows/cdenvironment.yml/badge.svg)
![CD Apps workflow](/contoso-exchange-matching-engine/actions/workflows/cdapps.yml/badge.svg)

# Introduction
This repo contains a Matching engine demo on New Single Order made via FIX protocol with multicast broadcast to Market Data Recipients running on Azure VMs (Infrastruture-as-a-service). It is build to suit our specific use case today around New Single Order (buy and sell) via FIX 4.4 protocol and there are 2 market data recipients are listening for the executed order over UDP multicast. The architecture of the demo is NOT representative of how a trading platform should be designed and run on Azure and does not speak to how we would recommand a cloud native architecture

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
1. As an Azure Subscription Owner, we will need to configure our Azure Subscription with [Azure Blueprint](BLUEPRINT.md) to create the necessary resources and resource groups. This will also create the right tags on your resources and resource groups which our scripts used in CI/CD rely on.
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
6. Now, we can trigger the Deploy Azure Resources and Environment workflow from the workflow screen.


### Secrets
| Name | Value |
| --- | --- |
| AZURE_CREDENTIALS | <pre>{<br/>&nbsp;&nbsp;&nbsp;&nbsp;"clientId": "",<br/>&nbsp;&nbsp;&nbsp;&nbsp;"clientSecret": "", <br/>&nbsp;&nbsp;&nbsp;&nbsp;"subscriptionId": "",<br/>&nbsp;&nbsp;&nbsp;&nbsp;"tenantId": "" <br/>}</pre> |
3. Now we are ready to execute our first GitHub Workflow for creating our networking environments.

# VM Setup
1. On the Matching engine server, you should execute ``` ssh-keygen ``` to generate your ssh keys. 
2. Use the value from id_rsa.pub and use this for each client VM install so you can ssh into the client VMs. To get value, first, ``` C:\Users\devuser\.ssh ```, then run ``` Get-Content id_rsa.pub ```.
3. Save this file into the Storage account inside of the container tmx and name the file ``` pub.txt ```.

# Multicast
We are using cloudSwXtch for our multicast needs. Please create an instance of it in your VNET. Note that subnetDataName should be configured to the default subnet and subnetCtrlName should be configured to appsvccs subnet.

# References
The following are libraries used in this project.

* FIX Library: https://github.com/connamara/quickfixn
* Matching Engine: https://github.com/ArjunVachhani/order-matcher
* Multicast on Azure via a virtual network switch of Market Data https://azuremarketplace.microsoft.com/en-us/marketplace/apps/swxtchiollc1614108926893.sdmc-1_1?tab=Overview
* Documentation of cloudSwXtch https://docs.swxtch.io/

## Have an issue?
You are welcome to create an issue if you need help but please note that there is no timeline to answer or resolve any issues you have with the contents of this project. Use the contents of this project at your own risk! If you are interested to volunteer to maintain this, please feel free to reach out to be added as a contributor and send Pull Requests (PR).
