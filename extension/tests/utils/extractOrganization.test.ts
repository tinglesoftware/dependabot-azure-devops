import extractOrganization from '../../tasks/utils/extractOrganization';

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
