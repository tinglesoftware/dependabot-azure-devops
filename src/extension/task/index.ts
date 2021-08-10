import tl = require("azure-pipelines-task-lib/task");
import tr = require("azure-pipelines-task-lib/toolrunner");
import { IDependabotUpdate } from "./models/IDependabotUpdate";
import extractOrganization from "./utils/extractOrganization";
import extractVirtualDirectory from "./utils/extractVirtualDirectory";
import getAzureDevOpsAccessToken from "./utils/getAzureDevOpsAccessToken";
import getConfigFromInputs from "./utils/getConfigFromInputs";
import getGithubAccessToken from "./utils/getGithubAccessToken";
import getTargetRepository from "./utils/getTargetRepository";
import parseConfigFile from "./utils/parseConfigFile";

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
    let githubAccessToken: string= getGithubAccessToken(); 

    // Prepare the access token for Azure DevOps Repos.
    let systemAccessToken: string = getAzureDevOpsAccessToken()

    // Prepare the repository
    let repository: string = getTargetRepository();

    // Get the override values for allow and ignore
    let allowOvr = tl.getVariable("DEPENDABOT_ALLOW_CONDITIONS");
    let ignoreOvr = tl.getVariable("DEPENDABOT_IGNORE_CONDITIONS");

    // Check if to use dependabot.yml or task inputs
    let useConfigFile: boolean = tl.getBoolInput("useConfigFile", false);
    var updates: IDependabotUpdate[];

    if (useConfigFile) updates = parseConfigFile();
    else updates = getConfigFromInputs();

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
      tl.getDelimitedInput("extraEnvironmentVariables", ";", false).forEach(extraEnvVar => {
        dockerRunner.arg(["-e", extraEnvVar]);
      });

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
