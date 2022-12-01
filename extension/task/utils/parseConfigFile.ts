import { IDependabotConfig, IDependabotRegistry, IDependabotUpdate } from "../IDependabotConfig";
import { load } from "js-yaml";
import * as fs from "fs";
import * as path from "path";
import * as tl from "azure-pipelines-task-lib/task"
import { getVariable } from "azure-pipelines-task-lib/task";
import convertPlaceholder from "./convertPlaceholder";

/**
 * Parse the dependabot config YAML file to specify update configuration
 *
 * The file should be located at '/.github/dependabot.yml' or '/.github/dependabot.yaml'
 *
 * To view YAML file format, visit
 * https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#allow
 *
 * @returns {IDependabotConfig} config - the dependabot configuration
 */
export default function parseConfigFile(): IDependabotConfig {

  /*
   * If the file under the .github folder does not exist, check for one under the .azuredevops folder.
   */
  const possibleFilePaths = [
    "/.github/dependabot.yml",
    "/.github/dependabot.yaml",
    "/.azuredevops/dependabot.yml",
    "/.azuredevops/dependabot.yaml",
  ];

  // Find configuration file
  let filePath: string;
  let rootDir = getVariable("Build.SourcesDirectory");
  possibleFilePaths.forEach(fp => {
    var fullPath = path.join(rootDir, fp);
    if (fs.existsSync(fullPath)) {
      filePath = fullPath;
    }
  });

  // Ensure we have the file. Otherwise throw a well readable error.
  if (filePath) {
    tl.debug(`Found configuration file at ${filePath}`);
    if (filePath.includes(".azuredevops/dependabot")) {
      tl.warning(
        `
        The docker container used to run this task checks for a configuration file in the .github folder. Migrate to it.
        Using the .azuredevops folder is deprecated and will be removed in version 0.11.0.

        See https://github.com/tinglesoftware/dependabot-azure-devops#using-a-configuration-file for more information.
        `
      );
    }
  } else {
    throw new Error(`Configuration file not found at possible locations: ${possibleFilePaths.join(', ')}`);
  }

  let config: any;
  config = load(fs.readFileSync(filePath, "utf-8"));

  // Ensure the config object parsed is an object
  if (config === null || typeof config !== "object") {
    throw new Error("Invalid dependabot config object");
  }

  const rawVersion = config["version"];
  let version = -1;

  // Ensure the version has been specified
  if(!!!rawVersion) throw new Error("The version must be specified in dependabot.yml");

  // Try convert the version to integer
  try{
    version = parseInt(rawVersion, 10);
  }
  catch(e) {
    throw new Error("Dependabot version specified must be a valid integer");
  }

  // Ensure the version is == 2
  if(version !== 2) throw new Error("Only version 2 of dependabot is supported. Version specified: " + version);

  var dependabotConfig: IDependabotConfig = {
    version: version,
    updates: parseUpdates(config),
    registries: parseRegistries(config),
  }

  return dependabotConfig;
}


function parseUpdates(config: any) : IDependabotUpdate[] {
  var updates: IDependabotUpdate[] = [];

  // Check the updates parsed
  var rawUpdates = config["updates"];

  // Check if the array of updates exists
  if (!Array.isArray(rawUpdates))
    throw new Error(
      "Invalid dependabot config object: Dependency updates config array not found"
    );

  // Parse the value of each of the updates obtained from the file
  rawUpdates.forEach((update) => {
    var dependabotUpdate: IDependabotUpdate = {
      packageEcosystem: update["package-ecosystem"],
      directory: update["directory"],

      openPullRequestLimit: update["open-pull-requests-limit"] || 5,

      targetBranch: update["target-branch"],
      versioningStrategy: update["versioning-strategy"],
      milestone: update["milestone"],
      branchNameSeparator: update["pull-request-branch-name"] ? update["pull-request-branch-name"]["separator"] : undefined,
      rejectExternalCode: update["insecure-external-code-execution"] === 'deny',

      // Convert to JSON and shorten the names as required by the script
      allow: update["allow"] ? JSON.stringify(update["allow"]) : undefined,
      labels: update["labels"] ? JSON.stringify(update["labels"]) : undefined,
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

function parseRegistries(config: any) : IDependabotRegistry[] {
  var registries : IDependabotRegistry[] = [];

  var rawRegistries = config["registries"];

  if (rawRegistries == undefined)
    return registries;

  // Parse the value of each of the registries obtained from the file
  Object.entries(rawRegistries).forEach((item) => {
    var registryConfigKey = item[0];
    var registryConfig = item[1];
    var dependabotRegistry: IDependabotRegistry = {
      type: registryConfig["type"]?.replace("-", "_"),
      url: registryConfig["url"],

      username: registryConfig["username"],
      password: convertPlaceholder(registryConfig["password"]),
      key: convertPlaceholder(registryConfig["key"]),
      token: convertPlaceholder(registryConfig["token"]),

      "replaces-base": registryConfig["replaces-base"]
    };

    if (!dependabotRegistry.type) {
      throw new Error(
        `The value for 'type' in dependency registry config '${registryConfigKey}' is missing`
      );
    }

    if (!dependabotRegistry.url) {
      throw new Error(
        `The value 'url' in dependency registry config '${registryConfigKey}' is missing`
      );
    }

    registries.push(dependabotRegistry);
  });
  return registries;
}

