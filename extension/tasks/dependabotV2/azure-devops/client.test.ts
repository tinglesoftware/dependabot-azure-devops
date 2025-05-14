import { jest } from '@jest/globals';

import { VersionControlChangeType } from 'azure-devops-node-api/interfaces/TfvcInterfaces';

import { IHttpClientResponse } from 'typed-rest-client/Interfaces';
import { AzureDevOpsWebApiClient, isErrorTemporaryFailure, sendRestApiRequestWithRetry } from '../azure-devops/client';
import { HttpRequestError, ICreatePullRequest } from './models';
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
      const pullRequestId = await client.createPullRequest(pr);

      // Assert
      expect(mockRestApiPost).toHaveBeenCalledTimes(2);
      expect((mockRestApiPost.mock.calls[1] as any)[1].reviewers.length).toBe(2);
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
      expect(pullRequestId).toBe(1);
    });
  });
});

describe('sendRestApiRequestWithRetry', () => {
  let mockRequestAsync: jest.MockedFunction<() => Promise<IHttpClientResponse>>;
  let mockResponseBody: any;
  let mockResponse: Partial<IHttpClientResponse>;

  beforeEach(() => {
    mockRequestAsync = jest.fn();
    mockResponseBody = {};
    mockResponse = {
      readBody: jest.fn(async () => JSON.stringify(mockResponseBody)),
      message: {
        statusCode: 200,
        statusMessage: 'OK',
      } as any,
    };
  });

  it('should send a request and return the response', async () => {
    mockRequestAsync.mockResolvedValue(mockResponse as IHttpClientResponse);
    mockResponseBody = { hello: 'world' };

    const result = await sendRestApiRequestWithRetry('GET', 'https://example.com', undefined, mockRequestAsync);

    expect(mockRequestAsync).toHaveBeenCalledTimes(1);
    expect(mockResponse.readBody).toHaveBeenCalledTimes(1);
    expect(result).toEqual(mockResponseBody);
  });

  it('should throw an error if the response status code is not in the 2xx range', async () => {
    mockRequestAsync.mockResolvedValue(mockResponse as IHttpClientResponse);
    mockResponse.message.statusCode = 400;
    mockResponse.message.statusMessage = 'Bad Request';

    await expect(
      sendRestApiRequestWithRetry('GET', 'https://example.com', undefined, mockRequestAsync),
    ).rejects.toThrow(/400 Bad Request/i);
  });

  it('should throw an error if the response cannot be parsed as JSON', async () => {
    mockRequestAsync.mockResolvedValue(mockResponse as IHttpClientResponse);
    mockResponse.readBody = jest.fn(async () => 'invalid json');

    await expect(
      sendRestApiRequestWithRetry('GET', 'https://example.com', undefined, mockRequestAsync),
    ).rejects.toThrow(/unexpected token .* in JSON/i);
  });

  it('should throw an error after retrying a request three times', async () => {
    const err = Object.assign(new Error('connect ETIMEDOUT 127.0.0.1:443'), { code: 'ETIMEDOUT' });
    mockRequestAsync.mockRejectedValue(err);

    await expect(
      sendRestApiRequestWithRetry('GET', 'https://example.com', undefined, mockRequestAsync, true, 3, 0),
    ).rejects.toThrow(err);
    expect(mockRequestAsync).toHaveBeenCalledTimes(3);
  });

  it('should retry the request if a temporary failure error is thrown', async () => {
    const err = Object.assign(new Error('connect ETIMEDOUT 127.0.0.1:443'), { code: 'ETIMEDOUT' });
    mockRequestAsync.mockRejectedValueOnce(err);
    mockRequestAsync.mockResolvedValueOnce(mockResponse as IHttpClientResponse);
    mockResponseBody = { hello: 'world' };

    const result = await sendRestApiRequestWithRetry(
      'GET',
      'https://example.com',
      undefined,
      mockRequestAsync,
      true,
      3,
      0,
    );

    expect(mockRequestAsync).toHaveBeenCalledTimes(2);
    expect(mockResponse.readBody).toHaveBeenCalledTimes(1);
    expect(result).toEqual(mockResponseBody);
  });
});

describe('isErrorTemporaryFailure', () => {
  it('should return true for HttpRequestError with status code 502', () => {
    const error = new HttpRequestError('Bad Gateway', 502);
    expect(isErrorTemporaryFailure(error)).toBe(true);
  });

  it('should return true for HttpRequestError with status code 503', () => {
    const error = new HttpRequestError('Service Unavailable', 503);
    expect(isErrorTemporaryFailure(error)).toBe(true);
  });

  it('should return true for HttpRequestError with status code 504', () => {
    const error = new HttpRequestError('Gateway Timeout', 504);
    expect(isErrorTemporaryFailure(error)).toBe(true);
  });

  it('should return false for HttpRequestError with other status codes', () => {
    const error = new HttpRequestError('Bad Request', 400);
    expect(isErrorTemporaryFailure(error)).toBe(false);
  });

  it('should return true for Node.js system error with code ETIMEDOUT', () => {
    const error = { code: 'ETIMEDOUT', message: 'Operation timed out' };
    expect(isErrorTemporaryFailure(error)).toBe(true);
  });

  it('should return false for Node.js system error with other codes', () => {
    const error = { code: 'ECONNREFUSED', message: 'Connection refused' };
    expect(isErrorTemporaryFailure(error)).toBe(false);
  });

  it('should return false for undefined or null errors', () => {
    expect(isErrorTemporaryFailure(undefined)).toBe(false);
    expect(isErrorTemporaryFailure(null)).toBe(false);
  });
});
