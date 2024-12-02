import { debug, error, setResult, TaskResult, warning, which } from 'azure-pipelines-task-lib/task';
import { AzureDevOpsWebApiClient } from './utils/azure-devops/AzureDevOpsWebApiClient';
import { section, setSecrets } from './utils/azure-devops/formattingCommands';
import { DependabotCli } from './utils/dependabot-cli/DependabotCli';
import { DependabotJobBuilder } from './utils/dependabot-cli/DependabotJobBuilder';
import {
  DependabotOutputProcessor,
  parsePullRequestProperties,
} from './utils/dependabot-cli/DependabotOutputProcessor';
import { IDependabotUpdateOperationResult } from './utils/dependabot-cli/interfaces/IDependabotUpdateOperationResult';
import { IDependabotUpdate } from './utils/dependabot/interfaces/IDependabotConfig';
import parseDependabotConfigFile from './utils/dependabot/parseConfigFile';
import parseTaskInputConfiguration from './utils/getSharedVariables';
import { GitHubGraphClient } from './utils/github/GitHubGraphClient';
import { IPackage } from './utils/github/IPackage';
import { ISecurityVulnerability } from './utils/github/ISecurityVulnerability';
import { getGhsaPackageEcosystemFromDependabotPackageEcosystem } from './utils/github/PackageEcosystem';

