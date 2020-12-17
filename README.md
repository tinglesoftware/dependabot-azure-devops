# Dependabot for Azure DevOps

This repository contains convenience tool(s) for updating dependencies in Azure DevOps repositories using [Dependabot](https://dependabot.com).

The work in this repository is based on inspired and occasionally guided by:

1. Official Script support: [code](https://github.com/dependabot/dependabot-script)
2. Andrew Craven's work: [blog](https://medium.com/@acraven/updating-dependencies-in-azure-devops-repos-773cbbb6029d), [code](https://github.com/acraven/azure-dependabot)
3. Chris' work: [code](https://github.com/chris5287/dependabot-for-azuredevops)

In this repository you'll find:

1. Dependabot's [Update script](./src/script/update-script.rb) in Ruby.
2. Dockerfile and build/image for running the script via Docker [here](./src/docker).
3. Azure DevOps [Extension source](./src/extension).
4. Kubernetes CronJob [template](./templates/dependabot-template.yml).
5. Semi-hosted version of Dependabot [here](./src/hosting).

## Running in Azure DevOps

To run dependabot in Azure Pipelines, you need to install the extension from the [marketplace](https://marketplace.visualstudio.com/items?itemName=tingle-software.dependabot).

It's up to the user to schedule the pipeline in whatever is correct for their solution.

An example of a YAML pipeline:

```yaml
trigger: none # Disable CI trigger

schedules:
- cron: '0 2 0 0 0' # daily at 2am UTC
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
    packageManager: 'nuget'
- task: dependabot@1
  inputs:
    packageManager: 'docker'
    directory: '/docker'
```

Since this task makes use of a docker image, it may take time to install the docker image. The user can choose to speed this up by using [Caching for Docker](https://docs.microsoft.com/en-us/azure/devops/pipelines/release/caching?view=azure-devops#docker-images) in Azure Pipelines. See the [source file](./src/extension/task/index.ts) for the exact image tag, e.g. `tingle/dependabot-azure-devops:0.1.1`. Subsequent dependabot tasks in a job will be faster after the first one pulls the image for the first time.

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
              - name: TARGET_BRANCH
                value: 'master'
              - name: AZURE_HOSTNAME
                value: 'dev.azure.com'
              - name: AZURE_HOSTNAME_PACKAGING
                value: 'pkgs.dev.azure.com'
          restartPolicy: OnFailure

```

### Issues &amp; Comments

Please leave all comments, bugs, requests, and issues on the Issues page. We'll respond to your request ASAP!

### License

The code is licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php "Read more about the MIT license form") license. Refer to the [LICENSE](./LICENSE) file for more information.
