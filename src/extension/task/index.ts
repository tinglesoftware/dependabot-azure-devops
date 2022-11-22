import * as tl from "azure-pipelines-task-lib/task"
import { ToolRunner } from "azure-pipelines-task-lib/toolrunner"
import { IDependabotUpdate } from "./models/IDependabotUpdate";
import getConfigFromInputs from "./utils/getConfigFromInputs";
import getSharedVariables from "./utils/getSharedVariables";
import parseConfigFile from "./utils/parseConfigFile";

async function run() {
  try {
    // Checking if docker is installed
    tl.debug("Checking for docker install ...");
    tl.which("docker", true);

    // prepare the shared variables
    const variables = getSharedVariables();

    var updates: IDependabotUpdate[];

    if (variables.useConfigFile) updates = parseConfigFile();
    else {
      tl.warning(
        `
        Using explicit inputs instead of a configuration file is deprecated and will be removed in version 0.11.0.\r\n
        No new features will be added to the use of explicit inputs that can also be specified in the configuration file.\r\n\r\n
        Migrate to using a config file at .github/dependabot.yml.\r\n
        See https://github.com/tinglesoftware/dependabot-azure-devops/tree/main/src/extension#usage for more information.
        `
      );
      updates = getConfigFromInputs();
    }

    if (variables.useConfigFile && tl.getInput("targetRepositoryName")) {
      tl.warning(
        `
        Using targetRepositoryName input does not work when useConfigFile is set to true.
        Either use a pipeline for each repository or consider using the [managed version](https://managed-dependabot.com).
        Using targetRepositoryName will be deprecated and removed in a future minor release.\r\n
        `
      );
    }

    // For each update run docker container
    for (const update of updates) {
      // Prepare the docker task
      let dockerRunner: ToolRunner = tl.tool(tl.which("docker", true));
      dockerRunner.arg(["run"]); // run command
      dockerRunner.arg(["--rm"]); // remove after execution
      dockerRunner.arg(["-i"]); // attach pseudo tty

      // Set env variables in the runner
      dockerRunner.arg(["-e", `DEPENDABOT_PACKAGE_MANAGER=${update.packageEcosystem}`]);
      dockerRunner.arg(["-e", `DEPENDABOT_FAIL_ON_EXCEPTION=${variables.failOnException}`]); // Set exception behaviour
      dockerRunner.arg(["-e", `DEPENDABOT_EXCLUDE_REQUIREMENTS_TO_UNLOCK=${variables.excludeRequirementsToUnlock}`]);
      dockerRunner.arg(["-e", `AZURE_PROTOCOL=${variables.protocol}`]);
      dockerRunner.arg(["-e", `AZURE_HOSTNAME=${variables.hostname}`]);
      dockerRunner.arg(["-e", `AZURE_ORGANIZATION=${variables.organization}`]); // Set the organization
      dockerRunner.arg(["-e", `AZURE_PROJECT=${variables.project}`]); // Set the project
      dockerRunner.arg(["-e", `AZURE_REPOSITORY=${variables.repository}`]);
      dockerRunner.arg(["-e", `AZURE_ACCESS_TOKEN=${variables.systemAccessToken}`]);
      dockerRunner.arg(["-e", `AZURE_SET_AUTO_COMPLETE=${variables.setAutoComplete}`]); // Set auto complete, if set
      dockerRunner.arg(["-e", `AZURE_MERGE_STRATEGY=${variables.mergeStrategy}`]);

      // Set Username
      if (variables.systemAccessUser) {
        dockerRunner.arg(["-e", `AZURE_ACCESS_USERNAME=${variables.systemAccessUser}`]);
      }

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

      // Set the milestone, if provided
      if (update.milestone) {
        dockerRunner.arg(["-e", `DEPENDABOT_MILESTONE=${update.milestone}`]);
      }

      // Set the dependencies to allow
      let allow = update.allow || variables.allowOvr;
      if (allow) {
        dockerRunner.arg(["-e", `DEPENDABOT_ALLOW_CONDITIONS=${allow}`]);
      }

      // Set the dependencies to ignore
      let ignore = update.ignore || variables.ignoreOvr;
      if (ignore) {
        dockerRunner.arg(["-e", `DEPENDABOT_IGNORE_CONDITIONS=${ignore}`]);
      }

      // Set the custom labels/tags
      let labels = update.labels || variables.labelsOvr;
      if (labels) {
        dockerRunner.arg(["-e", `DEPENDABOT_LABELS=${labels}`]);
      }

      // Set the PR branch separator
      if (update.branchNameSeparator) {
        dockerRunner.arg(["-e", `DEPENDABOT_BRANCH_NAME_SEPARATOR=${update.branchNameSeparator}`]);
      }

      // Set the extra credentials
      if (variables.extraCredentials) {
        dockerRunner.arg(["-e", `DEPENDABOT_EXTRA_CREDENTIALS=${variables.extraCredentials}`]);
      }

      // Set the github token, if one is provided
      if (variables.githubAccessToken) {
        dockerRunner.arg(["-e", `GITHUB_ACCESS_TOKEN=${variables.githubAccessToken}`]);
      }

      // Set the port
      if (variables.port && variables.port !== "") {
        dockerRunner.arg(["-e", `AZURE_PORT=${variables.port}`]);
      }

      // Set the virtual directory
      if (variables.virtualDirectory !== "") {
        dockerRunner.arg(["-e", `AZURE_VIRTUAL_DIRECTORY=${variables.virtualDirectory}`]);
      }

      // Set auto complete
      dockerRunner.arg(["-e", `AZURE_AUTO_APPROVE_PR=${variables.autoApprove}`]);
      if (variables.autoApproveUserEmail) {
        dockerRunner.arg(["-e", `AZURE_AUTO_APPROVE_USER_EMAIL=${variables.autoApproveUserEmail}`]);
      }
      if (variables.autoApproveUserToken) {
        dockerRunner.arg(["-e", `AZURE_AUTO_APPROVE_USER_TOKEN=${variables.autoApproveUserToken}`]);
      }

      // Add in extra environment variables
      variables.extraEnvironmentVariables.forEach(extraEnvVar => {
        dockerRunner.arg(["-e", extraEnvVar]);
      });

      // Forward the host ssh socket
      if (variables.forwardHostSshSocket) {
        dockerRunner.arg(['-e', 'SSH_AUTH_SOCK=/ssh-agent']);
        dockerRunner.arg(['--volume', '${SSH_AUTH_SOCK}:/ssh-agent']);
      }

      // Form the docker image based on the repository and the tag, e.g. tingle/dependabot-azure-devops
      // For custom/enterprise registries, prefix with the registry, e.g. contoso.azurecr.io/tingle/dependabot-azure-devops
      let dockerImage : string = `${variables.dockerImageRepository}:${variables.dockerImageTag}`;
      if (variables.dockerImageRegistry) {
        dockerImage = `${variables.dockerImageRegistry}/${dockerImage}`.replace("//", "/");
      }

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
