import { WebApi, getPersonalAccessTokenHandler } from 'azure-devops-node-api';
import {
  CommentThreadStatus,
  CommentType,
  ItemContentType,
  PullRequestAsyncStatus,
  PullRequestStatus,
  type IdentityRefWithVote,
} from 'azure-devops-node-api/interfaces/GitInterfaces';
import { debug, error, warning } from 'azure-pipelines-task-lib/task';
import { type IHttpClientResponse } from 'typed-rest-client/Interfaces';
import { normalizeBranchName, normalizeFilePath } from './formatting';
import {
  HttpRequestError,
  type IAbandonPullRequest,
  type IApprovePullRequest,
  type ICreatePullRequest,
  type IPullRequestProperties,
  type IUpdatePullRequest,
} from './models';

/**
 * Wrapper for DevOps WebApi client with helper methods for easier management of dependabot pull requests
 */
export class AzureDevOpsWebApiClient {
  private readonly organisationApiUrl: string;
  private readonly identityApiUrl: string;
  private readonly connection: WebApi;
  private readonly debug: boolean;

  private authenticatedUserId: string;
  private resolvedUserIds: Record<string, string>;

  public static API_VERSION = '5.0'; // this is the same version used by dependabot-core

  constructor(organisationApiUrl: string, accessToken: string, debug: boolean = false) {
    this.organisationApiUrl = organisationApiUrl.replace(/\/$/, ''); // trim trailing slash
    this.identityApiUrl = getIdentityApiUrl(organisationApiUrl).replace(/\/$/, ''); // trim trailing slash
    this.connection = new WebApi(organisationApiUrl, getPersonalAccessTokenHandler(accessToken, true));
    this.debug = debug;
    this.resolvedUserIds = {};
  }

  /**
   * Get the identity of the authenticated user.
   * @returns
   */
  public async getUserId(): Promise<string> {
    this.authenticatedUserId ||= (await this.connection.connect()).authenticatedUser.id;
    return this.authenticatedUserId;
  }

  /**
   * Get the identity id from a user name, email, or group name.
   * Requires scope "Identity (Read)" (vso.identity).
   * @param userNameEmailOrGroupName
   * @returns
   */
  public async resolveIdentityId(userNameEmailOrGroupName?: string): Promise<string | undefined> {
    if (this.resolvedUserIds[userNameEmailOrGroupName]) {
      return this.resolvedUserIds[userNameEmailOrGroupName];
    }
    try {
      const identities = await this.restApiGet(`${this.identityApiUrl}/_apis/identities`, {
        searchFilter: 'General',
        filterValue: userNameEmailOrGroupName,
        queryMembership: 'None',
      });
      if (!identities?.value || identities.value.length === 0) {
        return undefined;
      }
      this.resolvedUserIds[userNameEmailOrGroupName] = identities.value[0]?.id;
      return this.resolvedUserIds[userNameEmailOrGroupName];
    } catch (e) {
      error(`Failed to resolve user id: ${e}`);
      console.debug(e); // Dump the error stack trace to help with debugging
      return undefined;
    }
  }

  /**
   * Get the default branch for a repository.
   * Requires scope "Code (Read)" (vso.code).
   * @param project
   * @param repository
   * @returns
   */
  public async getDefaultBranch(project: string, repository: string): Promise<string | undefined> {
    try {
      const repo = await this.restApiGet(`${this.organisationApiUrl}/${project}/_apis/git/repositories/${repository}`);
      if (!repo) {
        throw new Error(`Repository '${project}/${repository}' not found`);
      }

      return normalizeBranchName(repo.defaultBranch);
    } catch (e) {
      error(`Failed to get default branch for '${project}/${repository}': ${e}`);
      console.debug(e); // Dump the error stack trace to help with debugging
      return undefined;
    }
  }

