# Dependabot for Azure DevOps

This repository contains convenience tool(s) for updating dependencies in Azure DevOps repositories using [Dependabot](https://dependabot.com).

![GitHub Workflow Status](https://img.shields.io/github/workflow/status/tinglesoftware/dependabot-azure-devops/Docker?style=flat-square)
[![Release](https://img.shields.io/github/release/tinglesoftware/dependabot-azure-devops.svg?style=flat-square)](https://github.com/tinglesoftware/dependabot-azure-devops/releases/latest)
[![Docker Image](https://img.shields.io/docker/image-size/tingle/dependabot-azure-devops/latest?style=flat-square)](https://hub.docker.com/r/tingle/dependabot-azure-devops)
[![Docker Pulls](https://img.shields.io/docker/pulls/tingle/dependabot-azure-devops?style=flat-square)](https://hub.docker.com/r/tingle/dependabot-azure-devops)
[![license](https://img.shields.io/github/license/tinglesoftware/dependabot-azure-devops.svg?style=flat-square)](LICENSE)

In this repository you'll find:

1. Dependabot's [Update script](./src/script/update-script.rb) in Ruby.
2. Dockerfile and build/image for running the script via Docker [here](./src/script/Dockerfile).
3. Azure DevOps [Extension](https://marketplace.visualstudio.com/items?itemName=tingle-software.dependabot) and [source](./src/extension).
4. Kubernetes CronJob [template](#kubernetes-cronjob).
5. Hosted versions: [fully hosted](#hosted-version), self hosted (source code and instructions coming soon).

## Using a configuration file

Similar to the GitHub native version where you add a `.github/dependabot.yml` file, this repository adds support for the same official [configuration options](https://help.github.com/github/administering-a-repository/configuration-options-for-dependency-updates) via a file located at `.azuredevops/dependabot.yml` or `.github/dependabot.yml`. This support is only available in the Azure DevOps extension and the hosted version. However, the extension does not currently support automatically picking up the file, a pipeline is still required. See [docs](./src/extension/README.md#usage).

## Credentials for private registries and feeds

Besides accessing the repository, sometimes, private feeds/registries may need to be accessed. For example a private NuGet feed or a company internal docker registry. Adding credentials is currently done via the `DEPENDABOT_EXTRA_CREDENTIALS` environment variable. The value is supplied in JSON hence allowing any type of credentials even if they are not for private feeds/registries.

When working with Azure Artifacts, some extra steps need to be done:

1. The PAT should have *Packaging Read* permission.
2. The user owning the PAT must be granted permissions to access the feed either directly or via a group. An easy way for this is to give `Contributor` permissions the `[{project_name}]\Contributors` group under the `Feed Settings -> Permissions` page. The page has the url format: `https://dev.azure.com/{organization}/{project}/_packaging?_a=settings&feed={feed-name}&view=permissions`.

## Kubernetes CronJob

A Kubernetes CronJobs is a useful resource for running tasks (a.k.a Jobs) on a recurring schedule. For more information on them read the [documentation](https://kubernetes.io/docs/concepts/workloads/controllers/cron-jobs/). Using the Docker image, we can create a CronJob and have it run periodically. The [environment variables](./src/script/README.md#environment-variables) are supplied in the job template but can be stored in a [ConfigMap](https://kubernetes.io/docs/concepts/configuration/configmap/) for ease of reuse.

Use the [template provided](./cronjob-template.yaml) and replace the parameters in curly braces (e.g. replace `{{azure_organization}}` with the actual value for your organization), then deploy it. Be sure to replace the `{{k8s_schedule}}` variable with the desired schedule as per the [Cron format](https://en.wikipedia.org/wiki/Cron).

### Notes

1. Timezone support is not yet available in Kubernetes ([Issue 1](https://github.com/kubernetes/kubernetes/issues/47202), [Issue 2](https://github.com/kubernetes/kubernetes/issues/78795)). If this is important to you, consider using [cronjobber](https://github.com/hiddeco/cronjobber).
2. History for successful and failed jobs is restricted to 1 (change to suit you).
3. Jobs are removed after 2 days (`ttlSecondsAfterFinished: 172800`). No need keep it for too long.
4. Jobs run duration is capped at 1 hour (`activeDeadlineSeconds: 3600`). This should be enough time.
5. Labels can be used to find cronjobs created.
6. Annotations can be used to store extra data for comparison but not searching/finding e.g. package ecosystem.

## Hosted version

The hosted version ([source code](https://github.com/tinglesoftware/zote)) for Azure DevOps work almost similar to the native version of dependabot on GitHub, hosted in your own Kubernetes cluster. It supports:

1. Pulling configuration from a file located at `.azuredevops/dependabot.yml` or `.github/dependabot.yml`.
2. Adding/updating the file, triggers a run.
3. Extra credentials for private registries, feeds and package repositories.
4. Hosted on Kubernetes; easier compared to using Azure build agents.
5. Auto resolving of merge conflicts using webhooks.
6. Viewing the most recent runs for each repository, project and organization configured.

## Still using the old `*.visualstudio.com` URL?

The new URL in the format `https://dev.azure.com/{organization}` is recommended. If you are still using the older `{organization}.visualstudio.com` URL, you need to toggle on the new URL. As far as out testing has gone, we have not had any issues using both the new and old URL. It is possible to keep both. The [core implementation](https://github.com/dependabot/dependabot-core) will only support the new one. See [#27](https://github.com/tinglesoftware/dependabot-azure-devops/issues/27#issuecomment-768054722) for more explanation. For someone really looking to use dependabot to keep dependencies up to date, migrating to the new URL should really be a no-brainer.

### Acknowledgements

The work in this repository is based on inspired and occasionally guided by some predecessors in the same area:

1. Official Script support: [code](https://github.com/dependabot/dependabot-script)
2. Andrew Craven's work: [blog](https://medium.com/@acraven/updating-dependencies-in-azure-devops-repos-773cbbb6029d), [code](https://github.com/acraven/azure-dependabot)
3. Chris' work: [code](https://github.com/chris5287/dependabot-for-azuredevops)
4. andrcun's work on GitLab: [code](https://gitlab.com/dependabot-gitlab/dependabot)
5. WeWork's work for GitLab: [code](https://github.com/wemake-services/kira-dependencies)

### Issues &amp; Comments

Please leave all comments, bugs, requests, and issues on the Issues page. We'll respond to your request ASAP!

### License

The code is licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php "Read more about the MIT license form") license. Refer to the [LICENSE](./LICENSE) file for more information.
