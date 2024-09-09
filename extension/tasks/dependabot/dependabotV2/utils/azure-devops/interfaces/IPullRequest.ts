import { GitPullRequestMergeStrategy } from "azure-devops-node-api/interfaces/GitInterfaces";
import { IFileChange } from "./IFileChange";

/**
 * Pull request creation
 */
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
        ignorePolicyConfigIds?: number[],
        mergeStrategy?: GitPullRequestMergeStrategy
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