  /**
   * Get the list of branch names for a repository.
   * Requires scope "Code (Read)" (vso.code).
   * @param project
   * @param repository
   * @returns
   */
  public async getBranchNames(project: string, repository: string): Promise<string[]> {
    try {
      const refs = await this.restApiGet(
        `${this.organisationApiUrl}/${project}/_apis/git/repositories/${repository}/refs`,
      );
      if (!refs) {
        throw new Error(`Repository '${project}/${repository}' not found`);
      }

      return refs.value?.map((r) => normalizeBranchName(r.name)) || [];
    } catch (e) {
      error(`Failed to list branch names for '${project}/${repository}': ${e}`);
      console.debug(e); // Dump the error stack trace to help with debugging
      return undefined;
    }
  }

  /**
   * Get the properties for all active pull request created by the supplied user.
   * Requires scope "Code (Read)" (vso.code).
   * @param project
   * @param repository
   * @param creator
   * @returns
   */
  public async getActivePullRequestProperties(
    project: string,
    repository: string,
    creator: string,
  ): Promise<IPullRequestProperties[]> {
    try {
      const pullRequests = await this.restApiGet(
        `${this.organisationApiUrl}/${project}/_apis/git/repositories/${repository}/pullrequests`,
        {
          'searchCriteria.creatorId': isGuid(creator) ? creator : await this.getUserId(),
          'searchCriteria.status': 'Active',
        },
      );
      if (!pullRequests?.value || pullRequests.value.length === 0) {
        return [];
      }

      return await Promise.all(
        pullRequests.value.map(async (pr) => {
          const properties = await this.restApiGet(
            `${this.organisationApiUrl}/${project}/_apis/git/repositories/${repository}/pullrequests/${pr.pullRequestId}/properties`,
          );
          return {
            id: pr.pullRequestId,
            properties:
              Object.keys(properties?.value || {}).map((key) => {
                return {
                  name: key,
                  value: properties.value[key]?.$value,
                };
              }) || [],
          };
        }),
      );
    } catch (e) {
      error(`Failed to list active pull request properties: ${e}`);
      console.debug(e); // Dump the error stack trace to help with debugging
      return [];
    }
  }

