import { which, setResult, TaskResult } from "azure-pipelines-task-lib/task"
import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { IDependabotUpdateJob } from "../../utils/dependabotTypes";
import { DependabotUpdater } from '../../utils/dependabotUpdater';
import { AzureDevOpsClient } from "../../utils/azureDevOpsApiClient";
import { AzureDevOpsDependabotOutputProcessor } from "../../utils/azureDevOpsDependabotOutputProcessor";
import { parseConfigFile } from '../../utils/parseConfigFile';
import getSharedVariables from '../../utils/getSharedVariables';

async function run() {
  let dependabotCli: DependabotUpdater = undefined;
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
    const azdoApi = new AzureDevOpsClient(
      taskVariables.organizationUrl.toString(), taskVariables.systemAccessToken
    );

    // Initialise the Dependabot updater
    dependabotCli = new DependabotUpdater(
      DependabotUpdater.CLI_IMAGE_LATEST, // TODO: Add config for this?
      new AzureDevOpsDependabotOutputProcessor(azdoApi, taskVariables),
      taskVariables.debug
    );

    // Process the updates per-ecosystem
    let taskWasSuccessful: boolean = true;
    dependabotConfig.updates.forEach(async (update) => {

      // TODO: Fetch all existing PRs from DevOps

      let registryCredentials = new Array();
      for (const key in dependabotConfig.registries) {
        const registry = dependabotConfig.registries[key];
        registryCredentials.push({
          type: registry.type,
          host: registry.host,
          url: registry.url,
          registry: registry.registry,
          region: undefined, // TODO: registry.region,
          username: registry.username,
          password: registry.password,
          token: registry.token,
          'replaces-base': registry['replaces-base'] || false
        });
      };

      let job: IDependabotUpdateJob = {
        job: {
          // TODO: Parse all options from `config` and `variables`
          id: 'job-1', // TODO: Make timestamp or auto-incrementing id?
          'package-manager': update.packageEcosystem,
          'updating-a-pull-request': false,
          'allowed-updates': [
            { 'update-type': 'all' } // TODO: update.allow
          ],
          'security-updates-only': false,
          source: {
            provider: 'azure',
            'api-endpoint': taskVariables.apiEndpointUrl,
            hostname: taskVariables.hostname,
            repo: `${taskVariables.organization}/${taskVariables.project}/_git/${taskVariables.repository}`,
            branch: update.targetBranch, // TODO: add config for 'source branch'??
            commit: undefined, // TODO: add config for this?
            directory: update.directories?.length == 0 ? update.directory : undefined,
            directories: update.directories?.length > 0 ? update.directories : undefined
          }
        },
        credentials: (registryCredentials || []).concat([
          {
            type: 'git_source',
            host: taskVariables.hostname,
            username: taskVariables.systemAccessUser?.trim()?.length > 0 ? taskVariables.systemAccessUser : 'x-access-token',
            password: taskVariables.systemAccessToken
          }
        ])
      };

      // Run dependabot updater for the job
      if ((await dependabotCli.update(job)).filter(u => !u.success).length > 0) {
        taskWasSuccessful = false;
      }

      // TODO: Loop through all existing PRs and do a single update job for each, update/close the PR as needed
      //       e.g. https://github.com/dependabot/cli/blob/main/testdata/go/update-pr.yaml

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
    dependabotCli?.cleanup();
  }
}

run();
