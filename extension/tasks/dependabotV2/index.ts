import { debug, error, setResult, TaskResult, warning, which } from 'azure-pipelines-task-lib/task';
import { AzureDevOpsWebApiClient } from './utils/azure-devops/AzureDevOpsWebApiClient';
import { section, setSecrets } from './utils/azure-devops/formattingCommands';
import { IPullRequestProperties } from './utils/azure-devops/interfaces/IPullRequest';
import { DependabotCli } from './utils/dependabot-cli/DependabotCli';
import { DependabotJobBuilder, mapPackageEcosystemToPackageManager } from './utils/dependabot-cli/DependabotJobBuilder';
import {
  DependabotOutputProcessor,
  parsePullRequestProperties,
} from './utils/dependabot-cli/DependabotOutputProcessor';
import { IDependabotUpdateOperationResult } from './utils/dependabot-cli/interfaces/IDependabotUpdateOperationResult';
import { IDependabotConfig, IDependabotUpdate } from './utils/dependabot/interfaces/IDependabotConfig';
import parseDependabotConfigFile from './utils/dependabot/parseConfigFile';
import parseTaskInputConfiguration, { ISharedVariables } from './utils/getSharedVariables';
import { GitHubGraphClient } from './utils/github/GitHubGraphClient';
import { IPackage } from './utils/github/IPackage';
import { ISecurityVulnerability } from './utils/github/ISecurityVulnerability';
import { getGhsaPackageEcosystemFromDependabotPackageManager } from './utils/github/PackageEcosystem';

