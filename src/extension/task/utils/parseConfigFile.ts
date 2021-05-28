import { IDependabotUpdate } from "../models/IDependabotUpdate";
import { load } from "js-yaml";
import * as fs from "fs";
import * as path from "path";
import { getVariable } from "azure-pipelines-task-lib";

/**
 * Parse the dependabot config YAML file to specify update(s) configuration
 *
 * The file should be located in '/.azuredevops/dependabot.yml' at the root of your repository
 *
 * To view YAML file format, visit
 * https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#allow
 *
 * @returns {IDependabotUpdate[]} updates - array of dependency update configurations
 */
export default function parseConfigFile(): IDependabotUpdate[] {
  var filePath = path.join(
    getVariable("Build.SourcesDirectory"),
    "/.azuredevops/dependabot.yml"
  );

  let config: string | number | object;
  config = load(fs.readFileSync(filePath, "utf-8"));

  // ensure the config object parsed is an object
  if (config === null || typeof config !== "object") {
    throw new Error("Invalid dependabot config object");
  }

  var updates: IDependabotUpdate[] = [];

  //check the updates parsed
  var rawUpdates = config["updates"];

  //check if the array of updates exists
  if (!Array.isArray(rawUpdates))
    throw new Error(
      "Invalid dependabot config object: Dependency updates config array not found"
    );

  // parse the value of each of the updates obtained from the file
  rawUpdates.forEach((update) => {
    var dependabotUpdate: IDependabotUpdate = {
      packageEcosystem: update["package-ecosystem"],
      directory: update["directory"],

      openPullRequestLimit: update["open-pull-requests-limit"] || 5,

      targetBranch: update["target-branch"],
      versioningStrategy: update["versioning-strategy"],

      // Convert to JSON and shorten the names as required by the script
      allow: updates["allow"] ? JSON.stringify(updates["allow"]) : undefined,
      ignore: updates["ignore"] ? JSON.stringify(updates["ignore"]) : undefined,
    };

    if (!dependabotUpdate.packageEcosystem) {
      throw new Error(
        "The value 'package-ecosystem' in dependency update config is missing"
      );
    }

    if (!dependabotUpdate.directory) {
      throw new Error(
        "The value 'directory' in dependency update config is missing"
      );
    }

    updates.push(dependabotUpdate);
  });

  return updates;
}
