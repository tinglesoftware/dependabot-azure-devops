import { which, setResult, TaskResult } from "azure-pipelines-task-lib/task"
import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { DependabotUpdater, IUpdateScenarioOutput } from '../../utils/dependabotUpdater';
import { parseConfigFile } from '../../utils/parseConfigFile';
import getSharedVariables  from '../../utils/getSharedVariables';

async function run() {
  let updater: DependabotUpdater = undefined;
  try {

    // Check if required tools are installed
    debug('Checking for `docker` install...');
    which('docker', true);
    debug('Checking for `go` install...');
    which('go', true);

    // Parse the dependabot configuration file
    const variables = getSharedVariables();
    const config = await parseConfigFile(variables);

    // Initialise the dependabot updater
    updater = new DependabotUpdater(undefined /* TODO: Add config for this */, variables.debug);

    // Process the updates per-ecosystem
    let updatedSuccessfully: boolean = true;
    config.updates.forEach(async (update) => {

      // TODO: Fetch all existing PRs from DevOps
      
      let extraCredentials = new Array();
      for (const key in config.registries) {
        const registry = config.registries[key];
        extraCredentials.push({
            type: registry.type,
            host: registry.host,
            url: registry.url,
            registry: registry.registry,
            username: registry.username,
            password: registry.password,
            token: registry.token
        });
      };

      // Run dependabot updater for the job
      const result = processUpdateOutputs(
        await updater.update({
          // TODO: Parse this from `config` and `variables`
          job: {
            id: 'job-1',
            job: {
              'package-manager': update.packageEcosystem,
              'allowed-updates': [
                { 'update-type': 'all' }
              ],
              source: {
                  provider: 'azure',
                  repo: `${variables.organization}/${variables.project}/_git/${variables.repository}`,
                  directory: update.directory,
                  commit: undefined
              }
            },
            credentials: (extraCredentials || []).concat([
              {
                type: 'git_source',
                host: new URL(variables.organizationUrl).hostname,
                username: 'x-access-token',
                password: variables.systemAccessToken
              }
            ])
          }
        })
      );
      if (!result) {
        updatedSuccessfully = false;
      }

      // TODO: Loop through all existing PRs and do a single update job for each, update/close the PR as needed

    });

    setResult(
      updatedSuccessfully ? TaskResult.Succeeded : TaskResult.Failed, 
      updatedSuccessfully ? 'All update jobs completed successfully' : 'One or more update jobs failed, check logs for more information'
    );

  }
  catch (e: any) {
    error(`Unhandled task exception: ${e}`);
    console.log(e);
    setResult(TaskResult.Failed, e?.message);
  }
  finally {
    updater?.cleanup();
  }
}

// Process the job outputs and apply changes to DevOps
// TODO: Move this to a new util class, e.g. `dependabotOutputProcessor.ts`
function processUpdateOutputs(outputs: IUpdateScenarioOutput[]) : boolean {
  let success: boolean = true;
  outputs.forEach(output => {
    switch (output.type) {

      case 'update_dependency_list':
        console.log('TODO: UPDATED DEPENDENCY LIST: ', output.data);
        // TODO: Save data to DevOps? This would be really useful for generating a dependency graph hub page or HTML report (future feature maybe?)
        break;

      case 'create_pull_request':
        console.log('TODO: CREATE PULL REQUEST: ', output.data);
        // TODO: Implement logic from /updater/lib/tinglesoftware/dependabot/api_clients/azure_apu_client.rb :: create_pull_request()
        break;

      case 'update_pull_request':
        console.log('TODO: UPDATE PULL REQUEST ', output.data);
        // TODO: Implement logic from /updater/lib/tinglesoftware/dependabot/api_clients/azure_apu_client.rb :: update_pull_request()
        break;

      case 'close_pull_request':
        console.log('TODO: CLOSE PULL REQUEST ', output.data);
        // TODO: Implement logic from /updater/lib/tinglesoftware/dependabot/api_clients/azure_apu_client.rb :: close_pull_request()
        break;

      case 'mark_as_processed':
        console.log('TODO: MARK AS PROCESSED: ', output.data);
        // TODO: Log info?
        break;

      case 'record_ecosystem_versions':
        console.log('TODO: RECORD ECOSYSTEM VERSIONS: ', output.data);
        // TODO: Log info?
        break;
        
      case 'record_update_job_error':
        console.log('TODO: RECORD UPDATE JOB ERROR: ', output.data);
        // TODO: Log error?
        success = false;
        break;
        
      case 'record_update_job_unknown_error':
        console.log('TODO: RECORD UPDATE JOB UNKNOWN ERROR: ', output.data);
        // TODO: Log error?
        success = false;
        break;
        
      case 'increment_metric':
        console.log('TODO: INCREMENT METRIC: ', output.data);
        // TODO: Log info?
        break;
        
      default:
        warning(`Unknown dependabot output type: ${output.type}`);
        success = false;
        break;
    }
  });
  return success;
}

run();
