import * as tl from "azure-pipelines-task-lib/task";
import extractHostname from "./extractHostname";
import extractOrganization from "./extractOrganization";
import extractVirtualDirectory from "./extractVirtualDirectory";
import getAzureDevOpsAccessToken from "./getAzureDevOpsAccessToken";
import getDockerImageTag from "./getDockerImageTag";
import getGithubAccessToken from "./getGithubAccessToken";

export interface ISharedVariables {
  /** Job ID */
  jobId: string;

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

  /** The github token */
  githubAccessToken: string;
  /** The access User for Azure DevOps Repos */
  systemAccessUser: string;
  /** The access token for Azure DevOps Repos */
  systemAccessToken: string;

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

  /** Determines if the execution should fail when an exception occurs */
  failOnException: boolean;
  excludeRequirementsToUnlock: string;
  updaterOptions: string;

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

  /** List of extra environment variables */
  extraEnvironmentVariables: string[];

  /** Flag used to forward the host ssh socket */
  forwardHostSshSocket: boolean;

  /** Tag of the docker image to be pulled */
  dockerImageTag: string;

  /** Dependabot command to run */
  command: string;
}

/**
 * Extract shared variables
 *
 * @returns shared variables
 */
export default function getSharedVariables(): ISharedVariables {
  let jobId = tl.getInput("jobId", false);

  let organizationUrl = tl.getVariable("System.TeamFoundationCollectionUri");
  //convert url string into a valid JS URL object
  let formattedOrganizationUrl = new URL(organizationUrl);

  let protocol: string = formattedOrganizationUrl.protocol.slice(0, -1);
  let hostname: string = extractHostname(formattedOrganizationUrl);
  let port: string = formattedOrganizationUrl.port;
  let virtualDirectory: string = extractVirtualDirectory(
    formattedOrganizationUrl
  );
  let organization: string = extractOrganization(organizationUrl);
  let project: string = encodeURI(tl.getVariable("System.TeamProject")); // encode special characters like spaces
  let repository: string = tl.getInput("targetRepositoryName");
  let repositoryOverridden = typeof repository === "string";
  if (!repositoryOverridden) {
    tl.debug(
      "No custom repository provided. The Pipeline Repository Name shall be used."
    );
    repository = tl.getVariable("Build.Repository.Name");
  }
  repository = encodeURI(repository); // encode special characters like spaces

  // Prepare the access credentials
  let githubAccessToken: string = getGithubAccessToken();
  let systemAccessUser: string = tl.getInput("azureDevOpsUser");
  let systemAccessToken: string = getAzureDevOpsAccessToken();

  // Prepare variables for auto complete
  let setAutoComplete = tl.getBoolInput("setAutoComplete", false);
  let mergeStrategy = tl.getInput("mergeStrategy", true);
  let autoCompleteIgnoreConfigIds = tl
    .getDelimitedInput("autoCompleteIgnoreConfigIds", ";", false)
    .map(Number);

  // Prepare variables for auto approve
  let autoApprove: boolean = tl.getBoolInput("autoApprove", false);
  let autoApproveUserToken: string = tl.getInput("autoApproveUserToken");

  // Prepare control flow variables
  let failOnException = tl.getBoolInput("failOnException", true);
  let excludeRequirementsToUnlock =
    tl.getInput("excludeRequirementsToUnlock") || "";
  let updaterOptions = tl.getInput("updaterOptions");

  let debug: boolean = tl.getVariable("System.Debug")?.localeCompare("true") === 0;

  // Get the target identifiers
  let targetUpdateIds = tl
    .getDelimitedInput("targetUpdateIds", ";", false)
    .map(Number);

  // Prepare other variables
  let securityAdvisoriesFile: string | undefined = tl.getInput(
    "securityAdvisoriesFile"
  );
  let skipPullRequests: boolean = tl.getBoolInput("skipPullRequests", false);
  let commentPullRequests: boolean = tl.getBoolInput("commentPullRequests", false);
  let abandonUnwantedPullRequests: boolean = tl.getBoolInput("abandonUnwantedPullRequests", true);

  let extraEnvironmentVariables = tl.getDelimitedInput(
    "extraEnvironmentVariables",
    ";",
    false
  );

  let forwardHostSshSocket: boolean = tl.getBoolInput(
    "forwardHostSshSocket",
    false
  );

  // Prepare variables for the docker image to use
  let dockerImageTag: string = getDockerImageTag();

  let command: string = tl.getBoolInput("useUpdateScriptvNext", false)
    ? "update-script"
    : "update-script-vnext";

  return {
    jobId,

    organizationUrl: formattedOrganizationUrl,
    protocol,
    hostname,
    port,
    virtualDirectory,
    organization,
    project,
    repository,
    repositoryOverridden,

    githubAccessToken,
    systemAccessUser,
    systemAccessToken,

    setAutoComplete,
    mergeStrategy,
    autoCompleteIgnoreConfigIds,

    autoApprove,
    autoApproveUserToken,

    failOnException,
    excludeRequirementsToUnlock,
    updaterOptions,
    
    debug,

    targetUpdateIds,
    securityAdvisoriesFile,

    skipPullRequests,
    commentPullRequests,
    abandonUnwantedPullRequests,
    
    extraEnvironmentVariables,

    forwardHostSshSocket,

    dockerImageTag,

    command
  };
}
