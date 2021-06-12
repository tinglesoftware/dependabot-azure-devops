import tl = require("azure-pipelines-task-lib/task");
import tr = require("azure-pipelines-task-lib/toolrunner");
import { IDependabotUpdate } from "./models/IDependabotUpdate";
import getConfigFromInputs from "./utils/getConfigFromInputs";
import parseConfigFile from "./utils/parseConfigFile";

function getGithubEndPointToken(githubEndpoint: string): string {
  const githubEndpointObject = tl.getEndpointAuthorization(
    githubEndpoint,
    false
  );
  let githubEndpointToken: string = null;

  if (!!githubEndpointObject) {
    tl.debug("Endpoint scheme: " + githubEndpointObject.scheme);

    if (githubEndpointObject.scheme === "PersonalAccessToken") {
      githubEndpointToken = githubEndpointObject.parameters.accessToken;
    } else if (githubEndpointObject.scheme === "OAuth") {
      githubEndpointToken = githubEndpointObject.parameters.AccessToken;
    } else if (githubEndpointObject.scheme === "Token") {
      githubEndpointToken = githubEndpointObject.parameters.AccessToken;
    } else if (githubEndpointObject.scheme) {
      throw new Error(
        tl.loc("InvalidEndpointAuthScheme", githubEndpointObject.scheme)
      );
    }
  }

  if (!githubEndpointToken) {
    throw new Error(tl.loc("InvalidGitHubEndpoint", githubEndpoint));
  }

  return githubEndpointToken;
}

function extractVirtualDirectory(organizationUrl: URL): string {
  let path = organizationUrl.pathname.split("/");
  // Virtual Directories are sometimes used in on-premises
  // URLs tipically are like this: https://server.domain.com/tfs/x/
  if (path.length == 4) {
    return path[1];
  }
  return "";
}

function extractOrganization(organizationUrl: string): string {
  let parts = organizationUrl.split("/");

  // Check for on-premise style: https://server.domain.com/tfs/x/
  if (parts.length === 6) {
    return parts[4];
  }

  // Check for new style: https://dev.azure.com/x/
  if (parts.length === 5) {
    return parts[3];
  }

  // Check for old style: https://x.visualstudio.com/
  if (parts.length === 4) {
    // Get x.visualstudio.com part.
    let part = parts[2];

    // Return organization part (x).
    return part.split(".")[0];
  }

  tl.setResult(
    tl.TaskResult.Failed,
    `Error parsing organization from organization url: '${organizationUrl}'.`
  );
}

function extractExtraEnvironmentVariables(rawExtraEnvironmentVariables: string[]): Map<string, string> {
  let formattedEnvironmentVariables = new Map<string, string>();

  // With how this is set up later environment variables could overwrite earlier environment variables but that is on the user
  for (const extraEnvironmentVariable in rawExtraEnvironmentVariables) {
    let environmentVariableParts = extraEnvironmentVariable.split("=");

    if (environmentVariableParts.length === 2) {
      // Add both the given name and value to the formatted list
      formattedEnvironmentVariables.set(environmentVariableParts[0], environmentVariableParts[1]);
    } else if (environmentVariableParts.length === 1) {
      // Treat the single argument as the name and push an empty string as the value
      formattedEnvironmentVariables.set(environmentVariableParts[0], "");
    }
  }

  return formattedEnvironmentVariables;
}

