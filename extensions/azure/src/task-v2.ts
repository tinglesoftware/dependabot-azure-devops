import { debug, error, setResult, setVariable, TaskResult, warning, which } from 'azure-pipelines-task-lib/task';
import { existsSync } from 'fs';
import { readFile } from 'fs/promises';

import {
  DependabotJobBuilder,
  mapPackageEcosystemToPackageManager,
  type DependabotConfig,
  type DependabotOperationResult,
  type DependabotUpdate,
} from 'paklo/dependabot';
import {
  filterVulnerabilities,
  getGhsaPackageEcosystemFromDependabotPackageManager,
  GitHubGraphClient,
  SecurityVulnerabilitySchema,
  type Package,
  type SecurityVulnerability,
} from 'paklo/github';
import { AzureDevOpsWebApiClient } from './azure-devops/client';
import { normalizeBranchName, section, setSecrets } from './azure-devops/formatting';
import { DEVOPS_PR_PROPERTY_MICROSOFT_GIT_SOURCE_REF_NAME, type IPullRequestProperties } from './azure-devops/models';
import { DependabotCli, type DependabotCliOptions } from './dependabot/cli';
import { getDependabotConfig } from './dependabot/get-config';
import { DependabotOutputProcessor, parsePullRequestProperties } from './dependabot/output-processor';
import parseTaskInputConfiguration, { type ISharedVariables } from './utils/shared-variables';

async function run() {
  let dependabotCli: DependabotCli | undefined;
  try {
    // Check if required tools are installed
    debug('Checking for `docker` install...');
    which('docker', true);
    debug('Checking for `go` install...');
    which('go', true);

    // Parse task input configuration
    const taskInputs = parseTaskInputConfiguration();
    if (!taskInputs) {
      throw new Error('Failed to parse task input configuration');
    }

    // Mask environment, organisation, and project specific variables from the logs.
    // Most user's environments are private and they're less likely to share diagnostic info when it exposes information about their environment or organisation.
    // Although not exhaustive, this will mask the most common information that could be used to identify the user's environment.
    if (taskInputs.secrets) {
      setSecrets(
        taskInputs.url.hostname,
        taskInputs.url.project,
        taskInputs.url.repository,
        taskInputs.githubAccessToken,
        taskInputs.systemAccessUser,
        taskInputs.systemAccessToken,
        taskInputs.autoApproveUserToken,
        taskInputs.authorEmail,
      );
    }

    // Parse dependabot.yaml configuration file
    const dependabotConfig = await getDependabotConfig(taskInputs);
    if (!dependabotConfig) {
      throw new Error('Failed to parse dependabot.yaml configuration file from the target repository');
    }

    // Print a warning about the required workarounds for security-only updates, if any update is configured as such
    // TODO: If and when Dependabot supports a better way to do security-only updates, remove this.
    if (dependabotConfig.updates?.some((u) => u['open-pull-requests-limit'] === 0)) {
      warning(
        'Security-only updates incur a slight performance overhead due to limitations in Dependabot CLI. For more info, see: https://github.com/mburumaxwell/dependabot-azure-devops/blob/main/README.md#configuring-security-advisories-and-known-vulnerabilities',
      );
    }

    // Initialise the DevOps API clients
    // There are two clients; one for authoring pull requests and one for auto-approving pull requests (if configured)
    const devOpsPrAuthorClient = new AzureDevOpsWebApiClient(
      taskInputs.url.url.toString(),
      taskInputs.systemAccessToken,
      taskInputs.debug,
    );
    const devOpsPrApproverClient = taskInputs.autoApprove
      ? new AzureDevOpsWebApiClient(
          taskInputs.url.url.toString(),
          taskInputs.autoApproveUserToken || taskInputs.systemAccessToken,
          taskInputs.debug,
        )
      : undefined;

    // Fetch the active pull requests created by the author user
    const existingBranchNames = await devOpsPrAuthorClient.getBranchNames(
      taskInputs.url.project,
      taskInputs.url.repository,
    );
    const existingPullRequests = await devOpsPrAuthorClient.getActivePullRequestProperties(
      taskInputs.url.project,
      taskInputs.url.repository,
      await devOpsPrAuthorClient.getUserId(),
    );

    // Initialise the Dependabot updater
    dependabotCli = new DependabotCli(
      taskInputs.dependabotCliPackage || DependabotCli.CLI_PACKAGE_LATEST,
      new DependabotOutputProcessor(
        taskInputs,
        devOpsPrAuthorClient,
        devOpsPrApproverClient,
        existingBranchNames,
        existingPullRequests,
        taskInputs.debug,
      ),
      taskInputs.debug,
    );

    const dependabotCliUpdateOptions: DependabotCliOptions = {
      sourceProvider: 'azure',
      azureDevOpsAccessToken: taskInputs.systemAccessToken,
      gitHubAccessToken: taskInputs.githubAccessToken,
      collectorImage: undefined, // TODO: Add config for this?
      collectorConfigPath: undefined, // TODO: Add config for this?
      proxyCertPath: taskInputs.proxyCertPath,
      proxyImage: undefined, // TODO: Add config for this?
      updaterImage: taskInputs.dependabotUpdaterImage,
      timeoutDurationMinutes: undefined, // TODO: Add config for this?
      flamegraph: taskInputs.debug,
      apiUrl: taskInputs.dependabotCliApiUrl,
      apiListeningPort: taskInputs.dependabotCliApiListeningPort,
    };

    // If update identifiers are specified, select them; otherwise handle all
    let dependabotUpdatesToPerform: DependabotUpdate[] = [];
    const targetIds = taskInputs.targetUpdateIds;
    if (targetIds && targetIds.length > 0) {
      for (const id of targetIds) {
        const upd = dependabotConfig.updates[id];
        if (!upd) {
          warning(
            `
            Unable to find target update id '${id}'.
            This value should be a zero based index of the update in your config file.
            Expected range: 0-${dependabotConfig.updates.length - 1}
            `,
          );
        } else {
          dependabotUpdatesToPerform.push(upd);
        }
      }
    } else {
      dependabotUpdatesToPerform = dependabotConfig.updates;
    }

    // Abandon all pull requests where the source branch has been deleted
    await abandonPullRequestsWhereSourceRefIsDeleted(
      taskInputs,
      devOpsPrAuthorClient,
      existingBranchNames,
      existingPullRequests,
    );

    // Perform updates for each of the [targeted] update blocks in dependabot.yaml
    const {
      result: taskResult,
      message: taskResultMessage,
      prs,
    } = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotUpdatesToPerform,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    setResult(taskResult, taskResultMessage);

    setVariable(
      'affectedPrs', // name
      prs.join(','), // val
      false, // secret
      true, // isOutput
    );
  } catch (e) {
    const err = e as Error;
    setResult(TaskResult.Failed, err.message);
    exception(err);
  } finally {
    dependabotCli?.cleanup();
  }
}