  /**
   * Create a new pull request.
   * Requires scope "Code (Write)" (vso.code_write).
   * Requires scope "Identity (Read)" (vso.identity), if assignees are specified.
   * @param pr
   * @returns
   */
  public async createPullRequest(pr: ICreatePullRequest): Promise<number | null> {
    console.info(`Creating pull request '${pr.title}'...`);
    try {
      const userId = await this.getUserId();

      // Map the list of the pull request reviewer ids
      // NOTE: Azure DevOps does not have a concept of assignees.
      //       We treat assignees as required reviewers and all other reviewers as optional.
      const allReviewers: IdentityRefWithVote[] = [];
      if (pr.assignees?.length > 0) {
        for (const assignee of pr.assignees) {
          const identityId = isGuid(assignee) ? assignee : await this.resolveIdentityId(assignee);
          if (identityId && !allReviewers.some((r) => r.id === identityId)) {
            allReviewers.push({
              id: identityId,
              isRequired: true,
              isFlagged: true,
            });
          } else {
            warning(`Unable to resolve assignee identity '${assignee}'`);
          }
        }
      }

      // Create the source branch and push a commit with the dependency file changes
      console.info(` - Pushing ${pr.changes.length} file change(s) to branch '${pr.source.branch}'...`);
      const push = await this.restApiPost(
        `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/pushes`,
        {
          refUpdates: [
            {
              name: `refs/heads/${pr.source.branch}`,
              oldObjectId: pr.source.commit,
            },
          ],
          commits: [
            {
              comment: pr.commitMessage,
              author: pr.author,
              changes: pr.changes.map((change) => {
                return {
                  changeType: change.changeType,
                  item: {
                    path: normalizeFilePath(change.path),
                  },
                  newContent: {
                    content: Buffer.from(change.content, <BufferEncoding>change.encoding).toString('base64'),
                    contentType: ItemContentType.Base64Encoded,
                  },
                };
              }),
            },
          ],
        },
      );
      if (!push?.commits?.length) {
        throw new Error('Failed to push changes to source branch, no commits were created');
      }
      console.info(` - Pushed commit: ${push.commits.map((c) => c.commitId).join(', ')}.`);

      // Create the pull request
      console.info(` - Creating pull request to merge '${pr.source.branch}' into '${pr.target.branch}'...`);
      const pullRequest = await this.restApiPost(
        `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/pullrequests`,
        {
          sourceRefName: `refs/heads/${pr.source.branch}`,
          targetRefName: `refs/heads/${pr.target.branch}`,
          title: pr.title,
          description: pr.description,
          reviewers: allReviewers,
          workItemRefs: pr.workItems?.map((id) => {
            return { id: id };
          }),
          labels: pr.labels?.map((label) => {
            return { name: label };
          }),
        },
      );
      if (!pullRequest?.pullRequestId) {
        throw new Error('Failed to create pull request, no pull request id was returned');
      }
      console.info(` - Created pull request: #${pullRequest.pullRequestId}.`);

      // Add the pull request properties
      if (pr.properties?.length > 0) {
        console.info(` - Adding dependency metadata to pull request properties...`);
        const newProperties = await this.restApiPatch(
          `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/pullrequests/${pullRequest.pullRequestId}/properties`,
          pr.properties.map((property) => {
            return {
              op: 'add',
              path: '/' + property.name,
              value: property.value,
            };
          }),
          'application/json-patch+json',
        );
        if (!newProperties?.count) {
          throw new Error('Failed to add dependency metadata properties to pull request');
        }
      }

      // TODO: Upload the pull request description as a 'changes.md' file attachment?
      //       This might be a way to work around the 4000 character limit for PR descriptions, but needs more investigation.
      //       https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-attachments/create?view=azure-devops-rest-7.1

      // Set the pull request auto-complete status
      if (pr.autoComplete) {
        console.info(` - Updating auto-complete options...`);
        const updatedPullRequest = await this.restApiPatch(
          `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/pullrequests/${pullRequest.pullRequestId}`,
          {
            autoCompleteSetBy: {
              id: userId,
            },
            completionOptions: {
              autoCompleteIgnoreConfigIds: pr.autoComplete.ignorePolicyConfigIds,
              deleteSourceBranch: true,
              mergeCommitMessage: mergeCommitMessage(pullRequest.pullRequestId, pr.title, pr.description),
              mergeStrategy: pr.autoComplete.mergeStrategy,
              transitionWorkItems: false,
            },
          },
        );
        if (!updatedPullRequest || updatedPullRequest.autoCompleteSetBy?.id !== userId) {
          throw new Error('Failed to set auto-complete on pull request');
        }
      }

      console.info(` - Pull request was created successfully.`);
      return pullRequest.pullRequestId;
    } catch (e) {
      error(`Failed to create pull request: ${e}`);
      console.debug(e); // Dump the error stack trace to help with debugging
      return null;
    }
  }

