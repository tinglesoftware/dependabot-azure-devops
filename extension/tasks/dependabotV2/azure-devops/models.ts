import { GitPullRequestMergeStrategy } from 'azure-devops-node-api/interfaces/GitInterfaces';
import { VersionControlChangeType } from 'azure-devops-node-api/interfaces/TfvcInterfaces';

/**
 * File change
 */
export interface IFileChange {
  changeType: VersionControlChangeType;
  path: string;
  content: string;
  encoding: string;
}

/**
 * Pull request properties
 */
export interface IPullRequestProperties {
  id: number;
  properties?: {
    name: string;
    value: string;
  }[];
}

/**
 * Pull request creation request
 */
export interface ICreatePullRequest {
  project: string;
  repository: string;
  source: {
    commit: string;
    branch: string;
  };
  target: {
    branch: string;
  };
  author?: {
    email: string;
    name: string;
  };
  title: string;
  description: string;
  commitMessage: string;
  autoComplete?: {
    ignorePolicyConfigIds?: number[];
    mergeStrategy?: GitPullRequestMergeStrategy;
  };
  assignees?: string[];
  reviewers?: string[];
  labels?: string[];
  workItems?: string[];
  changes: IFileChange[];
  properties?: {
    name: string;
    value: string;
  }[];
}

/**
 * Pull request update request
 */
export interface IUpdatePullRequest {
  project: string;
  repository: string;
  pullRequestId: number;
  commit?: string;
  author?: {
    email: string;
    name: string;
  };
  changes: IFileChange[];
  skipIfDraft?: boolean;
  skipIfCommitsFromAuthorsOtherThan?: string;
  skipIfNotBehindTargetBranch?: boolean;
}

/**
 * Pull request approval request
 */
export interface IApprovePullRequest {
  project: string;
  repository: string;
  pullRequestId: number;
}

/**
 * Pull request abandon request
 */
export interface IAbandonPullRequest {
  project: string;
  repository: string;
  pullRequestId: number;
  comment: string;
  deleteSourceBranch: boolean;
}
