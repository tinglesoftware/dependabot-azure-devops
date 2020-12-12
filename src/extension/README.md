# Dependabot Azure DevOps Extension

This is the unofficial [dependabot](https://github.com/Dependabot/dependabot-core) extension for [Azure DevOps](https://azure.microsoft.com/en-gb/services/devops/). It will allow you to run Dependabot inside a build pipeline. You will find it [here in the Visual Studio marketplace](https://marketplace.visualstudio.com/items?itemName=tingle-software.dependabot). See the website for more [instructions](https://github.com/tinglesoftware/dependabot-azure-devops#running-in-azure-devops).

## Usage

To use in a YAML pipeline:

```yaml
- task: dependabot@1
  inputs:
    packageManager: 'nuget'
```

## Task Parameters

|Input|Description|
|--|--|
|packageManager|**_Required_**. The type of packages to check for dependecy upgrades. Examples: `nuget`, `maven`, `gradle`, `npm`, etc. See the [updated-script](./src/update-script.rb) or [docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#package-ecosystem) for more.|
|gitHubConnection|**_Optional_**. The GitHub service connection for authenticating requests against GitHub public repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [GitHub docs](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens and [Azure DevOps docs](https://docs.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=azure-devops&tabs=yaml#sep-github) for the GitHub service connection.|
|feedName|**_Optional_**. The name of the private feed within the Azure DevOps organization to use when resolving updates for private packages/dependencies. Project scoped feeds are not supported.|
|directory|**_Optional_**. The directory in which dependancies are to be checked. Examples: `/` for root, `/src` for src folder.|
|targetBranch|**_Optional_**. The branch to be targeted when creating pull requests. When not specified, Dependabot will resolve the default branch of the repository. Examples: `master`, `main`, `develop`|
|azureDevOpsAccessToken|**_Optional_**. The Personal Access Token for accessing Azure DevOps. Supply a value here to avoid using permissions for the Build Service either because you cannot change its permissions or because you prefer that the Pull Requests be done by a different user. When not provided, the current authentication scope is used. In either case, be use the following permissions are granted: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Packaging (Read)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token|
|packagingHostname|**_Optional_**. The hostname for private package repositories, feeds and registries. By default this is inferred from the current environment but may occassionally be different. When working using he new domain `dev.azure.com` the value used is `pkgs.dev.azure.com` whereas when working in the old url `xxx.visualstudio.com`, the value takes the format `xxx.pkgs.visualstudio.com`. In some situations, the code may still be referencing the older packaging urls but your organization is transitioning, in this case, you can specify `xxx.pkgs.visualstudio.com`.
