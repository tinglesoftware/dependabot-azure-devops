import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { ISharedVariables } from "./getSharedVariables";
import { IDependabotUpdateJob, IDependabotUpdateOutputProcessor } from "./dependabotTypes";
import { AzureDevOpsClient } from "./azureDevOpsApiClient";
import { VersionControlChangeType } from "azure-devops-node-api/interfaces/GitInterfaces";
import * as path from 'path';

// Processes dependabot update outputs using the DevOps API
export class AzureDevOpsDependabotOutputProcessor implements IDependabotUpdateOutputProcessor {
    private readonly api: AzureDevOpsClient;
    private readonly taskVariables: ISharedVariables;

    constructor(api: AzureDevOpsClient, taskVariables: ISharedVariables) {
        this.api = api;
        this.taskVariables = taskVariables;
    }

    // Process the appropriate DevOps API actions for the supplied dependabot update output
    public async process(update: IDependabotUpdateJob, type: string, data: any): Promise<boolean> {
        console.debug(`Processing '${type}' with data:`, data);
        let success: boolean = true;
        switch (type) {

            case 'update_dependency_list':
                // TODO: Store dependency list info in DevOps project data? 
                //       This could be used to generate a dependency graph hub/page/report (future feature maybe?)
                break;

            case 'create_pull_request':
                if (this.taskVariables.skipPullRequests) {
                    warning(`Skipping pull request creation as 'skipPullRequests' is set to 'true'`);
                    return;
                }
                // TODO: Skip if active pull request limit reached.

                const sourceRepoParts = update.job.source.repo.split('/'); // "{organisation}/{project}/_git/{repository}""
                await this.api.createPullRequest({
                    organisation: sourceRepoParts[0],
                    project: sourceRepoParts[1],
                    repository: sourceRepoParts[3],
                    source: {
                        commit: data['base-commit-sha'] || update.job.source.commit,
                        branch: `dependabot/${update.job["package-manager"]}/${update.job.id}` // TODO: Get from dependabot.yaml
                    },
                    target: {
                        branch: 'main' // TODO: Get from dependabot.yaml
                    },
                    author: {
                        email: 'noreply@github.com', // TODO: Get from task variables?
                        name: 'dependabot[bot]', // TODO: Get from task variables?
                    },
                    title: data['pr-title'],
                    description: data['pr-body'],
                    commitMessage: data['commit-message'],
                    mergeStrategy: this.taskVariables.mergeStrategy,
                    autoComplete: {
                        enabled: this.taskVariables.setAutoComplete,
                        bypassPolicyIds: this.taskVariables.autoCompleteIgnoreConfigIds
                    },
                    autoApprove: {
                        enabled: this.taskVariables.autoApprove,
                        approverUserToken: this.taskVariables.autoApproveUserToken
                    },
                    assignees: [], // TODO: Get from dependabot.yaml
                    reviewers: [], // TODO: Get from dependabot.yaml
                    labels: [], // TODO: Get from dependabot.yaml
                    workItems: [], // TODO: Get from dependabot.yaml
                    dependencies: data['dependencies'],
                    changes: data['updated-dependency-files'].filter((file) => file['type'] === 'file').map((file) => {
                        let changeType = VersionControlChangeType.None;
                        if (file['deleted'] === true) {
                            changeType = VersionControlChangeType.Delete;
                        } else if (file['operation'] === 'update') {
                            changeType = VersionControlChangeType.Edit;
                        } else {
                            changeType = VersionControlChangeType.Add;
                        }
                        return {
                            changeType: changeType,
                            path: path.join(file['directory'], file['name']),
                            content: file['content'],
                            encoding: file['content_encoding']
                        }
                    })
                })
                break;

            case 'update_pull_request':
                if (this.taskVariables.skipPullRequests) {
                    warning(`Skipping pull request update as 'skipPullRequests' is set to 'true'`);
                    return;
                }
                // TODO: Implement logic from /updater/lib/tinglesoftware/dependabot/api_clients/azure_apu_client.rb :: update_pull_request()
                break;

            case 'close_pull_request':
                if (this.taskVariables.abandonUnwantedPullRequests) {
                    warning(`Skipping pull request closure as 'abandonUnwantedPullRequests' is set to 'true'`);
                    return;
                }
                // TODO: Implement logic from /updater/lib/tinglesoftware/dependabot/api_clients/azure_apu_client.rb :: close_pull_request()
                break;

            case 'mark_as_processed':
                // TODO: Log this?
                break;

            case 'record_ecosystem_versions':
                // TODO: Log this?
                break;

            case 'record_update_job_error':
                // TODO: Log this?
                success = false;
                break;

            case 'record_update_job_unknown_error':
                // TODO: Log this?
                success = false;
                break;

            case 'increment_metric':
                // TODO: Log this?
                break;

            default:
                warning(`Unknown dependabot output type '${type}', ignoring...`);
                break;
        }

        return success;
    }
}