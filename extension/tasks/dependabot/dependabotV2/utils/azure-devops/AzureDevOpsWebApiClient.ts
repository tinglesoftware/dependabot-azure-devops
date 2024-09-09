import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { WebApi, getPersonalAccessTokenHandler } from "azure-devops-node-api";
import { CommentThreadStatus, CommentType, ItemContentType, PullRequestAsyncStatus, PullRequestStatus } from "azure-devops-node-api/interfaces/GitInterfaces";
import { IPullRequestProperties } from "./interfaces/IPullRequestProperties";
import { IPullRequest } from "./interfaces/IPullRequest";
import { IFileChange } from "./interfaces/IFileChange";

/**
 * Wrapper for DevOps WebApi client with helper methods for easier management of dependabot pull requests
 */
export class AzureDevOpsWebApiClient {

    private readonly connection: WebApi;
    private userId: string | null = null;

    constructor(organisationApiUrl: string, accessToken: string) {
        this.connection = new WebApi(
            organisationApiUrl,
            getPersonalAccessTokenHandler(accessToken)
        );
    }

    private async getUserId(): Promise<string> {
        return (this.userId ||= (await this.connection.connect()).authenticatedUser?.id || "");
    }

    /**
     * Get the default branch for a repository
     * @param project 
     * @param repository 
     * @returns 
     */
    public async getDefaultBranch(project: string, repository: string): Promise<string> {
        try {
            const git = await this.connection.getGitApi();
            const repo = await git.getRepository(repository, project);
            if (!repo) {
                throw new Error(`Repository '${project}/${repository}' not found`);
            }

            return repo.defaultBranch;
        }
        catch (e) {
            error(`Failed to get default branch for '${project}/${repository}': ${e}`);
            throw e;
        }
    }

