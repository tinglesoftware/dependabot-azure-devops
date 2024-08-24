
# Table of Contents

- [Why should I use the server?](#why-should-i-use-the-server)
- [Composition](#composition)
- [Deployment](#deployment)
  - [Single click deployment](#single-click-deployment)
  - [Deployment Parameters](#deployment-parameters)
  - [Deployment with CLI](#deployment-with-cli)
  - [Service Hooks and Subscriptions](#service-hooks-and-subscriptions)
- [Keeping updated](#keeping-updated)

# Why should I use the server?

Running multiple pipelines in Azure DevOps can quickly become overwhelming especially when you have many repositories. In some cases, you might want to keep one pipeline to manage multiple repositories but that can quickly get opaque with over-generalization in templates.

The extension is a good place to start but when you need to roll out across all the repositories in your organization, you would need something similar to the GitHub-hosted dependabot. The [server](../server) component in this repository provides similar functionality:

1. Support for the `schedule` node hence different update times and timezones.
2. Trigger based on pushes to the default branch. The configuration file is picked up automatically.
3. Automatic conflict resolution after PR merge. *Coming soon*.
4. Control via comments in the PR e.g. `@dependabot recreate`. *Coming soon*
5. Management UI similar to GitHub-hosted version. *Coming soon*
6. Zero maintenance, after initial deployment. Also cheap.

# Composition

The server is deployed in your Azure Subscription. To function properly, the server component is run as a single application in Azure Container Apps (Consumption Tier), backed by a single Azure SQL Server database (Basic tier), a single Service Bus namespace (Basic tier), and jobs scheduled using Azure Container Instances (Consumption Tier).

The current cost we have internally for this in the `westeurope` region:

- Azure SQL Database: approx $5/month
- Azure Service Bus namespace: approx $0.05/month
- Azure Container Instances: approx $2/month for 21 repositories
- Azure Container Apps: approx $15/month given about 80% idle time
- **Total: approx $22/month** (expected to reduce when jobs are added to Azure Container Apps, see <https://github.com/microsoft/azure-container-apps/issues/526>)

# Deployment

## Single click deployment

The easiest means of deployment is to use the relevant button below.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Ftinglesoftware%2Fdependabot-azure-devops%2Fmain%2Fserver%2Fmain.json)
[![Deploy to Azure US Gov](https://aka.ms/deploytoazuregovbutton)](https://portal.azure.us/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Ftinglesoftware%2Fdependabot-azure-devops%2Fmain%2Fserver%2Fmain.json)
[![Visualize](https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/visualizebutton.svg?sanitize=true)](http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2Ftinglesoftware%2Fdependabot-azure-devops%2Fmain%2Fserver%2Fmain.json)

You can also use the [server/main.json](../server/main.json) file, [server/main.bicep](../server/main.bicep) file, or pull either file from the [latest release](https://github.com/tinglesoftware/dependabot-azure-devops/releases/latest). You will need an Azure subscription and a resource group to deploy to.

## Deployment Parameters

The deployment exposes the following parameters that can be tuned to suit the setup.

|Parameter Name|Remarks|Required|Default|
|--|--|--|--|
|location|Location to deploy the resources.|No|&lt;resource-group-location&gt;|
|name|The name of all resources.|No|`dependabot`|
|projectSetups|A JSON array string representing the projects to be setup on startup. This is useful when running your own setup. Example: `[{\"url\":\"https://dev.azure.com/tingle/dependabot\",\"token\":\"dummy\",\"AutoComplete\":true}]`|Yes|&lt;empty&gt;|
|githubToken|Access token for authenticating requests to GitHub. Required for vulnerability checks and to avoid rate limiting on free requests|No|&lt;empty&gt;|
|imageTag|The image tag to use when pulling the docker containers. A tag also defines the version. You should avoid using `latest`. Example: `1.1.0`|No|&lt;version-downloaded&gt;|

> The template includes a User Assigned Managed Identity, which is used when performing Azure Resource Manager operations such as deletions. In the deployment it creates the role assignments that it needs. These role assignments are on the resource group that you deploy to.

## Deployment with CLI

> Ensure the Azure CLI tools are installed and that you are logged in.

For a one time deployment, it is similar to how you deploy other resources on Azure.

```bash
az deployment group create --resource-group DEPENDABOT \
                           --template-file main.bicep \
                           --parameters githubToken=<your-github-classic-pat> \
                           --confirm-with-what-if
```

Add more `--parameters name=value` to the script to tune the [available parameters](#deployment-parameters).

You can choose to use a parameters file because it is more convenient in a CI/CD setup, especially for token replacement.

The script becomes:

```bash
az deployment group create --resource-group DEPENDABOT \
                           --template-file main.bicep \
                           --parameters main.parameters.json \
                           --confirm-with-what-if
```

The parameters file (`main.parameters.json`):

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2015-01-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "name": {
      "value": "dependabot-fabrikam"
    },
    "githubToken": {
      "value": "#{DependabotGithubToken}#"
    },
    "imageTag": {
      "value": "#{DependabotImageTag}#"
    }
  }
}
```

## Service Hooks and Subscriptions

To enable automatic pickup of configuration files, merge conflict resolution and commands via comments, subscriptions need to be setup on Azure DevOps. You should let the application create them on startup to because it is easier. See [code](https://github.com/tinglesoftware/dependabot-azure-devops/blob/b4e87bfeea133b8e9fa278c98157b7a0123bfdd3/server/Tingle.Dependabot/Workflow/AzureDevOpsProvider.cs#L18-L21) for the list of events subscribed to.

# Keeping updated

If you wish to keep your deployment updated, you can create a private repository with this one as a git submodule, configure dependabot to update it then add a new workflow that deploys to your preferred host using a manual trigger (or one of your choice).

You can also choose to watch the repository so as to be notified when a new release is published.
