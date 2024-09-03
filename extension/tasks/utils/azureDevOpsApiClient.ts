import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { WebApi, getPersonalAccessTokenHandler } from "azure-devops-node-api";
import { ItemContentType, VersionControlChangeType } from "azure-devops-node-api/interfaces/GitInterfaces";

export interface IPullRequest {
    organisation: string,
    project: string,
    repository: string,
    source: {
        commit: string,
        branch: string
    },
    target: {
        branch: string
    },
    author: {
        email: string,
        name: string
    },
    title: string,
    description: string,
    commitMessage: string,
    mergeStrategy: string,
    autoComplete: {
        enabled: boolean,
        bypassPolicyIds: number[],
    },
    autoApprove: {
        enabled: boolean,
        approverUserToken: string
    },
    assignees: string[],
    reviewers: string[],
    labels: string[],
    workItems: number[],
    dependencies: any,
    changes: {
        changeType: VersionControlChangeType,
        path: string,
        content: string,
        encoding: string
    }[]
};

// Wrapper for DevOps API actions
export class AzureDevOpsClient {

    private readonly connection: WebApi;
    private userId: string | null = null;

    constructor(apiUrl: string, accessToken: string) {
        this.connection = new WebApi(
            apiUrl,
            getPersonalAccessTokenHandler(accessToken)
        );
    }

    public async createPullRequest(pr: IPullRequest): Promise<number | null> {
        console.info(`Creating pull request for '${pr.title}'...`);
        try {
            const userId = await this.getUserId();
            const git = await this.connection.getGitApi();

            // Create a new branch for the pull request and commit the changes
            console.info(`Pushing ${pr.changes.length} change(s) to branch '${pr.source.branch}'...`);
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
                            author: {
                                email: pr.author.email,
                                name: pr.author.name
                            },
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
            console.info(`Creating pull request to merge '${pr.source.branch}' into '${pr.target.branch}'...`);
            const pullRequest = await git.createPullRequest(
                {
                    sourceRefName: `refs/heads/${pr.target.branch}`, // Merge from the new dependabot update branch
                    targetRefName: `refs/heads/${pr.source.branch}`, // Merge to the original branch
                    title: pr.title,
                    description: pr.description

                },
                pr.repository,
                pr.project,
                true
            );
            
            console.info(`Pull request #${pullRequest?.pullRequestId} created successfully.`);
            return pullRequest?.pullRequestId || null;
        }
        catch (e) {
            error(`Failed to create pull request: ${e}`);
            console.log(e);
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
