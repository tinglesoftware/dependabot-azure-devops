import { getVariable, getBoolInput, getInput } from "azure-pipelines-task-lib";
import extractOrganization from "./extractOrganization";
import extractVirtualDirectory from "./extractVirtualDirectory";
import getAzureDevOpsAccessToken from "./getAzureDevOpsAccessToken";
import getGithubAccessToken from "./getGithubAccessToken";
import getTargetRepository from "./getTargetRepository";

interface ISharedVariables {
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
  project: string;
  setAutoComplete: boolean;
  failOnException: boolean;
  excludeRequirementsToUnlock: string;
  autoApprove: boolean;
  autoApproveUserEmail: string;
  autoApproveUserToken: string;
  extraCredentials: string;
  dockerImageTag: string;
  /** the github token */
  githubAccessToken: string;
  /** the access token for Azure DevOps Repos */
  systemAccessToken: string;
  /** Dependabot's target repository */
  repository: string;
  /** override value for allow */
  allowOvr: string;
  /** override value for ignore */
  ignoreOvr: string;
  /** Flag used to check if to use dependabot.yml or task inputs */
  useConfigFile: boolean;
  /** Semicolon delimited list of environment variables */
  extraEnvironmentVariables: string[];
}

/**
 * Extract shared variables
 *
 * @returns shared variables
 */
export default function getSharedVariables(): ISharedVariables {
  // Prepare shared variables
  let organizationUrl = getVariable("System.TeamFoundationCollectionUri");
  let parsedUrl = new URL(organizationUrl);
  let protocol: string = parsedUrl.protocol.slice(0, -1);
  let hostname: string = parsedUrl.hostname;
  let port: string = parsedUrl.port;
  let virtualDirectory: string = extractVirtualDirectory(parsedUrl);
  let organization: string = extractOrganization(organizationUrl);
  let project: string = encodeURI(getVariable("System.TeamProject")); // encode special characters like spaces
  let setAutoComplete = getBoolInput("setAutoComplete", false);
  let failOnException = getBoolInput("failOnException", true);
  let excludeRequirementsToUnlock =
    getInput("excludeRequirementsToUnlock") || "";
  let autoApprove: boolean = getBoolInput("autoApprove", false);
  let autoApproveUserEmail: string = getInput("autoApproveUserEmail");
  let autoApproveUserToken: string = getInput("autoApproveUserToken");
  let extraCredentials = getVariable("DEPENDABOT_EXTRA_CREDENTIALS");
  let dockerImageTag: string = getInput("dockerImageTag"); // TODO: get the latest version to use from a given url

  // Prepare the github token, if one is provided
  let githubAccessToken: string = getGithubAccessToken();

  // Prepare the access token for Azure DevOps Repos.
  let systemAccessToken: string = getAzureDevOpsAccessToken();

  // Prepare the repository
  let repository: string = getTargetRepository();

  // Get the override values for allow and ignore
  let allowOvr = getVariable("DEPENDABOT_ALLOW_CONDITIONS");
  let ignoreOvr = getVariable("DEPENDABOT_IGNORE_CONDITIONS");

  // Check if to use dependabot.yml or task inputs
  let useConfigFile: boolean = getBoolInput("useConfigFile", false);

  // prepare extra env variables
  let extraEnvironmentVariables = getDelimitedInput(
    "extraEnvironmentVariables",
    ";",
    false
  );

  return {
    protocol,
    hostname,
    port,
    virtualDirectory,
    organization,
    project,
    setAutoComplete,
    failOnException,
    excludeRequirementsToUnlock,
    autoApprove,
    autoApproveUserEmail,
    autoApproveUserToken,
    extraCredentials,
    dockerImageTag,
    githubAccessToken,
    systemAccessToken,
    repository,
    allowOvr,
    ignoreOvr,
    useConfigFile,
    extraEnvironmentVariables,
  };
}
