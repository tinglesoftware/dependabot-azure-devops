
> [!WARNING]
> **This is a work in progress;** `dependabot@V2` is still under development and this document may change without notice up until general availability (GA).

# Table of Contents
- [Summary of changes V1 → V2](#summary-of-changes-v1-v2)
   * [Resolving private feed/registry authentication issues](#resolving-private-feedregistry-authentication-issues)
- [Breaking changes V1 → V2](#breaking-changes-v1-v2)
- [Steps to migrate V1 → V2](#steps-to-migrate-v1-v2)
- [Todo before general availability](#todo-before-general-availability)

# Summary of changes V1 → V2
V2 is a complete re-write of the Dependabot task; It aims to:

- Resolve the [numerous private feed/registry authentication issues](https://github.com/tinglesoftware/dependabot-azure-devops/discussions/1317) that currently exist in V1;
- More closely align the update logic with the GitHub-hosted Dependabot service;

The task now uses [Dependabot CLI](https://github.com/dependabot/cli) to perform dependency updates, which is the _[current]_ recommended approach for running Dependabot.

See [extension task architecture](../extension.md#architecture) for more technical details on changes to the update process.

# Breaking changes V1 → V2

### New pipeline agent requirements; "Go" must be installed
Dependabot CLI requires [Go](https://go.dev/doc/install) (1.22+) and [Docker](https://docs.docker.com/get-started/get-docker/) (with Linux containers).
If you use [Microsoft-hosted agents](https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=azure-devops&tabs=yaml#software), we recommend using the [ubuntu-latest](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md) image, which meets all task requirements.
For self-hosted agents, you will need to install Go 1.22+.

### Security-only updates and "fixed vulnerabilities" are not implemented (yet)
Using configuration `open-pull-requests-limit: 0` will cause a "not implemented" error. This is [current limitation of V2](../../README.md#unsupported-features-and-configurations). A solution is still under development and is expected to be resolved before general availability.
See: https://github.com/dependabot/cli/issues/360 for more technical details.

### Task Input `updaterOptions` has been renamed to `experiments`
Renamed to match Dependabot Core/CLI terminology. The input value remains unchanged. See [configuring experiments](../../README.md#configuring-experiments) for more details.

### Task Input `failOnException` has been removed
Due to the design of Dependabot CLI, the update process can no longer be interrupted once the update has started. Because of this, the update will now continue on error and summarise all error at the end of the update process.

### Task Input `excludeRequirementsToUnlock` has been removed
This was a customisation/workaround specific to the V1 update script that can no longer be implemented with Dependabot CLI as it is not an official configuration option.

### Task Input `dockerImageTag` has been removed
This is no longer required as the [custom] [Dependabot Updater image](../updater.md) is no longer used.

### Task Input `extraEnvironmentVariables` has been removed
Due to the containerised design of Dependabot CLI, environment variables can no longer be passed from the task to the updater process. All Dependabot config must now set via `dependabot.yaml` or as task inputs. The following old environment variables have been converted to task inputs:

| Environment Variable | New Task Input |
|--|--|
|DEPENDABOT_AUTHOR_EMAIL|authorEmail|
|DEPENDABOT_AUTHOR_NAME|authorName|


## Todo before general availability
Before removing the preview flag from V2 `task.json`, we need to:
 - [x] Open an issue in Dependabot-CLI, enquire how security-advisories are expected to be provided **before** knowing the list of dependencies. (https://github.com/dependabot/cli/issues/360)
 - [ ] Convert GitHub security advisory client in `vulnerabilities.rb` to TypeScript code
 - [ ] Implement `security-advisories` config once the answer the above is known
 - [x] Review `task.json`, add documentation for new V2 inputs
 - [x] Update `\docs\extension.md` with V2 docs
 - [x] Update `\extension\README.MD` with V2 docs
 - [x] Update `\README.MD` with V2 docs
 - [ ] Do a general code tidy-up pass (check all "TODO" comments)
 - [ ] Add unit tests for V2 utils scripts
 - [ ] Investigate https://zod.dev/