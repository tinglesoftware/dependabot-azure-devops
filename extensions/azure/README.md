# Dependabot Azure DevOps Extension

This is the unofficial [dependabot](https://github.com/Dependabot/dependabot-core) extension for [Azure DevOps](https://azure.microsoft.com/en-gb/services/devops/). It will allow you to run Dependabot inside a build pipeline.

> [!WARNING]
> It is strongly recommended that you complete (or abandon) all active pull requests created by the same user that were created manually or using earlier versions of the task.

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
  - cron: '0 0 * * 0' # weekly on sunday at midnight UTC
    always: true # run even when there are no code changes
    branches:
      include:
        - master
    batch: true
    displayName: Weekly

pool:
  vmImage: 'ubuntu-latest' # requires macos or ubuntu (windows is not supported)

# Uncomment the lines below to have secrets protected in the logs
# variables:
#  System.Secrets: true

steps:
  - task: dependabot@2
    inputs:
      mergeStrategy: 'squash'
```

## Task Requirements

The task uses [dependabot-cli](https://github.com/dependabot/cli), which requires [Go](https://go.dev/doc/install) (1.22+) and [Docker](https://docs.docker.com/engine/install/) (with Linux containers) be installed on the pipeline agent.
If you use [Microsoft-hosted agents](https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=azure-devops&tabs=yaml#software), we recommend using the [ubuntu-latest](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md) image, which meets all task requirements.

Dependabot uses Docker containers, which may take time to install if not already cached. Subsequent dependabot tasks in the same job will be faster after initially pulling the images. An alternative way to run your pipelines faster is by leveraging Docker caching in Azure Pipelines (See [#113](https://github.com/tinglesoftware/dependabot-azure-devops/issues/113#issuecomment-894771611)).

## Task Parameters

|Input|Description|
|--|--|
|skipPullRequests|**_Optional_**. Determines whether to skip creation and updating of pull requests. When set to `true` the logic to update the dependencies is executed but the actual Pull Requests are not created/updated. This is useful for debugging. Defaults to `false`.|
|abandonUnwantedPullRequests|**_Optional_**. Determines whether to abandon unwanted pull requests. Defaults to `false`.|
|commentPullRequests|**_Optional_**. Determines whether to comment on pull requests with an explanation of the reason for closing. Defaults to `false`.|
|setAutoComplete|**_Optional_**. Determines if the pull requests that dependabot creates should have auto complete set. When set to `true`, pull requests that pass all policies will be merged automatically. Defaults to `false`.|
|mergeStrategy|**_Optional_**. The merge strategy to use when auto complete is set. Learn more [here](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-requests/update?view=azure-devops-rest-6.0&tabs=HTTP#gitpullrequestmergestrategy). Defaults to `squash`.|
|autoCompleteIgnoreConfigIds|**_Optional_**. List of any policy configuration Id's which auto-complete should not wait for. Only applies to optional policies. Auto-complete always waits for required (blocking) policies.|
|autoApprove|**_Optional_**. Determines if the pull requests that dependabot creates should be automatically completed. When set to `true`, pull requests will be approved automatically. To use a different user for approval, supply `autoApproveUserToken` input. Defaults to `false`. Requires [Azure DevOps REST API 7.1](https://learn.microsoft.com/en-us/azure/devops/integrate/concepts/rest-api-versioning?view=azure-devops#supported-versions).|
|autoApproveUserToken|**_Optional_**. A personal access token for the user to automatically approve the created PR.|
|authorEmail|**_Optional_**. The email address to use for the change commit author. Can be used to associate the committer with an existing account, to provide a profile picture. Defaults to `noreply@github.com`.|
|authorName|**_Optional_**. The name to use as the git commit author of the pull requests. Defaults to `dependabot[bot]`.|
|azureDevOpsServiceConnection|**_Optional_**. A Service Connection to use for accessing Azure DevOps. Supply a value here to avoid using permissions for the Build Service either because you cannot change its permissions or because you prefer that the Pull Requests be done by a different user. When not provided, the current authentication scope is used.<br/>See the [documentation](https://learn.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=azure-devops) to know more about creating a Service Connections|
|azureDevOpsAccessToken|**_Optional_**. The Personal Access Token for accessing Azure DevOps. Supply a value here to avoid using permissions for the Build Service either because you cannot change its permissions or because you prefer that the Pull Requests be done by a different user. When not provided, the current authentication scope is used. In either case, be use the following permissions are granted: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token.<br/>Use this in place of `azureDevOpsServiceConnection` such as when it is not possible to create a service connection.|
|gitHubConnection|**_Optional_**. The GitHub service connection for authenticating requests against GitHub repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [GitHub docs](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens and [Azure DevOps docs](https://docs.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=azure-devops&tabs=yaml#sep-github) for the GitHub service connection.|
|gitHubAccessToken|**_Optional_**. The raw GitHub PAT for authenticating requests against GitHub repositories. Use this in place of `gitHubConnection` such as when it is not possible to create a service connection.|
|storeDependencyList|**_Optional_**. Determines if the last know dependency list information should be stored in the parent DevOps project properties. If enabled, the authenticated user must have the "Project & Team (Write)" permission for the project. Defaults to `false`.|
|targetProjectName|**_Optional_**. The Name/ID of the project to target for processing. If this value is not supplied then the Build Project ID is used. Supplying this value allows creation of a single pipeline that runs Dependabot against multiple projects in an organisation by running a `dependabot` task for each project to update. This must be used together with `targetRepositoryName`. Ensure the PAT provided also has access to the project specified.|
|targetRepositoryName|**_Optional_**. The name of the repository to target for processing. If this value is not supplied then the Build Repository Name is used. Supplying this value allows creation of a single pipeline that runs Dependabot against multiple repositories in a project by running a `dependabot` task for each repository to update.|
|targetUpdateIds|**_Optional_**. A semicolon (`;`) delimited list of update identifiers run. Index are zero-based and in the order written in the configuration file. When not present, all the updates are run. This is meant to be used in scenarios where you want to run updates a different times from the same configuration file given you cannot schedule them independently in the pipeline.|
|dependabotCliPackage|**_Optional_**. The [Dependabot CLI package](https://pkg.go.dev/github.com/dependabot/cli) to use for updates. This is intended to be used in scenarios where 'latest' has issues and you want to pin a known working version, or use a custom package. Defaults to `github.com/dependabot/cli/cmd/dependabot@latest`|
|dependabotCliApiUrl|**_Optional_**. The --api-url argument of `dependabot update` command|
|dependabotCliApiListeningPort|**_Optional_**. This set fixed listening port for of the dependabot cli using `FAKE_API_PORT`. It should match the `dependabotCliApiUrl` option|
|dependabotUpdaterImage|**_Optional_**. The [Dependabot CLI container image](https://github.com/dependabot/dependabot-core/pkgs/container/dependabot-updater-bundler) to use for updates. The image must contain a '{ecosystem}' placeholder, which will be substituted with the package ecosystem for each update operation. This is intended to be used in scenarios where 'latest' has issues and you want to pin a known working version, or use a custom package. Defaults to `ghcr.io/dependabot/dependabot-updater-{ecosystem}:latest`|
|proxyCertPath|**_Optional_**. This is useful when the proxy is expected to connect to a server using a self-signed certificate. The default one is located at `/usr/local/share/ca-certificates/custom-ca-cert.crt` in the closed-source updater proxy container image.|
|experiments|**_Optional_**. Comma separated list of Dependabot experiments; available options depend on the ecosystem. Example: `tidy=true,vendor=true,goprivate=*`. If specified, this overrides the [default experiments](https://github.com/tinglesoftware/dependabot-azure-devops/blob/main/extensions/azure/src/dependabot/experiments.ts). See: [Configuring experiments](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-experiments)|

## Advanced

- [Configuring private feeds and registries](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-private-feeds-and-registries)
- [Configuring security advisories and known vulnerabilities](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-security-advisories-and-known-vulnerabilities)
- [Configuring experiments](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-experiments)
- [Configuring assignees](https://github.com/tinglesoftware/dependabot-azure-devops/#configuring-assignees)
- [Unsupported features and configurations](https://github.com/tinglesoftware/dependabot-azure-devops/#unsupported-features-and-configurations)
