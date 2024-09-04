import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { ISharedVariables } from "../getSharedVariables";
import { IDependabotUpdateJob } from "./interfaces/IDependabotUpdateJob";
import { IDependabotUpdateOutputProcessor } from "./interfaces/IDependabotUpdateOutputProcessor";
import { AzureDevOpsWebApiClient } from "../azure-devops/AzureDevOpsWebApiClient";
import { GitPullRequestMergeStrategy, VersionControlChangeType } from "azure-devops-node-api/interfaces/GitInterfaces";
import * as path from 'path';
import * as crypto from 'crypto';

// Processes dependabot update outputs using the DevOps API
export class DependabotOutputProcessor implements IDependabotUpdateOutputProcessor {
    private readonly api: AzureDevOpsWebApiClient;
    private readonly taskVariables: ISharedVariables;

    // Custom properties used to store dependabot metadata in pull requests.
    // https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-properties
    public static PR_PROPERTY_NAME_PACKAGE_MANAGER = "Dependabot.PackageManager";
    public static PR_PROPERTY_NAME_DEPENDENCIES = "Dependabot.Dependencies";

    constructor(api: AzureDevOpsWebApiClient, taskVariables: ISharedVariables) {
        this.api = api;
        this.taskVariables = taskVariables;
    }

    // Process the appropriate DevOps API actions for the supplied dependabot update output
    public async process(update: IDependabotUpdateJob, type: string, data: any): Promise<boolean> {
        console.debug(`Processing output '${type}' with data:`, data);
        let success: boolean = true;
        switch (type) {

            // Documentation on the 'data' model for each output type can be found here:
            // See: https://github.com/dependabot/cli/blob/main/internal/model/update.go

            case 'update_dependency_list':
                // TODO: Store dependency list info in DevOps project properties? 
                //       This could be used to generate a dependency graph hub/page/report (future feature maybe?)
                //       https://learn.microsoft.com/en-us/rest/api/azure/devops/core/projects/set-project-properties
                break;

            case 'create_pull_request':
                if (this.taskVariables.skipPullRequests) {
                    warning(`Skipping pull request creation as 'skipPullRequests' is set to 'true'`);
                    return;
                }

                // TODO: Skip if active pull request limit reached.

                const sourceRepoParts = update.job.source.repo.split('/'); // "{organisation}/{project}/_git/{repository}""
                const dependencyGroupName = data['dependency-group']?.['name'];
                let dependencies: any = data['dependencies']?.map((dep) => {
                    return {
                        'dependency-name': dep['name'],
                        'dependency-version': dep['version'],
                        'directory': dep['directory'],
                    };
                });
                if (dependencyGroupName) {
                    dependencies = {
                        'dependency-group-name': dependencyGroupName,
                        'dependencies': dependencies
                    };
                }
                await this.api.createPullRequest({
                    project: sourceRepoParts[1],
                    repository: sourceRepoParts[3],
                    source: {
                        commit: data['base-commit-sha'] || update.job.source.commit,
                        branch: sourceBranchNameForUpdate(update.job["package-manager"], update.config.targetBranch, dependencies)
                    },
                    target: {
                        branch: update.config.targetBranch
                    },
                    author: {
                        email: 'noreply@github.com', // TODO: this.taskVariables.extraEnvironmentVariables['DEPENDABOT_AUTHOR_EMAIL']
                        name: 'dependabot[bot]', // TODO: this.taskVariables.extraEnvironmentVariables['DEPENDABOT_AUTHOR_NAME']
                    },
                    title: data['pr-title'],
                    description: data['pr-body'],
                    commitMessage: data['commit-message'],
                    autoComplete: this.taskVariables.setAutoComplete ? {
                        userId: undefined, // TODO: add config for this?
                        ignorePolicyConfigIds: this.taskVariables.autoCompleteIgnoreConfigIds,
                        mergeStrategy: GitPullRequestMergeStrategy[this.taskVariables.mergeStrategy as keyof typeof GitPullRequestMergeStrategy]
                    } : undefined,
                    autoApprove: this.taskVariables.autoApprove ? {
                        userId: this.taskVariables.autoApproveUserToken // TODO: convert token to user id
                    } : undefined,
                    assignees: update.config.assignees,
                    reviewers: update.config.reviewers,
                    labels: update.config.labels?.split(',').map((label) => label.trim()) || [],
                    workItems: update.config.milestone ? [Number(update.config.milestone)] : [],
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
                    }),
                    properties: [
                        {
                            name: DependabotOutputProcessor.PR_PROPERTY_NAME_PACKAGE_MANAGER,
                            value: update.job["package-manager"]
                        },
                        {
                            name: DependabotOutputProcessor.PR_PROPERTY_NAME_DEPENDENCIES,
                            value: JSON.stringify(dependencies)
                        }
                    ]
                })
                break;

