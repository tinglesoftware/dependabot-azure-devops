
# Table of Contents

- [Using the extension](#using-the-extension)
- [Troubleshooting issues](#troubleshooting-issues)
- [Development guide](#development-guide)
   * [Getting the development environment ready](#getting-the-development-environment-ready)
   * [Building the extension](#building-the-extension)
   * [Installing the extension](#installing-the-extension)
   * [Running the task locally](#running-the-task-locally)
   * [Running the unit tests](#running-the-unit-tests)
- [Architecture](#architecture)
   * [dependabot@2 versioned update process diagram](#dependabot2-versioned-update-process-diagram)
   * [dependabot@2 hsecurity-only update process diagram](#dependabot2-security-only-update-process-diagram)

# Using the extension

Refer to the extension [README.md](../extension/README.md).

# Troubleshooting issues

Dependabot will log more diagnostic information when [verbose logs are enabled](https://learn.microsoft.com/en-us/azure/devops/pipelines/troubleshooting/review-logs?view=azure-devops&tabs=windows-agent#configure-verbose-logs); i.e. `System.Debug` variable is set to `true`.

When verbose logs are enable, Dependabot will also generate a [Flame Graph performance metrics report](https://www.brendangregg.com/flamegraphs.html), which can be viewed by [downloading the pipeline logs](https://learn.microsoft.com/en-us/azure/devops/pipelines/troubleshooting/review-logs?view=azure-devops&tabs=windows-agent#view-and-download-logs), then locating the corresponding HTML report file in the `Job` folder. To understand how to read Flame Graph reports, see: https://www.brendangregg.com/flamegraphs.html#summary

> [!WARNING]
> When sharing pipeline logs, please be aware that the **task log contains potentionally sensitive information** such as your DevOps organisation name, project names, repository names, private package feeds URLs, list of used dependency names/versions, and the contents of any dependency files that are updated (e.g. `package.json`, `*.csproj`, etc). The Flame Graph report does **not** contain any sensitive information about your DevOps environment.

> [!TIP]
> To mask environment secrets from the task log, set the `System.Secrets` variable to `true` in your pipeline.

# Development guide

## Getting the development environment ready

Install [Node.js](https://docs.docker.com/engine/install/) (18+), [Go](https://go.dev/doc/install) (1.22+), and [Docker](https://docs.docker.com/engine/install/) (with Linux containers); Install project dependencies using NPM:

```bash
cd extension
npm install
```

## Building the extension

```bash
cd extension
npm run build
```

To then generate the a Azure DevOps `.vsix` extension package for testing, you'll first need to [create a publisher account](https://learn.microsoft.com/en-us/azure/devops/extend/publish/overview?view=azure-devops#create-a-publisher) for the [Visual Studio Marketplace Publishing Portal](https://marketplace.visualstudio.com/manage/createpublisher?managePageRedirect=true). After this, use `npm run package` to build the package, with an override for your publisher ID:

```bash
npm run package -- --overrides-file overrides.local.json --rev-version --publisher your-publisher-id-here
```

## Installing the extension

To test the extension in a Azure DevOps organisation:
1. [Build the extension `.vsix` package](#building-the-extension)
1. [Publish the extension to your publisher account](https://learn.microsoft.com/en-us/azure/devops/extend/publish/overview?view=azure-devops#publish-your-extension)
1. [Share the extension with the organisation](https://learn.microsoft.com/en-us/azure/devops/extend/publish/overview?view=azure-devops#share-your-extension).

## Running the task locally
To run the latest task version:
```bash
npm start
```

To run a specific task version:
```bash
npm run start:V1 # runs dependabot@1 task
npm run start:V2 # runs dependabot@2 task
```

## Running the unit tests

```bash
cd extension
npm test
```

# Architecture

## dependabot2 versioned update process diagram
High-level sequence diagram illustrating how the `dependabot@2` task performs versioned updates using [dependabot-cli](https://github.com/dependabot/cli). For more technical details, see [how dependabot-cli works](https://github.com/dependabot/cli?tab=readme-ov-file#how-it-works).

```mermaid
 sequenceDiagram
    participant ext as Dependabot DevOps Extension
    participant agent as DevOps Pipeline Agent
    participant devops as DevOps API
    participant cli as Dependabot CLI
    participant core as Dependabot Updater
    participant feed as Package Feed

    ext->>ext: Read and parse `dependabot.yml`
    ext->>ext: Write `job.yaml`
    ext->>agent: Download dependabot-cli from github
    ext->>+cli: Execute `dependabot update -f job.yaml -o update-scenario.yaml`
    cli->>+core: Run update for `job.yaml` with proxy and dependabot-updater docker containers
    core->>devops: Fetch source files from repository
    core->>core: Discover dependencies
    loop for each dependency
        core->>feed: Fetch latest version
        core->>core: Update dependency files
    end
    core-->>-cli: Report outputs
    cli->>cli: Write outputs to `update-sceario.yaml`
    cli-->>-ext: Update completed

    ext->>ext: Read and parse `update-sceario.yaml`
    loop for each output
      alt when output is "create_pull_request"
        ext->>devops: Create pull request source branch
        ext->>devops: Push commit to source branch
        ext->>devops: Create pull request
        ext->>devops: Set auto-approve
        ext->>devops: Set auto-complete
      end
      alt when output is "update_pull_request"
        ext->>devops: Push commit to pull request
        ext->>devops: Update pull request description
        ext->>devops: Set auto-approve
        ext->>devops: Set auto-complete
      end
      alt when output is "close_pull_request"
        ext->>devops: Create comment thread on pull request with close reason
        ext->>devops: Abandon pull request
        ext->>devops: Delete source branch
      end
    end

```

## dependabot2 security-only update process diagram
High-level sequence diagram illustrating how the `dependabot@2` task performs security-only updates using [dependabot-cli](https://github.com/dependabot/cli).

```mermaid
 sequenceDiagram
    participant ext as TaskV2
    participant cli as Dependabot CLI
    participant gha as GitHub Advisory Database

    ext->>ext: Write `list-dependencies-job.yml`
    Note right of ext: The job file contains `ignore: [ 'dependency-name': '*' ]`.<br>This will make Dependabot to discover all dependencies, but not update anything.<br>We can then extract the dependency list from the "depenedency_list" output.
    ext->>+cli: Execute `dependabot update -f list-dependencies-job.yml -o output.yml`
    cli->>cli: Run update job
    cli->>cli: Write `output.yaml`
    cli-->>-ext: Update completed

    ext->>ext: Read and parse `output.yaml`, extract "dependency_list"
    loop for each dependency
      ext->>gha: Check security advisories for dependency
    end
    ext->>ext: Filter dependency list to only ones containing security advisories
    ext->>ext: Write `security-only-update-job.yml`
    Note right of ext: The job file contains the list of `dependency-names` and `security-advisories`.<br>This will make Dependanbot only update the dependencies named in the job file.
    ext->>+cli: Execute `dependabot update -f security-only-update-job-job.yml -o output.yml`
    cli->>cli: Run update job
    cli->>cli: Write `output.yaml`
    cli-->>-ext: Update completed
    ext->>ext: Read and parse `output.yaml`
    Note right of ext: Normal update logic resumes from this point.<br/>Outputs are parsed, pull requests are created/updated/closed based on the outputs
```