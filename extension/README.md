# Dependabot Azure DevOps Extension

This is the unofficial [dependabot](https://github.com/Dependabot/dependabot-core) extension for [Azure DevOps](https://azure.microsoft.com/en-gb/services/devops/). It will allow you to run Dependabot inside a build pipeline and is accessible[here in the Visual Studio marketplace](https://marketplace.visualstudio.com/items?itemName=tingle-software.dependabot). The extension first has to be installed before you can run it in your pipeline.

## Usage

Add a configuration file stored at `.github/dependabot.yml` or  `.github/dependabot.yaml` conforming to the [official spec](https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates).

To use in a YAML pipeline:

```yaml
- task: dependabot@1
  inputs:
    useConfigFile: true
```

You can schedule the pipeline as is appropriate for your solution.

An example of a YAML pipeline:

```yaml
trigger: none # Disable CI trigger

schedules:
- cron: '0 2 * * *' # daily at 2am UTC
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
    useConfigFile: true
```

This task makes use of a docker image, which may take time to install. Subsequent dependabot tasks in a job will be faster after initially pulling the image using the first task. An alternative way to run your pipelines faster is by leveraging Docker caching in Azure Pipelines (See [#113](https://github.com/tinglesoftware/dependabot-azure-devops/issues/113#issuecomment-894771611)). 

## Task Parameters

|Input|Description|
|--|--|
|useConfigFile|**_Optional_** Determines if to use the config file or not. Defaults to `false`.|
|packageManager|**_Required (when useConfig=false)_**. The type of packages to check for dependency upgrades. Examples: `nuget`, `maven`, `gradle`, `npm`, etc. See the [updated-script](./update-script.rb) or [docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#package-ecosystem) for more.|
|directory|**_Optional_**. The directory in which dependencies are to be checked. Examples: `/` for root, `/src` for src folder.|
|targetBranch|**_Optional_**. The branch to be targeted when creating pull requests. When not specified, Dependabot will resolve the default branch of the repository. Examples: `master`, `main`, `develop`|
|openPullRequestsLimit|**_Optional_**. The maximum number of open pull requests to have at any one time. Defaults to 5.|
|versioningStrategy|**_Optional_**. The versioning strategy to use. See the [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#versioning-strategy). Defaults to `auto`.|
|failOnException|**_Optional_**. Determines if the execution should fail when an exception occurs. Defaults to `true`.|
|milestone|**_Optional_**. The identifier of the work item to be linked to the Pull Requests that dependabot creates.|
|updaterOptions|**_Optional_**. Comma separated list of updater options; available options depend on the ecosystem. Example: `goprivate=true,kubernetes_updates=true`.|
|setAutoComplete|**_Optional_**. Determines if the pull requests that dependabot creates should have auto complete set. When set to `true`, pull requests that pass all policies will be merged automatically.|
|mergeStrategy|**_Optional_**. The merge strategy to use when auto complete is set. Learn more [here](https://docs.microsoft.com/en-us/javascript/api/azure-devops-extension-api/gitpullrequestmergestrategy). Defaults to `2` (Squash merge).|
|autoApprove|**_Optional_**. Determines if the pull requests that dependabot creates should be automatically completed. When set to `true`, pull requests will be approved automatically by the user specified in the `autoApproveUserEmail` field.|
|autoApproveUserEmail|**_Optional_**. Email of the user that should be used to automatically approve pull requests. Required if `autoApprove` is set to `true`.|
|autoApproveUserToken|**_Optional_**. A personal access token that is assigned to the user specified in `autoApproveUserEmail` to automatically approve the created PR. Required if `autoApprove` is set to `true`.|
|gitHubConnection|**_Optional_**. The GitHub service connection for authenticating requests against GitHub repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [GitHub docs](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens and [Azure DevOps docs](https://docs.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=azure-devops&tabs=yaml#sep-github) for the GitHub service connection.|
|gitHubAccessToken|**_Optional_**. The raw GitHub PAT for authenticating requests against GitHub repositories. Use this in place of `gitHubConnection` such as when it is not possible to create a service connection.|
|azureDevOpsAccessToken|**_Optional_**. The Personal Access Token for accessing Azure DevOps. Supply a value here to avoid using permissions for the Build Service either because you cannot change its permissions or because you prefer that the Pull Requests be done by a different user. When not provided, the current authentication scope is used. In either case, be use the following permissions are granted: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token|
|targetRepositoryName|**_Optional_**. The name of the repository to target for processing. If this value is not supplied then the Build Repository Name is used. Supplying this value allows creation of a single pipeline that runs Dependablot against multiple repositories.|
|excludeRequirementsToUnlock|**_Optional_**. Space-separated list of dependency updates requirements to be excluded. See list of allowed values [here](https://github.com/dependabot/dependabot-core/issues/600#issuecomment-407808103). Useful if you have lots of dependencies and the update script too slow. The values provided are space-separated. Example: `own all` to only use the `none` version requirement.|
|dockerImageTag|**_Optional_**. The image tag to use when pulling the docker container used by the task. A tag also defines the version. By default, the task decides which tag/version to use. This can be the latest or most stable version.|
|extraEnvironmentVariables|**_Optional_**. A semicolon (`;`) delimited list of environment variables that are sent to the docker container. See possible use case [here](https://github.com/tinglesoftware/dependabot-azure-devops/issues/138)|

## Advanced

In some situations, such as when getting the latest bits for testing, you might want to override the docker image tag that is pulled. Even though doing so is discouraged you can declare a global variable, for example:

```yaml
trigger: none # Disable CI trigger

schedules:
- cron: '0 2 * * *' # daily at 2am UTC
  always: true # run even when there are no code changes
  branches:
    include:
      - master
  batch: true
  displayName: Daily

# variables declared below can be put in one or more Variable Groups for sharing across pipelines
variables:
  DEPENDABOT_EXTRA_CREDENTIALS: '[{\"type\":\"npm_registry\",\"token\":\"<redacted>\",\"registry\":\"npm.fontawesome.com\"}]' # put the credentials for private registries and feeds
  DEPENDABOT_ALLOW_CONDITIONS: '[{\"dependency-name\":"django*",\"dependency-type\":\"direct\"}]' # packages allowed to be updated
  DEPENDABOT_IGNORE_CONDITIONS: '[{\"dependency-name\":\"express\",\"versions\":[\"4.x\",\"5.x\"]}]' # packages to be ignored

pool:
  vmImage: 'ubuntu-latest' # requires macos or ubuntu (windows is not supported)

steps:
- task: dependabot@1
  inputs:
    useConfigFile: true
```

Check the logs for the image that is pulled.
