# Running on Docker

First, you need to pull the image locally to your machine:

```bash
docker pull tingle/dependabot-azure-devops:0.2.0
```

Next create and run a container from the image:

```bash
docker run --rm -t \
           -e GITHUB_ACCESS_TOKEN=<your-github-token-here> \
           -e AZURE_HOSTNAME=<your-hostname> \
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
           -e DEPENDABOT_ALLOW=<your-allowed-packages> \
           -e DEPENDABOT_IGNORE=<your-ignore-packages> \
           tingle/dependabot-azure-devops:0.2.0
```

An example:

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
           -e DEPENDABOT_ALLOW='[{\"name\":"django*",\"type\":\"direct\"}]' \
           -e DEPENDABOT_IGNORE='[{\"name\":\"express\",\"versions\":[\"4.x\",\"5.x\"]}]' \
           tingle/dependabot-azure-devops:0.2.0
```

## Environment Variables

To run the script, some environment variables are required.

|Variable Name|Description|
|--|--|
|GITHUB_ACCESS_TOKEN|**_Optional_**. The GitHub token for authenticating requests against GitHub public repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [documentation](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens.|
|AZURE_HOSTNAME|**_Optional_**. The hostname of the where the organization is hosted. Defaults to `dev.azure.com` but for older organizations this may have the format `xxx.visualstudio.com`. Check the url on the browser. For Azure DevOps Server, this may be the unexposed one e.g. `localhost:8080` or one that you have exposed publicly via DNS.|
|AZURE_ACCESS_TOKEN|**_Required_**. The Personal Access in Azure DevOps for accessing the repository and creating pull requests. The required permissions are: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token|
|AZURE_ORGANIZATION|**_Required_**. The name of the Azure DevOps Organization. This is can be extracted from the URL of the home page. https://dev.azure.com/{organization}/|
|AZURE_PROJECT|**_Required_**. The name of the Azure DevOps Project within the above organization. This can be extracted them the URL too. https://dev.azure.com/{organization}/{project}/|
|AZURE_REPOSITORY|**_Required_**. The name of the Azure DevOps Repository within the above project to run Dependabot against. This can be extracted from the URL of the repository. https://dev.azure.com/{organization}/{project}/_git/{repository}/|
|DEPENDABOT_PACKAGE_MANAGER|**_Required_**. The type of packages to check for dependency upgrades. Examples: `nuget`, `maven`, `gradle`, `npm_and_yarn`, etc. See the [updated-script](./src/script/update-script.rb) or [docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#package-ecosystem) for more.|
|DEPENDABOT_DIRECTORY|**_Optional_**. The directory in which dependencies are to be checked. When not specified, the root of the repository (denoted as '/') is used.|
|DEPENDABOT_TARGET_BRANCH|**_Optional_**. The branch to be targeted when creating a pull request. When not specified, Dependabot will resolve the default branch of the repository.|
|DEPENDABOT_VERSIONING_STRATEGY|**_Optional_**. The versioning strategy to use. See [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#versioning-strategy) for the allowed values|
|DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT|**_Optional_**. The maximum number of open pull requests to have at any one time. Defaults to 5.|
|DEPENDABOT_EXTRA_CREDENTIALS|**_Optional_**. The extra credentials in JSON format. Extra credentials can be used to access private NuGet feeds, docker registries, maven repositories, etc. For example a private registry authentication (For example FontAwesome Pro: `[{"type":"npm_registry","token":"<redacted>","registry":"npm.fontawesome.com"}]`)|
|DEPENDABOT_ALLOW|**_Optional_**. The dependencies whose updates are allowed, in JSON format. This can be used to control which packages can be updated. For example: `[{\"name\":"django*",\"type\":\"direct\"}]`. See [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#allow) for more.|
|DEPENDABOT_IGNORE|**_Optional_**. The dependencies to be ignored, in JSON format. This can be used to control which packages can be updated. For example: `[{\"name\":\"express\",\"versions\":[\"4.x\",\"5.x\"]}]`. See [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#ignore) for more.|
|DEPENDABOT_FAIL_ON_EXCEPTION|**_Optional_**. Determines if the execution should fail when an exception occurs. Defaults to `true`.|
