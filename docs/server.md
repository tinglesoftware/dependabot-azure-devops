# Table of Contents

- [Why should I use the server?](#why-should-i-use-the-server)
- [Composition](#composition)
- [Deployment](#deployment)
  - [Service Hooks and Subscriptions](#service-hooks-and-subscriptions)
  - [Docker Compose](#docker-compose)
- [Keeping updated](#keeping-updated)
- [Development guide](#development-guide)
  - [Getting the development environment ready](#getting-the-development-environment-ready)
  - [Running the unit tests](#running-the-unit-tests)

## Why should I use the server?

Running multiple pipelines in Azure DevOps can quickly become overwhelming especially when you have many repositories. In some cases, you might want to keep one pipeline to manage multiple repositories but that can quickly get opaque with over-generalization in templates.

The extension is a good place to start but when you need to roll out across all the repositories in your organization, you would need something similar to the GitHub-hosted dependabot. The [server](../server) component in this repository provides similar functionality:

1. Support for the `schedule` node hence different update times and timezones.
2. Trigger based on pushes to the default branch. The configuration file is picked up automatically.
3. Automatic conflict resolution after PR merge. _Coming soon_.
4. Control via comments in the PR e.g. `@dependabot recreate`. _Coming soon_
5. Management UI similar to GitHub-hosted version. _Coming soon_
6. Zero maintenance, after initial deployment. Also cheap.

## Composition

The server is deployed in your Azure Subscription. To function properly, the server component is run as a single application in Azure Container Apps (Consumption Tier), backed by a single Azure SQL Server database (Basic tier), a single Service Bus namespace (Basic tier), and jobs scheduled using Azure Container Instances (Consumption Tier).

The current cost we have internally for this in the `westeurope` region:

- Azure SQL Database: approx $5/month
- Azure Service Bus namespace: approx $0.05/month
- Azure Container Instances: approx $2/month for 21 repositories
- Azure Container Apps: approx $15/month given about 80% idle time
- **Total: approx $22/month** (expected to reduce when jobs are added to Azure Container Apps, see <https://github.com/microsoft/azure-container-apps/issues/526>)

## Deployment

## Service Hooks and Subscriptions

To enable automatic pickup of configuration files, merge conflict resolution and commands via comments, subscriptions need to be setup on Azure DevOps. You should let the application create them on startup to because it is easier. See [code](https://github.com/tinglesoftware/dependabot-azure-devops/blob/b4e87bfeea133b8e9fa278c98157b7a0123bfdd3/server/Tingle.Dependabot/Workflow/AzureDevOpsProvider.cs) for the list of events subscribed to.

### Docker Compose

Create a new `docker-compose.local.yml` file to setup the project and token. For example:

```yml
services:
  server:
    environment:
      InitialSetup__Projects: '[{"url":"https://dev.azure.com/tingle/dependabot","token":"dummy","AutoComplete":true,"GithubToken":"dummy"}]'
```

Next, run the setup:

```bash
docker compose -p dependabot -f docker-compose.yml -f docker-compose.local.yml up
```

If you have made some changes in the code, you might want a fresh Docker image to be built or need to see more logs:

```bash
docker compose -p dependabot -f docker-compose.yml -f docker-compose.dev.yml -f docker-compose.local.yml up --build
```

On macOS, the default docker socket is not available. You would need run

```bash
sudo ln -s $HOME/.docker/run/docker.sock /var/run/docker.sock
```

> [!IMPORTANT]
> The project name specified as `-p dependabot` must be as is as the values are hardcoded in the code too.

## Keeping updated

If you wish to keep your deployment updated, you can create a private repository with this one as a git submodule, configure dependabot to update it then add a new workflow that deploys to your preferred host using a manual trigger (or one of your choice).

You can also choose to watch the repository so as to be notified when a new release is published.

## Development guide

### Getting the development environment ready

Install [.NET 8](https://dotnet.microsoft.com/en-us/download) and [Docker](https://docs.docker.com/engine/install/) (with Linux containers); Install project dependencies using `dotnet` or Visual Studio [Code]:

```bash
cd server
dotnet restore Tingle.Dependabot
dotnet restore Tingle.Dependabot.Tests
```

### Running the unit tests

```bash
cd server
dotnet test Tingle.Dependabot.Tests
```
