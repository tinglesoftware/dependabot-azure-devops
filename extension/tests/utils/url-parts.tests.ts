import { describe, expect, it } from 'vitest';

import { extractHostname, extractOrganization } from '../../src/utils/url-parts';

describe('Extract hostname', () => {
  it('Should convert old *.visualstudio.com hostname to dev.azure.com', () => {
    var url = new URL('https://contoso.visualstudio.com');
    var hostname = extractHostname(url);

    expect(hostname).toBe('dev.azure.com');
  });

  it('Should retain the hostname', () => {
    var url = new URL('https://dev.azure.com/Core/contoso');
    var hostname = extractHostname(url);

    expect(hostname).toBe('dev.azure.com');
  });

  it('Should retain localhost hostname', () => {
    var url = new URL('https://localhost:8080/contoso');
    var hostname = extractHostname(url);

    expect(hostname).toBe('localhost');
  });
});

describe('Extract organization name', () => {
  it('Should extract organization for on-premise domain', () => {
    var url = 'https://server.domain.com/tfs/contoso/';
    var organization = extractOrganization(url);

    expect(organization).toBe('contoso');
  });

  it('Should extract organization for azure devops domain', () => {
    var url = 'https://dev.azure.com/contoso/';
    var organization = extractOrganization(url);

    expect(organization).toBe('contoso');
  });

  it('Should extract organization for old style devops url', () => {
    var url = 'https://contoso.visualstudio.com/';
    var organization = extractOrganization(url);

    expect(organization).toBe('contoso');
  });
});
