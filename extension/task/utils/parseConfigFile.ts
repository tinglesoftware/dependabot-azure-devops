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

  const possibleFilePaths = [
    "/.github/dependabot.yml",
    "/.github/dependabot.yaml",
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
  if (!!!rawVersion) throw new Error("The version must be specified in dependabot.yml");

  // Try convert the version to integer
  try {
    version = parseInt(rawVersion, 10);
  }
  catch (e) {
    throw new Error("Dependabot version specified must be a valid integer");
  }

  // Ensure the version is == 2
  if (version !== 2) throw new Error("Only version 2 of dependabot is supported. Version specified: " + version);

  var dependabotConfig: IDependabotConfig = {
    version: version,
    updates: parseUpdates(config),
    registries: parseRegistries(config),
  }

  return dependabotConfig;
}


function parseUpdates(config: any): IDependabotUpdate[] {
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

      openPullRequestsLimit: update["open-pull-requests-limit"],

      targetBranch: update["target-branch"],
      versioningStrategy: update["versioning-strategy"],
      milestone: update["milestone"],
      branchNameSeparator: update["pull-request-branch-name"] ? update["pull-request-branch-name"]["separator"] : undefined,
      rejectExternalCode: update["insecure-external-code-execution"] === 'deny',

      // Convert to JSON or as required by the script
      allow: update["allow"] ? JSON.stringify(update["allow"]) : undefined,
      labels: update["labels"] ? JSON.stringify(update["labels"]) : undefined,
      reviewers: update["reviewers"] ? JSON.stringify(update["reviewers"]) : undefined,
      assignees: update["assignees"] ? JSON.stringify(update["assignees"]) : undefined,
    };

    if (!dependabotUpdate.packageEcosystem) {
      throw new Error(
        "The value 'package-ecosystem' in dependency update config is missing"
      );
    }

    if (!dependabotUpdate.openPullRequestsLimit) {
      throw new Error(
        "The value 'open-pull-requests-limit' in dependency update config is missing"
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

function parseRegistries(config: any): IDependabotRegistry[] {
  var registries: IDependabotRegistry[] = [];

  var rawRegistries = config["registries"];

  if (rawRegistries == undefined)
    return registries;

  // Parse the value of each of the registries obtained from the file
  Object.entries(rawRegistries).forEach((item) => {
    var registryConfigKey = item[0];
    var registryConfig = item[1];
    var type = registryConfig["type"]?.replace("-", "_");
    if (!type) { // Consider checking against known values for the field
      throw new Error(
        `The value for 'type' in dependency registry config '${registryConfigKey}' is missing`
      );
    }

    var url = registryConfig["url"];
    if (!url) {
      throw new Error(
        `The value 'url' in dependency registry config '${registryConfigKey}' is missing`
      );
    }

    // In Ruby, the some credentials use 'registry' property/field name instead of 'url'
    var useRegistryProperty = type.includes("npm") || type.includes("docker"); // This may also apply for terraform but we don't have enough tests to know

    var dependabotRegistry: IDependabotRegistry = {
      type: type,

      url: useRegistryProperty ? null : url,
      registry: useRegistryProperty ? url : null,

      username: registryConfig["username"],
      password: convertPlaceholder(registryConfig["password"]),
      key: convertPlaceholder(registryConfig["key"]),
      token: convertPlaceholder(registryConfig["token"]),

      "replaces-base": registryConfig["replaces-base"]
    };

    registries.push(dependabotRegistry);
  });
  return registries;
}