async function run() {
  let dependabot: DependabotCli = undefined;
  let failedTasks: number = 0;
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
    const prAuthorClient = new AzureDevOpsWebApiClient(
      taskInputs.organizationUrl.toString(),
      taskInputs.systemAccessToken,
      taskInputs.debug,
    );
    const prApproverClient = taskInputs.autoApprove
      ? new AzureDevOpsWebApiClient(
          taskInputs.organizationUrl.toString(),
          taskInputs.autoApproveUserToken || taskInputs.systemAccessToken,
          taskInputs.debug,
        )
      : null;

    // Fetch the active pull requests created by the author user
    const existingBranchNames = await prAuthorClient.getBranchNames(taskInputs.project, taskInputs.repository);
    const existingPullRequests = await prAuthorClient.getActivePullRequestProperties(
      taskInputs.project,
      taskInputs.repository,
      await prAuthorClient.getUserId(),
    );

    // Initialise the Dependabot updater
    dependabot = new DependabotCli(
      DependabotCli.CLI_IMAGE_LATEST, // TODO: Add config for this?
      new DependabotOutputProcessor(
        taskInputs,
        prAuthorClient,
        prApproverClient,
        existingBranchNames,
        existingPullRequests,
        taskInputs.debug,
      ),
      taskInputs.debug,
    );

    const dependabotUpdaterOptions = {
      sourceProvider: 'azure',
      azureDevOpsAccessToken: taskInputs.systemAccessToken,
      gitHubAccessToken: taskInputs.githubAccessToken,
      collectorImage: undefined, // TODO: Add config for this?
      collectorConfigPath: undefined, // TODO: Add config for this?
      proxyImage: undefined, // TODO: Add config for this?
      updaterImage: undefined, // TODO: Add config for this?
      timeoutDuration: undefined, // TODO: Add config for this?
      flamegraph: taskInputs.debug,
    };

    // If update identifiers are specified, select them; otherwise handle all
    let updates: IDependabotUpdate[] = [];
    const targetIds = taskInputs.targetUpdateIds;
    if (targetIds && targetIds.length > 0) {
      for (const id of targetIds) {
        updates.push(dependabotConfig.updates[id]);
      }
    } else {
      updates = dependabotConfig.updates;
    }

    // Loop through the [targeted] update blocks in dependabot.yaml and perform updates
    for (const update of updates) {
      const updateId = updates.indexOf(update).toString();
      const packageEcosystem = update['package-ecosystem'];

      // Parse the Dependabot metadata for the existing pull requests that are related to this update
      // Dependabot will use this to determine if we need to create new pull requests or update/close existing ones
      const existingPullRequestsForPackageEcosystem = parsePullRequestProperties(
        existingPullRequests,
        packageEcosystem,
      );
      const existingPullRequestDependenciesForPackageEcosystem = Object.entries(
        existingPullRequestsForPackageEcosystem,
      ).map(([id, deps]) => deps);

      // If this is a security-only update (i.e. 'open-pull-requests-limit: 0'), then we first need to discover the dependencies
      // that need updating and check each one for vulnerabilities. This is because Dependabot requires the list of vulnerable dependencies
      // to be supplied in the job definition of security-only update job, it will not automatically discover them like a versioned update does.
      // https://docs.github.com/en/code-security/dependabot/dependabot-security-updates/configuring-dependabot-security-updates#overriding-the-default-behavior-with-a-configuration-file
      let securityVulnerabilities: ISecurityVulnerability[] = [];
      let dependencyNamesToUpdate: string[] = [];
      const securityUpdatesOnly = update['open-pull-requests-limit'] === 0;
      if (securityUpdatesOnly) {
        // Run an update job to discover all dependencies
        const discoveredDependencyListOutputs = await dependabot.update(
          DependabotJobBuilder.listAllDependenciesJob(taskInputs, updateId, update, dependabotConfig.registries),
          dependabotUpdaterOptions,
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
            getGhsaPackageEcosystemFromDependabotPackageEcosystem(packageEcosystem),
            packagesToCheckForVulnerabilities || [],
          );

          // Only update dependencies that have vulnerabilities
          dependencyNamesToUpdate = Array.from(new Set(securityVulnerabilities.map((v) => v.package.name)));
          console.info(
            `Detected ${securityVulnerabilities.length} vulnerabilities affecting ${dependencyNamesToUpdate.length} dependencies`,
          );
          console.log(dependencyNamesToUpdate);
        } else {
          console.info('No vulnerabilities detected in any dependencies');
        }
      }

      // Run an update job for "all dependencies"; this will create new pull requests for dependencies that need updating
      const openPullRequestsLimit = update['open-pull-requests-limit'];
      const openPullRequestsCount = Object.entries(existingPullRequestsForPackageEcosystem).length;
      const hasReachedOpenPullRequestLimit =
        openPullRequestsLimit > 0 && openPullRequestsCount >= openPullRequestsLimit;
      if (!hasReachedOpenPullRequestLimit) {
        const dependenciesHaveVulnerabilities = dependencyNamesToUpdate.length && securityVulnerabilities.length;
        if (!securityUpdatesOnly || dependenciesHaveVulnerabilities) {
          failedTasks += handleUpdateOperationResults(
            await dependabot.update(
              DependabotJobBuilder.updateAllDependenciesJob(
                taskInputs,
                updateId,
                update,
                dependabotConfig.registries,
                dependencyNamesToUpdate,
                existingPullRequestDependenciesForPackageEcosystem,
                securityVulnerabilities,
              ),
              dependabotUpdaterOptions,
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
      const numberOfPullRequestsToUpdate = Object.keys(existingPullRequestsForPackageEcosystem).length;
      if (numberOfPullRequestsToUpdate > 0) {
        if (!taskInputs.skipPullRequests) {
          for (const pullRequestId in existingPullRequestsForPackageEcosystem) {
            failedTasks += handleUpdateOperationResults(
              await dependabot.update(
                DependabotJobBuilder.updatePullRequestJob(
                  taskInputs,
                  pullRequestId,
                  update,
                  dependabotConfig.registries,
                  existingPullRequestDependenciesForPackageEcosystem,
                  existingPullRequestsForPackageEcosystem[pullRequestId],
                ),
                dependabotUpdaterOptions,
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

    setResult(
      failedTasks ? TaskResult.Failed : TaskResult.Succeeded,
      failedTasks
        ? `${failedTasks} update tasks(s) failed, check logs for more information`
        : `All update tasks completed successfully`,
    );
  } catch (e) {
    setResult(TaskResult.Failed, e?.message);
    exception(e);
  } finally {
    dependabot?.cleanup();
  }
}

/**
 * Handles the results of an update operation.
 * @param outputs The processed outputs of the update operation.
 * @returns The number of failed tasks (i.e. outputs that could not be processed).
 * @remarks
 * If the update operation completed with all outputs processed successfully, it will return 0.
 * If the update operation completed with no outputs, it will return 1.
 * If the update operation completed with some outputs processed unsuccessfully, it will return the number of failed outputs.
 */
function handleUpdateOperationResults(outputs: IDependabotUpdateOperationResult[] | undefined) {
  let failedTasks = 0; // assume success, initially
  if (outputs) {
    // The update operation completed, but some output tasks may have failed
    const failedUpdateTasks = outputs.filter((u) => !u.success);
    if (failedUpdateTasks.length > 0) {
      // At least one output task failed to process
      failedUpdateTasks.forEach((u) => exception(u.error));
      failedTasks += failedUpdateTasks.length;
    }
  } else {
    // The update operation critically failed, it produced no output
    failedTasks++;
  }

  return failedTasks;
}

function exception(e: Error) {
  if (e) {
    error(`An unhandled exception occurred: ${e}`);
    console.debug(e); // Dump the stack trace to help with debugging
  }
}

run();
