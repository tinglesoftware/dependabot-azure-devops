import { which, setResult, TaskResult } from "azure-pipelines-task-lib/task"
import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { DependabotCli } from './utils/dependabot-cli/DependabotCli';
import { AzureDevOpsWebApiClient } from "./utils/azure-devops/AzureDevOpsWebApiClient";
import { DependabotOutputProcessor, parseDependencyListProperty, parsePullRequestProperties } from "./utils/dependabot-cli/DependabotOutputProcessor";
import { DependabotJobBuilder } from "./utils/dependabot-cli/DependabotJobBuilder";
import parseDependabotConfigFile from './utils/dependabot/parseConfigFile';
import parseTaskInputConfiguration from './utils/getSharedVariables';

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
    const taskInputs = parseTaskInputConfiguration();
    if (!taskInputs) {
      throw new Error('Failed to parse task input configuration');
    }

    // Parse dependabot.yaml configuration file
    const dependabotConfig = await parseDependabotConfigFile(taskInputs);
    if (!dependabotConfig) {
      throw new Error('Failed to parse dependabot.yaml configuration file from the target repository');
    }

    // Initialise the DevOps API clients
    // There are two clients; one for authoring pull requests and one for auto-approving pull requests (if configured)
    const prAuthorClient = new AzureDevOpsWebApiClient(taskInputs.organizationUrl.toString(), taskInputs.systemAccessToken);
    const prApproverClient = taskInputs.autoApprove ? new AzureDevOpsWebApiClient(taskInputs.organizationUrl.toString(), taskInputs.autoApproveUserToken) : null;

    // Fetch the active pull requests created by the author user
    const prAuthorActivePullRequests = await prAuthorClient.getMyActivePullRequestProperties(
      taskInputs.project, taskInputs.repository
    );

    // Initialise the Dependabot updater
    dependabot = new DependabotCli(
      DependabotCli.CLI_IMAGE_LATEST, // TODO: Add config for this?
      new DependabotOutputProcessor(taskInputs, prAuthorClient, prApproverClient, prAuthorActivePullRequests),
      taskInputs.debug
    );

    const dependabotUpdaterOptions = {
      collectorImage: undefined, // TODO: Add config for this?
      proxyImage: undefined, // TODO: Add config for this?
      updaterImage: undefined // TODO: Add config for this?
    };

    // Loop through each 'update' block in dependabot.yaml and perform updates
    await Promise.all(dependabotConfig.updates.map(async (update) => {
      
      // Parse the last dependency list snapshot (if any) from the project properties.
      // This is required when doing a security-only update as dependabot requires the list of vulnerable dependencies to be updated update.
      // Automatic discovery of vulnerable dependencies during a security-only update is not currently supported by dependabot-updater.
      const dependencyList = parseDependencyListProperty(
        await prAuthorClient.getProjectProperty(taskInputs.project, DependabotOutputProcessor.PROJECT_PROPERTY_NAME_DEPENDENCY_LIST),
        taskInputs.repository,
        update["package-ecosystem"]
      );

      // Parse the Dependabot metadata for the existing pull requests that are related to this update
      // Dependabot will use this to determine if we need to create new pull requests or update/close existing ones
      const existingPullRequests = parsePullRequestProperties(prAuthorActivePullRequests, update["package-ecosystem"]);

      // Run an update job for "all dependencies"; this will create new pull requests for dependencies that need updating
      const allDependenciesJob = DependabotJobBuilder.newUpdateAllJob(taskInputs, update, dependabotConfig.registries, dependencyList['dependencies'], existingPullRequests);
      const allDependenciesUpdateOutputs = await dependabot.update(allDependenciesJob, dependabotUpdaterOptions);
      if (!allDependenciesUpdateOutputs || allDependenciesUpdateOutputs.filter(u => !u.success).length > 0) {
        allDependenciesUpdateOutputs.filter(u => !u.success).forEach(u => exception(u.error));
        taskSucceeded = false;
      }

      // Run an update job for each existing pull request; this will resolve merge conflicts and close pull requests that are no longer needed
      if (!taskInputs.skipPullRequests) {
        for (const pr of existingPullRequests) {
          const updatePullRequestJob = DependabotJobBuilder.newUpdatePullRequestJob(taskInputs, update, dependabotConfig.registries, existingPullRequests, pr);
          const updatePullRequestOutputs = await dependabot.update(updatePullRequestJob, dependabotUpdaterOptions);
          if (!updatePullRequestOutputs || updatePullRequestOutputs.filter(u => !u.success).length > 0) {
            updatePullRequestOutputs.filter(u => !u.success).forEach(u => exception(u.error));
            taskSucceeded = false;
          }
        }
      } else if (existingPullRequests.length > 0) {
        warning(`Skipping update of existing pull requests as 'skipPullRequests' is set to 'true'`);
        return;
      }

    }));

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
    dependabot?.cleanup();
  }
}

function exception(e: Error) {
  if (e?.stack) {
    error(e.stack);
  }
}

run();
