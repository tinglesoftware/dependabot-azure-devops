import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { WebApi, getPersonalAccessTokenHandler } from "azure-devops-node-api";
import { ItemContentType, PullRequestStatus } from "azure-devops-node-api/interfaces/GitInterfaces";
import { IPullRequestProperties } from "./interfaces/IPullRequestProperties";
import { IPullRequest } from "./interfaces/IPullRequest";

// Wrapper for DevOps WebApi client with helper methods for easier management of dependabot pull requests
export class AzureDevOpsWebApiClient {

    private readonly connection: WebApi;
    private userId: string | null = null;

    constructor(organisationApiUrl: string, accessToken: string) {
        this.connection = new WebApi(
            organisationApiUrl,
            getPersonalAccessTokenHandler(accessToken)
        );
    }

    // Get the properties for all active pull request created by the current user
    public async getMyActivePullRequestProperties(project: string, repository: string): Promise<IPullRequestProperties[]> {
        console.info(`Fetching active pull request properties in '${project}/${repository}'...`);
        try {
            const userId = await this.getUserId();
            const git = await this.connection.getGitApi();
            const pullRequests = await git.getPullRequests(
                repository,
                {
                    creatorId: userId,
                    status: PullRequestStatus.Active
                },
                project
            );

            return await Promise.all(
                pullRequests.map(async pr => {
                    const properties = (await git.getPullRequestProperties(repository, pr.pullRequestId, project))?.value;
                    return {
                        id: pr.pullRequestId,
                        properties: Object.keys(properties)?.map(key => {
                            return {
                                name: key,
                                value: properties[key].$value
                            };
                        }) || []
                    };
                })
            );
        }
        catch (e) {
            error(`Failed to list active pull request properties: ${e}`);
            return [];
        }
    }

    // Create a new pull request
    public async createPullRequest(pr: IPullRequest): Promise<number | null> {
        console.info(`Creating pull request '${pr.title}'...`);
        try {
            const userId = await this.getUserId();
            const git = await this.connection.getGitApi();

            // Create the source branch and commit the file changes
            console.info(` - Pushing ${pr.changes.length} change(s) to branch '${pr.source.branch}'...`);
            const push = await git.createPush(
                {
                    refUpdates: [
                        {
                            name: `refs/heads/${pr.source.branch}`,
                            oldObjectId: pr.source.commit
                        }
                    ],
                    commits: [
                        {
                            comment: pr.commitMessage,
                            author: pr.author,
                            changes: pr.changes.map(change => {
                                return {
                                    changeType: change.changeType,
                                    item: {
                                        path: normalizeDevOpsPath(change.path)
                                    },
                                    newContent: {
                                        content: change.content,
                                        contentType: ItemContentType.RawText
                                    }
                                };
                            })
                        }
                    ]
                },
                pr.repository,
                pr.project
            );

            // Create the pull request
            console.info(` - Creating pull request to merge '${pr.source.branch}' into '${pr.target.branch}'...`);
            const pullRequest = await git.createPullRequest(
                {
                    sourceRefName: `refs/heads/${pr.source.branch}`,
                    targetRefName: `refs/heads/${pr.target.branch}`,
                    title: pr.title,
                    description: pr.description
                },
                pr.repository,
                pr.project,
                true
            );

            // Add the pull request properties
            if (pr.properties?.length > 0) {
                console.info(` - Adding dependency metadata to pull request properties...`);
                await git.updatePullRequestProperties(
                    null,
                    pr.properties.map(property => {
                        return {
                            op: "add",
                            path: "/" + property.name,
                            value: property.value
                        };
                    }),
                    pr.repository,
                    pullRequest.pullRequestId,
                    pr.project
                );
            }

            // Set the pull request auto-complete status 
            if (pr.autoComplete) {
                console.info(` - Setting auto-complete...`);
                const autoCompleteUserId = pr.autoComplete.userId || userId;
                await git.updatePullRequest(
                    {
                        autoCompleteSetBy: {
                            id: autoCompleteUserId
                        },
                        completionOptions: {
                            autoCompleteIgnoreConfigIds: pr.autoComplete.ignorePolicyConfigIds,
                            deleteSourceBranch: true,
                            mergeCommitMessage: mergeCommitMessage(pullRequest.pullRequestId, pr.title, pr.description),
                            mergeStrategy: pr.autoComplete.mergeStrategy,
                            transitionWorkItems: false,
                        }
                    },
                    pr.repository,
                    pullRequest.pullRequestId,
                    pr.project
                );
            }

            // Set the pull request auto-approve status
            if (pr.autoApprove) {
                console.info(` - Approving pull request...`);
                const approverUserId = pr.autoApprove.userId || userId;
                await git.createPullRequestReviewer(
                    {
                        vote: 10, // 10 - approved 5 - approved with suggestions 0 - no vote -5 - waiting for author -10 - rejected
                        isReapprove: true
                    },
                    pr.repository,
                    pullRequest.pullRequestId,
                    approverUserId,
                    pr.project
                );
            }

            console.info(` - Pull request ${pullRequest.pullRequestId} was created successfully.`);
            return pullRequest.pullRequestId;
        }
        catch (e) {
            error(`Failed to create pull request: ${e}`);
            return null;
        }
    }

    private async getUserId(): Promise<string> {
        return (this.userId ||= (await this.connection.connect()).authenticatedUser?.id || "");
    }
}

function normalizeDevOpsPath(path: string): string {
    // Convert backslashes to forward slashes, convert './' => '/' and ensure the path starts with a forward slash if it doesn't already, this is how DevOps paths are formatted
    return path.replace(/\\/g, "/").replace(/^\.\//, "/").replace(/^([^/])/, "/$1");
}

function mergeCommitMessage(id: number, title: string, description: string): string {
    //
    // The merge commit message should contain the PR number and title for tracking.
    // This is the default behaviour in Azure DevOps.
    // Example:
    //   Merged PR 24093: Bump Tingle.Extensions.Logging.LogAnalytics from 3.4.2-ci0005 to 3.4.2-ci0006
    //
    //   Bumps [Tingle.Extensions.Logging.LogAnalytics](...) from 3.4.2-ci0005 to 3.4.2-ci0006
    //   - [Release notes](....)
    //   - [Changelog](....)
    //   - [Commits](....)
    //
    // There appears to be a DevOps bug when setting "completeOptions" with a "mergeCommitMessage" even when truncated to 4000 characters. 
    // The error message is:
    //   Invalid argument value.
    //   Parameter name: Completion options have exceeded the maximum encoded length (4184/4000)
    //
    // The effective limit seems to be about 3500 characters:
    //   https://developercommunity.visualstudio.com/t/raise-the-character-limit-for-pull-request-descrip/365708#T-N424531
    //
    return `Merged PR ${id}: ${title}\n\n${description}`.slice(0, 3500);
}