  /**
   * Update a pull request.
   * Requires scope "Code (Read & Write)" (vso.code, vso.code_write).
   * @param pr
   * @returns
   */
  public async updatePullRequest(pr: IUpdatePullRequest): Promise<boolean> {
    console.info(`Updating pull request #${pr.pullRequestId}...`);
    try {
      // Get the pull request details
      const pullRequest = await this.restApiGet(
        `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/pullrequests/${pr.pullRequestId}`,
      );
      if (!pullRequest) {
        throw new Error(`Pull request #${pr.pullRequestId} not found`);
      }

      // Skip if the pull request is a draft
      if (pr.skipIfDraft && pullRequest.isDraft) {
        console.info(` - Skipping update as pull request is currently marked as a draft.`);
        return true;
      }

      // Skip if the pull request has been modified by another author
      if (pr.skipIfCommitsFromAuthorsOtherThan) {
        const commits = await this.restApiGet(
          `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/pullrequests/${pr.pullRequestId}/commits`,
        );
        if (commits?.value?.some((c) => c.author?.email !== pr.skipIfCommitsFromAuthorsOtherThan)) {
          console.info(` - Skipping update as pull request has been modified by another user.`);
          return true;
        }
      }

      // Get the branch stats to check if the source branch is behind the target branch
      const stats = await this.restApiGet(
        `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/stats/branches`,
        {
          name: normalizeBranchName(pullRequest.sourceRefName),
        },
      );
      if (stats?.behindCount === undefined) {
        throw new Error(`Failed to get branch stats for '${pullRequest.sourceRefName}'`);
      }

      // Skip if the source branch is not behind the target branch
      if (pr.skipIfNotBehindTargetBranch && stats.behindCount === 0) {
        console.info(` - Skipping update as source branch is not behind target branch.`);
        return true;
      }

      // Rebase the target branch into the source branch to reset the "behind" count
      const sourceBranchName = normalizeBranchName(pullRequest.sourceRefName);
      const targetBranchName = normalizeBranchName(pullRequest.targetRefName);
      if (stats.behindCount > 0) {
        console.info(
          ` - Rebasing '${targetBranchName}' into '${sourceBranchName}' (${stats.behindCount} commit(s) behind)...`,
        );
        const rebase = await this.restApiPost(
          `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/refs`,
          [
            {
              name: pullRequest.sourceRefName,
              oldObjectId: pullRequest.lastMergeSourceCommit.commitId,
              newObjectId: pr.commit,
            },
          ],
        );
        if (rebase?.value?.[0]?.success !== true) {
          throw new Error('Failed to rebase the target branch into the source branch');
        }
      }

      // Push all file changes to the source branch
      console.info(` - Pushing ${pr.changes.length} file change(s) to branch '${pullRequest.sourceRefName}'...`);
      const push = await this.restApiPost(
        `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/pushes`,
        {
          refUpdates: [
            {
              name: pullRequest.sourceRefName,
              oldObjectId: pr.commit,
            },
          ],
          commits: [
            {
              comment:
                pullRequest.mergeStatus === PullRequestAsyncStatus.Conflicts
                  ? 'Resolve merge conflicts'
                  : `Rebase '${sourceBranchName}' onto '${targetBranchName}'`,
              author: pr.author,
              changes: pr.changes.map((change) => {
                return {
                  changeType: change.changeType,
                  item: {
                    path: normalizeFilePath(change.path),
                  },
                  newContent: {
                    content: Buffer.from(change.content, <BufferEncoding>change.encoding).toString('base64'),
                    contentType: ItemContentType.Base64Encoded,
                  },
                };
              }),
            },
          ],
        },
      );
      if (!push?.commits?.length) {
        throw new Error('Failed to push changes to source branch, no commits were created');
      }
      console.info(` - Pushed commit: ${push.commits.map((c) => c.commitId).join(', ')}.`);

      console.info(` - Pull request was updated successfully.`);
      return true;
    } catch (e) {
      error(`Failed to update pull request: ${e}`);
      console.debug(e); // Dump the error stack trace to help with debugging
      return false;
    }
  }

  /**
   * Approve a pull request.
   * Requires scope "Code (Write)" (vso.code_write).
   * @param pr
   * @returns
   */
  public async approvePullRequest(pr: IApprovePullRequest): Promise<boolean> {
    console.info(`Approving pull request #${pr.pullRequestId}...`);
    try {
      // Approve the pull request
      console.info(` - Updating reviewer vote on pull request...`);
      const userId = await this.getUserId();
      const userVote = await this.restApiPut(
        `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/pullrequests/${pr.pullRequestId}/reviewers/${userId}`,
        {
          // Vote 10 = "approved"; 5 = "approved with suggestions"; 0 = "no vote"; -5 = "waiting for author"; -10 = "rejected"
          vote: 10,
          // Reapprove must be set to true after the 2023 August 23 update;
          // Approval of a previous PR iteration does not count in later iterations, which means we must (re)approve every after push to the source branch
          // See: https://learn.microsoft.com/en-us/azure/devops/release-notes/2023/sprint-226-update#new-branch-policy-preventing-users-to-approve-their-own-changes
          //      https://github.com/tinglesoftware/dependabot-azure-devops/issues/1069
          isReapprove: true,
        },
        // API version 7.1 is required to use the 'isReapprove' parameter
        // See: https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-reviewers/create-pull-request-reviewer?view=azure-devops-rest-7.1&tabs=HTTP#request-body
        //      https://learn.microsoft.com/en-us/azure/devops/integrate/concepts/rest-api-versioning?view=azure-devops#supported-versions
        '7.1',
      );
      if (userVote?.vote != 10) {
        throw new Error('Failed to approve pull request, vote was not recorded');
      }

      console.info(` - Pull request was approved successfully.`);
    } catch (e) {
      error(`Failed to approve pull request: ${e}`);
      console.debug(e); // Dump the error stack trace to help with debugging
      return false;
    }
  }

