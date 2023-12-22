# Dependabot Azure DevOps Extension

This is the unofficial [dependabot](https://github.com/Dependabot/dependabot-core) extension for [Azure DevOps](https://azure.microsoft.com/en-gb/services/devops/). It will allow you to run Dependabot inside a build pipeline and is accessible [here in the Visual Studio marketplace](https://marketplace.visualstudio.com/items?itemName=tingle-software.dependabot). The extension first has to be installed before you can run it in your pipeline.

## Usage

Add a configuration file stored at `.github/dependabot.yml` or  `.github/dependabot.yaml` conforming to the [official spec](https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates).

To use in a YAML pipeline:

```yaml
- task: dependabot@1
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
```

This task makes use of a docker image, which may take time to install. Subsequent dependabot tasks in a job will be faster after initially pulling the image using the first task. An alternative way to run your pipelines faster is by leveraging Docker caching in Azure Pipelines (See [#113](https://github.com/tinglesoftware/dependabot-azure-devops/issues/113#issuecomment-894771611)). 

## Task Parameters

|Input|Description|
|--|--|
|failOnException|**_Optional_**. Determines if the execution should fail when an exception occurs. Defaults to `true`.|
|updaterOptions|**_Optional_**. Comma separated list of updater options; available options depend on the ecosystem. Example: `goprivate=true,kubernetes_updates=true`.|
|setAutoComplete|**_Optional_**. Determines if the pull requests that dependabot creates should have auto complete set. When set to `true`, pull requests that pass all policies will be merged automatically. Defaults to `false`.|
|mergeStrategy|**_Optional_**. The merge strategy to use when auto complete is set. Learn more [here](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-requests/update?view=azure-devops-rest-6.0&tabs=HTTP#gitpullrequestmergestrategy). Defaults to `squash`.|
|autoApprove|**_Optional_**. Determines if the pull requests that dependabot creates should be automatically completed. When set to `true`, pull requests will be approved automatically. To use a different user for approval, supply `autoApproveUserToken` input. Defaults to `false`.|
|autoApproveUserToken|**_Optional_**. A personal access token for the user to automatically approve the created PR.|
|skipPullRequests|**_Optional_**. Determines whether to skip creation and updating of pull requests. When set to `true` the logic to update the dependencies is executed but the actual Pull Requests are not created/updated. This is useful for debugging. Defaults to `false`.|
|abandonUnwantedPullRequests|**_Optional_**. Determines whether to abandon unwanted pull requests. Defaults to `false`.|
|gitHubConnection|**_Optional_**. The GitHub service connection for authenticating requests against GitHub repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [GitHub docs](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens and [Azure DevOps docs](https://docs.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=azure-devops&tabs=yaml#sep-github) for the GitHub service connection.|
|gitHubAccessToken|**_Optional_**. The raw GitHub PAT for authenticating requests against GitHub repositories. Use this in place of `gitHubConnection` such as when it is not possible to create a service connection.|
|azureDevOpsServiceConnection|**_Optional_**. A Service Connection to use for accessing Azure DevOps. Supply a value here to avoid using permissions for the Build Service either because you cannot change its permissions or because you prefer that the Pull Requests be done by a different user. When not provided, the current authentication scope is used.<br/>See the [documentation](https://learn.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=azure-devops) to know more about creating a Service Connections|
|azureDevOpsAccessToken|**_Optional_**. The Personal Access Token for accessing Azure DevOps. Supply a value here to avoid using permissions for the Build Service either because you cannot change its permissions or because you prefer that the Pull Requests be done by a different user. When not provided, the current authentication scope is used. In either case, be use the following permissions are granted: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token.<br/>Use this in place of `azureDevOpsServiceConnection` such as when it is not possible to create a service connection.|
|targetRepositoryName|**_Optional_**. The name of the repository to target for processing. If this value is not supplied then the Build Repository Name is used. Supplying this value allows creation of a single pipeline that runs Dependabot against multiple repositories by running a `dependabot` task for each repository to update.|
|targetUpdateIds|**_Optional_**. A semicolon (`;`) delimited list of update identifiers run. Index are zero-based and in the order written in the configuration file. When not present, all the updates are run. This is meant to be used in scenarios where you want to run updates a different times from the same configuration file given you cannot schedule them independently in the pipeline.|
|excludeRequirementsToUnlock|**_Optional_**. Space-separated list of dependency updates requirements to be excluded. See list of allowed values [here](https://github.com/dependabot/dependabot-core/issues/600#issuecomment-407808103). Useful if you have lots of dependencies and the update script too slow. The values provided are space-separated. Example: `own all` to only use the `none` version requirement.|
|dockerImageTag|**_Optional_**. The image tag to use when pulling the docker container used by the task. A tag also defines the version. By default, the task decides which tag/version to use. This can be the latest or most stable version. When not provided, the value is inferred from the current task version|
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
  DEPENDABOT_ALLOW_CONDITIONS: '[{\"dependency-name\":"django*",\"dependency-type\":\"direct\"}]' # packages allowed to be updated
  DEPENDABOT_IGNORE_CONDITIONS: '[{\"dependency-name\":"@types/*"}]' # packages ignored to be updated

pool:
  vmImage: 'ubuntu-latest' # requires macos or ubuntu (windows is not supported)

steps:
- task: dependabot@1
```

Check the logs for the image that is pulled.
