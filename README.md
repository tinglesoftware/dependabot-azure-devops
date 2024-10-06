# Dependabot for Azure DevOps

This repository contains tools for updating dependencies in Azure DevOps repositories using [Dependabot](https://dependabot.com).

![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/tinglesoftware/dependabot-azure-devops/updater.yml?branch=main&style=flat-square)
[![Release](https://img.shields.io/github/release/tinglesoftware/dependabot-azure-devops.svg?style=flat-square)](https://github.com/tinglesoftware/dependabot-azure-devops/releases/latest)
[![license](https://img.shields.io/github/license/tinglesoftware/dependabot-azure-devops.svg?style=flat-square)](LICENSE)

In this repository you'll find:

1. Azure DevOps [Extension](https://marketplace.visualstudio.com/items?itemName=tingle-software.dependabot), [source code](./extension) and [docs](./docs/extension.md).
1. Dependabot Server, [source code](./server/) and [docs](./docs/server.md).
1. Dependabot Updater image, [Dockerfile](./updater/Dockerfile), [source code](./updater/) and [docs](./docs/updater.md). **(Deprecated since v2.0)**

> [!IMPORTANT]
> This project is currently undergoing a major version increment (V1 → V2); See the [migration guide](./docs/migrations/v1-to-v2.md#summary-of-changes-v1--v2) for more details and progress updates.

## Table of Contents
- [Getting started](#getting-started)
- [Using a configuration file](#using-a-configuration-file)
- [Configuring private feeds and registries](#configuring-private-feeds-and-registries)
- [Configuring security advisories and known vulnerabilities](#configuring-security-advisories-and-known-vulnerabilities)
- [Configuring experiments](#configuring-experiments)
- [Configuring assignees and reviewers](#configuring-assignees-and-reviewers)
- [Unsupported features and configurations](#unsupported-features-and-configurations)
   * [Extension Task](#extension-task)
      + [dependabot@V2](#dependabotv2)
      + [dependabot@V1](#dependabotv1)
   * [Updater Docker image](#updater-docker-image)
   * [Server](#server)
- [Migration Guide](#migration-guide)
- [Contributing](#contributing)
   * [Reporting issues and feature requests](#reporting-issues-and-feature-requests)
   * [Submitting pull requests](#submitting-pull-requests)
- [Acknowledgements](#acknowledgements)

## Getting started

Unlike the GitHub-hosted version, Dependabot for Azure DevOps must be explicitly setup in your organisation; creating a `dependabot.yml` file alone is **not** enough to enable updates. There are two ways to enable Dependabot, using:

- [Azure DevOps Extension](https://marketplace.visualstudio.com/items?itemName=tingle-software.dependabot) - Ideal if you want to get Dependabot running with minimal administrative effort. The extension can run directly inside your existing pipeline agents and doesn't require hosting of any additional services. Because the extension runs in pipelines, this option does **not** scale well if you have a large number of projects and repositories.

- [Hosted Server](./docs/server.md) - Ideal if you have a large number of projects and repositories or prefer to run Dependabot as a managed service instead of using pipeline agents. See [why should I use the server?](./docs/server.md#why-should-i-use-the-server) for more info.

> [!NOTE]
> A hosted version is available to sponsors (most, but not all). It includes hassle free runs where the infrastructure is maintained for you. Much like the GitHub hosted version. Alternatively, you can run and host your own [self-hosted server](./docs/server.md). Once you sponsor, you can send out an email to a maintainer or wait till they reach out. This is meant to ease the burden until GitHub/Azure/Microsoft can get it working natively (which could also be never) and hopefully for free.

## Using a configuration file

Similar to the GitHub-hosted version, Dependabot is configured using a [dependabot.yml file](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file) located at `.azuredevops/dependabot.yml` or `.github/dependabot.yml` in your repository. 

Most [official configuration options](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file) are supported since V2; Earlier versions have several limitations, see [unsupported features and configurations](#unsupported-features-and-configurations) for more. 

## Configuring private feeds and registries

Besides accessing the repository, sometimes private feeds/registries may need to be accessed. For example a private NuGet feed or a company internal docker registry.

Private registries are configured in `dependabot.yml`, refer to the [official documentation](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#configuration-options-for-private-registries).

Examples:

```yml
version: 2
registries:

  # Azure DevOps private feed, all views
  my-analyzers:
    type: nuget-feed
    url: https://dev.azure.com/organization2/_packaging/my-analyzers/nuget/v3/index.json
    token: PAT:${{ MY_DEPENDABOT_ADO_PAT }}

  # Azure DevOps private feed, "Release" view only
  my-Extern@Release: 
    type: nuget-feed
    url: https://dev.azure.com/organization1/_packaging/my-Extern@Release/nuget/v3/index.json
    token: PAT:${{ MY_DEPENDABOT_ADO_PAT }}

  # Artifactory private feed using PAT
  artifactory:
    type: nuget-feed
    url: https://artifactory.com/api/nuget/v3/myfeed
    token: PAT:${{ MY_DEPENDABOT_ARTIFACTORY_PAT }}

  # Other private feed using basic auth (username/password)
  telerik:
    type: nuget-feed
    url: https://nuget.telerik.com/v3/index.json
    username: ${{ MY_TELERIK_USERNAME }}
    password: ${{ MY_TELERIK_PASSWORD }}
    token: ${{ MY_TELERIK_USERNAME }}:${{ MY_TELERIK_PASSWORD }}

updates:
  ...
```

Note when using authentication secrets in configuration files:

> [!IMPORTANT]
> `${{ VARIABLE_NAME }}` notation is used liked described [here](https://docs.github.com/en/code-security/dependabot/working-with-dependabot/managing-encrypted-secrets-for-dependabot)
BUT the values will be used from pipeline environment variables. Template variables are not supported for this replacement. Replacement only works for values considered secret in the registries section i.e. `username`, `password`, `token`, and `key`

> [!IMPORTANT]
> When using an Azure DevOps Artifact feed, the token format must be `PAT:${{ VARIABLE_NAME }}` where `VARIABLE_NAME` is a pipeline/environment variable containing the PAT token. The PAT must:
>    1. Have `Packaging (Read)` permission.
>    2. Be issued by a user with permission to the feed either directly or via a group. An easy way for this is to give `Contributor` permissions the `[{project_name}]\Contributors` group under the `Feed Settings -> Permissions` page. The page has the url format: `https://dev.azure.com/{organization}/{project}/_packaging?_a=settings&feed={feed-name}&view=permissions`.

> [!NOTE]
> When using `dependabot@V1` with a private feed/registry secured with basic auth, the `username`, `password`, **and** `token` properties are all required. The token format must be `${{ USERNAME }}:${{ PASSWORD }}`.

> [!NOTE]
> When using `dependabot@V1` with a repository containing a `nuget.config` file configured with custom package sources, the `key` property is required for each registry. The key must match between `dependabot.yml` and `nuget.config` otherwise the package source will be duplicated, package source mappings will be ignored, and auth errors will occur during dependency discovery. If your `nuget.config` looks like this:
> ```xml
>  <?xml version="1.0" encoding="utf-8"?>
>  <configuration>
>    <packageSources>
>      <clear />
>      <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
>      <add key="my-organisation1-nuget" value="https://dev.azure.com/my-organization/_packaging/my-nuget-feed/nuget/v3/index.json" />
>    </packageSources>
>    <packageSourceMapping>
>      <packageSource key="nuget.org">
>        <package pattern="*" />
>      </packageSource>
>      <packageSource key="my-organisation-nuget">
>        <package pattern="Organisation.*" />
>      </packageSource>
>    </packageSourceMapping>
>  </configuration>
> ```
>
> Then your `dependabot.yml` registry should look like this:
> ```yml
>  version: 2
>  registries:
>    my-org:
>      type: nuget-feed
>      key: my-organisation1-nuget
>      url: https://dev.azure.com/my-organization/_packaging/my-nuget-feed/nuget/v3/index.json
>      token: PAT:${{ MY_DEPENDABOT_ADO_PAT }}
> ```

## Configuring security advisories and known vulnerabilities

Security-only updates is a mechanism to only create pull requests for dependencies with vulnerabilities by updating them to the earliest available non-vulnerable version. [Security updates are supported in the same way as the GitHub-hosted version](https://docs.github.com/en/code-security/dependabot/dependabot-security-updates/configuring-dependabot-security-updates#overriding-the-default-behavior-with-a-configuration-file) provided that a GitHub access token with `public_repo` access is provided in the `gitHubAccessToken` or `gitHubConnection` task inputs. 

You can provide extra security advisories, such as those for an internal dependency, in a JSON file via the `securityAdvisoriesFile` task input e.g. `securityAdvisoriesFile: '$(Pipeline.Workspace)/advisories.json'`. An example file is available in [./advisories-example.json](./advisories-example.json).

## Configuring experiments
Dependabot uses an internal feature flag system called "experiments". Typically, experiments represent new features or changes in logic which are still being internally tested before becoming generally available. In some cases, you may want to opt-in to experiments to work around known issues or to opt-in to preview features ahead of general availability (GA).

Experiments vary depending on the package ecyosystem used; They can be enabled using the `experiments` task input with a comma-seperated list of key/value pairs representing the experiments e.g. `experiments: 'tidy=true,vendor=true,goprivate=*'`.

> [!NOTE]
> Dependabot experinment names are not [publicly] documented. For convenience, some known experiments are listed below; However, **be aware that this may be out-of-date at the time of reading.**

<details>
<summary>List of known experiments from dependabot-core@0.278.0</summary>

|Package Ecosystem|Experiment Name|Value Type|Description|
|--|--|--|--|
| All | dedup_branch_names | true/false | |
| All | grouped_updates_experimental_rules | true/false | |
| All | grouped_security_updates_disabled | true/false | |
| All | record_ecosystem_versions | true/false | |
| All | record_update_job_unknown_error | true/false | |
| All | dependency_change_validation | true/false | |
| All | add_deprecation_warn_to_pr_message | true/false | |
| All | threaded_metadata | true/false | |
| Bundler | bundler_v1_unsupported_error | true/false | |
| Go | tidy | true/false | |
| Go | vendor | true/false | |
| Go | goprivate | string | |
| NPM and Yarn | enable_pnpm_yarn_dynamic_engine | true/false | |
| NuGet | nuget_native_analysis | true/false | https://github.com/dependabot/dependabot-core/pull/10025 |
| NuGet | nuget_native_updater | true/false | https://github.com/dependabot/dependabot-core/pull/10521 |
| NuGet | nuget_dependency_solver | true/false | https://github.com/dependabot/dependabot-core/pull/10343 |

> [!TIP]
> To find the latest list of Dependabot experiments, search the `dependabot-core` GitHub repository using queries like ["enabled?(x)"](https://github.com/search?q=repo%3Adependabot%2Fdependabot-core+%2Fenabled%5CW%5C%28.*%5C%29%2F&type=code) and ["options.fetch(x)"](https://github.com/search?q=repo%3Adependabot%2Fdependabot-core+%2Foptions%5C.fetch%5C%28.*%2C%2F&type=code). 

</details>

## Configuring assignees and reviewers
Dependabot allows for the configuration of both [`assignees`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#assignees) and [`reviewers`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#reviewers). However, Azure DevOps does not have the concept of pull request assignees. Because of this, `assignees` will be treated as **required** reviewers and `reviewers` will be treated as **optional** reviewers.

Reviewers can be any of the following values:

- User GUID
- User username
- User email address
- User full [display] name
- Group name
- Team name

## Unsupported features and configurations
We aim to support all [official configuration options](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file), but there are some limitations for:

### Extension Task

#### `dependabot@V2`
- [`schedule`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#scheduleinterval) is ignored, use [pipeline scheduled triggers](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/scheduled-triggers?view=azure-devops&tabs=yaml#scheduled-triggers) instead.
- [Security-only updates](https://docs.github.com/en/code-security/dependabot/dependabot-security-updates/configuring-dependabot-security-updates#overriding-the-default-behavior-with-a-configuration-file) (`open-pull-requests-limit: 0`) are not supported. _(coming soon)_

#### `dependabot@V1`
- [`schedule`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#scheduleinterval) is ignored, use [pipeline scheduled triggers](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/scheduled-triggers?view=azure-devops&tabs=yaml#scheduled-triggers) instead.
- [`directories`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#directories) are only supported if task input `useUpdateScriptVNext: true` is set.
- [`groups`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#groups) are only supported if task input `useUpdateScriptVNext: true` is set.
- [`ignore`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#ignore) may not behave to official specifications unless task input `useUpdateScriptVNext: true` is set. If you are having issues, search for related issues such as <https://github.com/tinglesoftware/dependabot-azure-devops/pull/582> before creating a new issue.
- [`assignees`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#assignees) and [`reviewers`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#reviewers) must be a list of user guids or email addresses; group/team names are not supported.
- Private feed/registry authentication may not work with all package ecyosystems. Support is _slightly_ improved when task input `useUpdateScriptVNext: true` is set, but not still not fully supported. See [problems with authentication](https://github.com/tinglesoftware/dependabot-azure-devops/discussions/1317) for more.

### Updater Docker image
- `DEPENDABOT_ASSIGNEES` and `DEPENDABOT_REVIEWERS` must be a list of user guids; email addresses and group/team names are not supported.
- Private feed/registry authentication may not work with all package ecyosystems. See [problems with authentication](https://github.com/tinglesoftware/dependabot-azure-devops/discussions/1317) for more.

### Server

- [`directories`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#directories) are not supported.
- [`groups`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#groups) are not supported.
- [`assignees`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#assignees) and [`reviewers`](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#reviewers) must be a list of user guids; email addresses and group/team names are not supported.
- Private feed/registry authentication may not work with all package ecyosystems. See [problems with authentication](https://github.com/tinglesoftware/dependabot-azure-devops/discussions/1317) for more.

## Migration Guide
- [Extension Task V1 → V2](./docs/migrations/v1-to-v2.md)

## Contributing

:wave: Want to give us feedback on Dependabot for Azure DevOps, or contribute to it? That's great - thank you so much!

### Reporting issues and feature requests

Please leave all issues, bugs, and feature requests on the [issues page](https://github.com/tinglesoftware/dependabot-azure-devops/issues). We'll respond ASAP!
Use the [discussions page](https://github.com/tinglesoftware/dependabot-azure-devops/discussions) for all other questions and comments.

### Submitting pull requests

Please refer to the [contributing guidelines](./CONTRIBUTING.MD) for more information on how to get started.

## Acknowledgements

The work in this repository is based on inspired and occasionally guided by some predecessors in the same area:

1. Official Script support: [code](https://github.com/dependabot/dependabot-script)
2. Andrew Craven's work: [blog](https://medium.com/@acraven/updating-dependencies-in-azure-devops-repos-773cbbb6029d), [code](https://github.com/acraven/azure-dependabot)
3. Chris' work: [code](https://github.com/chris5287/dependabot-for-azuredevops)
4. andrcun's work on GitLab: [code](https://gitlab.com/dependabot-gitlab/dependabot)
5. WeWork's work for GitLab: [code](https://github.com/wemake-services/kira-dependencies)
