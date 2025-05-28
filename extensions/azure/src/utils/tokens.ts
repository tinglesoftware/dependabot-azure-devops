import {
  debug,
  getEndpointAuthorization,
  getEndpointAuthorizationParameter,
  getInput,
  loc,
} from 'azure-pipelines-task-lib/task';

/**
 * Extract access token from Github endpoint
 *
 * @param githubEndpoint
 * @returns
 */
function getGithubEndPointToken(githubEndpoint: string): string {
  const githubEndpointObject = getEndpointAuthorization(githubEndpoint, false);
  let githubEndpointToken: string | undefined;

  if (githubEndpointObject) {
    debug('Endpoint scheme: ' + githubEndpointObject.scheme);

    if (githubEndpointObject.scheme === 'PersonalAccessToken') {
      githubEndpointToken = githubEndpointObject.parameters.accessToken;
    } else if (githubEndpointObject.scheme === 'OAuth') {
      githubEndpointToken = githubEndpointObject.parameters.AccessToken;
    } else if (githubEndpointObject.scheme === 'Token') {
      githubEndpointToken = githubEndpointObject.parameters.AccessToken;
    } else if (githubEndpointObject.scheme) {
      throw new Error(loc('InvalidEndpointAuthScheme', githubEndpointObject.scheme));
    }
  }

  if (!githubEndpointToken) {
    throw new Error(loc('InvalidGitHubEndpoint', githubEndpoint));
  }

  return githubEndpointToken;
}

/**
 * Extract the Github access token from `gitHubAccessToken` and `gitHubConnection` inputs
 *
 * @returns the Github access token
 */
export function getGithubAccessToken() {
  let gitHubAccessToken = getInput('gitHubAccessToken');
  if (gitHubAccessToken) {
    debug('gitHubAccessToken provided, using for authenticating');
    return gitHubAccessToken;
  }

  const githubEndpointId = getInput('gitHubConnection');
  if (githubEndpointId) {
    debug('GitHub connection supplied. A token shall be extracted from it.');
    gitHubAccessToken = getGithubEndPointToken(githubEndpointId);
  }

  return gitHubAccessToken;
}

/**
 * Prepare the access token for Azure DevOps Repos.
 *
 *
 * If the user has not provided one, we use the one from the SystemVssConnection
 *
 * @returns Azure DevOps Access Token
 */
export function getAzureDevOpsAccessToken() {
  const systemAccessToken = getInput('azureDevOpsAccessToken');
  if (systemAccessToken) {
    debug('azureDevOpsAccessToken provided, using for authenticating');
    return systemAccessToken;
  }

  const serviceConnectionName = getInput('azureDevOpsServiceConnection');
  if (serviceConnectionName) {
    debug('TFS connection supplied. A token shall be extracted from it.');
    return getEndpointAuthorizationParameter(serviceConnectionName, 'apitoken', false)!;
  }

  debug("No custom token provided. The SystemVssConnection's AccessToken shall be used.");
  return getEndpointAuthorizationParameter('SystemVssConnection', 'AccessToken', false)!;
}
