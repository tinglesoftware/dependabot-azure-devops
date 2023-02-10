# Dependabot for Azure DevOps

This repository contains tools for updating dependencies in Azure DevOps repositories using [Dependabot](https://dependabot.com).

![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/tinglesoftware/dependabot-azure-devops/docker.yml?branch=main&style=flat-square)
[![Release](https://img.shields.io/github/release/tinglesoftware/dependabot-azure-devops.svg?style=flat-square)](https://github.com/tinglesoftware/dependabot-azure-devops/releases/latest)
[![Docker Image](https://img.shields.io/docker/image-size/tingle/dependabot-azure-devops/latest?style=flat-square)](https://hub.docker.com/r/tingle/dependabot-azure-devops)
[![Docker Pulls](https://img.shields.io/docker/pulls/tingle/dependabot-azure-devops?style=flat-square)](https://hub.docker.com/r/tingle/dependabot-azure-devops)
[![license](https://img.shields.io/github/license/tinglesoftware/dependabot-azure-devops.svg?style=flat-square)](LICENSE)

In this repository you'll find:

1. Dependabot [updater](./updater) in Ruby.
2. Dockerfile and build/image for running the updater via Docker [here](./Dockerfile).
3. Azure DevOps [Extension](https://marketplace.visualstudio.com/items?itemName=tingle-software.dependabot) and [source](./extension).
4. Kubernetes CronJob [template](#kubernetes-cronjob).

## Using a configuration file

Similar to the GitHub native version where you add a `.github/dependabot.yml` file, this repository adds support for the same official [configuration options](https://help.github.com/github/administering-a-repository/configuration-options-for-dependency-updates) via a file located at `.github/dependabot.yml`. This support is only available in the Azure DevOps extension and the [managed version](https://managd.dev). However, the extension does not currently support automatically picking up the file, a pipeline is still required. See [docs](./extension/README.md#usage).

## Credentials for private registries and feeds

Besides accessing the repository only, sometimes private feeds/registries may need to be accessed.
For example a private NuGet feed or a company internal docker registry.

Adding configuration options for private registries is setup in `dependabot.yml`
according to the dependabot [description](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#configuration-options-for-private-registries).

Example:

```yml
version: 2
registries:
  my-Extern@Release:
    type: nuget-feed
    url: https://dev.azure.com/organization1/_packaging/my-Extern@Release/nuget/v3/index.json
    token: PAT:${{MY_DEPENDABOT_ADO_PAT}}
  my-analyzers:
    type: nuget-feed
    url: https://dev.azure.com/organization2/_packaging/my-analyzers/nuget/v3/index.json
    token: PAT:${{ANOTHER_PAT}}
  artifactory:
    type: nuget-feed
    url: https://artifactory.com/api/nuget/v3/myfeed
    token: PAT:${{DEPENDABOT_ARTIFACTORY_PAT}}
updates:
...
```

Note:

1. `${{VARIABLE_NAME}}` notation is used liked described [here](https://docs.github.com/en/code-security/dependabot/working-with-dependabot/managing-encrypted-secrets-for-dependabot)
BUT the values will be used from Environment Variables in the pipeline/environment. Template variables are not supported for this replacement.

2. When using a token the notation should be `PAT:${{VARIABLE_NAME}}`. Otherwise the wrong authentication mechanism is used by dependabot, see [here](https://github.com/tinglesoftware/dependabot-azure-devops/issues/50).

When working with Azure Artifacts, some extra permission steps need to be done:

1. The PAT should have *Packaging Read* permission.
2. The user owning the PAT must be granted permissions to access the feed either directly or via a group. An easy way for this is to give `Contributor` permissions the `[{project_name}]\Contributors` group under the `Feed Settings -> Permissions` page. The page has the url format: `https://dev.azure.com/{organization}/{project}/_packaging?_a=settings&feed={feed-name}&view=permissions`.

## Security Advisories, Vulnerabilities, and Updates

Security-only updates ia a mechanism to only create pull requests for dependencies with vulnerabilities by updating them to the earliest available non-vulnerable version. Security updates are supported in the same way as the GitHub-hosted version. In addition, you can provide extra advisories, such as those for an internal dependency, in a JSON file via the `securityAdvisoriesFile` input e.g. `securityAdvisoriesFile: '$(Pipeline.Workspace)/advisories.json'`. A file example is available [here](./advisories-example.json).

A GitHub access token with `public_repo` access is required to perform the GitHub GraphQL for `securityVulnerabilities`.

## Kubernetes CronJob

A Kubernetes CronJobs is a useful resource for running tasks (a.k.a Jobs) on a recurring schedule. For more information on them read the [documentation](https://kubernetes.io/docs/concepts/workloads/controllers/cron-jobs/). Using the Docker image, we can create a CronJob and have it run periodically. The [environment variables](./docs/docker.md#environment-variables) are supplied in the job template but can be stored in a [ConfigMap](https://kubernetes.io/docs/concepts/configuration/configmap/) for ease of reuse.

Use the [template provided](./cronjob-template.yaml) and replace the parameters in curly braces (e.g. replace `{{azure_organization}}` with the actual value for your organization), then deploy it. Be sure to replace the `{{k8s_schedule}}` variable with the desired schedule as per the [Cron format](https://en.wikipedia.org/wiki/Cron).

### Notes

1. History for successful and failed jobs is restricted to 1 (change to suit you).
2. Jobs are removed after 2 days (`ttlSecondsAfterFinished: 172800`). No need keep it for too long.
3. Jobs run duration is capped at 1 hour (`activeDeadlineSeconds: 3600`). This should be enough time.
4. Labels can be used to find cronjobs created.
5. Annotations can be used to store extra data for comparison but not searching/finding e.g. package ecosystem.

### Acknowledgements

The work in this repository is based on inspired and occasionally guided by some predecessors in the same area:

1. Official Script support: [code](https://github.com/dependabot/dependabot-script)
2. Andrew Craven's work: [blog](https://medium.com/@acraven/updating-dependencies-in-azure-devops-repos-773cbbb6029d), [code](https://github.com/acraven/azure-dependabot)
3. Chris' work: [code](https://github.com/chris5287/dependabot-for-azuredevops)
4. andrcun's work on GitLab: [code](https://gitlab.com/dependabot-gitlab/dependabot)
5. WeWork's work for GitLab: [code](https://github.com/wemake-services/kira-dependencies)

### Issues &amp; Comments

Please leave all comments, bugs, requests, and issues on the Issues page. We'll respond to your request ASAP!
