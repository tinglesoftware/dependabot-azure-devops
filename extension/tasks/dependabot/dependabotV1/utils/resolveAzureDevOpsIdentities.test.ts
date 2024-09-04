import axios from 'axios';
import { describe } from 'node:test';
import { isHostedAzureDevOps, resolveAzureDevOpsIdentities } from './resolveAzureDevOpsIdentities';

describe('isHostedAzureDevOps', () => {
  it('Old visualstudio url is hosted.', () => {
    const url = new URL('https://example.visualstudio.com/abc');
    const result = isHostedAzureDevOps(url);

    expect(result).toBeTruthy();
  });
  it('Dev Azure url is hosted.', () => {
    const url = new URL('https://dev.azure.com/example');
    const result = isHostedAzureDevOps(url);

    expect(result).toBeTruthy();
  });
  it('private url is not hosted.', () => {
    const url = new URL('https://tfs.example.com/tfs/Collection');
    const result = isHostedAzureDevOps(url);

    expect(result).toBeFalsy();
  });
});

jest.mock('axios');
const mockedAxios = axios as jest.Mocked<typeof axios>;

const aliceOnPrem = {
  id: 'any id',
  email: 'alice@example.com',
  providerDisplayName: 'Alice',
};

const aliceHostedId = 'any Id';
const aliceHosted = {
  descriptor: 'aad.' + Buffer.from(aliceHostedId, 'utf8').toString('base64'),
  email: 'alice@example.com',
  providerDisplayName: 'Alice',
};

describe('resolveAzureDevOpsIdentities', () => {
  it('No email input, is directly returned.', async () => {
    const url = new URL('https://example.visualstudio.com/abc');

    const input = ['be9321e2-f404-4ffa-8d6b-44efddb04865'];
    const results = await resolveAzureDevOpsIdentities(url, input);

    const outputs = results.map((identity) => identity.id);
    expect(outputs).toHaveLength(1);
    expect(outputs).toContain(input[0]);
  });
  it('successfully resolve id for azure devops server', async () => {
    const url = new URL('https://example.onprem.com/abc');

    // Provide the data object to be returned
    mockedAxios.get.mockResolvedValue({
      data: {
        count: 1,
        value: [aliceOnPrem],
      },
      status: 200,
    });

    const input = [aliceOnPrem.email];
    const results = await resolveAzureDevOpsIdentities(url, input);

    const outputs = results.map((identity) => identity.id);
    expect(outputs).toHaveLength(1);
    expect(outputs).toContain(aliceOnPrem.id);
  });
  it('successfully resolve id for hosted azure devops', async () => {
    const url = new URL('https://dev.azure.com/exampleorganization');

    // Provide the data object to be returned
    mockedAxios.post.mockResolvedValue({
      data: {
        count: 1,
        value: [aliceHosted],
      },
      status: 200,
    });

    const input = [aliceHosted.email];
    const results = await resolveAzureDevOpsIdentities(url, input);

    const outputs = results.map((identity) => identity.id);
    expect(outputs).toHaveLength(1);
    expect(outputs).toContain(aliceHostedId);
  });
});
