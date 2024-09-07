import { GitPullRequestMergeStrategy, VersionControlChangeType } from "azure-devops-node-api/interfaces/GitInterfaces";

export interface IPullRequest {
    project: string,
    repository: string,
    source: {
        commit: string,
        branch: string
    },
    target: {
        branch: string
    },
    author?: {
        email: string,
        name: string
    },
    title: string,
    description: string,
    commitMessage: string,
    autoComplete?: {
        userId: string | undefined,
        ignorePolicyConfigIds?: number[],
        mergeStrategy?: GitPullRequestMergeStrategy
    },
    autoApprove?: {
        userId: string | undefined
    },
    assignees?: string[],
    reviewers?: string[],
    labels?: string[],
    workItems?: number[],
    changes: IFileChange[],
    properties?: {
        name: string,
        value: string
    }[]
};

export interface IFileChange {
    changeType: VersionControlChangeType,
    path: string,
    content: string,
    encoding: string
}