  /**
   * Abandon a pull request.
   * Requires scope "Code (Write)" (vso.code_write).
   * @param pr
   * @returns
   */
  public async abandonPullRequest(pr: IAbandonPullRequest): Promise<boolean> {
    console.info(`Abandoning pull request #${pr.pullRequestId}...`);
    try {
      const userId = await this.getUserId();

      // Add a comment to the pull request, if supplied
      if (pr.comment) {
        console.info(` - Adding abandonment reason comment to pull request...`);
        const thread = await this.restApiPost(
          `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/pullrequests/${pr.pullRequestId}/threads`,
          {
            status: CommentThreadStatus.Closed,
            comments: [
              {
                author: {
                  id: userId,
                },
                content: pr.comment,
                commentType: CommentType.System,
              },
            ],
          },
        );
        if (!thread?.id) {
          throw new Error('Failed to add comment to pull request, thread was not created');
        }
      }

      // Abandon the pull request
      console.info(` - Abandoning pull request...`);
      const abandonedPullRequest = await this.restApiPatch(
        `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/pullrequests/${pr.pullRequestId}`,
        {
          status: PullRequestStatus.Abandoned,
          closedBy: {
            id: userId,
          },
        },
      );
      if (abandonedPullRequest?.status?.toLowerCase() !== 'abandoned') {
        throw new Error('Failed to abandon pull request, status was not updated');
      }

      // Delete the source branch if required
      if (pr.deleteSourceBranch) {
        console.info(` - Deleting source branch...`);
        const deletedBranch = await this.restApiPost(
          `${this.organisationApiUrl}/${pr.project}/_apis/git/repositories/${pr.repository}/refs`,
          [
            {
              name: abandonedPullRequest.sourceRefName,
              oldObjectId: abandonedPullRequest.lastMergeSourceCommit.commitId,
              newObjectId: '0000000000000000000000000000000000000000',
              isLocked: false,
            },
          ],
        );
        if (deletedBranch?.value?.[0]?.success !== true) {
          throw new Error('Failed to delete the source branch');
        }
      }

      console.info(` - Pull request was abandoned successfully.`);
      return true;
    } catch (e) {
      error(`Failed to abandon pull request: ${e}`);
      console.debug(e); // Dump the error stack trace to help with debugging
      return false;
    }
  }

  private async restApiGet(
    url: string,
    params?: Record<string, string>,
    apiVersion: string = AzureDevOpsWebApiClient.API_VERSION,
  ) {
    const queryString = Object.keys(params || {})
      .map((key) => `${key}=${params[key]}`)
      .join('&');
    const fullUrl = `${url}?api-version=${apiVersion}${queryString ? `&${queryString}` : ''}`;
    return await sendRestApiRequestWithRetry(
      'GET',
      fullUrl,
      undefined,
      () =>
        this.connection.rest.client.get(fullUrl, {
          Accept: 'application/json',
        }),
      this.debug,
    );
  }

  private async restApiPost(url: string, data?: unknown, apiVersion: string = AzureDevOpsWebApiClient.API_VERSION) {
    const fullUrl = `${url}?api-version=${apiVersion}`;
    return await sendRestApiRequestWithRetry(
      'POST',
      fullUrl,
      data,
      () =>
        this.connection.rest.client.post(fullUrl, JSON.stringify(data), {
          'Content-Type': 'application/json',
        }),
      this.debug,
    );
  }

