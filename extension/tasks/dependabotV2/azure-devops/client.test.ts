import { jest } from '@jest/globals';

import { VersionControlChangeType } from 'azure-devops-node-api/interfaces/TfvcInterfaces';

import { AzureDevOpsWebApiClient } from '../azure-devops/client';
import { ICreatePullRequest } from './models';
import exp = require('constants');

jest.mock('azure-devops-node-api');
jest.mock('azure-pipelines-task-lib/task');

describe('AzureDevOpsWebApiClient', () => {
  const organisationApiUrl = 'https://dev.azure.com/mock-organization';
  const accessToken = 'mock-access-token';
  let client: AzureDevOpsWebApiClient;

  beforeEach(() => {
    client = new AzureDevOpsWebApiClient(organisationApiUrl, accessToken);
    jest.clearAllMocks();
  });

  describe('createPullRequest', () => {
    let pr: ICreatePullRequest;

    beforeEach(() => {
      pr = {
        project: 'project',
        repository: 'repository',
        source: {
          branch: 'update-branch',
          commit: 'commit-id',
        },
        target: {
          branch: 'main',
        },
        title: 'PR Title',
        description: 'PR Description',
        commitMessage: 'Commit Message',
        changes: [
          {
            path: 'file.txt',
            content: 'hello world',
            encoding: 'utf-8',
            changeType: VersionControlChangeType.Add,
          },
        ],
      };
    });

    it('should create a pull request without duplicate reviewer and assignee identities', async () => {
      // Arange
      const mockGetUserId = jest.spyOn(client, 'getUserId').mockResolvedValue('my-user-id');
      const mockResolveIdentityId = jest
        .spyOn(client, 'resolveIdentityId')
        .mockImplementation(async (identity?: string) => {
          return identity || '';
        });
      const mockRestApiPost = jest
        .spyOn(client as any, 'restApiPost')
        .mockResolvedValueOnce({
          commits: [{ commitId: 'new-commit-id' }],
        })
        .mockResolvedValueOnce({
          pullRequestId: 1,
        });
      const mockRestApiPatch = jest.spyOn(client as any, 'restApiPatch').mockResolvedValueOnce({
        count: 1,
      });

      // Act
      pr.assignees = ['user1', 'user2'];
      pr.reviewers = ['user1', 'user3'];
      const pullRequestId = await client.createPullRequest(pr);

      // Assert
      expect(mockRestApiPost).toHaveBeenCalledTimes(2);
      expect((mockRestApiPost.mock.calls[1] as any)[1].reviewers.length).toBe(3);
      expect((mockRestApiPost.mock.calls[1] as any)[1].reviewers).toContainEqual({
        id: 'user1',
        isRequired: true,
        isFlagged: true,
      });
      expect((mockRestApiPost.mock.calls[1] as any)[1].reviewers).toContainEqual({
        id: 'user2',
        isRequired: true,
        isFlagged: true,
      });
      expect((mockRestApiPost.mock.calls[1] as any)[1].reviewers).toContainEqual({ id: 'user3' });
      expect(pullRequestId).toBe(1);
    });
  });
});