/**
 * Abandon all pull requests where the source branch has been deleted.
 * @param taskInputs The shared task inputs.
 * @param devOpsPrAuthorClient The Azure DevOps API client for authoring pull requests.
 * @param existingBranchNames The names of the existing branches.
 * @param existingPullRequests The existing pull requests.
 */
export async function abandonPullRequestsWhereSourceRefIsDeleted(
  taskInputs: ISharedVariables,
  devOpsPrAuthorClient: AzureDevOpsWebApiClient,
  existingBranchNames?: string[],
  existingPullRequests?: IPullRequestProperties[],
): Promise<void> {
  if (!existingBranchNames || !existingPullRequests) {
    return;
  }

  for (const pullRequestIndex in existingPullRequests) {
    const pullRequest = existingPullRequests[pullRequestIndex]!;
    const pullRequestSourceRefName = normalizeBranchName(
      pullRequest.properties?.find((x) => x.name === DEVOPS_PR_PROPERTY_MICROSOFT_GIT_SOURCE_REF_NAME)?.value,
    );
    if (pullRequestSourceRefName && !existingBranchNames.includes(pullRequestSourceRefName)) {
      // The source branch for the pull request has been deleted; abandon the pull request (if not dry run)
      if (!taskInputs.dryRun) {
        warning(
          `Detected source branch for PR #${pullRequest.id} has been deleted; The pull request will be abandoned`,
        );
        await devOpsPrAuthorClient.abandonPullRequest({
          project: taskInputs.url.project,
          repository: taskInputs.url.repository,
          pullRequestId: pullRequest.id,
          // comment:
          //   'OK, I won't notify you again about this release, but will get in touch when a new version is available. ' +
          //   'If you'd rather skip all updates until the next major or minor version, add an ' +
          //   '[`ignore` condition](https://docs.github.com/en/code-security/dependabot/working-with-dependabot/dependabot-options-reference#ignore--) ' +
          //   'with the desired `update-types` to your config file.',
          comment:
            'It might be a good idea to add an ' +
            '[`ignore` condition](https://docs.github.com/en/code-security/dependabot/working-with-dependabot/dependabot-options-reference#ignore--) ' +
            'with the desired `update-types` to your config file.',
        });
      }

      // Remove the pull request from the list of existing pull requests to ensures that we don't attempt to update it later in the process.
      existingPullRequests.splice(existingPullRequests.indexOf(pullRequest), 1);
    }
  }
}

