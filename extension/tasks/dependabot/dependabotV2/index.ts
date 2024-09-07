import { which, setResult, TaskResult } from "azure-pipelines-task-lib/task"
import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { DependabotCli } from './utils/dependabot-cli/DependabotCli';
import { AzureDevOpsWebApiClient } from "./utils/azure-devops/AzureDevOpsWebApiClient";
import { DependabotOutputProcessor, parsePullRequestProperties } from "./utils/dependabot-cli/DependabotOutputProcessor";
import { DependabotJobBuilder } from "./utils/dependabot-cli/DependabotJobBuilder";
import { parseConfigFile } from './utils/parseConfigFile';
import getSharedVariables from './utils/getSharedVariables';

async function run() {
  let taskSucceeded: boolean = true;
  let dependabot: DependabotCli = undefined;
  try {

    // Check if required tools are installed
    debug('Checking for `docker` install...');
    which('docker', true);
    debug('Checking for `go` install...');
    which('go', true);

    // Parse task input configuration
    const taskVariables = getSharedVariables();
    if (!taskVariables) {
      throw new Error('Failed to parse task input configuration');
    }

    // Parse dependabot.yaml configuration file
    const dependabotConfig = await parseConfigFile(taskVariables);
    if (!dependabotConfig) {
      throw new Error('Failed to parse dependabot.yaml configuration file from the target repository');
    }

    // Initialise the DevOps API clients
    // There are two clients; one for authoring pull requests and one for auto-approving pull requests (if configured)
    const prAuthorClient = new AzureDevOpsWebApiClient(taskVariables.organizationUrl.toString(), taskVariables.systemAccessToken);
    const prApproverClient = taskVariables.autoApprove ? new AzureDevOpsWebApiClient(taskVariables.organizationUrl.toString(), taskVariables.autoApproveUserToken) : null;

    // Fetch the active pull requests created by the author user
    const prAuthorActivePullRequests = await prAuthorClient.getMyActivePullRequestProperties(
      taskVariables.project, taskVariables.repository
    );

    // Initialise the Dependabot updater
    dependabot = new DependabotCli(
      DependabotCli.CLI_IMAGE_LATEST, // TODO: Add config for this?
      new DependabotOutputProcessor(taskVariables, prAuthorClient, prApproverClient, prAuthorActivePullRequests),
      taskVariables.debug
    );

    // Loop through each 'update' block in dependabot.yaml and perform updates
    dependabotConfig.updates.forEach(async (update) => {

      // Parse the Dependabot metadata for the existing pull requests that are related to this update
      // Dependabot will use this to determine if we need to create new pull requests or update/close existing ones
      const existingPullRequests = parsePullRequestProperties(prAuthorActivePullRequests, update.packageEcosystem);

      // Run an update job for "all dependencies"; this will create new pull requests for dependencies that need updating
      const allDependenciesJob = DependabotJobBuilder.newUpdateAllJob(taskVariables, update, dependabotConfig.registries, existingPullRequests);
      const allDependenciesUpdateOutputs = await dependabot.update(allDependenciesJob);
      if (!allDependenciesUpdateOutputs || allDependenciesUpdateOutputs.filter(u => !u.success).length > 0) {
        allDependenciesUpdateOutputs.filter(u => !u.success).forEach(u => exception(u.error));
        taskSucceeded = false;
      }

      // Run an update job for each existing pull request; this will resolve merge conflicts and close pull requests that are no longer needed
      if (!taskVariables.skipPullRequests) {
        for (const pr of existingPullRequests) {
          const updatePullRequestJob = DependabotJobBuilder.newUpdatePullRequestJob(taskVariables, update, dependabotConfig.registries, existingPullRequests, pr);
          const updatePullRequestOutputs = await dependabot.update(updatePullRequestJob);
          if (!updatePullRequestOutputs || updatePullRequestOutputs.filter(u => !u.success).length > 0) {
            updatePullRequestOutputs.filter(u => !u.success).forEach(u => exception(u.error));
            taskSucceeded = false;
          }
        }
      } else if (existingPullRequests.length > 0) {
        warning(`Skipping update of existing pull requests as 'skipPullRequests' is set to 'true'`);
        return;
      }

    });

    setResult(
      taskSucceeded ? TaskResult.Succeeded : TaskResult.Failed,
      taskSucceeded ? 'All update jobs completed successfully' : 'One or more update jobs failed, check logs for more information'
    );

  }
  catch (e) {
    setResult(TaskResult.Failed, e?.message);
    exception(e);
  }
  finally {
    // TODO: dependabotCli?.cleanup();
  }
}

function exception(e: Error) {
  if (e?.stack) {
    error(e.stack);
  }
}

run();
