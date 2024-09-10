import * as tl from 'azure-pipelines-task-lib/task';
import extractHostname from './extractHostname';
import extractOrganization from './extractOrganization';
import extractVirtualDirectory from './extractVirtualDirectory';
import getAzureDevOpsAccessToken from './getAzureDevOpsAccessToken';
import getGithubAccessToken from './getGithubAccessToken';

export interface ISharedVariables {
  /** URL of the organization. This may lack the project name */
  organizationUrl: URL;

  /** Organization URL protocol */
  protocol: string;
  /** Organization URL hostname */
  hostname: string;
  /** Organization URL hostname */
  port: string;
  /** Organization URL virtual directory */
  virtualDirectory: string;
  /** Organization name */
  organization: string;
  /** Project name */
  project: string;
  /** Repository name */
  repository: string;
  /** Whether the repository was overridden via input */
  repositoryOverridden: boolean;

  /** Organisation API endpoint URL */
  apiEndpointUrl: string;

  /** The github token */
  githubAccessToken: string;
  /** The access User for Azure DevOps Repos */
  systemAccessUser: string;
  /** The access token for Azure DevOps Repos */
  systemAccessToken: string;

  authorEmail?: string;
  authorName?: string;

  storeDependencyList: boolean;
  
  /** Determines if the pull requests that dependabot creates should have auto complete set */
  setAutoComplete: boolean;
  /** Merge strategies which can be used to complete a pull request */
  mergeStrategy: string;
  /** List of any policy configuration Id's which auto-complete should not wait for */
  autoCompleteIgnoreConfigIds: number[];

  /** Determines if the pull requests that dependabot creates should be automatically approved */
  autoApprove: boolean;
  /** A personal access token of the user that should approve the PR */
  autoApproveUserToken: string;

  experiments: Record<string, string | boolean>;

  /** Determines if verbose log messages are logged */
  debug: boolean;

  /** List of update identifiers to run */
  targetUpdateIds: number[];

  securityAdvisoriesFile: string | undefined;

  /** Determines whether to skip creating/updating pull requests */
  skipPullRequests: boolean;
  /** Determines whether to comment on pull requests which an explanation of the reason for closing */
  commentPullRequests: boolean;
  /** Determines whether to abandon unwanted pull requests */
  abandonUnwantedPullRequests: boolean;
}

/**
 * Extract shared variables
 *
 * @returns shared variables
 */
export default function getSharedVariables(): ISharedVariables {
  let organizationUrl = tl.getVariable('System.TeamFoundationCollectionUri');
  
  //convert url string into a valid JS URL object
  let formattedOrganizationUrl = new URL(organizationUrl);
  let protocol: string = formattedOrganizationUrl.protocol.slice(0, -1);
  let hostname: string = extractHostname(formattedOrganizationUrl);
  let port: string = formattedOrganizationUrl.port;
  let virtualDirectory: string = extractVirtualDirectory(formattedOrganizationUrl);
  let organization: string = extractOrganization(organizationUrl);
  let project: string = encodeURI(tl.getVariable('System.TeamProject')); // encode special characters like spaces
  let repository: string = tl.getInput('targetRepositoryName');
  let repositoryOverridden = typeof repository === 'string';
  if (!repositoryOverridden) {
    tl.debug('No custom repository provided. The Pipeline Repository Name shall be used.');
    repository = tl.getVariable('Build.Repository.Name');
  }
  repository = encodeURI(repository); // encode special characters like spaces

  const virtualDirectorySuffix = virtualDirectory?.length > 0 ? `${virtualDirectory}/` : '';
  let apiEndpointUrl = `${protocol}://${hostname}:${port}/${virtualDirectorySuffix}`;

  // Prepare the access credentials
  let githubAccessToken: string = getGithubAccessToken();
  let systemAccessUser: string = tl.getInput('azureDevOpsUser');
  let systemAccessToken: string = getAzureDevOpsAccessToken();

  let authorEmail: string | undefined = tl.getInput('authorEmail');
  let authorName: string | undefined = tl.getInput('authorName');

  // Prepare variables for auto complete
  let setAutoComplete = tl.getBoolInput('setAutoComplete', false);
  let mergeStrategy = tl.getInput('mergeStrategy', true);
  let autoCompleteIgnoreConfigIds = tl.getDelimitedInput('autoCompleteIgnoreConfigIds', ';', false).map(Number);

  let storeDependencyList = tl.getBoolInput('storeDependencyList', false);

  // Prepare variables for auto approve
  let autoApprove: boolean = tl.getBoolInput('autoApprove', false);
  let autoApproveUserToken: string = tl.getInput('autoApproveUserToken');

  // Convert experiments from comma separated key value pairs to a record
  let experiments = tl.getInput('experiments', false)?.split(',')?.reduce(
    (acc, cur) => {
      let [key, value] = cur.split('=', 2);
      acc[key] = value || true;
      return acc;
    }, 
    {} as Record<string, string | boolean>
  );

  let debug: boolean = tl.getVariable('System.Debug')?.localeCompare('true') === 0;

  // Get the target identifiers
  let targetUpdateIds = tl.getDelimitedInput('targetUpdateIds', ';', false).map(Number);

  // Prepare other variables
  let securityAdvisoriesFile: string | undefined = tl.getInput('securityAdvisoriesFile');
  let skipPullRequests: boolean = tl.getBoolInput('skipPullRequests', false);
  let commentPullRequests: boolean = tl.getBoolInput('commentPullRequests', false);
  let abandonUnwantedPullRequests: boolean = tl.getBoolInput('abandonUnwantedPullRequests', true);

  return {
    organizationUrl: formattedOrganizationUrl,
    protocol,
    hostname,
    port,
    virtualDirectory,
    organization,
    project,
    repository,
    repositoryOverridden,

    apiEndpointUrl,

    githubAccessToken,
    systemAccessUser,
    systemAccessToken,

    authorEmail,
    authorName,
    
    storeDependencyList,

    setAutoComplete,
    mergeStrategy,
    autoCompleteIgnoreConfigIds,

    autoApprove,
    autoApproveUserToken,

    experiments,

    debug,

    targetUpdateIds,
    securityAdvisoriesFile,

    skipPullRequests,
    commentPullRequests,
    abandonUnwantedPullRequests,
  };
}
