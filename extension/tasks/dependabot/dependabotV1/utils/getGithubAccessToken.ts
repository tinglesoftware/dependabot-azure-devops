import { debug, getEndpointAuthorization, getInput, loc } from 'azure-pipelines-task-lib/task';

/**
 * Extract access token from Github endpoint
 *
 * @param githubEndpoint
 * @returns
 */
function getGithubEndPointToken(githubEndpoint: string): string {
  const githubEndpointObject = getEndpointAuthorization(githubEndpoint, false);
  let githubEndpointToken: string = null;

  if (!!githubEndpointObject) {
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
export default function getGithubAccessToken() {
  let gitHubAccessToken: string = getInput('gitHubAccessToken');
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
