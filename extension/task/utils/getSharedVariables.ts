import * as tl from "azure-pipelines-task-lib/task";
import extractHostname from "./extractHostname";
import extractOrganization from "./extractOrganization";
import extractVirtualDirectory from "./extractVirtualDirectory";
import getAzureDevOpsAccessToken from "./getAzureDevOpsAccessToken";
import getDockerImageTag from "./getDockerImageTag";
import getGithubAccessToken from "./getGithubAccessToken";

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

  /** Flag used to check if to use dependabot.yml or task inputs */
  useConfigFile: boolean;
  /** override value for allow */
  allowOvr: string; // TODO: remove this in 0.16.0

  securityAdvisoriesFile: string | undefined;
  /** Determines whether to skip creating/updating pull requests */
  skipPullRequests: boolean;
  /** Determines whether to abandon unwanted pull requests */
  abandonUnwantedPullRequests: boolean;
  /** List of extra environment variables */
  extraEnvironmentVariables: string[];
  /** Flag used to forward the host ssh socket */
  forwardHostSshSocket: boolean;

  /** Registry of the docker image to be pulled */
  dockerImageRegistry: string | undefined;
  /** Repository of the docker image to be pulled */
  dockerImageRepository: string;
  /** Tag of the docker image to be pulled */
  dockerImageTag: string;
}

/**
 * Extract shared variables
 *
 * @returns shared variables
 */
export default function getSharedVariables(): ISharedVariables {
  // Prepare shared variables
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

  // Check if to use dependabot.yml or task inputs
  let useConfigFile: boolean = tl.getBoolInput("useConfigFile", false);

  // Get the override values for allow, and ignore
  let allowOvr = tl.getVariable("DEPENDABOT_ALLOW_CONDITIONS");

  // Prepare other variables
  let securityAdvisoriesFile: string | undefined = tl.getInput(
    "securityAdvisoriesFile"
  );
  let skipPullRequests: boolean = tl.getBoolInput("skipPullRequests", false);
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
  let dockerImageRegistry: string | undefined = tl.getInput(
    "dockerImageRegistry"
  );
  let dockerImageRepository: string = tl.getInput(
    "dockerImageRepository",
    true
  );
  let dockerImageTag: string = getDockerImageTag();

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

    allowOvr,
    useConfigFile,

    securityAdvisoriesFile,
    skipPullRequests,
    abandonUnwantedPullRequests,
    extraEnvironmentVariables,
    forwardHostSshSocket,

    dockerImageRegistry,
    dockerImageRepository,
    dockerImageTag,
  };
}
