import { describe, expect, it } from 'vitest';

import { extractUrlParts } from './url-parts';

describe('extractUrlParts', () => {
  it('works for old style devops url', () => {
    const url = extractUrlParts({
      organisationUrl: 'https://contoso.visualstudio.com/',
      project: 'prj1',
      repository: 'repo1',
    });
    expect(url.hostname).toBe('dev.azure.com');
    expect(url['api-endpoint']).toBe('https://dev.azure.com/');
    expect(url.project).toBe('prj1');
    expect(url.repository).toBe('repo1');
    expect(url['repository-slug']).toBe('contoso/prj1/_git/repo1');
  });

  it('works for azure devops domain', () => {
    const url = extractUrlParts({
      organisationUrl: 'https://dev.azure.com/contoso/',
      project: 'prj1',
      repository: 'repo1',
    });
    expect(url.hostname).toBe('dev.azure.com');
    expect(url['api-endpoint']).toBe('https://dev.azure.com/');
    expect(url.project).toBe('prj1');
    expect(url.repository).toBe('repo1');
    expect(url['repository-slug']).toBe('contoso/prj1/_git/repo1');
  });

  it('works for on-premise domain', () => {
    const url = extractUrlParts({
      organisationUrl: 'https://server.domain.com/tfs/contoso/',
      project: 'prj1',
      repository: 'repo1',
    });
    expect(url.hostname).toBe('server.domain.com');
    expect(url['api-endpoint']).toBe('https://server.domain.com/tfs/');
    expect(url.project).toBe('prj1');
    expect(url.repository).toBe('repo1');
    expect(url['repository-slug']).toBe('tfs/contoso/prj1/_git/repo1');
  });

  it('works for on-premise domain with port', () => {
    const url = extractUrlParts({
      organisationUrl: 'https://server.domain.com:8081/tfs/contoso/',
      project: 'prj1',
      repository: 'repo1',
    });
    expect(url.hostname).toBe('server.domain.com');
    expect(url['api-endpoint']).toBe('https://server.domain.com:8081/tfs/');
    expect(url.project).toBe('prj1');
    expect(url.repository).toBe('repo1');
    expect(url['repository-slug']).toBe('tfs/contoso/prj1/_git/repo1');
  });

  it('works for localhost', () => {
    const url = extractUrlParts({
      organisationUrl: 'http://localhost:8080/contoso/',
      project: 'prj1',
      repository: 'repo1',
    });
    expect(url.hostname).toBe('localhost');
    expect(url['api-endpoint']).toBe('http://localhost:8080/');
    expect(url.project).toBe('prj1');
    expect(url.repository).toBe('repo1');
    expect(url['repository-slug']).toBe('contoso/prj1/_git/repo1');
  });

  it('works for project Uri', () => {
    const url = extractUrlParts({
      organisationUrl: 'https://dev.azure.com/contoso/Core',
      project: 'prj1',
      repository: 'repo1',
    });
    expect(url.hostname).toBe('dev.azure.com');
    expect(url['api-endpoint']).toBe('https://dev.azure.com/');
    expect(url.project).toBe('prj1');
    expect(url.repository).toBe('repo1');
    expect(url['repository-slug']).toBe('contoso/prj1/_git/repo1');
  });

  it('works for project or repository with spaces', () => {
    const url = extractUrlParts({
      organisationUrl: 'https://dev.azure.com/contoso/',
      project: 'prj 1',
      repository: 'repo 1',
    });
    expect(url.hostname).toBe('dev.azure.com');
    expect(url['api-endpoint']).toBe('https://dev.azure.com/');
    expect(url.project).toBe('prj%201');
    expect(url.repository).toBe('repo%201');
    expect(url['repository-slug']).toBe('contoso/prj%201/_git/repo%201');
  });
});
