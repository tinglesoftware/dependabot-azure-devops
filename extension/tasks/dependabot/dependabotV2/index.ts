import { which, setResult, TaskResult } from "azure-pipelines-task-lib/task"
import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { IDependabotUpdateJob } from "../../utils/dependabotTypes";
import { DependabotUpdater } from '../../utils/dependabotUpdater';
import { AzureDevOpsClient } from "../../utils/azureDevOpsApiClient";
import { AzureDevOpsDependabotOutputProcessor } from "../../utils/azureDevOpsDependabotOutputProcessor";
import { parseConfigFile } from '../../utils/parseConfigFile';
import getSharedVariables from '../../utils/getSharedVariables';

async function run() {
  let dependabot: DependabotUpdater = undefined;
  try {

    // Check if required tools are installed
    debug('Checking for `docker` install...');
    which('docker', true);
    debug('Checking for `go` install...');
    which('go', true);

    // Parse the dependabot.yaml configuration file
    const variables = getSharedVariables();
    const config = await parseConfigFile(variables);

    // Initialise the DevOps API client
    const api = new AzureDevOpsClient(
      variables.organizationUrl.toString(), variables.systemAccessToken
    );

    // Initialise the Dependabot updater
    dependabot = new DependabotUpdater(
      DependabotUpdater.CLI_IMAGE_LATEST, // TODO: Add config for this?
      new AzureDevOpsDependabotOutputProcessor(api),
      variables.debug
    );

    // Process the updates per-ecosystem
    let taskWasSuccessful: boolean = true;
    config.updates.forEach(async (update) => {

      // TODO: Fetch all existing PRs from DevOps

      let registryCredentials = new Array();
      for (const key in config.registries) {
        const registry = config.registries[key];
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
            'api-endpoint': variables.apiEndpointUrl,
            hostname: variables.hostname,
            repo: `${variables.organization}/${variables.project}/_git/${variables.repository}`,
            branch: update.targetBranch, // TODO: add config for 'source branch'??
            commit: undefined, // TODO: add config for this?
            directory: update.directories?.length == 0 ? update.directory : undefined,
            directories: update.directories?.length > 0 ? update.directories : undefined
          }
        },
        credentials: (registryCredentials || []).concat([
          {
            type: 'git_source',
            host: variables.hostname,
            username: variables.systemAccessUser?.trim()?.length > 0 ? variables.systemAccessUser : 'x-access-token',
            password: variables.systemAccessToken
          }
        ])
      };

      // Run dependabot updater for the job
      if ((await dependabot.update(job)).filter(u => !u.success).length > 0) {
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
    dependabot?.cleanup();
  }
}

run();