/**
 * Performs the Dependabot updates.
 * @param taskInputs The shared task inputs.
 * @param dependabotConfig The parsed Dependabot configuration.
 * @param dependabotUpdates The updates to perform.
 * @param dependabotCli The Dependabot CLI instance.
 * @param dependabotCliUpdateOptions The Dependabot updater options.
 * @param existingPullRequests The existing pull requests.
 * @returns The result of the update operation
 */
export async function performDependabotUpdatesAsync(
  taskInputs: ISharedVariables,
  dependabotConfig: DependabotConfig,
  dependabotUpdates: DependabotUpdate[],
  dependabotCli: DependabotCli,
  dependabotCliUpdateOptions: DependabotCliOptions,
  existingPullRequests: IPullRequestProperties[],
): Promise<{ result: TaskResult; message: string; prs: number[] }> {
  const successfulOperations: DependabotOperationResult[] = [];
  const failedOperations: DependabotOperationResult[] = [];
  for (const update of dependabotUpdates) {
    const updateId = dependabotUpdates.indexOf(update).toString();
    const packageEcosystem = update['package-ecosystem'];
    const packageManager = mapPackageEcosystemToPackageManager(packageEcosystem);

    // Parse the Dependabot metadata for the existing pull requests that are related to this update
    // Dependabot will use this to determine if we need to create new pull requests or update/close existing ones
    const existingPullRequestsForPackageManager = parsePullRequestProperties(existingPullRequests, packageManager);
    const existingPullRequestDependenciesForPackageManager = Object.values(existingPullRequestsForPackageManager);

    const builder = new DependabotJobBuilder({
      source: { provider: 'azure', ...taskInputs.url },
      config: dependabotConfig,
      update,
      experiments: taskInputs.experiments,
      systemAccessUser: taskInputs.systemAccessUser,
      systemAccessToken: taskInputs.systemAccessToken,
      githubToken: taskInputs.githubAccessToken,
      debug: taskInputs.debug,
    });

    // If this is a security-only update (i.e. 'open-pull-requests-limit: 0'), then we first need to discover the dependencies
    // that need updating and check each one for vulnerabilities. This is because Dependabot requires the list of vulnerable dependencies
    // to be supplied in the job definition of security-only update job, it will not automatically discover them like a versioned update does.
    // https://docs.github.com/en/code-security/dependabot/dependabot-security-updates/configuring-dependabot-security-updates#overriding-the-default-behavior-with-a-configuration-file
    let securityVulnerabilities: SecurityVulnerability[] = [];
    let dependencyNamesToUpdate: string[] = [];
    const securityUpdatesOnly = update['open-pull-requests-limit'] === 0;
    if (securityUpdatesOnly) {
      // Run an update job to discover all dependencies
      const job = builder.forDependenciesList({
        id: `discover-${updateId}-${update['package-ecosystem']}-dependency-list`,
      });
      const outputs = await dependabotCli.update(job, dependabotCliUpdateOptions);

      // Get the list of vulnerabilities that apply to the discovered dependencies
      section(`Dependency vulnerability check`);
      const packagesToCheckForVulnerabilities: Package[] | undefined = outputs
        ?.map((o) => o.output)
        .find((o) => o?.type == 'update_dependency_list')
        ?.data.dependencies?.map((d) => ({ name: d.name, version: d.version }));
      if (packagesToCheckForVulnerabilities?.length) {
        console.info(
          `Detected ${packagesToCheckForVulnerabilities.length} dependencies; Checking for vulnerabilities...`,
        );

        // parse security advisories from file (private)
        if (taskInputs.securityAdvisoriesFile) {
          const filePath = taskInputs.securityAdvisoriesFile;
          if (existsSync(filePath)) {
            const fileContents = await readFile(filePath, 'utf-8');
            securityVulnerabilities = await SecurityVulnerabilitySchema.array().parseAsync(JSON.parse(fileContents));
          } else {
            console.info(`Private security advisories file '${filePath}' does not exist`);
          }
        }

        if (taskInputs.githubAccessToken) {
          const ghsaClient = new GitHubGraphClient(taskInputs.githubAccessToken);
          const githubVulnerabilities = await ghsaClient.getSecurityVulnerabilitiesAsync(
            getGhsaPackageEcosystemFromDependabotPackageManager(packageManager),
            packagesToCheckForVulnerabilities || [],
          );
          securityVulnerabilities.push(...githubVulnerabilities);
        } else {
          console.info(
            'GitHub access token is not provided; Checking for vulnerabilities from GitHub is skipped. ' +
              'This is not an issue if you are using private security advisories file.',
          );
        }

        securityVulnerabilities = filterVulnerabilities(securityVulnerabilities);

        // Only update dependencies that have vulnerabilities
        dependencyNamesToUpdate = Array.from(new Set(securityVulnerabilities.map((v) => v.package.name)));
        console.info(
          `Detected ${securityVulnerabilities.length} vulnerabilities affecting ${dependencyNamesToUpdate.length} dependencies`,
        );
        if (dependencyNamesToUpdate.length) {
          console.log(dependencyNamesToUpdate);
        }
      } else {
        console.info('No vulnerabilities detected in any dependencies');
      }
    }

    // Run an update job for "all dependencies"; this will create new pull requests for dependencies that need updating
    const openPullRequestsLimit = update['open-pull-requests-limit']!;
    const openPullRequestsCount = Object.entries(existingPullRequestsForPackageManager).length;
    const hasReachedOpenPullRequestLimit = openPullRequestsLimit > 0 && openPullRequestsCount >= openPullRequestsLimit;
    if (!hasReachedOpenPullRequestLimit) {
      const dependenciesHaveVulnerabilities = dependencyNamesToUpdate.length && securityVulnerabilities.length;
      if (!securityUpdatesOnly || dependenciesHaveVulnerabilities) {
        const job = builder.forUpdate({
          id: `update-${updateId}-${update['package-ecosystem']}-${securityUpdatesOnly ? 'security-only' : 'all'}`,
          dependencyNamesToUpdate,
          existingPullRequests: existingPullRequestDependenciesForPackageManager,
          securityVulnerabilities,
        });
        const outputs = await dependabotCli.update(job, dependabotCliUpdateOptions);
        successfulOperations.push(...(outputs?.filter((u) => u.success) || []));
        failedOperations.push(...(outputs?.filter((u) => !u.success) || []));
      } else {
        console.info('Nothing to update; dependencies are not affected by any known vulnerability');
      }
    } else {
      warning(
        `Skipping update for ${packageEcosystem} packages as the open pull requests limit (${openPullRequestsLimit}) has already been reached`,
      );
    }

    // If there are existing pull requests, run an update job for each one; this will resolve merge conflicts and close pull requests that are no longer needed
    const numberOfPullRequestsToUpdate = Object.keys(existingPullRequestsForPackageManager).length;
    if (numberOfPullRequestsToUpdate > 0) {
      if (!taskInputs.dryRun) {
        for (const pullRequestId in existingPullRequestsForPackageManager) {
          const job = builder.forUpdate({
            id: `update-pr-${pullRequestId}`,
            existingPullRequests: existingPullRequestDependenciesForPackageManager,
            pullRequestToUpdate: existingPullRequestsForPackageManager[pullRequestId]!,
            securityVulnerabilities,
          });
          const outputs = await dependabotCli.update(job, dependabotCliUpdateOptions);
          successfulOperations.push(...(outputs?.filter((u) => u.success) || []));
          failedOperations.push(...(outputs?.filter((u) => !u.success) || []));
        }
      } else {
        warning(
          `Skipping update of ${numberOfPullRequestsToUpdate} existing ${packageEcosystem} package pull request(s) as 'dryRun' is set to 'true'`,
        );
      }
    }
  }

  // Collect PRs from successful operations (unique numbers only, to be clean and for mocked tests)
  const prs = [
    ...new Set(
      successfulOperations
        .map((o) => o.pr)
        .filter(Boolean)
        .map((n) => n!),
    ),
  ];

  // Determine an overall result based on the success/failure of all the update operations
  let result: TaskResult;
  let message: string = '';
  if (successfulOperations.length > 0) {
    if (failedOperations.length == 0) {
      result = TaskResult.Succeeded;
      message = 'All update tasks completed successfully';
    } else {
      result = TaskResult.SucceededWithIssues;
      message = 'Partial success; some update tasks completed with issues. Check the logs for more information';
    }
  } else if (failedOperations.length > 0) {
    result = TaskResult.Failed;
    message = 'Update tasks failed. Check the logs for more information';
  } else {
    result = TaskResult.Skipped;
  }

  return { result, message, prs };
}

function exception(e: Error) {
  if (e) {
    error(`An unhandled exception occurred: ${e}`);
    console.debug(e); // Dump the stack trace to help with debugging
  }
}

run();
