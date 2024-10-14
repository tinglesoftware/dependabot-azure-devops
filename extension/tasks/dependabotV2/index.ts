import { debug, error, setResult, setSecret, TaskResult, warning, which } from 'azure-pipelines-task-lib/task';
import { AzureDevOpsWebApiClient } from './utils/azure-devops/AzureDevOpsWebApiClient';
import { DependabotCli } from './utils/dependabot-cli/DependabotCli';
import { DependabotJobBuilder } from './utils/dependabot-cli/DependabotJobBuilder';
import {
  DependabotOutputProcessor,
  parseProjectDependencyListProperty,
  parsePullRequestProperties,
} from './utils/dependabot-cli/DependabotOutputProcessor';
import { IDependabotUpdateOperationResult } from './utils/dependabot-cli/interfaces/IDependabotUpdateOperationResult';
import { IDependabotUpdate } from './utils/dependabot/interfaces/IDependabotConfig';
import parseDependabotConfigFile from './utils/dependabot/parseConfigFile';
import parseTaskInputConfiguration from './utils/getSharedVariables';

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
      taskInputs.projectId,
      taskInputs.project,
      taskInputs.repository,
      taskInputs.githubAccessToken,
      taskInputs.systemAccessUser,
      taskInputs.systemAccessToken,
      taskInputs.authorEmail,
      taskInputs.authorName,
      taskInputs.autoApproveUserToken,
    );

    // Parse dependabot.yaml configuration file
    const dependabotConfig = await parseDependabotConfigFile(taskInputs);
    if (!dependabotConfig) {
      throw new Error('Failed to parse dependabot.yaml configuration file from the target repository');
    }

    // Initialise the DevOps API clients
    // There are two clients; one for authoring pull requests and one for auto-approving pull requests (if configured)
    const prAuthorClient = new AzureDevOpsWebApiClient(
      taskInputs.organizationUrl.toString(),
      taskInputs.systemAccessToken,
    );
    const prApproverClient = taskInputs.autoApprove
      ? new AzureDevOpsWebApiClient(
          taskInputs.organizationUrl.toString(),
          taskInputs.autoApproveUserToken || taskInputs.systemAccessToken,
        )
      : null;

    // Fetch the active pull requests created by the author user
    const prAuthorActivePullRequests = await prAuthorClient.getActivePullRequestProperties(
      taskInputs.project,
      taskInputs.repository,
      await prAuthorClient.getUserId(),
    );

    // Initialise the Dependabot updater
    dependabot = new DependabotCli(
      DependabotCli.CLI_IMAGE_LATEST, // TODO: Add config for this?
      new DependabotOutputProcessor(taskInputs, prAuthorClient, prApproverClient, prAuthorActivePullRequests),
      taskInputs.debug,
    );

    const dependabotUpdaterOptions = {
      sourceProvider: 'azure',
      sourceLocalPath: taskInputs.repositorySourcePath,
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

      // Parse the last dependency list snapshot (if any) from the project properties.
      // This is required when doing a security-only update as dependabot requires the list of vulnerable dependencies to be updated.
      // Automatic discovery of vulnerable dependencies during a security-only update is not currently supported by dependabot-updater.
      const dependencyList = parseProjectDependencyListProperty(
        await prAuthorClient.getProjectProperties(taskInputs.projectId),
        taskInputs.repository,
        update['package-ecosystem'],
      );

      // Parse the Dependabot metadata for the existing pull requests that are related to this update
      // Dependabot will use this to determine if we need to create new pull requests or update/close existing ones
      const existingPullRequests = parsePullRequestProperties(prAuthorActivePullRequests, update['package-ecosystem']);
      const existingPullRequestDependencies = Object.entries(existingPullRequests).map(([id, deps]) => deps);

      // Run an update job for "all dependencies"; this will create new pull requests for dependencies that need updating
      failedTasks += handleUpdateOperationResults(
        await dependabot.update(
          DependabotJobBuilder.newUpdateAllJob(
            taskInputs,
            updateId,
            update,
            dependabotConfig.registries,
            dependencyList?.['dependencies'],
            existingPullRequestDependencies,
          ),
          dependabotUpdaterOptions,
        ),
      );

      // If there are existing pull requests, run an update job for each one; this will resolve merge conflicts and close pull requests that are no longer needed
      const numberOfPullRequestsToUpdate = Object.keys(existingPullRequests).length;
      if (numberOfPullRequestsToUpdate > 0) {
        if (!taskInputs.skipPullRequests) {
          for (const pullRequestId in existingPullRequests) {
            failedTasks += handleUpdateOperationResults(
              await dependabot.update(
                DependabotJobBuilder.newUpdatePullRequestJob(
                  taskInputs,
                  pullRequestId,
                  update,
                  dependabotConfig.registries,
                  existingPullRequestDependencies,
                  existingPullRequests[pullRequestId],
                ),
                dependabotUpdaterOptions,
              ),
            );
          }
        } else {
          warning(
            `Skipping update of ${numberOfPullRequestsToUpdate} existing pull request(s) as 'skipPullRequests' is set to 'true'`,
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
 * Masks the supplied values in the task log output.
 * @param args
 */
function setSecrets(...args: string[]) {
  for (const arg of args.filter((a) => a && a?.toLowerCase() !== 'dependabot')) {
    // Mask the value and the uri encoded value. This is required to ensure that API and package feed url don't expose the value.
    // e.g. "Contoso Ltd" would appear as "Contoso%20Ltd" unless the uri encoded value was set as a secret.
    setSecret(arg);
    setSecret(encodeURIComponent(arg));
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
