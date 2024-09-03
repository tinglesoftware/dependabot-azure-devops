import { which, setResult, TaskResult } from "azure-pipelines-task-lib/task"
import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { DependabotCli } from '../../utils/dependabot-cli/DependabotCli';
import { AzureDevOpsWebApiClient } from "../../utils/azure-devops/AzureDevOpsWebApiClient";
import { DependabotOutputProcessor } from "../../utils/dependabot-cli/DependabotOutputProcessor";
import { DependabotJobBuilder } from "../../utils/dependabot-cli/DependabotJobBuilder";
import { parseConfigFile } from '../../utils/parseConfigFile';
import getSharedVariables from '../../utils/getSharedVariables';

async function run() {
  let taskWasSuccessful: boolean = true;
  let dependabot: DependabotCli = undefined;
  try {

    // Check if required tools are installed
    debug('Checking for `docker` install...');
    which('docker', true);
    debug('Checking for `go` install...');
    which('go', true);

    // Parse the dependabot.yaml configuration file
    const taskVariables = getSharedVariables();
    const dependabotConfig = await parseConfigFile(taskVariables);

    // Initialise the DevOps API client
    const azdoApi = new AzureDevOpsWebApiClient(
      taskVariables.organizationUrl.toString(), taskVariables.systemAccessToken
    );

    // Initialise the Dependabot updater
    dependabot = new DependabotCli(
      DependabotCli.CLI_IMAGE_LATEST, // TODO: Add config for this?
      new DependabotOutputProcessor(azdoApi, taskVariables),
      taskVariables.debug
    );

    // Fetch all active Dependabot pull requests from DevOps
    const myActivePullRequests = await azdoApi.getMyActivePullRequestProperties(
      taskVariables.project, taskVariables.repository
    );

    // Loop through each package ecyosystem and perform updates
    dependabotConfig.updates.forEach(async (update) => {
      const existingPullRequests = myActivePullRequests
        .filter(pr => {
          return pr.properties.find(p => p.name === DependabotOutputProcessor.PR_PROPERTY_NAME_PACKAGE_MANAGER && p.value === update.packageEcosystem);
        })
        .map(pr => {
          return JSON.parse(
            pr.properties.find(p => p.name === DependabotOutputProcessor.PR_PROPERTY_NAME_DEPENDENCIES)?.value
          )
        });

      // Update all dependencies, this will update create new pull requests
      const allDependenciesJob = DependabotJobBuilder.updateAllDependenciesJob(taskVariables, update, dependabotConfig.registries, existingPullRequests);
      if ((await dependabot.update(allDependenciesJob)).filter(u => !u.success).length > 0) {
        taskWasSuccessful = false;
      }

      // Update existing pull requests, this will either resolve merge conflicts or close pull requests that are no longer needed
      for (const pr of existingPullRequests) {
        const updatePullRequestJob = DependabotJobBuilder.updatePullRequestJob(taskVariables, update, dependabotConfig.registries, existingPullRequests, pr);
        if ((await dependabot.update(updatePullRequestJob)).filter(u => !u.success).length > 0) {
          taskWasSuccessful = false;
        }
      }
    });

    setResult(
      taskWasSuccessful ? TaskResult.Succeeded : TaskResult.Failed,
      taskWasSuccessful ? 'All update jobs completed successfully' : 'One or more update jobs failed, check logs for more information'
    );

  }
  catch (e) {
    error(`Unhandled task exception: ${e}`);
    setResult(TaskResult.Failed, e?.message);
  }
  finally {
    // TODO: dependabotCli?.cleanup();
  }
}

run();
