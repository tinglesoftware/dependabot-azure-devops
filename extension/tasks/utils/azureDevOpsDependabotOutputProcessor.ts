import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { IDependabotUpdateOutputProcessor } from "./dependabotTypes";
import { AzureDevOpsClient } from "./azureDevOpsApiClient";

// Processes dependabot update outputs using the DevOps API
export class AzureDevOpsDependabotOutputProcessor implements IDependabotUpdateOutputProcessor {
    private readonly api: AzureDevOpsClient;

    constructor(api: AzureDevOpsClient) {
        this.api = api;
    }

    // Process the appropriate DevOps API actions for the supplied dependabot update output
    public async process(type: string, data: any): Promise<boolean> {
        let success: boolean = true;
        switch (type) {

            case 'update_dependency_list':
                console.log('TODO: UPDATED DEPENDENCY LIST: ', data);
                // TODO: Save data to DevOps? This would be really useful for generating a dependency graph hub page or HTML report (future feature maybe?)
                break;

            case 'create_pull_request':
                console.log('TODO: CREATE PULL REQUEST: ', data);
                // TODO: Implement logic from /updater/lib/tinglesoftware/dependabot/api_clients/azure_apu_client.rb :: create_pull_request()
                break;

            case 'update_pull_request':
                console.log('TODO: UPDATE PULL REQUEST ', data);
                // TODO: Implement logic from /updater/lib/tinglesoftware/dependabot/api_clients/azure_apu_client.rb :: update_pull_request()
                break;

            case 'close_pull_request':
                console.log('TODO: CLOSE PULL REQUEST ', data);
                // TODO: Implement logic from /updater/lib/tinglesoftware/dependabot/api_clients/azure_apu_client.rb :: close_pull_request()
                break;

            case 'mark_as_processed':
                console.log('TODO: MARK AS PROCESSED: ', data);
                // TODO: Log info?
                break;

            case 'record_ecosystem_versions':
                console.log('TODO: RECORD ECOSYSTEM VERSIONS: ', data);
                // TODO: Log info?
                break;

            case 'record_update_job_error':
                console.log('TODO: RECORD UPDATE JOB ERROR: ', data);
                // TODO: Log error?
                success = false;
                break;

            case 'record_update_job_unknown_error':
                console.log('TODO: RECORD UPDATE JOB UNKNOWN ERROR: ', data);
                // TODO: Log error?
                success = false;
                break;

            case 'increment_metric':
                console.log('TODO: INCREMENT METRIC: ', data);
                // TODO: Log info?
                break;

            default:
                warning(`Unknown dependabot output type: ${type}`);
                success = false;
                break;
        }

        return success;
    }
}