async function run() {
  let dependabotCli: DependabotCli = undefined;
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
    setSecrets(
      taskInputs.hostname,
      taskInputs.virtualDirectory,
      taskInputs.organization,
      taskInputs.project,
      taskInputs.repository,
      taskInputs.githubAccessToken,
      taskInputs.systemAccessUser,
      taskInputs.systemAccessToken,
      taskInputs.autoApproveUserToken,
      taskInputs.authorEmail,
    );

    // Parse dependabot.yaml configuration file
    const dependabotConfig = await parseDependabotConfigFile(taskInputs);
    if (!dependabotConfig) {
      throw new Error('Failed to parse dependabot.yaml configuration file from the target repository');
    }

    // Print a warning about the required workarounds for security-only updates, if any update is configured as such
    // TODO: If and when Dependabot supports a better way to do security-only updates, remove this.
    if (dependabotConfig.updates?.some((u) => u['open-pull-requests-limit'] === 0)) {
      warning(
        'Security-only updates incur a slight performance overhead due to limitations in Dependabot CLI. For more info, see: https://github.com/tinglesoftware/dependabot-azure-devops/blob/main/docs/migrations/v1-to-v2.md#security-only-updates',
      );
    }

    // Initialise the DevOps API clients
    // There are two clients; one for authoring pull requests and one for auto-approving pull requests (if configured)
    const devOpsPrAuthorClient = new AzureDevOpsWebApiClient(
      taskInputs.organizationUrl.toString(),
      taskInputs.systemAccessToken,
      taskInputs.debug,
    );
    const devOpsPrApproverClient = taskInputs.autoApprove
      ? new AzureDevOpsWebApiClient(
          taskInputs.organizationUrl.toString(),
          taskInputs.autoApproveUserToken || taskInputs.systemAccessToken,
          taskInputs.debug,
        )
      : null;

    // Fetch the active pull requests created by the author user
    const existingBranchNames = await devOpsPrAuthorClient.getBranchNames(taskInputs.project, taskInputs.repository);
    const existingPullRequests = await devOpsPrAuthorClient.getActivePullRequestProperties(
      taskInputs.project,
      taskInputs.repository,
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

    const dependabotCliUpdateOptions = {
      sourceProvider: 'azure',
      azureDevOpsAccessToken: taskInputs.systemAccessToken,
      gitHubAccessToken: taskInputs.githubAccessToken,
      collectorImage: undefined, // TODO: Add config for this?
      collectorConfigPath: undefined, // TODO: Add config for this?
      proxyImage: undefined, // TODO: Add config for this?
      updaterImage: taskInputs.dependabotUpdaterImage,
      timeoutDuration: undefined, // TODO: Add config for this?
      flamegraph: taskInputs.debug,
    };

    // If update identifiers are specified, select them; otherwise handle all
    let dependabotUpdatesToPerform: IDependabotUpdate[] = [];
    const targetIds = taskInputs.targetUpdateIds;
    if (targetIds && targetIds.length > 0) {
      for (const id of targetIds) {
        dependabotUpdatesToPerform.push(dependabotConfig.updates[id]);
      }
    } else {
      dependabotUpdatesToPerform = dependabotConfig.updates;
    }

    // Perform updates for each of the [targeted] update blocks in dependabot.yaml
    const failedUpdateOperations = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotUpdatesToPerform,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    setResult(
      failedUpdateOperations == 0 ? TaskResult.Succeeded : TaskResult.Failed,
      failedUpdateOperations > 0
        ? `${failedUpdateOperations} update tasks(s) failed, check logs for more information`
        : `All update tasks completed successfully`,
    );
  } catch (e) {
    setResult(TaskResult.Failed, e?.message);
    exception(e);
  } finally {
    dependabotCli?.cleanup();
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
 * @returns The number of successful and failed update operations.
 */
export async function performDependabotUpdatesAsync(
  taskInputs: ISharedVariables,
  dependabotConfig: IDependabotConfig,
  dependabotUpdates: IDependabotUpdate[],
  dependabotCli: DependabotCli,
  dependabotCliUpdateOptions: any,
  existingPullRequests: IPullRequestProperties[],
): Promise<number> {
  let failedOperations = 0;
  for (const update of dependabotUpdates) {
    const updateId = dependabotUpdates.indexOf(update).toString();
    const packageEcosystem = update['package-ecosystem'];
    const packageManager = mapPackageEcosystemToPackageManager(packageEcosystem);

    // Parse the Dependabot metadata for the existing pull requests that are related to this update
    // Dependabot will use this to determine if we need to create new pull requests or update/close existing ones
    const existingPullRequestsForPackageManager = parsePullRequestProperties(existingPullRequests, packageManager);
    const existingPullRequestDependenciesForPackageManager = Object.entries(existingPullRequestsForPackageManager).map(
      ([id, deps]) => deps,
    );

    // If this is a security-only update (i.e. 'open-pull-requests-limit: 0'), then we first need to discover the dependencies
    // that need updating and check each one for vulnerabilities. This is because Dependabot requires the list of vulnerable dependencies
    // to be supplied in the job definition of security-only update job, it will not automatically discover them like a versioned update does.
    // https://docs.github.com/en/code-security/dependabot/dependabot-security-updates/configuring-dependabot-security-updates#overriding-the-default-behavior-with-a-configuration-file
    let securityVulnerabilities: ISecurityVulnerability[] = [];
    let dependencyNamesToUpdate: string[] = [];
    const securityUpdatesOnly = update['open-pull-requests-limit'] === 0;
    if (securityUpdatesOnly) {
      // Run an update job to discover all dependencies
      const discoveredDependencyListOutputs = await dependabotCli.update(
        DependabotJobBuilder.listAllDependenciesJob(taskInputs, updateId, update, dependabotConfig.registries),
        dependabotCliUpdateOptions,
      );

      // Get the list of vulnerabilities that apply to the discovered dependencies
      section(`GHSA dependency vulnerability check`);
      const ghsaClient = new GitHubGraphClient(taskInputs.githubAccessToken);
      const packagesToCheckForVulnerabilities: IPackage[] = discoveredDependencyListOutputs
        ?.find((x) => x.output.type == 'update_dependency_list')
        ?.output?.data?.dependencies?.map((d) => ({ name: d.name, version: d.version }));
      if (packagesToCheckForVulnerabilities?.length) {
        console.info(
          `Detected ${packagesToCheckForVulnerabilities.length} dependencies; Checking for vulnerabilities...`,
        );
        securityVulnerabilities = await ghsaClient.getSecurityVulnerabilitiesAsync(
          getGhsaPackageEcosystemFromDependabotPackageManager(packageManager),
          packagesToCheckForVulnerabilities || [],
        );

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
    const openPullRequestsLimit = update['open-pull-requests-limit'];
    const openPullRequestsCount = Object.entries(existingPullRequestsForPackageManager).length;
    const hasReachedOpenPullRequestLimit = openPullRequestsLimit > 0 && openPullRequestsCount >= openPullRequestsLimit;
    if (!hasReachedOpenPullRequestLimit) {
      const dependenciesHaveVulnerabilities = dependencyNamesToUpdate.length && securityVulnerabilities.length;
      if (!securityUpdatesOnly || dependenciesHaveVulnerabilities) {
        failedOperations += handleUpdateOperationResults(
          await dependabotCli.update(
            DependabotJobBuilder.updateAllDependenciesJob(
              taskInputs,
              updateId,
              update,
              dependabotConfig.registries,
              dependencyNamesToUpdate,
              existingPullRequestDependenciesForPackageManager,
              securityVulnerabilities,
            ),
            dependabotCliUpdateOptions,
          ),
        );
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
      if (!taskInputs.skipPullRequests) {
        for (const pullRequestId in existingPullRequestsForPackageManager) {
          failedOperations += handleUpdateOperationResults(
            await dependabotCli.update(
              DependabotJobBuilder.updatePullRequestJob(
                taskInputs,
                pullRequestId,
                update,
                dependabotConfig.registries,
                existingPullRequestDependenciesForPackageManager,
                existingPullRequestsForPackageManager[pullRequestId],
                securityVulnerabilities,
              ),
              dependabotCliUpdateOptions,
            ),
          );
        }
      } else {
        warning(
          `Skipping update of ${numberOfPullRequestsToUpdate} existing ${packageEcosystem} package pull request(s) as 'skipPullRequests' is set to 'true'`,
        );
      }
    }
  }

  return failedOperations;
}

/**
 * Handles the results of an update operation.
 * @param outputs The processed outputs of the update operation.
 * @returns The number of failed tasks (i.e. outputs that could not be processed).
 * @remarks
 * If the update operation completed with all outputs processed successfully, it will return 0.
 * If the update operation completed with some outputs processed unsuccessfully, it will return the number of failed outputs.
 */
function handleUpdateOperationResults(outputs: IDependabotUpdateOperationResult[] | undefined): number {
  let failedOperations = 0; // assume success, initially
  if (outputs) {
    // The update operation completed, but some output tasks may have failed
    const failedUpdateOutputs = outputs.filter((u) => !u.success);
    if (failedUpdateOutputs.length > 0) {
      // At least one output task failed to process
      failedUpdateOutputs.forEach((u) => exception(u.error));
      failedOperations += failedUpdateOutputs.length;
    }
  } else {
    // The update operation critically failed, it produced no output
    failedOperations++;
  }

  return failedOperations;
}

function exception(e: Error) {
  if (e) {
    error(`An unhandled exception occurred: ${e}`);
    console.debug(e); // Dump the stack trace to help with debugging
  }
}

run();
