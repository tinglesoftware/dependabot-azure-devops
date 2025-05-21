import { describe, expect, it } from 'vitest';

import { extractHostname, extractOrganization } from '../../src/utils/url-parts';

describe('Extract hostname', () => {
  it('Should convert old *.visualstudio.com hostname to dev.azure.com', () => {
    const url = new URL('https://contoso.visualstudio.com');
    const hostname = extractHostname(url);

    expect(hostname).toBe('dev.azure.com');
  });

  it('Should retain the hostname', () => {
    const url = new URL('https://dev.azure.com/Core/contoso');
    const hostname = extractHostname(url);

    expect(hostname).toBe('dev.azure.com');
  });

  it('Should retain localhost hostname', () => {
    const url = new URL('https://localhost:8080/contoso');
    const hostname = extractHostname(url);

    expect(hostname).toBe('localhost');
  });
});

describe('Extract organization name', () => {
  it('Should extract organization for on-premise domain', () => {
    const url = 'https://server.domain.com/tfs/contoso/';
    const organization = extractOrganization(url);

    expect(organization).toBe('contoso');
  });

  it('Should extract organization for azure devops domain', () => {
    const url = 'https://dev.azure.com/contoso/';
    const organization = extractOrganization(url);

    expect(organization).toBe('contoso');
  });

  it('Should extract organization for old style devops url', () => {
    const url = 'https://contoso.visualstudio.com/';
    const organization = extractOrganization(url);

    expect(organization).toBe('contoso');
  });
});
