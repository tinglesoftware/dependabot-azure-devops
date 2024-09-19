# Dependabot Azure DevOps Extension

This is the unofficial [dependabot](https://github.com/Dependabot/dependabot-core) extension for [Azure DevOps](https://azure.microsoft.com/en-gb/services/devops/). It will allow you to run Dependabot inside a build pipeline.

## Usage

Add a configuration file stored at `.azuredevops/dependabot.yml` or `.github/dependabot.yml` conforming to the [official spec](https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates).

To use in a YAML pipeline:

```yaml
- task: dependabot@2
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
- task: dependabot@2
```

## Task Requirements

The task makes use of [dependabot-cli](https://github.com/dependabot/cli), which requires [Go](https://go.dev/doc/install) (1.22+) and [Docker](https://docs.docker.com/get-started/get-docker/) (with Linux containers) be installed on the pipeline agent.
If you use [Microsoft-hosted agents](https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=azure-devops&tabs=yaml#software), we recommend using the [ubuntu-latest](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md) image, which meets all task requirements.

Dependabot uses Docker containers, which may take time to install if not already cached. Subsequent dependabot tasks in the same job will be faster after initially pulling the images. An alternative way to run your pipelines faster is by leveraging Docker caching in Azure Pipelines (See [#113](https://github.com/tinglesoftware/dependabot-azure-devops/issues/113#issuecomment-894771611)). 

## Task Parameters

### `dependabot@V2` **(Preview)**

|Input|Description|
|--|--|
|skipPullRequests|**_Optional_**. Determines whether to skip creation and updating of pull requests. When set to `true` the logic to update the dependencies is executed but the actual Pull Requests are not created/updated. This is useful for debugging. Defaults to `false`.|
|abandonUnwantedPullRequests|**_Optional_**. Determines whether to abandon unwanted pull requests. Defaults to `false`.|
|commentPullRequests|**_Optional_**. Determines whether to comment on pull requests which an explanation of the reason for closing. Defaults to `false`.|
|setAutoComplete|**_Optional_**. Determines if the pull requests that dependabot creates should have auto complete set. When set to `true`, pull requests that pass all policies will be merged automatically. Defaults to `false`.|
|mergeStrategy|**_Optional_**. The merge strategy to use when auto complete is set. Learn more [here](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-requests/update?view=azure-devops-rest-6.0&tabs=HTTP#gitpullrequestmergestrategy). Defaults to `squash`.|
|autoCompleteIgnoreConfigIds|**_Optional_**. List of any policy configuration Id's which auto-complete should not wait for. Only applies to optional policies. Auto-complete always waits for required (blocking) policies.|
|autoApprove|**_Optional_**. Determines if the pull requests that dependabot creates should be automatically completed. When set to `true`, pull requests will be approved automatically. To use a different user for approval, supply `autoApproveUserToken` input. Defaults to `false`.|
|autoApproveUserToken|**_Optional_**. A personal access token for the user to automatically approve the created PR.|
|authorEmail|**_Optional_**. The email address to use for the change commit author. Can be used to associate the committer with an existing account, to provide a profile picture.|
|authorName|**_Optional_**. The display name to use for the change commit author.|
|securityAdvisoriesFile|**_Optional_**. The path to a JSON file containing additional security advisories to be included when performing package updates. See: [Configuring security advisories and known vulnerabilities](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-security-advisories-and-known-vulnerabilities).|
|azureDevOpsServiceConnection|**_Optional_**. A Service Connection to use for accessing Azure DevOps. Supply a value here to avoid using permissions for the Build Service either because you cannot change its permissions or because you prefer that the Pull Requests be done by a different user. When not provided, the current authentication scope is used.<br/>See the [documentation](https://learn.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=azure-devops) to know more about creating a Service Connections|
|azureDevOpsAccessToken|**_Optional_**. The Personal Access Token for accessing Azure DevOps. Supply a value here to avoid using permissions for the Build Service either because you cannot change its permissions or because you prefer that the Pull Requests be done by a different user. When not provided, the current authentication scope is used. In either case, be use the following permissions are granted: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token.<br/>Use this in place of `azureDevOpsServiceConnection` such as when it is not possible to create a service connection.|
|gitHubConnection|**_Optional_**. The GitHub service connection for authenticating requests against GitHub repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [GitHub docs](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens and [Azure DevOps docs](https://docs.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=azure-devops&tabs=yaml#sep-github) for the GitHub service connection.|
|gitHubAccessToken|**_Optional_**. The raw GitHub PAT for authenticating requests against GitHub repositories. Use this in place of `gitHubConnection` such as when it is not possible to create a service connection.|
|storeDependencyList|**_Optional_**. Determines if the last know dependency list information should be stored in the parent DevOps project properties. If enabled, the authenticated user must have the "Project & Team (Write)" permission for the project. Enabling this option improves performance when doing security-only updates. Defaults to `false`.|
|targetRepositoryName|**_Optional_**. The name of the repository to target for processing. If this value is not supplied then the Build Repository Name is used. Supplying this value allows creation of a single pipeline that runs Dependabot against multiple repositories by running a `dependabot` task for each repository to update.|
|targetUpdateIds|**_Optional_**. A semicolon (`;`) delimited list of update identifiers run. Index are zero-based and in the order written in the configuration file. When not present, all the updates are run. This is meant to be used in scenarios where you want to run updates a different times from the same configuration file given you cannot schedule them independently in the pipeline.|
|experiments|**_Optional_**. Comma separated list of Dependabot experiments; available options depend on the ecosystem. Example: `tidy=true,vendor=true,goprivate=*`. See: [Configuring experiments](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-experiments)|

### `dependabot@V1` **(Deprecated)**

|Input|Description|
|--|--|
|useUpdateScriptvNext|**_Optional_**. Determines if the task should use the new "vNext" update script based on Dependabot Updater (true), or the original update script based on `dry-run.rb` (false). Defaults to `false`. For more information, see: [PR #1186](https://github.com/tinglesoftware/dependabot-azure-devops/pull/1186).|
|failOnException|**_Optional_**. Determines if the execution should fail when an exception occurs. Defaults to `true`.|
|updaterOptions|**_Optional_**. Comma separated list of updater options; available options depend on the ecosystem. Example: `tidy=true,vendor=true,goprivate=*`. See: [Configuring experiments](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-experiments)|
|setAutoComplete|**_Optional_**. Determines if the pull requests that dependabot creates should have auto complete set. When set to `true`, pull requests that pass all policies will be merged automatically. Defaults to `false`.|
|mergeStrategy|**_Optional_**. The merge strategy to use when auto complete is set. Learn more [here](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-requests/update?view=azure-devops-rest-6.0&tabs=HTTP#gitpullrequestmergestrategy). Defaults to `squash`.|
|autoCompleteIgnoreConfigIds|**_Optional_**. List of any policy configuration Id's which auto-complete should not wait for. Only applies to optional policies. Auto-complete always waits for required (blocking) policies.|
|autoApprove|**_Optional_**. Determines if the pull requests that dependabot creates should be automatically completed. When set to `true`, pull requests will be approved automatically. To use a different user for approval, supply `autoApproveUserToken` input. Defaults to `false`.|
|autoApproveUserToken|**_Optional_**. A personal access token for the user to automatically approve the created PR.|
|skipPullRequests|**_Optional_**. Determines whether to skip creation and updating of pull requests. When set to `true` the logic to update the dependencies is executed but the actual Pull Requests are not created/updated. This is useful for debugging. Defaults to `false`.|
|abandonUnwantedPullRequests|**_Optional_**. Determines whether to abandon unwanted pull requests. Defaults to `false`.|
|commentPullRequests|**_Optional_**. Determines whether to comment on pull requests which an explanation of the reason for closing. Defaults to `false`.|
|securityAdvisoriesFile|**_Optional_**. The path to a JSON file containing additional security advisories to be included when performing package updates. See: [Configuring security advisories and known vulnerabilities](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-security-advisories-and-known-vulnerabilities).|
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

- [Configuring private feeds and registries](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-private-feeds-and-registries)
- [Configuring security advisories and known vulnerabilities](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-security-advisories-and-known-vulnerabilities)
- [Configuring experiments](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-experiments)
- [Unsupported features and configurations](https://github.com/tinglesoftware/dependabot-azure-devops/#unsupported-features-and-configurations)
- [Task migration guide for V1 → V2](https://github.com/tinglesoftware/dependabot-azure-devops/blob/main/docs/migrations/v1-to-v2.md)