    /**
     * Get the properties for all active pull request created by the current user
     * @param project 
     * @param repository 
     * @returns 
     */
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
                pullRequests?.map(async pr => {
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

    /**
     * Create a new pull request
     * @param pr 
     * @returns 
     */
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
                                        content: Buffer.from(change.content, <BufferEncoding>change.encoding).toString('base64'),
                                        contentType: ItemContentType.Base64Encoded
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

            // TODO: Upload the pull request description as a 'changes.md' file attachment?
            //       This might be a way to work around the 4000 character limit for PR descriptions, but needs more investigation.
            // https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-attachments/create?view=azure-devops-rest-7.1

            // Set the pull request auto-complete status 
            if (pr.autoComplete) {
                console.info(` - Setting auto-complete...`);
                await git.updatePullRequest(
                    {
                        autoCompleteSetBy: {
                            id: userId
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

            console.info(` - Pull request ${pullRequest.pullRequestId} was created successfully.`);
            return pullRequest.pullRequestId;
        }
        catch (e) {
            error(`Failed to create pull request: ${e}`);
            return null;
        }
    }

    /**
     * Update a pull request
     * @param options 
     * @returns 
     */
    public async updatePullRequest(options: {
        project: string,
        repository: string,
        pullRequestId: number,
        changes: IFileChange[],
        skipIfCommitsFromUsersOtherThan?: string,
        skipIfNoConflicts?: boolean
    }): Promise<boolean> {
        console.info(`Updating pull request #${options.pullRequestId}...`);
        try {
            const userId = await this.getUserId();
            const git = await this.connection.getGitApi();

            // Get the pull request details
            const pullRequest = await git.getPullRequest(options.repository, options.pullRequestId, options.project);
            if (!pullRequest) {
                throw new Error(`Pull request #${options.pullRequestId} not found`);
            }

            // Skip if no merge conflicts
            if (options.skipIfNoConflicts && pullRequest.mergeStatus !== PullRequestAsyncStatus.Conflicts) {
                console.info(` - Skipping update as pull request has no merge conflicts.`);
                return true;
            }

            // Skip if the pull request has been modified by another user
            const commits = await git.getPullRequestCommits(options.repository, options.pullRequestId, options.project);
            if (options.skipIfCommitsFromUsersOtherThan && commits.some(c => c.author?.email !== options.skipIfCommitsFromUsersOtherThan)) {
                console.info(` - Skipping update as pull request has been modified by another user.`);
                return true;
            }

            // Push changes to the source branch
            console.info(` - Pushing ${options.changes.length} change(s) branch '${pullRequest.sourceRefName}'...`);
            const push = await git.createPush(
                {
                    refUpdates: [
                        {
                            name: pullRequest.sourceRefName,
                            oldObjectId: pullRequest.lastMergeSourceCommit.commitId
                        }
                    ],
                    commits: [
                        {
                            comment: (pullRequest.mergeStatus === PullRequestAsyncStatus.Conflicts)
                                ? "Resolve merge conflicts"
                                : "Update dependency files",
                            changes: options.changes.map(change => {
                                return {
                                    changeType: change.changeType,
                                    item: {
                                        path: normalizeDevOpsPath(change.path)
                                    },
                                    newContent: {
                                        content: Buffer.from(change.content, <BufferEncoding>change.encoding).toString('base64'),
                                        contentType: ItemContentType.Base64Encoded
                                    }
                                };
                            })
                        }
                    ]
                },
                options.repository,
                options.project
            );

            console.info(` - Pull request #${options.pullRequestId} was updated successfully.`);
            return true;
        }
        catch (e) {
            error(`Failed to update pull request: ${e}`);
            return false;
        }
    }

    /**
     * Approve a pull request
     * @param options 
     * @returns 
     */
    public async approvePullRequest(options: {
        project: string,
        repository: string,
        pullRequestId: number
    }): Promise<boolean> {
        console.info(`Approving pull request #${options.pullRequestId}...`);
        try {
            const userId = await this.getUserId();
            const git = await this.connection.getGitApi();

            // Approve the pull request
            console.info(` - Approving pull request...`);
            await git.createPullRequestReviewer(
                {
                    vote: 10, // 10 - approved 5 - approved with suggestions 0 - no vote -5 - waiting for author -10 - rejected
                    isReapprove: true
                },
                options.repository,
                options.pullRequestId,
                userId,
                options.project
            );
        }
        catch (e) {
            error(`Failed to approve pull request: ${e}`);
            return false;
        }
    }

    /**
     * Close a pull request
     * @param options 
     * @returns 
     */
    public async closePullRequest(options: {
        project: string,
        repository: string,
        pullRequestId: number,
        comment: string,
        deleteSourceBranch: boolean
    }): Promise<boolean> {
        console.info(`Closing pull request #${options.pullRequestId}...`);
        try {
            const userId = await this.getUserId();
            const git = await this.connection.getGitApi();

            // Add a comment to the pull request, if supplied
            if (options.comment) {
                console.info(` - Adding comment to pull request...`);
                await git.createThread(
                    {
                        status: CommentThreadStatus.Closed,
                        comments: [
                            {
                                author: {
                                    id: userId
                                },
                                content: options.comment,
                                commentType: CommentType.System
                            }
                        ]
                    },
                    options.repository,
                    options.pullRequestId,
                    options.project
                );
            }

            // Close the pull request
            console.info(` - Abandoning pull request...`);
            const pullRequest = await git.updatePullRequest(
                {
                    status: PullRequestStatus.Abandoned,
                    closedBy: {
                        id: userId
                    }
                },
                options.repository,
                options.pullRequestId,
                options.project
            );

            // Delete the source branch if required
            if (options.deleteSourceBranch) {
                console.info(` - Deleting source branch...`);
                await git.updateRef(
                    {
                        name: `refs/heads/${pullRequest.sourceRefName}`,
                        oldObjectId: pullRequest.lastMergeSourceCommit.commitId,
                        newObjectId: "0000000000000000000000000000000000000000",
                        isLocked: false
                    },
                    options.repository,
                    '',
                    options.project
                );
            }

            console.info(` - Pull request #${options.pullRequestId} was closed successfully.`);
            return true;
        }
        catch (e) {
            error(`Failed to close pull request: ${e}`);
            return false;
        }
    }
    
    /**
     * Get a project property
     * @param project 
     * @param name 
     * @param valueBuilder 
     * @returns 
     */
    public async getProjectProperty(project: string, name: string): Promise<string | null> {
        try {
            const core = await this.connection.getCoreApi();
            const projects = await core.getProjects();
            const projectGuid = projects?.find(p => p.name === project)?.id;
            const properties = await core.getProjectProperties(projectGuid);
            return properties?.find(p => p.name === name)?.value;
        } catch (e) {
            error(`Failed to get project property '${name}': ${e}`);
            console.log(e);
            return null;
        }
    }

    /**
     * Update a project property
     * @param project 
     * @param name 
     * @param valueBuilder 
     * @returns 
     */
    public async updateProjectProperty(project: string, name: string, valueBuilder: (existingValue: string) => string): Promise<boolean> {
        try {
            
            // Get the existing project property value
            const core = await this.connection.getCoreApi();
            const projects = await core.getProjects();
            const projectGuid = projects?.find(p => p.name === project)?.id;
            const properties = await core.getProjectProperties(projectGuid);
            const propertyValue = properties?.find(p => p.name === name)?.value;
            
            // Update the project property
            await core.setProjectProperties(
                undefined,
                projectGuid,
                [
                    {
                        op: "add",
                        path: "/" + name,
                        value: valueBuilder(propertyValue || "")
                    }
                ]
            );

            return true;

        } catch (e) {
            error(`Failed to update project property '${name}': ${e}`);
            console.log(e);
            return false;
        }
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