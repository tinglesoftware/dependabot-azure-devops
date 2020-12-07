# Dependabot for Azure DevOps

This repository contains convenience tool(s) for updating dependencies in Azure DevOps repositories using [Dependabot](https://dependabot.com).

The work in this repository is based on inspired and ocassionally guided by:

1. Official Script support: [code](https://github.com/dependabot/dependabot-script)
2. Andrew Craven's work: [blog](https://medium.com/@acraven/updating-dependencies-in-azure-devops-repos-773cbbb6029d), [code](https://github.com/acraven/azure-dependabot)
3. Chris' work: [code](https://github.com/chris5287/dependabot-for-azuredevops)

In this repository you'll find:

1. Dependabot's [Update script](./src/update-script.rb) in Ruby.
2. Dockerfile and build/image for running the script via Docker [here](./src/Dockerfile).
3. Azure DevOps Extension source (./src/Extension). _[coming soon]_
4. Kubernetes CronJob [template](./templates/dependabot-template.yml).
5. Semi-hosted version of Dependabot [here](./src/Hosting). _[coming soon]_

## Environment Variables

To run the script, some environment variables are required.

|Variable Name|Description|
|--|--|
|ORGANIZATION|**_Required_**. The name of the Azure DevOps Organization. This is can be extracted from the URL of the home page. https://dev.azure.com/{organization}/|
|PROJECT|**_Required_**. The name of the Azure DevOps Project within the above organization. This can be extracted them the URL too. https://dev.azure.com/{organization}/{project}/|
|REPOSITORY|**_Required_**. The name of the Azure DevOps Repository within the above project to run Dependabot against. This can be extracted from the URL of the repository. https://dev.azure.com/{organization}/{project}/_git/{repository}/|
|PACKAGE_MANAGER|**_Required_**. The type of packages to check for dependecy upgrades. Examples: `nuget`, `maven`, `gradle`, `npm_and_yarn`, etc. See the [updated-script](./src/update-script.rb) for more.
|SYSTEM_ACCESSTOKEN|**_Required_**. The Personal Access in Azure DevOps for accessing the repository and creating pull requests. The required permissions are: <br/>-&nbsp;Code (Full)<br/>-&nbsp;Packaging (Read)<br/>-&nbsp;Pull Requests Threads (Read & Write).<br/>See the [documentation](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=preview-page#create-a-pat) to know more about creating a Personal Access Token|
|GITHUB_ACCESS_TOKEN|**_Optional_**. The GitHub token for authenticating requests against GitHub public repositories. This is useful to avoid rate limiting errors. The token must include permissions to read public repositories. See the [documentation](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token) for more on Personal Access Tokens.|
|PRIVATE_FEED_NAME|**_Optional_**. The name of the private feed within the Azure DevOps organization to use when resolving private packages. The script automatically adds the correct feed/registry URL to the process depending on the value set for `PACKAGE_MANAGER`. This is only required if there are packages in a private feed.|
|DIRECTORY|**_Optional_**. The directory in which dependancies are to be checked. When not specified, the root of the repository (denoted as '/') is used.

## Running in docker

First, you must pull the image locally to your machine:

```bash
docker pull tingle/dependabot-azure-devops:0.1.1
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
           tingle/dependabot-azure-devops:0.1.1
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
           tingle/dependabot-azure-devops:0.1.1
```

## Running in Azure DevOps

Coming soon

## Running using a Kubernetes CronJob

Kubernetes CronJobs are useful tools for running repeated jobs on a schedule. For more information on them read the [documentation](https://kubernetes.io/docs/concepts/workloads/controllers/cron-jobs/).
Using the Docker version of tools, we can create a CronJob and have it run periodically. The [environment variables](#environment-variables) discussed above are supplied in the job template but can be stored in a [ConfigMap](https://kubernetes.io/docs/concepts/configuration/configmap/) for ease of reuse.

Use the [template provided](./templates/dependabot-template.yml) and replace the parameters in curly braces (e.g. replace `{{ORGANIZATION}}` with the actual value for your organization), then deploy it. Be sure to replace the `{{CRON_SCHEDULE}}` variable with the desired schedule as per the [Cron format](https://en.wikipedia.org/wiki/Cron) An example would like:

```yml
apiVersion: batch/v1beta1
kind: CronJob
metadata:
  name: dependabot-oss-ado-dp
  labels:
    tingle.io/dependabot: 'true'
  annotations:
    project: 'oss'
    repository: 'ado-dp'
spec:
  schedule: '0 2 * * *' # 2 am GMT, every day
  jobTemplate:
    metadata:
      labels:
        tingle.io/dependabot: 'true'
      annotations:
        project: 'oss'
        repository: 'ado-dp'
    spec:
      template:
        spec:
          containers:
            - name: dependabot
              image: 'tingle/dependabot-azure-devops:0.1.1'
              env:
                - name: ORGANIZATION
                  value: 'tinglesoftware'
                - name: PROJECT
                  value: 'ado'
                - name: REPOSITORY
                  value: 'ado-dp'
                - name: PACKAGE_MANAGER
                  value: 'nuget'
                - name: SYSTEM_ACCESSTOKEN
                  value: 'abcd...efgh'
                - name: GITHUB_ACCESS_TOKEN
                  value: 'ijkl..mnop'
                - name: PRIVATE_FEED_NAME
                  value: 'tinglesoftware'
                - name: DIRECTORY
                  value: '/'
          restartPolicy: OnFailure

```

### Issues &amp; Comments

Please leave all comments, bugs, requests, and issues on the Issues page. We'll respond to your request ASAP!

### License

The code is licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php "Read more about the MIT license form") license. Refere to the [LICENSE](./LICENSE.md) file for more information.
