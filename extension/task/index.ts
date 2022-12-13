import * as tl from "azure-pipelines-task-lib/task"
import { ToolRunner } from "azure-pipelines-task-lib/toolrunner"
import { IDependabotConfig } from "./IDependabotConfig";
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

    var config: IDependabotConfig;
    if (variables.useConfigFile) {
      config = parseConfigFile();
    } else {
      tl.warning(
        `
        Using explicit inputs instead of a configuration file is deprecated and will be removed in version 0.13.0.
        No new features will be added to the use of explicit inputs that can also be specified in the configuration file.\r\n
        Migrate to using a config file at .github/dependabot.yml.
        See https://github.com/tinglesoftware/dependabot-azure-devops/tree/main/extension#usage for more information.
        `
      );
      config = getConfigFromInputs();
    }

    if (variables.useConfigFile && tl.getInput("targetRepositoryName")) {
      tl.warning(
        `
        Using targetRepositoryName input does not work when useConfigFile is set to true.
        Either use a pipeline for each repository or consider using the [managed version](https://managed-dependabot.com).
        Using targetRepositoryName will be deprecated and removed in a future minor release.
        `
      );
    }

    // For each update run docker container
    for (const update of config.updates) {
      // Prepare the docker task
      let dockerRunner: ToolRunner = tl.tool(tl.which("docker", true));
      dockerRunner.arg(["run"]); // run command
      dockerRunner.arg(["--rm"]); // remove after execution
      dockerRunner.arg(["-i"]); // attach pseudo tty

      // Set the github token, if one is provided
      if (variables.githubAccessToken) {
        dockerRunner.arg(["-e", `GITHUB_ACCESS_TOKEN=${variables.githubAccessToken}`]);
      }

      /*
       * Set env variables in the runner for Dependabot
       */
      dockerRunner.arg(["-e", `DEPENDABOT_PACKAGE_MANAGER=${update.packageEcosystem}`]);

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

      // Set the PR branch separator
      if (update.branchNameSeparator) {
        dockerRunner.arg(["-e", `DEPENDABOT_BRANCH_NAME_SEPARATOR=${update.branchNameSeparator}`]);
      }

      // Set exception behaviour if true
      if (update.rejectExternalCode === true) {
        dockerRunner.arg(["-e", 'DEPENDABOT_REJECT_EXTERNAL_CODE=true']);
      }

      // Set the dependencies to allow
      let allow = update.allow || variables.allowOvr;
      if (allow) {
        dockerRunner.arg(["-e", `DEPENDABOT_ALLOW_CONDITIONS=${allow}`]);
      }

      // Set the requirements that should not be unlocked
      if (variables.excludeRequirementsToUnlock) {
        dockerRunner.arg(["-e", `DEPENDABOT_EXCLUDE_REQUIREMENTS_TO_UNLOCK=${variables.excludeRequirementsToUnlock}`]);
      }

      // Set the dependencies to ignore only when not using the config file
      if (variables.useConfigFile && variables.ignoreOvr) {
        tl.warning(`Using 'DEPENDABOT_IGNORE_CONDITIONS' is not supported when using a config file. Specify the same values in the .github/dependabot.yml file.`);
      }
      if (!variables.useConfigFile && variables.ignoreOvr) {
        dockerRunner.arg(["-e", `DEPENDABOT_IGNORE_CONDITIONS=${variables.ignoreOvr}`]);
      }

      // Set the custom labels/tags
      if (update.labels) {
        dockerRunner.arg(["-e", `DEPENDABOT_LABELS=${update.labels}`]);
      }

      // Set the reviewers
      if (update.reviewers) {
        dockerRunner.arg(["-e", `DEPENDABOT_REVIEWERS=${update.reviewers}`]);
      }

      // Set the assignees
      if (update.assignees) {
        dockerRunner.arg(["-e", `DEPENDABOT_ASSIGNEES=${update.assignees}`]);
      }

      // Set the updater options, if provided
      if (variables.updaterOptions) {
        dockerRunner.arg(["-e", `DEPENDABOT_UPDATER_OPTIONS=${variables.updaterOptions}`]);
      }

      // Set the extra credentials
      if (variables.useConfigFile && variables.extraCredentials) {
        tl.warning(`Using 'DEPENDABOT_EXTRA_CREDENTIALS' is not recommended when using a config file. Specify the same values in the registries section of .github/dependabot.yml file.`);
      }
      if (variables.extraCredentials) {
        //TODO remove variables.extraCredentials in future in favor default yml configuration.
        if (variables.extraCredentials.length > 0 && variables.extraCredentials !== '[]') {
          dockerRunner.arg(["-e", `DEPENDABOT_EXTRA_CREDENTIALS=${variables.extraCredentials}`]);
        }
      } else if (config.registries != undefined) {
        if (config.registries.length > 0) {
          let extraCredentials = JSON.stringify(config.registries);
          dockerRunner.arg(["-e", `DEPENDABOT_EXTRA_CREDENTIALS=${extraCredentials}`]);
        }
      }

      // Set exception behaviour if true
      if (variables.failOnException === true) {
        dockerRunner.arg(["-e", 'DEPENDABOT_FAIL_ON_EXCEPTION=true']);
      }

      // Set skip pull requests if true
      if (variables.skipPullRequests === true) {
        dockerRunner.arg(["-e", 'DEPENDABOT_SKIP_PULL_REQUESTS=true']);
      }

      // Set the security advisories
      if (variables.securityAdvisoriesEnabled) {
        if (variables.securityAdvisoriesJson) { // TODO: remove this once we migrate fully to files
          dockerRunner.arg(["-e", `DEPENDABOT_SECURITY_ADVISORIES_JSON=${variables.securityAdvisoriesJson}`]);
        }
        else if (variables.securityAdvisoriesFile) { // TODO: remove this once we have files on CDN/GitHub repo
          const containerPath = "/mnt/security_advisories.json"
          dockerRunner.arg(['--mount', `type=bind,source=${variables.securityAdvisoriesFile},target=${containerPath}`]);
          dockerRunner.arg(["-e", `DEPENDABOT_SECURITY_ADVISORIES_FILE=${containerPath}`]);
        } else {
          // TODO: consider downloading a file from Azure CDN for the current ecosystem
          // For example:
          // download from https://contoso.azureedge.net/security_advisories/nuget.json
          //            to $(Pipeline.Workspace)/security_advisories/nuget.json
          //
          // Then pass this via a mount and ENV
        }
      }

      /*
       * Set env variables in the runner for Azure
       */
      dockerRunner.arg(["-e", `AZURE_ORGANIZATION=${variables.organization}`]); // Set the organization
      dockerRunner.arg(["-e", `AZURE_PROJECT=${variables.project}`]); // Set the project
      dockerRunner.arg(["-e", `AZURE_REPOSITORY=${variables.repository}`]);
      dockerRunner.arg(["-e", `AZURE_ACCESS_TOKEN=${variables.systemAccessToken}`]);
      dockerRunner.arg(["-e", `AZURE_MERGE_STRATEGY=${variables.mergeStrategy}`]);

      // Set Username
      if (variables.systemAccessUser) {
        dockerRunner.arg(["-e", `AZURE_ACCESS_USERNAME=${variables.systemAccessUser}`]);
      }

      // Set the protocol if not the default value
      if (variables.protocol !== 'https') {
        dockerRunner.arg(["-e", `AZURE_PROTOCOL=${variables.protocol}`]);
      }

      // Set the host name if not the default value
      if (variables.hostname !== "dev.azure.com") {
        dockerRunner.arg(["-e", `AZURE_HOSTNAME=${variables.hostname}`]);
      }

      // Set auto complete, if set
      if (variables.setAutoComplete === true) {
        dockerRunner.arg(["-e", 'AZURE_SET_AUTO_COMPLETE=true']);

        // Set the ignore config IDs for auto complete if not the default value
        if (variables.autoCompleteIgnoreConfigIds.length > 0) {
          dockerRunner.arg(["-e", `AZURE_AUTO_COMPLETE_IGNORE_CONFIG_IDS=${JSON.stringify(variables.autoCompleteIgnoreConfigIds)}`]);
        }
      }

      // Set the port
      if (variables.port && variables.port !== "") {
        dockerRunner.arg(["-e", `AZURE_PORT=${variables.port}`]);
      }

      // Set the virtual directory
      if (variables.virtualDirectory !== "") {
        dockerRunner.arg(["-e", `AZURE_VIRTUAL_DIRECTORY=${variables.virtualDirectory}`]);
      }

      // Set auto approve
      if (variables.autoApprove === true) {
        dockerRunner.arg(["-e", 'AZURE_AUTO_APPROVE_PR=true']);

        // Set the email to use for auto approve if provided
        if (variables.autoApproveUserEmail) {
          dockerRunner.arg(["-e", `AZURE_AUTO_APPROVE_USER_EMAIL=${variables.autoApproveUserEmail}`]);
        }

        // Set the token to use for auto approve if provided
        if (variables.autoApproveUserToken) {
          dockerRunner.arg(["-e", `AZURE_AUTO_APPROVE_USER_TOKEN=${variables.autoApproveUserToken}`]);
        }
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
      let dockerImage: string = `${variables.dockerImageRepository}:${variables.dockerImageTag}`;
      if (variables.dockerImageRegistry) {
        dockerImage = `${variables.dockerImageRegistry}/${dockerImage}`.replace("//", "/");
      }

      tl.debug(`Running docker container -> '${dockerImage}' ...`);
      dockerRunner.arg(dockerImage);

      // Now execute using docker
      await dockerRunner.exec();
    }

    tl.debug("Docker container execution completed!");
  } catch (err) {
    tl.setResult(tl.TaskResult.Failed, err.message);
  }
}

run();
