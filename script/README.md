# Running on Docker

First, you need to pull the image locally to your machine:

```bash
docker pull tingle/dependabot-azure-devops:0.10
```

Next create and run a container from the image:

```bash
docker run --rm -t \
           -e GITHUB_ACCESS_TOKEN=<your-github-token-here> \
           -e AZURE_PROTOCOL=<your-azure-devops-installation-transport-protocol> \
           -e AZURE_HOSTNAME=<your-azure-devops-installation-hostname> \
           -e AZURE_PORT=<your-azure-devops-installation-port> \
           -e AZURE_VIRTUAL_DIRECTORY=<your-azure-devops-installation-virtual-directory> \
           -e AZURE_ACCESS_TOKEN=<your-devops-token-here> \
           -e AZURE_ORGANIZATION=<your-organization-here> \
           -e AZURE_PROJECT=<your-project-here> \
           -e AZURE_REPOSITORY=<your-repository-here> \
           -e DEPENDABOT_PACKAGE_MANAGER=<your-package-manager-here> \
           -e DEPENDABOT_DIRECTORY=/ \
           -e DEPENDABOT_TARGET_BRANCH=<your-target-branch> \
           -e DEPENDABOT_VERSIONING_STRATEGY=<your-versioning-strategy> \
           -e DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT=10 \
           -e DEPENDABOT_EXTRA_CREDENTIALS=<your-extra-credentials> \
           -e DEPENDABOT_ALLOW_CONDITIONS=<your-allowed-packages> \
           -e DEPENDABOT_IGNORE_CONDITIONS=<your-ignore-packages> \
           -e DEPENDABOT_LABELS=<your-custom-labels> \
           -e DEPENDABOT_BRANCH_NAME_SEPARATOR=<your-custom-separator> \
           -e DEPENDABOT_MILESTONE=<your-work-item-id> \
           -e DEPENDABOT_UPDATER_OPTIONS=<your-updater-options> \
           -e AZURE_SET_AUTO_COMPLETE=<true/false> \
           -e AZURE_AUTO_APPROVE_PR=<true/false> \
           -e AZURE_AUTO_APPROVE_USER_EMAIL=<approving-user-email> \
           -e AZURE_AUTO_APPROVE_USER_TOKEN=<approving-user-token-here> \
           tingle/dependabot-azure-devops:0.10
```

An example, for Azure DevOps Services:

```bash
docker run --rm -t \
           -e GITHUB_ACCESS_TOKEN=ijkl..mnop \
           -e AZURE_HOSTNAME=dev.azure.com \
           -e AZURE_ACCESS_TOKEN=abcd..efgh \
           -e AZURE_ORGANIZATION=tinglesoftware \
           -e AZURE_PROJECT=oss \
           -e AZURE_REPOSITORY=dependabot-azure-devops \
           -e DEPENDABOT_PACKAGE_MANAGER=nuget \
           -e DEPENDABOT_DIRECTORY=/ \
           -e DEPENDABOT_TARGET_BRANCH=main \
           -e DEPENDABOT_VERSIONING_STRATEGY=auto \
           -e DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT=10 \
           -e DEPENDABOT_EXTRA_CREDENTIALS='[{\"type\":\"npm_registry\",\"token\":\"<redacted>\",\"registry\":\"npm.fontawesome.com\"}]' \
           -e DEPENDABOT_ALLOW_CONDITIONS='[{\"dependency-name\":"django*",\"dependency-type\":\"direct\"}]' \
           -e DEPENDABOT_IGNORE_CONDITIONS='[{\"dependency-name\":\"express\",\"versions\":[\"4.x\",\"5.x\"]}]' \
           -e DEPENDABOT_LABELS='[\"npm dependencies\",\"triage-board\"]' \
           -e DEPENDABOT_BRANCH_NAME_SEPARATOR='/' \
           -e DEPENDABOT_MILESTONE=123 \
           -e DEPENDABOT_UPDATER_OPTIONS='goprivate=true,kubernetes_updates=true' \
           -e AZURE_SET_AUTO_COMPLETE=true \
           -e AZURE_AUTO_APPROVE_PR=true \
           -e AZURE_AUTO_APPROVE_USER_EMAIL=supervisor@contoso.com \
           -e AZURE_AUTO_APPROVE_USER_TOKEN=ijkl..mnop \
           tingle/dependabot-azure-devops:0.10
```

An example, for Azure DevOps Server:

