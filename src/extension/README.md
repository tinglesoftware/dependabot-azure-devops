# Dependabot Azure DevOps Extension

This is the unofficial [dependabot](https://github.com/Dependabot/dependabot-core) extension for [Azure DevOps](https://azure.microsoft.com/en-gb/services/devops/). It will allow you to run Dependabot inside a build pipeline. You will find it [here in the Visual Studio marketplace](https://marketplace.visualstudio.com/items?itemName=tingle-software.dependabot). You need to install it before running dependabot in your pipeline.

## Usage

To use in a YAML pipeline:

```yaml
- task: dependabot@1
  inputs:
    packageManager: 'nuget'
```

It's up to the user to schedule the pipeline in whatever is correct for their solution.

An example of a YAML pipeline:

```yaml
trigger: none # Disable CI trigger

schedules:
- cron: '0 2 0 0 0' # daily at 2am UTC
  always: true # run even when there are no code changes
  branches:
    include:
      - master
  batch: true
  displayName: Daily

pool:
  vmImage: 'ubuntu-latest' # requires macos or ubuntu (windows is not supported)

steps:
- task: dependabot@1
  inputs:
    packageManager: 'nuget'
- task: dependabot@1
  inputs:
    packageManager: 'docker'
    directory: '/docker'
    openPullRequestsLimit: 10
    versioningStrategy: 'auto'
```

Since this task makes use of a docker image, it may take time to install the docker image. The user can choose to speed this up by using [Caching for Docker](https://docs.microsoft.com/en-us/azure/devops/pipelines/release/caching?view=azure-devops#docker-images) in Azure Pipelines. See the [source file](./src/extension/task/index.ts) for the exact image tag, e.g. `tingle/dependabot-azure-devops:0.1.1`. Subsequent dependabot tasks in a job will be faster after the first one pulls the image for the first time.

## Task Parameters

|Input|Description|
|--|--|
|packageManager|**_Required_**. The type of packages to check for dependency upgrades. Examples: `nuget`, `maven`, `gradle`, `npm`, etc. See the [updated-script](./src/update-script.rb) or [docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#package-ecosystem) for more.|
|gitHubConnection|**_Optional_**. The GitHub service connection for authenticating requests against GitHub public repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [GitHub docs](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens and [Azure DevOps docs](https://docs.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=azure-devops&tabs=yaml#sep-github) for the GitHub service connection.|
|directory|**_Optional_**. The directory in which dependencies are to be checked. Examples: `/` for root, `/src` for src folder.|
|targetBranch|**_Optional_**. The branch to be targeted when creating pull requests. When not specified, Dependabot will resolve the default branch of the repository. Examples: `master`, `main`, `develop`|
|azureDevOpsAccessToken|**_Optional_**. The Personal Access Token for accessing Azure DevOps. Supply a value here to avoid using permissions for the Build Service either because you cannot change its permissions or because you prefer that the Pull Requests be done by a different user. When not provided, the current authentication scope is used. In either case, be use the following permissions are granted: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token|
|openPullRequestsLimit|**_Optional_**. The maximum number of open pull requests to have at any one time. Defaults to 5.|
|versioningStrategy|**_Optional_**. The versioning strategy to use. See the [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#versioning-strategy). Defaults to `auto`.|

## Advanced

In some situations you might want to override the docker image tag that is pulled. For example, to get the latest bits for testing. This is discouraged. Declare a global variable, for example:

```yaml
trigger: none # Disable CI trigger

schedules:
- cron: '0 2 0 0 0' # daily at 2am UTC
  always: true # run even when there are no code changes
  branches:
    include:
      - master
  batch: true
  displayName: Daily

variables:
  DEPENDABOT_DOCKER_IMAGE_TAG: '0.1.3' # could also be 'latest'
  DEPENDABOT_EXTRA_CREDENTIALS: '[{\"type\":\"npm_registry\",\"token\":\"<redacted>\",\"registry\":\"npm.fontawesome.com\"}]' # put the credentials for private registries and feeds

pool:
  vmImage: 'ubuntu-latest' # requires macos or ubuntu (windows is not supported)

steps:
- task: dependabot@1
  inputs:
    packageManager: 'nuget'
- task: dependabot@1
  inputs:
    packageManager: 'docker'
    directory: '/docker'
    openPullRequestsLimit: 10
```

Check the logs for the image that is pulled.
