# Running the server

Running multiple pipelines in Azure DevOps can quickly become overwhelming especially when you have many repositories. In some cases, you might want to keep one pipeline to manage multiple repositories but that can quickly get opaque with over-generalization in templates.

The extension is a good place to start but when you need to roll out across all the repositories in your organization, you would need something similar to the GitHub-hosted dependabot. The [server](../server) component in this repository provides similar functionality:

1. Support for the `schedule` node hence different update times and timezones.
2. Trigger based on pushes to the default branch. The configuration file is picked up automatically.
3. Automatic conflict resolution after PR merge. *Coming soon*.
4. Control via comments in the PR e.g. `@dependabot recreate`. *Coming soon*
5. Management UI similar to GitHub-hosted version. *Coming soon*
6. Zero maintenance, after initial deployment. Also cheap.

## Documentation

- [Composition](#composition)
- [Deployment](#deployment)
  - [Deployment with Buttons](#single-click-deployment)
  - [Deployment Parameters](#deployment-parameters)
  - [Deployment with CLI](#deployment-with-cli)
  - [Service Hooks and Subscriptions](#service-hooks-and-subscriptions)
- [Keeping updated](#keeping-updated)

## Composition

The server is deployed in your Azure Subscription. To function properly, the server component is run as a single application in Azure Container Apps (Consumption Tier), backed by a single Azure SQL Server database (Basic tier), a single Service Bus namespace (Basic tier), and jobs scheduled using Azure Container Instances (Consumption Tier).

The current cost we have internally for this in the `westeurope` region:

- Azure SQL Database: approx $5/month
- Azure Service Bus namespace: approx $0.05/month
- Azure Container Instances: approx $2/month for 21 repositories
- Azure Container Apps: approx $15/month given about 80% idle time
- **Total: approx $22/month** (expected to reduce when jobs are added to Azure Container Apps, see https://github.com/microsoft/azure-container-apps/issues/526)

## Deployment

### Single click deployment

The easiest means of deployment is to use the relevant button below.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Ftinglesoftware%2Fdependabot-azure-devops%2Fmain%2Fserver%2Fmain.json)
[![Deploy to Azure US Gov](https://aka.ms/deploytoazuregovbutton)](https://portal.azure.us/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Ftinglesoftware%2Fdependabot-azure-devops%2Fmain%2Fserver%2Fmain.json)
[![Visualize](https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/visualizebutton.svg?sanitize=true)](http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2Ftinglesoftware%2Fdependabot-azure-devops%2Fmain%2Fserver%2Fmain.json)

You can also use the [server/main.json](../server/main.json) file, [server/main.bicep](../server/main.bicep) file, or pull either file from the [latest release](https://github.com/tinglesoftware/dependabot-azure-devops/releases/latest). You will need an Azure subscription and a resource group to deploy to.

### Deployment Parameters

The deployment exposes the following parameters that can be tuned to suit the setup.

|Parameter Name|Remarks|Required|Default|
|--|--|--|--|
|projectUrl|The URL of the Azure DevOps project or collection. For example `https://dev.azure.com/fabrikam/DefaultCollection`. This URL must be accessible from the network that the deployment is done in. You can modify the deployment to be done in an private network but you are on your own there.|Yes|**none**|
|projectToken|Personal Access Token (PAT) for accessing the Azure DevOps project. The required permissions are: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>-&nbsp;Notifications (Read, Write & Manage).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token|Yes|**none**|
|location|Location to deploy the resources.|No|&lt;resource-group-location&gt;|
|name|The name of all resources.|No|`dependabot`|
|synchronizeOnStartup|Whether to synchronize repositories on startup. This option is useful for initial deployments since the server synchronizes every 6 hours. Leaving it on has no harm, it actually helps you find out if the token works based on the logs.|No|false|
|createOrUpdateWebhooksOnStartup|Whether to create or update Azure DevOps subscriptions on startup. This is required if you want configuration files to be picked up automatically and other event driven functionality.<br/>When this is set to `true`, ensure the value provided for `projectToken` has permissions for service hooks and the owner is a Project Administrator. Leaving this on has no harm because the server will only create new subscription if there are no existing ones based on the URL.|No|false|
|githubToken|Access token for authenticating requests to GitHub. Required for vulnerability checks and to avoid rate limiting on free requests|No|&lt;empty&gt;|
|autoComplete|Whether to set auto complete on created pull requests.|No|true|
|autoCompleteIgnoreConfigs|Identifiers of configs to be ignored in auto complete. E.g 3,4,10|No|&lt;empty&gt;|
|autoCompleteMergeStrategy|Merge strategy to use when setting auto complete on created pull requests. Allowed values: `NoFastForward`, `Rebase`, `RebaseMerge`, or `Squash`|No|`Squash`|
|autoApprove|Whether to automatically approve created pull requests.|No|false|
|jobHostType|Where to host new update jobs. Update jobs are run independent of the server. In the future, `ContainerApps` would be supported or the selection of type be removed. See [upcoming jobs support](https://github.com/microsoft/azure-container-apps/issues/526). Working with `ContainerInstances` is easy, because the instances run to completion and the server cleans up after it.|No|`ContainerInstances`|
|notificationsPassword|The password used to authenticate incoming requests from Azure DevOps|No|&lt;auto-generated&gt;|
|dockerImageRegistry|The docker registry to use when pulling the docker containers if needed. By default this will GitHub Container Registry. This can be useful if the container needs to come from an internal docker registry mirror or alternative source for testing. If the registry requires authentication ensure to assign `acrPull` permissions to the managed identity.<br />Example: `contoso.azurecr.io`|No|`ghcr.io`|
|serverImageRepository|The docker container repository to use when pulling the server docker container. This can be useful if the default container requires customizations such as custom certs.|No|`tinglesoftware/dependabot-server`|
|serverImageTag|The image tag to use when pulling the docker container. A tag also defines the version. You should avoid using `latest`. Example: `0.1.0`|No|&lt;version-downloaded&gt;|
|updaterImageRepository|The docker container repository to use when pulling the updater docker container. This can be useful if the default container requires customizations such as custom certs.|No|`tinglesoftware/dependabot-updater`|
|updaterImageTag|The image tag to use when pulling the updater docker container. A tag also defines the version. You should avoid using `latest`. Example: `0.1.0`|No|&lt;version-downloaded&gt;|
|minReplicas|The minimum number of replicas to required for the deployment. Given that scheduling runs in process, this value cannot be less than `1`. This may change in the future.|No|1|
|maxReplicas|The maximum number of replicas when automatic scaling engages. In most cases, you do not need more than 1.|No|1|

> The template includes a User Assigned Managed Identity, which is used when performing Azure Resource Manager operations such as deletions. In the deployment it creates the role assignments that it needs. These role assignments are on the resource group that you deploy to.

### Deployment with CLI

> Ensure the Azure CLI tools are installed and that you are logged in.

For a one time deployment, it is similar to how you deploy other resources on Azure.

```bash
az deployment group create --resource-group DEPENDABOT \
                           --template-file main.bicep \
                           --parameters projectUrl=<your-project-url> \
                           --parameters projectToken=<your-pat> \
                           --parameters githubToken=<your-github-classic-pat> \
                           --confirm-with-what-if
```

Add more `--parameters name=value` to the script to tune the [available parameters](#deployment-parameters).

You can choose to use a parameters file because it is more convenient in a CI/CD setup, especially for token replacement.

The script becomes:

```bash
az deployment group create --resource-group DEPENDABOT \
                           --template-file main.bicep \
                           --parameters dependabot.parameters.json \
                           --confirm-with-what-if
```

The parameters file (`dependabot.parameters.json`):

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2015-01-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "projectUrl": {
      "value": "#{System_TeamFoundationCollectionUri}##{System_TeamProject}#"
    },
    "projectToken": {
      "value": "#{DependabotProjectToken}#"
    },
    "autoComplete": {
      "value": true
    },
    "githubToken": {
      "value": "#{DependabotGithubToken}#"
    },
    "serverImageTag": {
      "value": "#{DependabotImageTag}#"
    },
    "updaterImageTag": {
      "value": "#{DependabotImageTag}#"
    }
  }
}
```

### Service Hooks and Subscriptions

To enable automatic pickup of configuration files, merge conflict resolution and commands via comments, subscriptions need to be setup on Azure DevOps. You should let the application create them on startup to because it is easier. See [code](https://github.com/tinglesoftware/dependabot-azure-devops/blob/b4e87bfeea133b8e9fa278c98157b7a0123bfdd3/server/Tingle.Dependabot/Workflow/AzureDevOpsProvider.cs#L18-L21) for the list of events subscribed to.

## Keeping updated

If you wish to keep your deployment updated, you can create a private repository with this one as a git submodule, configure dependabot to update it then add a new workflow that deploys to your preferred host using a manual trigger (or one of your choice).

You can also choose to watch the repository so as to be notified when a new release is published.