            case 'update_pull_request':
                if (this.taskVariables.skipPullRequests) {
                    warning(`Skipping pull request update as 'skipPullRequests' is set to 'true'`);
                    return;
                }
                // TODO: Implement logic from /updater/lib/tinglesoftware/dependabot/api_clients/azure_apu_client.rb :: update_pull_request()
                /*
                type UpdatePullRequest struct {
                    BaseCommitSha          string           `json:"base-commit-sha" yaml:"base-commit-sha"`
                    DependencyNames        []string         `json:"dependency-names" yaml:"dependency-names"`
                    UpdatedDependencyFiles []DependencyFile `json:"updated-dependency-files" yaml:"updated-dependency-files"`
                    PRTitle                string           `json:"pr-title" yaml:"pr-title,omitempty"`
                    PRBody                 string           `json:"pr-body" yaml:"pr-body,omitempty"`
                    CommitMessage          string           `json:"commit-message" yaml:"commit-message,omitempty"`
                    DependencyGroup        map[string]any   `json:"dependency-group" yaml:"dependency-group,omitempty"`
                }
                type DependencyFile struct {
                    Content         string `json:"content" yaml:"content"`
                    ContentEncoding string `json:"content_encoding" yaml:"content_encoding"`
                    Deleted         bool   `json:"deleted" yaml:"deleted"`
                    Directory       string `json:"directory" yaml:"directory"`
                    Name            string `json:"name" yaml:"name"`
                    Operation       string `json:"operation" yaml:"operation"`
                    SupportFile     bool   `json:"support_file" yaml:"support_file"`
                    SymlinkTarget   string `json:"symlink_target,omitempty" yaml:"symlink_target,omitempty"`
                    Type            string `json:"type" yaml:"type"`
                    Mode            string `json:"mode" yaml:"mode,omitempty"`
                }
                */
                break;

            case 'close_pull_request':
                if (this.taskVariables.abandonUnwantedPullRequests) {
                    warning(`Skipping pull request closure as 'abandonUnwantedPullRequests' is set to 'true'`);
                    return;
                }
                // TODO: Implement logic from /updater/lib/tinglesoftware/dependabot/api_clients/azure_apu_client.rb :: close_pull_request()
                /*
                type ClosePullRequest struct {
                    DependencyNames []string `json:"dependency-names" yaml:"dependency-names"`
                    Reason          string   `json:"reason" yaml:"reason"`
                }
                */
                break;

            case 'mark_as_processed':
                // No action required
                break;

            case 'record_ecosystem_versions':
                // No action required
                break;

            case 'record_update_job_error':
                error(`Update job error: ${data['error-type']}`);
                console.log(data['error-details']);
                success = false;
                break;

            case 'record_update_job_unknown_error':
                error(`Update job unknown error: ${data['error-type']}`);
                console.log(data['error-details']);
                success = false;
                break;

            case 'increment_metric':
                // No action required
                break;

            default:
                warning(`Unknown dependabot output type '${type}', ignoring...`);
                break;
        }

        return success;
    }
}

function sourceBranchNameForUpdate(packageEcosystem: string, targetBranch: string, dependencies: any): string {
    const target = targetBranch?.replace(/^\/+|\/+$/g, ''); // strip leading/trailing slashes
    if (dependencies['dependency-group-name']) {
        // Group dependency update
        // e.g. dependabot/nuget/main/microsoft-3b49c54d9e
        const dependencyGroupName = dependencies['dependency-group-name'];
        const dependencyHash = crypto.createHash('md5').update(dependencies['dependencies'].map(d => `${d['dependency-name']}-${d['dependency-version']}`).join(',')).digest('hex').substring(0, 10);
        return `dependabot/${packageEcosystem}/${target}/${dependencyGroupName}-${dependencyHash}`;
    }
    else {
        // Single dependency update
        // e.g. dependabot/nuget/main/Microsoft.Extensions.Logging-1.0.0
        const leadDependency = dependencies.length === 1 ? dependencies[0] : null;
        return `dependabot/${packageEcosystem}/${target}/${leadDependency['dependency-name']}-${leadDependency['dependency-version']}`;
    }
}
