# Dependabot for Azure DevOps

This repository contains convenience tool(s) for updating dependencies in Azure DevOps repositories using [Dependabot](https://dependabot.com). In this repository you'll find:

1. Dependabot's [Update script](./src/update-script.rb) in Ruby.
2. Dockerfile and build/image for running the script via Docker [here](./src/Dockerfile).
3. Azure DevOps Extension source (./src/Extension). _[coming soon]_
4. Kubernetes CronJob [template](./src/templates/dependabot-template.yml). _[coming soon]_
5. Semi-hosted version of Dependabot [here](./src/Hosting). _[coming soon]_

## Environment Variables

To run the script, some environment variables are required.

|Variable Name|Description|
|--|--|
|ORGANIZATION|**_Required_**. The name of the Azure DevOps Organization. This is can be extracted from the URL of the home page. https://dev.azure.com/{organization}/|
|PROJECT|**_Required_**. The name of the Azure DevOps Project within the above organization. This can be extracted them the URL too. https://dev.azure.com/{organization}/{project}/|
|REPOSITORY|**_Required_**. The name of the Azure DevOps Repository within the above project to run Dependabot against. This can be extracted from the URL of the repository. https://dev.azure.com/{organization}/{project}/_git/{repository}/|
|DIRECTORY|**_Optional_**. The directory in which dependancies are to be checked. When not specified, the root of the repository (denoted as '/') is used.
|PACKAGE_MANAGER|**_Required_**. The type of packages to check for dependecy upgrades. Examples: `nuget`, `maven`, `gradle`, `npm_and_yarn`, etc. See the [updated-script](./src/update-script.rb) for more.
|SYSTEM_ACCESSTOKEN|**_Required_**. The Personal Access in Azure DevOps for accessing the repository and creating pull requests. The required permissions are: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Packaging (Read)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token|
|GITHUB_ACCESS_TOKEN|**_Optional_**. The GitHub token for authenticating requests against GitHub public repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [documentation](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens.|
|PRIVATE_FEED_NAME|**_Optional_**. The name of the private feed within the Azure DevOps organization to use when resolving private packages. The script automatically adds the correct feed/registry URL to the process depending on the value set for `PACKAGE_MANAGER`. This is only required if there are packages in a private feed.|

## Running in docker

First, you must pull the image locally to your machine:

```bash
docker pull docker.pkg.github.com/tinglesoftware/dependabot-azure-devops/dependabot-azure-devops:0.1.0
```

Next create and run a container from the image:

```bash
docker run --rm -it \
           -e ORGANIZATION=<your-organization-here> \
           -e PROJECT=<your-project-here> \
           -e REPOSITORY=<your-repository-here> \
           -e PACKAGE_MANAGER=<your-package-manager-here> \
           -e SYSTEM_ACCESSTOKEN=<your-devops-token-here> \
           -e GITHUB_ACCESS_TOKEN=<your-github-token-here> \
           -e PRIVATE_FEED_NAME=,your-private-feed> \
           -e DIRECTORY=/ \
           tinglesoftware/dependabot-azure-devops/dependabot-azure-devops:0.1.0
```

An example:

```bash
docker run --rm -it \
           -e ORGANIZATION=tinglesoftware \
           -e PROJECT=oss \
           -e REPOSITORY=dependabot-azure-devops \
           -e PACKAGE_MANAGER=nuget \
           -e SYSTEM_ACCESSTOKEN=abcd..efgh \
           -e GITHUB_ACCESS_TOKEN=ijkl..mnop \
           -e PRIVATE_FEED_NAME=tinglesoftware \
           -e DIRECTORY=/ \
           tinglesoftware/dependabot-azure-devops/dependabot-azure-devops:0.1.0
```

## Running in Azure DevOps

Coming soon

## Running using a Kubernetes CronJob of Job

Coming soon