async function run() {
  try {
    // Checking if docker is installed
    tl.debug("Checking for docker install ...");
    tl.which("docker", true);

    // Prepare shared variables
    let organizationUrl = tl.getVariable("System.TeamFoundationCollectionUri");
    let parsedUrl = new URL(organizationUrl);
    let protocol: string = parsedUrl.protocol.slice(0, -1);
    let hostname: string = parsedUrl.hostname;
    let port: string = parsedUrl.port;
    let virtualDirectory: string = extractVirtualDirectory(parsedUrl);
    let organization: string = extractOrganization(organizationUrl);
    let project: string = encodeURI(tl.getVariable("System.TeamProject")); // encode special characters like spaces
    let setAutoComplete = tl.getBoolInput('setAutoComplete', false);
    let failOnException = tl.getBoolInput("failOnException", true);
    let excludeRequirementsToUnlock = tl.getInput('excludeRequirementsToUnlock') || "";
    let autoApprove: boolean = tl.getBoolInput('autoApprove', false);
    let autoApproveUserEmail: string = tl.getInput("autoApproveUserEmail");
    let autoApproveUserToken: string = tl.getInput("autoApproveUserToken");
    let extraCredentials = tl.getVariable("DEPENDABOT_EXTRA_CREDENTIALS");
    let dockerImageTag: string = tl.getInput('dockerImageTag'); // TODO: get the latest version to use from a given url

    // Prepare the github token, if one is provided
    let githubAccessToken: string= tl.getInput("gitHubAccessToken");
    if (!githubAccessToken) {
      const githubEndpointId = tl.getInput("gitHubConnection");
      if (githubEndpointId) {
        tl.debug("GitHub connection supplied. A token shall be extracted from it.");
        githubAccessToken = getGithubEndPointToken(githubEndpointId);
      }
    }

    // Prepare the access token for Azure DevOps Repos.
    // If the user has not provided one, we use the one from the SystemVssConnection
    let systemAccessToken: string = tl.getInput("azureDevOpsAccessToken");
    if (!systemAccessToken) {
      tl.debug("No custom token provided. The SystemVssConnection's AccessToken shall be used.");
      systemAccessToken = tl.getEndpointAuthorizationParameter(
        "SystemVssConnection",
        "AccessToken",
        false
      );
    }

    // Prepare the repository
    let repository: string = tl.getInput("targetRepositoryName");
    if (!repository) {
      tl.debug("No custom repository provided. The Pipeline Repository Name shall be used.");
      repository = tl.getVariable("Build.Repository.Name");
    }
    repository = encodeURI(repository); // encode special characters like spaces

    // Get the override values for allow and ignore
    let allowOvr = tl.getVariable("DEPENDABOT_ALLOW_CONDITIONS");
    let ignoreOvr = tl.getVariable("DEPENDABOT_IGNORE_CONDITIONS");

    // Check if to use dependabot.yml or task inputs
    let useConfigFile: boolean = tl.getBoolInput("useConfigFile", false);
    var updates: IDependabotUpdate[];

    if (useConfigFile) updates = parseConfigFile();
    else updates = getConfigFromInputs();

    // Get extraEnvironmentVariable list
    let extraEnvironmentVariables = extractExtraEnvironmentVariables(tl.getDelimitedInput("extraEnvironmentVariable", ";", false));

    // For each update run docker container
    for (const update of updates) {
      // Prepare the docker task
      let dockerRunner: tr.ToolRunner = tl.tool(tl.which("docker", true));
      dockerRunner.arg(["run"]); // run command
      dockerRunner.arg(["--rm"]); // remove after execution
      dockerRunner.arg(["-i"]); // attach pseudo tty

      // Set env variables in the runner
      dockerRunner.arg(["-e", `DEPENDABOT_PACKAGE_MANAGER=${update.packageEcosystem}`]);
      dockerRunner.arg(["-e", `DEPENDABOT_FAIL_ON_EXCEPTION=${failOnException}`]); // Set exception behaviour
      dockerRunner.arg(["-e", `DEPENDABOT_EXCLUDE_REQUIREMENTS_TO_UNLOCK=${excludeRequirementsToUnlock}`]);
      dockerRunner.arg(["-e", `AZURE_PROTOCOL=${protocol}`]);
      dockerRunner.arg(["-e", `AZURE_HOSTNAME=${hostname}`]);
      dockerRunner.arg(["-e", `AZURE_ORGANIZATION=${organization}`]); // Set the organization
      dockerRunner.arg(["-e", `AZURE_PROJECT=${project}`]); // Set the project
      dockerRunner.arg(["-e", `AZURE_REPOSITORY=${repository}`]);
      dockerRunner.arg(["-e", `AZURE_ACCESS_TOKEN=${systemAccessToken}`]);
      dockerRunner.arg(["-e", `AZURE_SET_AUTO_COMPLETE=${setAutoComplete}`]); // Set auto complete, if set

      // Set the directory
      if (update.directory) {
        dockerRunner.arg(["-e", `DEPENDABOT_DIRECTORY=${update.directory}`]);
      }

      // Set the target branch
      if (update.targetBranch) {
        dockerRunner.arg(["-e", `DEPENDABOT_TARGET_BRANCH=${update.targetBranch}`]);
      }

      // Set the versioning strategy
      if (update.versioningStrategy) {
        dockerRunner.arg(["-e", `DEPENDABOT_VERSIONING_STRATEGY=${update.versioningStrategy}`]);
      }
      // Set the open pull requests limit
      if (update.openPullRequestLimit) {
        dockerRunner.arg(["-e", `DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT=${update.openPullRequestLimit}`]);
      }

      // Set the dependencies to allow
      let allow = update.allow || allowOvr;
      if (allow) {
        dockerRunner.arg(["-e", `DEPENDABOT_ALLOW_CONDITIONS=${allow}`]);
      }

      // Set the milestone, if provided
      if (update.milestone) {
        dockerRunner.arg(["-e", `DEPENDABOT_MILESTONE=${update.milestone}`]);
      }

      // Set the dependencies to ignore
      let ignore = update.ignore || ignoreOvr;
      if (ignore) {
        dockerRunner.arg(["-e", `DEPENDABOT_IGNORE_CONDITIONS=${ignore}`]);
      }

      // Set the extra credentials
      if (extraCredentials) {
        dockerRunner.arg(["-e", `DEPENDABOT_EXTRA_CREDENTIALS=${extraCredentials}`]);
      }

      // Set the github token, if one is provided
      if (githubAccessToken) {
        dockerRunner.arg(["-e", `GITHUB_ACCESS_TOKEN=${githubAccessToken}`]);
      }

      // Set the port
      if (port && port !== "") {
        dockerRunner.arg(["-e", `AZURE_PORT=${port}`]);
      }

      // Set the virtual directory
      if (virtualDirectory !== "") {
        dockerRunner.arg(["-e", `AZURE_VIRTUAL_DIRECTORY=${virtualDirectory}`]);
      }

      // Set auto complete
      dockerRunner.arg(["-e", `AZURE_AUTO_APPROVE_PR=${autoApprove}`]);
      if (autoApproveUserEmail) {
        dockerRunner.arg(["-e", `AZURE_AUTO_APPROVE_USER_EMAIL=${autoApproveUserEmail}`]);
      }
      if (autoApproveUserToken) {
        dockerRunner.arg(["-e", `AZURE_AUTO_APPROVE_USER_TOKEN=${autoApproveUserToken}`]);
      }

      // Add in extra environment variables
      for (const [name, value] of extraEnvironmentVariables) {
        dockerRunner.arg(["-e", `${name}=${value}`]);
      }

      const dockerImage = `tingle/dependabot-azure-devops:${dockerImageTag}`;
      tl.debug(`Running docker container -> '${dockerImage}' ...`);
      dockerRunner.arg([dockerImage]);

      // Now execute using docker
      await dockerRunner.exec();
    }

    tl.debug("Docker container execution completed!");
  } catch (err) {
    tl.setResult(tl.TaskResult.Failed, err.message);
  }
}

run();