  private async restApiPut(url: string, data?: unknown, apiVersion: string = AzureDevOpsWebApiClient.API_VERSION) {
    const fullUrl = `${url}?api-version=${apiVersion}`;
    return await sendRestApiRequestWithRetry(
      'PUT',
      fullUrl,
      data,
      () =>
        this.connection.rest.client.put(fullUrl, JSON.stringify(data), {
          'Content-Type': 'application/json',
        }),
      this.debug,
    );
  }

  private async restApiPatch(
    url: string,
    data?: unknown,
    contentType?: string,
    apiVersion: string = AzureDevOpsWebApiClient.API_VERSION,
  ) {
    const fullUrl = `${url}?api-version=${apiVersion}`;
    return await sendRestApiRequestWithRetry(
      'PATCH',
      fullUrl,
      data,
      () =>
        this.connection.rest.client.patch(fullUrl, JSON.stringify(data), {
          'Content-Type': contentType || 'application/json',
        }),
      this.debug,
    );
  }
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

function isGuid(guid: string): boolean {
  const regex = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
  return regex.test(guid);
}

function getIdentityApiUrl(organisationApiUrl: string): string {
  const uri = new URL(organisationApiUrl);
  const hostname = uri.hostname.toLowerCase();

  // If the organisation is hosted on Azure DevOps, use the 'vssps.dev.azure.com' domain
  if (hostname === 'dev.azure.com' || hostname.endsWith('.visualstudio.com')) {
    uri.host = 'vssps.dev.azure.com';
  }
  return uri.toString();
}

export async function sendRestApiRequestWithRetry(
  method: string,
  url: string,
  payload: unknown,
  requestAsync: () => Promise<IHttpClientResponse>,
  isDebug: boolean = false,
  retryCount: number = 3,
  retryDelay: number = 3000,
) {
  let body: string;
  try {
    // Send the request, ready the response
    if (isDebug) debug(`🌎 🠊 [${method}] ${url}`);
    const response = await requestAsync();
    body = await response.readBody();
    if (isDebug) debug(`🌎 🠈 [${response.message.statusCode}] ${response.message.statusMessage}`);

    // Check that the request was successful
    if (response.message.statusCode < 200 || response.message.statusCode > 299) {
      throw new HttpRequestError(
        `HTTP ${method} '${url}' failed: ${response.message.statusCode} ${response.message.statusMessage}`,
        response.message.statusCode,
      );
    }

    // Parse the response
    return JSON.parse(body);
  } catch (e) {
    // Retry the request if the error is a temporary failure
    if (retryCount > 1 && isErrorTemporaryFailure(e)) {
      warning(e.message);
      if (isDebug) debug(`⏳ Retrying request in ${retryDelay}ms...`);
      await new Promise((resolve) => setTimeout(resolve, retryDelay));
      return sendRestApiRequestWithRetry(method, url, payload, requestAsync, isDebug, retryCount - 1, retryDelay);
    }

    // In debug mode, log the error, request, and response for debugging
    if (isDebug) {
      if (payload) {
        debug(`REQUEST: ${JSON.stringify(payload)}`);
      }
      if (body) {
        debug(`RESPONSE: ${body}`);
      }
    }

    console.log('THROW', e);
    throw e;
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function isErrorTemporaryFailure(e: any): boolean {
  if (e instanceof HttpRequestError) {
    // Check for common HTTP status codes that indicate a temporary failure
    // See: https://developer.mozilla.org/en-US/docs/Web/HTTP/Status
    switch (e.code) {
      case 502:
        return true; // 502 Bad Gateway
      case 503:
        return true; // 503 Service Unavailable
      case 504:
        return true; // 504 Gateway Timeout
      default:
        return false;
    }
  } else if (e?.code) {
    // Check for Node.js system errors that indicate a temporary failure
    // See: https://nodejs.org/api/errors.html#errors_common_system_errors
    switch (e.code) {
      case 'ETIMEDOUT':
        return true; // Operation timed out
      default:
        return false;
    }
  } else {
    return false;
  }
}
