[Main](README.md)

# DevOps (CI/CD) with GitHub Actions
GitHub Workflow allows us to create CI/CD pipelines.

# Requirements
This project contains several yaml files located in the Deployment directory. From a code scanning perspective, the workflows/codeql-analysis.yml contains the code language known as Code QL that specify the scanning parameters such as language and scanning triggers. 

## Steps
1. Create an environment called dev and prod in GitHub secrets. 
2. Create the following secrets as shown in thr secrets section below which are populate with some of the same values as in the shared Azure Key Vault.
    1. The client Id comes from the **Contoso Exchange** app registration.
    2. The client secret can be generated from the  **Contoso Exchange***.
    3. Use the subscription Id of your Azure Subscription.
    4. Use the tenant Id of your Azure AAD.
3. On your GitHub forked repo, go to settings, then Actions on the left blade, scroll down to the bottom and under Workflow permissions check the read and write permissions option.
4. Create a branch ex: ```git branch checkout -b demo``` and push into your git remote repo to kick off the CI process. Because you forked, you may see the following and it should be good to enable to workflow.

## Secrets
| Name | Value |
| --- | --- |
| AZURE_CREDENTIALS | <pre>{<br/>&nbsp;&nbsp;&nbsp;&nbsp;"clientId": "",<br/>&nbsp;&nbsp;&nbsp;&nbsp;"clientSecret": "", <br/>&nbsp;&nbsp;&nbsp;&nbsp;"subscriptionId": "",<br/>&nbsp;&nbsp;&nbsp;&nbsp;"tenantId": "" <br/>}</pre> |