```bash
docker run --rm -t \
           -e GITHUB_ACCESS_TOKEN=ijkl..mnop \
           -e AZURE_PROTOCOL=http \
           -e AZURE_HOSTNAME=my-devops.com \
           -e AZURE_PORT=8080 \
           -e AZURE_VIRTUAL_DIRECTORY=tfs \
           -e AZURE_ACCESS_TOKEN=abcd..efgh \
           -e AZURE_ORGANIZATION=tinglesoftware \
           -e AZURE_PROJECT=oss \
           -e AZURE_REPOSITORY=dependabot-azure-devops \
           -e DEPENDABOT_PACKAGE_MANAGER=nuget \
           -e DEPENDABOT_DIRECTORY=/ \
           -e DEPENDABOT_TARGET_BRANCH=main \
           -e DEPENDABOT_VERSIONING_STRATEGY=auto \
           -e DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT=10 \
           -e DEPENDABOT_EXTRA_CREDENTIALS='[{\"type\":\"npm_registry\",\"token\":\"<redacted>\",\"registry\":\"npm.fontawesome.com\"}]' \
           -e DEPENDABOT_ALLOW_CONDITIONS='[{\"dependency-name\":"django*",\"dependency-type\":\"direct\"}]' \
           -e DEPENDABOT_IGNORE_CONDITIONS='[{\"dependency-name\":\"express\",\"versions\":[\"4.x\",\"5.x\"]}]' \
           -e DEPENDABOT_LABELS='[\"npm dependencies\",\"triage-board\"]' \
           -e DEPENDABOT_BRANCH_NAME_SEPARATOR='/' \
           -e DEPENDABOT_MILESTONE=123 \
           -e DEPENDABOT_UPDATER_OPTIONS='goprivate=true,kubernetes_updates=true' \
           -e AZURE_SET_AUTO_COMPLETE=true \
           -e AZURE_AUTO_APPROVE_PR=true \
           -e AZURE_AUTO_APPROVE_USER_EMAIL=supervisor@contoso.com \
           -e AZURE_AUTO_APPROVE_USER_TOKEN=ijkl..mnop \
           tingle/dependabot-azure-devops:0.10
```

## Environment Variables

To run the script, some environment variables are required.

