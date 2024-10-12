import { debug, error, setResult, TaskResult, warning, which } from 'azure-pipelines-task-lib/task';
import { AzureDevOpsWebApiClient } from './utils/azure-devops/AzureDevOpsWebApiClient';
import { DependabotCli } from './utils/dependabot-cli/DependabotCli';
import { DependabotJobBuilder } from './utils/dependabot-cli/DependabotJobBuilder';
import {
  DependabotOutputProcessor,
  parsePullRequestProperties,
} from './utils/dependabot-cli/DependabotOutputProcessor';
import { IDependabotUpdate } from './utils/dependabot/interfaces/IDependabotConfig';
import parseDependabotConfigFile from './utils/dependabot/parseConfigFile';
import parseTaskInputConfiguration from './utils/getSharedVariables';
import { ISecurityAdvisory } from './utils/github/getSecurityAdvisories';

async function run() {
  let dependabot: DependabotCli = undefined;
  let failedJobs: number = 0;
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
      azureDevOpsAccessToken: taskInputs.systemAccessToken,
      gitHubAccessToken: taskInputs.githubAccessToken,
      collectorImage: undefined, // TODO: Add config for this?
      proxyImage: undefined, // TODO: Add config for this?
      updaterImage: undefined, // TODO: Add config for this?
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
      const existingPullRequests = parsePullRequestProperties(prAuthorActivePullRequests, packageEcosystem);
      const existingPullRequestDependencies = Object.entries(existingPullRequests).map(([id, deps]) => deps);

      // If this is a security-only update (i.e. 'open-pull-requests-limit: 0'), then we first need to discover the dependencies
      // that need updating and check each one for security advisories. This is because Dependabot requires the list of vulnerable dependencies
      // to be supplied in the job definition of security-only update job, it will not automatically discover them like a versioned update does.
      // https://docs.github.com/en/code-security/dependabot/dependabot-security-updates/configuring-dependabot-security-updates#overriding-the-default-behavior-with-a-configuration-file
      let securityAdvisories: ISecurityAdvisory[] = undefined;
      let dependencyNamesToUpdate: string[] = undefined;
      const securityUpdatesOnly = update['open-pull-requests-limit'] === 0;
      if (securityUpdatesOnly) {
        // TODO: If and when Dependabot supports a better way to do security-only updates, we should remove this code block.
        warning(
          'Security-only updates are only partially supported by Dependabot CLI. For more info, see: https://github.com/tinglesoftware/dependabot-azure-devops/blob/main/docs/migrations/v1-to-v2.md#security-only-updates',
        );
        warning(
          'To work around the limitations of Dependabot CLI, vulnerable dependencies will be discovered using an "ignore everything" regular update job. ' +
            'After discovery has completed, security advisories for your dependencies will be checked before finally performing your requested security-only update job. ' +
            'Because of these required extra steps, the task may take longer to complete than usual.',
        );
        const discoveredDependencyListOutputs = await dependabot.update(
          DependabotJobBuilder.newDiscoverDependencyListJob(taskInputs, updateId, update, dependabotConfig.registries),
          dependabotUpdaterOptions,
        );
      }

      // Run an update job for "all dependencies"; this will create new pull requests for dependencies that need updating
      const updateAllDependenciesJob = DependabotJobBuilder.newUpdateAllJob(
        taskInputs,
        updateId,
        update,
        dependabotConfig.registries,
        dependencyNamesToUpdate,
        existingPullRequestDependencies,
        securityAdvisories,
      );
      const updateAllDependenciesOutputs = await dependabot.update(updateAllDependenciesJob, dependabotUpdaterOptions);
      if (!updateAllDependenciesOutputs || updateAllDependenciesOutputs.filter((u) => !u.success).length > 0) {
        updateAllDependenciesOutputs?.filter((u) => !u.success)?.forEach((u) => exception(u.error));
        failedJobs++;
      }

      // If there are existing pull requests, run an update job for each one; this will resolve merge conflicts and close pull requests that are no longer needed
      const numberOfPullRequestsToUpdate = Object.keys(existingPullRequests).length;
      if (numberOfPullRequestsToUpdate > 0) {
        if (!taskInputs.skipPullRequests) {
          for (const pullRequestId in existingPullRequests) {
            const updatePullRequestJob = DependabotJobBuilder.newUpdatePullRequestJob(
              taskInputs,
              pullRequestId,
              update,
              dependabotConfig.registries,
              existingPullRequestDependencies,
              existingPullRequests[pullRequestId],
            );
            const updatePullRequestOutputs = await dependabot.update(updatePullRequestJob, dependabotUpdaterOptions);
            if (!updatePullRequestOutputs || updatePullRequestOutputs.filter((u) => !u.success).length > 0) {
              updatePullRequestOutputs?.filter((u) => !u.success)?.forEach((u) => exception(u.error));
              failedJobs++;
            }
          }
        } else {
          warning(
            `Skipping update of ${numberOfPullRequestsToUpdate} existing pull request(s) as 'skipPullRequests' is set to 'true'`,
          );
        }
      }
    }

    setResult(
      failedJobs ? TaskResult.Failed : TaskResult.Succeeded,
      failedJobs
        ? `${failedJobs} update job(s) failed, check logs for more information`
        : `All update jobs completed successfully`,
    );
  } catch (e) {
    setResult(TaskResult.Failed, e?.message);
    exception(e);
  } finally {
    dependabot?.cleanup();
  }
}

function exception(e: Error) {
  if (e) {
    error(`An unhandled exception occurred: ${e}`);
    console.debug(e); // Dump the stack trace to help with debugging
  }
}

run();
