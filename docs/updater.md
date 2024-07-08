
# Table of Contents

- [Running the updater](#running-the-updater)
  - [Environment variables](#environment-variables)
- [Development guide](#development-guide)
  - [Getting the development environment ready](#getting-the-development-environment-ready)
  - [Building the Docker image](#building-the-docker-image)
  - [Running your code changes](#running-your-code-changes)
  - [Running the code linter](#running-the-code-linter)
  - [Running the unit tests](#running-the-unit-tests)

# Running the updater

First, you need to pull the docker image locally to your machine:

```bash
docker pull ghcr.io/tinglesoftware/dependabot-updater-<ecosystem>
```

Next create and run a container from the image:

```bash
docker run --rm -t \
           -e GITHUB_ACCESS_TOKEN=<your-github-token-here> \
           -e DEPENDABOT_JOB_ID=<any-unique-number-here> \
           -e DEPENDABOT_JOB_PATH=<any-unique-directory-path-here> \
           -e DEPENDABOT_PACKAGE_MANAGER=<your-package-manager-here> \
           -e DEPENDABOT_DIRECTORY=/ \
           -e DEPENDABOT_TARGET_BRANCH=<your-target-branch> \
           -e DEPENDABOT_VERSIONING_STRATEGY=<your-versioning-strategy> \
           -e DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT=10 \
           -e DEPENDABOT_EXTRA_CREDENTIALS=<your-extra-credentials> \
           -e DEPENDABOT_ALLOW_CONDITIONS=<your-allowed-packages> \
           -e DEPENDABOT_IGNORE_CONDITIONS=<your-ignored-packages> \
           -e DEPENDABOT_COMMIT_MESSAGE_OPTIONS=<your-commit-message-options> \
           -e DEPENDABOT_BRANCH_NAME_SEPARATOR=<your-custom-separator> \
           -e DEPENDABOT_MILESTONE=<your-work-item-id> \
           -e DEPENDABOT_UPDATER_OPTIONS=<your-updater-options> \
           -e AZURE_PROTOCOL=<your-azure-devops-installation-transport-protocol> \
           -e AZURE_HOSTNAME=<your-azure-devops-installation-hostname> \
           -e AZURE_PORT=<your-azure-devops-installation-port> \
           -e AZURE_VIRTUAL_DIRECTORY=<your-azure-devops-installation-virtual-directory> \
           -e AZURE_ACCESS_TOKEN=<your-devops-token-here> \
           -e AZURE_ORGANIZATION=<your-organization-here> \
           -e AZURE_PROJECT=<your-project-here> \
           -e AZURE_REPOSITORY=<your-repository-here> \
           -e AZURE_SET_AUTO_COMPLETE=<true/false> \
           -e AZURE_AUTO_APPROVE_PR=<true/false> \
           -e AZURE_AUTO_APPROVE_USER_TOKEN=<approving-user-token-here> \
           ghcr.io/tinglesoftware/dependabot-updater-<ecosystem> update_script
```

An example, for Azure DevOps Services:

```bash
docker run --rm -t \
           -e GITHUB_ACCESS_TOKEN=ijkl..mnop \
           -e DEPENDABOT_JOB_ID=1 \
           -e DEPENDABOT_JOB_PATH=/tmp/dependabot-job-1 \
           -e DEPENDABOT_PACKAGE_MANAGER=nuget \
           -e DEPENDABOT_DIRECTORY=/ \
           -e DEPENDABOT_TARGET_BRANCH=main \
           -e DEPENDABOT_VERSIONING_STRATEGY=auto \
           -e DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT=10 \
           -e DEPENDABOT_EXTRA_CREDENTIALS='[{"type":"npm_registry","token":"<redacted>","registry":"npm.fontawesome.com"}]' \
           -e DEPENDABOT_ALLOW_CONDITIONS='[{"dependency-name":"django*","dependency-type":"direct"}]' \
           -e DEPENDABOT_IGNORE_CONDITIONS='[{"dependency-name":"@types/*"}]' \
           -e DEPENDABOT_COMMIT_MESSAGE_OPTIONS='{"prefix":"(dependabot)"}' \
           -e DEPENDABOT_BRANCH_NAME_SEPARATOR='/' \
           -e DEPENDABOT_MILESTONE=123 \
           -e DEPENDABOT_UPDATER_OPTIONS='goprivate=true,kubernetes_updates=true' \
           -e AZURE_HOSTNAME=dev.azure.com \
           -e AZURE_ACCESS_TOKEN=abcd..efgh \
           -e AZURE_ORGANIZATION=tinglesoftware \
           -e AZURE_PROJECT=oss \
           -e AZURE_REPOSITORY=repro-411 \
           -e AZURE_SET_AUTO_COMPLETE=true \
           -e AZURE_AUTO_APPROVE_PR=true \
           -e AZURE_AUTO_APPROVE_USER_TOKEN=ijkl..mnop \
           ghcr.io/tinglesoftware/dependabot-updater-nuget update_script
```

An example, for Azure DevOps Server:

```bash
docker run --rm -t \
           -e GITHUB_ACCESS_TOKEN=ijkl..mnop \
           -e DEPENDABOT_JOB_ID=1 \
           -e DEPENDABOT_JOB_PATH=/tmp/dependabot-job-1 \
           -e DEPENDABOT_PACKAGE_MANAGER=nuget \
           -e DEPENDABOT_DIRECTORY=/ \
           -e DEPENDABOT_TARGET_BRANCH=main \
           -e DEPENDABOT_VERSIONING_STRATEGY=auto \
           -e DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT=10 \
           -e DEPENDABOT_EXTRA_CREDENTIALS='[{"type":"npm_registry","token":"<redacted>","registry":"npm.fontawesome.com"}]' \
           -e DEPENDABOT_ALLOW_CONDITIONS='[{"dependency-name":"django*","dependency-type":"direct"}]' \
           -e DEPENDABOT_IGNORE_CONDITIONS='[{"dependency-name":"@types/*"}]' \
           -e DEPENDABOT_COMMIT_MESSAGE_OPTIONS='{"prefix":"(dependabot)"}' \
           -e DEPENDABOT_BRANCH_NAME_SEPARATOR='/' \
           -e DEPENDABOT_MILESTONE=123 \
           -e DEPENDABOT_UPDATER_OPTIONS='goprivate=true,kubernetes_updates=true' \
           -e AZURE_PROTOCOL=http \
           -e AZURE_HOSTNAME=my-devops.com \
           -e AZURE_PORT=8080 \
           -e AZURE_VIRTUAL_DIRECTORY=tfs \
           -e AZURE_ACCESS_TOKEN=abcd..efgh \
           -e AZURE_ORGANIZATION=tinglesoftware \
           -e AZURE_PROJECT=oss \
           -e AZURE_REPOSITORY=repro-411 \
           -e AZURE_SET_AUTO_COMPLETE=true \
           -e AZURE_AUTO_APPROVE_PR=true \
           -e AZURE_AUTO_APPROVE_USER_TOKEN=ijkl..mnop \
           ghcr.io/tinglesoftware/dependabot-updater-nuget update_script
```

## Environment Variables

To run the script, some environment variables are required.

|Variable Name|Description|
|--|--|
|GITHUB_ACCESS_TOKEN|**_Optional_**. The GitHub token (classic) for authenticating requests against GitHub public repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [documentation](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens.|
|DEPENDABOT_JOB_ID|**_Optional_**. The unique id for the update job run. Used for logging and auditing. When not specified, the current date/timestamp is used.|
|DEPENDABOT_JOB_PATH|**_Optional_**. The temporary working directory for dependency updates. When not specified, the path '/tmp/dependabot-job-<DEPENDABOT_JOB_ID>' is used.|
|DEPENDABOT_PACKAGE_MANAGER|**_Required_**. The type of packages to check for dependency upgrades. Examples: `nuget`, `maven`, `gradle`, `npm_and_yarn`, etc. See the [updated-script](./script/update_script.rb) or [docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#package-ecosystem) for more.|
|DEPENDABOT_DIRECTORY|**_Optional_**. The directory in which dependencies are to be checked. When not specified, the root of the repository (denoted as '/') is used.|
|DEPENDABOT_TARGET_BRANCH|**_Optional_**. The branch to be targeted when creating a pull request. When not specified, Dependabot will resolve the default branch of the repository.|
|DEPENDABOT_VERSIONING_STRATEGY|**_Optional_**. The versioning strategy to use. See [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#versioning-strategy) for the allowed values|
|DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT|**_Optional_**. The maximum number of open pull requests to have at any one time. Defaults to 5. Setting to 0 implies security only updates.|
|DEPENDABOT_EXTRA_CREDENTIALS|**_Optional_**. The extra credentials in JSON format. Extra credentials can be used to access private NuGet feeds, docker registries, maven repositories, etc. For example a private registry authentication (For example FontAwesome Pro: `[{"type":"npm_registry","token":"<redacted>","registry":"npm.fontawesome.com"}]`)|
|DEPENDABOT_ALLOW_CONDITIONS|**_Optional_**. The dependencies whose updates are allowed, in JSON format. This can be used to control which packages can be updated. For example: `[{\"dependency-name\":"django*",\"dependency-type\":\"direct\"}]`. See [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#allow) for more.|
|DEPENDABOT_IGNORE_CONDITIONS|**_Optional_**. The dependencies to be ignored, in JSON format. This can be used to control which packages can be updated. For example: `[{\"dependency-name\":\"express\",\"versions\":[\"4.x\",\"5.x\"]}]`. See [official docs](https://docs.github.com/en/free-pro-team@latest/github/administering-a-repository/configuration-options-for-dependency-updates#ignore) for more.|
|DEPENDABOT_COMMIT_MESSAGE_OPTIONS|**_Optional_**. The commit message options, in JSON format. For example: `{\"prefix\":\"(dependabot)\"}`. See [official docs](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#commit-message) for more.|
|DEPENDABOT_LABELS|**_Optional_**. The custom labels to be used, in JSON format. This can be used to override the default values. For example: `[\"npm dependencies\",\"triage-board\"]`. See [official docs](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/customizing-dependency-updates#setting-custom-labels) for more.|
|DEPENDABOT_REVIEWERS|**_Optional_**. The identifiers of the users to review the pull requests, in JSON format. These shall be added as optional approvers. For example: `[\"23d9f23d-981e-4a0c-a975-8e5c665914ec\",\"62b67ef1-58e9-4be9-83d3-690a6fc67d6b\"]`.
|DEPENDABOT_ASSIGNEES|**_Optional_**. The identifiers of the users to be assigned to the pull requests, in JSON format. These shall be added as required approvers. For example: `[\"be9321e2-f404-4ffa-8d6b-44efddb04865\"]`. |
|DEPENDABOT_BRANCH_NAME_SEPARATOR|**_Optional_**. The separator to use in created branches. For example: `-`. See [official docs](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#pull-request-branch-nameseparator) for more.|
|DEPENDABOT_REJECT_EXTERNAL_CODE|**_Optional_**. Determines if the execution external code is allowed. Defaults to `false`.|
|DEPENDABOT_FAIL_ON_EXCEPTION|**_Optional_**. Determines if the execution should fail when an exception occurs. Defaults to `true`.|
|DEPENDABOT_SECURITY_ADVISORIES_FILE|**_Optional_**. The absolute file path containing security advisories in JSON format. For example: `/mnt/security_advisories/nuget-2022-12-13.json`|
|DEPENDABOT_EXCLUDE_REQUIREMENTS_TO_UNLOCK|**_Optional_**. Exclude certain dependency updates requirements. See list of allowed values [here](https://github.com/dependabot/dependabot-core/issues/600#issuecomment-407808103). Useful if you have lots of dependencies and the update script too slow. The values provided are space-separated. Example: `own all` to only use the `none` version requirement.|
|DEPENDABOT_MILESTONE|**_Optional_**. The identifier of the work item to be linked to the Pull Requests that dependabot creates.|
|DEPENDABOT_UPDATER_OPTIONS|**_Optional_**. Comma separated list of updater options; available options depend on PACKAGE_MANAGER. Example: `goprivate=true,kubernetes_updates=true`.|
|DEPENDABOT_DEPENDENCY_GROUPS|**_Optional_**. The list of dependency group rules, in JSON format. For example: `[{"name":"microsoft","rules":{"patterns":["Microsoft.*"]}}]`|
|DEPENDABOT_SKIP_PULL_REQUESTS|**_Optional_**. Determines whether to skip creation and updating of pull requests. When set to `true` the logic to update the dependencies is executed but the actual Pull Requests are not created/updated. This is useful for debugging. Defaults to `false`.|
|DEPENDABOT_AUTHOR_EMAIL|**_Optional_**. The email address to use for the change commit author, can be used e.g. in private Azure DevOps Server deployments to associate the committer with an existing account, to provide a profile picture.|
|DEPENDABOT_AUTHOR_NAME|**_Optional_**. The display name to use for the change commit author.|
|DEPENDABOT_DEBUG|**_Optional_**. Determines if verbose log messages are logged. Useful for diagnosing issues. Defaults to `false`.|
|AZURE_PROTOCOL|**_Optional_**. The transport protocol (`http` or `https`) used by your Azure DevOps installation. Defaults to `https`.|
|AZURE_HOSTNAME|**_Optional_**. The hostname of the where the organization is hosted. Defaults to `dev.azure.com` but for older organizations this may have the format `xxx.visualstudio.com`. Check the url on the browser. For Azure DevOps Server, this may be the unexposed one e.g. `localhost` or one that you have exposed publicly via DNS.|
|AZURE_PORT|**_Optional_**. The TCP port used by your Azure DevOps installation. Defaults to `80` or `443`, depending on the indicated protocol.|
|AZURE_VIRTUAL_DIRECTORY|**_Optional_**. Some Azure DevOps Server installations are hosted in an IIS virtual directory, traditionally named tfs. This variable can be used to define the name of that virtual directory. By default, this is not set.|
|AZURE_ACCESS_USERNAME|**_Optional_**. This Variable can be used together with the User Password in the Access Token Variable to use basic Auth when connecting to Azure Dev Ops. By default, this is not set.|
|AZURE_ACCESS_TOKEN|**_Required_**. The Personal Access in Azure DevOps for accessing the repository and creating pull requests. The required permissions are: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token|
|AZURE_ORGANIZATION|**_Required_**. The name of the Azure DevOps Organization. This is can be extracted from the URL of the home page. https://dev.azure.com/{organization}/|
|AZURE_PROJECT|**_Required_**. The name of the Azure DevOps Project within the above organization. This can be extracted them the URL too. https://dev.azure.com/{organization}/{project}/|
|AZURE_REPOSITORY|**_Required_**. The name of the Azure DevOps Repository within the above project to run Dependabot against. This can be extracted from the URL of the repository. https://dev.azure.com/{organization}/{project}/_git/{repository}/|
|AZURE_SET_AUTO_COMPLETE|**_Optional_**. Determines if the pull requests that dependabot creates should have auto complete set. When set to `true`, pull requests that pass all policies will be merged automatically|
|AZURE_AUTO_COMPLETE_IGNORE_CONFIG_IDS|**_Optional_**. List of any policy configuration Id's which auto-complete should not wait for. Only applies to optional policies. Auto-complete always waits for required (blocking) policies.|
|AZURE_AUTO_APPROVE_PR|**_Optional_**. Determines if the pull requests that dependabot creates should be automatically completed. When set to `true`, pull requests will be approved automatically.|
|AZURE_AUTO_APPROVE_USER_TOKEN|**_Optional_**. A personal access token for the user to automatically approve the created PR. `AZURE_AUTO_APPROVE_PR` must be set to `true` for this to work.|

# Development guide

## Getting the development environment ready
First, ensure you have [Docker](https://docs.docker.com/engine/install/) and [Ruby](https://www.ruby-lang.org/en/documentation/installation/) installed.
On Linux, you'll need the the build essentials and Ruby development packages too; These are typically `build-essentials` and `ruby-dev`.

Next, install project build tools with bundle:

```bash
cd updater
bundle install
```

## Building the Docker image
Each package ecosystem must be built separately; You only need to build images for the ecosystems that you plan on testing.

```bash
docker build \
    -f updater/Dockerfile \
    --build-arg BUILDKIT_INLINE_CACHE=1 \
    --build-arg ECOSYSTEM=<your-ecosystem> \
    -t "ghcr.io/tinglesoftware/dependabot-updater-<your_ecosystem>:latest" \
    .
```

## Running your code changes
To test run your code changes, you'll first need to build the updater Docker image (see above), then run the updater Docker image in a container with all the required environment variables (see above).

## Running the code linter
```bash
cd updater
bundle exec rubocop
bundle exec rubocop -a # to automatically fix any correctable offenses
```

## Running the unit tests
```bash
cd updater
bundle exec rspec spec
```