|Variable Name|Description|
|--|--|
|GIT_AUTHOR_EMAIL|**_Optional_**. The email address to use for the change commit author, can be used e.g. in private Azure DevOps Server deployments to associate the committer with an existing account, to provide a profile picture.|
|GIT_AUTHOR_NAME|**_Optional_**. The display name to use for the change commit author.|
|GITHUB_ACCESS_TOKEN|**_Optional_**. The GitHub token for authenticating requests against GitHub public repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [documentation](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens.|
|AZURE_PROTOCOL|**_Optional_**. The transport protocol (`http` or `https`) used by your Azure DevOps installation. Defaults to `https`.|
|AZURE_HOSTNAME|**_Optional_**. The hostname of the where the organization is hosted. Defaults to `dev.azure.com` but for older organizations this may have the format `xxx.visualstudio.com`. Check the url on the browser. For Azure DevOps Server, this may be the unexposed one e.g. `localhost` or one that you have exposed publicly via DNS.|
|AZURE_PORT|**_Optional_**. The TCP port used by your Azure DevOps installation. Defaults to `80` or `443`, depending on the indicated protocol.|
|AZURE_VIRTUAL_DIRECTORY|**_Optional_**. Some Azure DevOps Server installations are hosted in an IIS virtual directory, traditionally named tfs. This variable can be used to define the name of that virtual directory. By default, this is not set.|
|AZURE_ACCESS_USERNAME|**_Optional_**. This Variable can be used together with the User Password in the Access Token Variable to use basic Auth when connecting to Azure Dev Ops. By default, this is not set.|
|AZURE_ACCESS_TOKEN|**_Required_**. The Personal Access in Azure DevOps for accessing the repository and creating pull requests. The required permissions are: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token|
|AZURE_ORGANIZATION|**_Required_**. The name of the Azure DevOps Organization. This is can be extracted from the URL of the home page. https://dev.azure.com/{organization}/|
|AZURE_PROJECT|**_Required_**. The name of the Azure DevOps Project within the above organization. This can be extracted them the URL too. https://dev.azure.com/{organization}/{project}/|
|AZURE_REPOSITORY|**_Required_**. The name of the Azure DevOps Repository within the above project to run Dependabot against. This can be extracted from the URL of the repository. https://dev.azure.com/{organization}/{project}/_git/{repository}/|
|DEPENDABOT_PACKAGE_MANAGER|**_Required_**. The type of packages to check for dependency upgrades. Examples: `nuget`, `maven`, `gradle`, `npm_and_yarn`, etc. See the [updated-script](./script/update-script.rb) or [docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#package-ecosystem) for more.|
|DEPENDABOT_DIRECTORY|**_Optional_**. The directory in which dependencies are to be checked. When not specified, the root of the repository (denoted as '/') is used.|
|DEPENDABOT_TARGET_BRANCH|**_Optional_**. The branch to be targeted when creating a pull request. When not specified, Dependabot will resolve the default branch of the repository.|
|DEPENDABOT_VERSIONING_STRATEGY|**_Optional_**. The versioning strategy to use. See [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#versioning-strategy) for the allowed values|
|DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT|**_Optional_**. The maximum number of open pull requests to have at any one time. Defaults to 5.|
|DEPENDABOT_EXTRA_CREDENTIALS|**_Optional_**. The extra credentials in JSON format. Extra credentials can be used to access private NuGet feeds, docker registries, maven repositories, etc. For example a private registry authentication (For example FontAwesome Pro: `[{"type":"npm_registry","token":"<redacted>","registry":"npm.fontawesome.com"}]`)|
|DEPENDABOT_ALLOW_CONDITIONS|**_Optional_**. The dependencies whose updates are allowed, in JSON format. This can be used to control which packages can be updated. For example: `[{\"dependency-name\":"django*",\"dependency-type\":\"direct\"}]`. See [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#allow) for more.|
|DEPENDABOT_IGNORE_CONDITIONS|**_Optional_**. The dependencies to be ignored, in JSON format. This can be used to control which packages can be updated. For example: `[{\"dependency-name\":\"express\",\"versions\":[\"4.x\",\"5.x\"]}]`. See [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#ignore) for more.|
|DEPENDABOT_LABELS|**_Optional_**. The custom labels to be used, in JSON format. This can be used to override the default values. For example: `[\"npm dependencies\",\"triage-board\"]`. See [official docs](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/customizing-dependency-updates#setting-custom-labels) for more.|
|DEPENDABOT_BRANCH_NAME_SEPARATOR|**_Optional_**. The separator to use in created branches. For example: `-`. See [official docs](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#pull-request-branch-nameseparator) for more.|
|DEPENDABOT_FAIL_ON_EXCEPTION|**_Optional_**. Determines if the execution should fail when an exception occurs. Defaults to `true`.|
|DEPENDABOT_EXCLUDE_REQUIREMENTS_TO_UNLOCK|**_Optional_**. Exclude certain dependency updates requirements. See list of allowed values [here](https://github.com/dependabot/dependabot-core/issues/600#issuecomment-407808103). Useful if you have lots of dependencies and the update script too slow. The values provided are space-separated. Example: `own all` to only use the `none` version requirement.|
|DEPENDABOT_MILESTONE|**_Optional_**. The identifier of the work item to be linked to the Pull Requests that dependabot creates.|
|DEPENDABOT_UPDATER_OPTIONS|**_Optional_**. Comma separated list of updater options; available options depend on PACKAGE_MANAGER. Example: `goprivate=true,kubernetes_updates=true`.|
|AZURE_SET_AUTO_COMPLETE|**_Optional_**. Determines if the pull requests that dependabot creates should have auto complete set. When set to `true`, pull requests that pass all policies will be merged automatically|
|AZURE_AUTO_APPROVE_PR|**_Optional_**. Determines if the pull requests that dependabot creates should be automatically completed. When set to `true`, pull requests will be approved automatically by the user specified in the `AZURE_AUTO_APPROVE_USER_EMAIL` environment variable.|
|AZURE_AUTO_APPROVE_USER_EMAIL|**_Optional_**. Email of the user that should be used to automatically approve pull requests. Required if `AZURE_AUTO_APPROVE_PR` is set to `true`.|
|AZURE_AUTO_APPROVE_USER_TOKEN|**_Optional_**. A personal access token that is assigned to the user specified in `AZURE_AUTO_APPROVE_USER_EMAIL` to automatically approve the created PR. Required if `AZURE_AUTO_APPROVE_PR` is set to `true`.|
