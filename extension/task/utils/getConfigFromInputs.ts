import { getInput } from "azure-pipelines-task-lib/task";
import { IDependabotConfig } from "../IDependabotConfig";

/**
 * Get dependency update configuration from inputs provided in the task
 *
 * To view YAML file format, visit
 * https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#allow
 *
 * @returns {IDependabotConfig} update configuration
 */
export default function getConfigFromInputs() : IDependabotConfig{
  var dependabotConfig: IDependabotConfig = {
    version: 2,
    updates: [{
      packageEcosystem: getInput("packageManager", true),
      directory: getInput("directory", false),

      openPullRequestLimit: parseInt(getInput("openPullRequestsLimit", true)),

      targetBranch: getInput("targetBranch", false),
      versioningStrategy: getInput("versioningStrategy", true),
      milestone: getInput("milestone"),
    }]
  };

  return dependabotConfig;
}
