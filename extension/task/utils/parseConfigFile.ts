import {
  IDependabotConfig,
  IDependabotRegistry,
  IDependabotUpdate,
} from "../IDependabotConfig";
import { load } from "js-yaml";
import * as fs from "fs";
import * as path from "path";
import * as tl from "azure-pipelines-task-lib/task";
import { getVariable } from "azure-pipelines-task-lib/task";
import { ISharedVariables } from "./getSharedVariables";
import { convertPlaceholder } from "./convertPlaceholder";
import axios from "axios";

/**
 * Parse the dependabot config YAML file to specify update configuration
 *
 * The file should be located at '/.github/dependabot.yml' or '/.github/dependabot.yaml'
 *
 * To view YAML file format, visit
 * https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#allow
 *
 * @param variables the shared variables of the task
 * @returns {IDependabotConfig} config - the dependabot configuration
 */
async function parseConfigFile(variables: ISharedVariables): Promise<IDependabotConfig> {
  const possibleFilePaths = [
    "/.github/dependabot.yaml",
    "/.github/dependabot.yml",
  ];

  let contents: null | string;

  /*
   * The configuration file can be available locally if the repository is cloned.
   * Otherwise, we should get it via the API which supports 2 scenarios:
   * 1. Running the pipeline without cloning, which is useful for huge repositories (multiple submodules or large commit log)
   * 2. Running a single pipeline to update multiple repositories https://github.com/tinglesoftware/dependabot-azure-devops/issues/328
   */
  if (variables.repositoryOverridden) {
    tl.debug(`Attempting to fetch configuration file via REST API ...`);
    for (const fp of possibleFilePaths) {
      // make HTTP request
      var url = `${variables.organizationUrl}${variables.project}/_apis/git/repositories/${variables.repository}/items?path=${fp}`;
      tl.debug(`GET ${url}`);

      try {
        var response = await axios.get(url, {
          auth: {
            username: "x-access-token",
            password: variables.systemAccessToken,
          },
          headers: {
            Accept: "*/*", // Gotcha!!! without this SH*T fails terribly
          },
        });
        if (response.status === 200) {
          tl.debug(`Found configuration file at '${url}'`);
          contents = response.data;
          break;
        }
      } catch (error) {
        var responseStatusCode = error?.response?.status;

        if (responseStatusCode === 404) {
          tl.debug(`No configuration file at '${url}'`);
          continue;
        } else if (responseStatusCode === 401) {
          throw new Error(
            `No access token has been provided to access '${url}'`
          );
        } else if (responseStatusCode === 403) {
          throw new Error(
            `The access token provided does not have permissions to access '${url}'`
          );
        } else {
          throw error;
        }
      }
    }
  } else {
    let rootDir = getVariable("Build.SourcesDirectory");
    for (const fp of possibleFilePaths) {
      var filePath = path.join(rootDir, fp);
      if (fs.existsSync(filePath)) {
        tl.debug(`Found configuration file cloned at ${filePath}`);
        contents = fs.readFileSync(filePath, "utf-8");
        break;
      } else {
        tl.debug(`No configuration file cloned at ${filePath}`);
      }
    }
  }

  // Ensure we have file contents. Otherwise throw a well readable error.
  if (!contents || typeof contents !== "string") {
    throw new Error(
      `Configuration file not found at possible locations: ${possibleFilePaths.join(
        ", "
      )}`
    );
  } else {
    tl.debug("Configuration file contents read.");
  }

  let config: any = load(contents);

  // Ensure the config object parsed is an object
  if (config === null || typeof config !== "object") {
    throw new Error("Invalid dependabot config object");
  } else {
    tl.debug("Parsed YAML content from configuration file contents.");
  }

  const rawVersion = config["version"];
  let version = -1;

  // Ensure the version has been specified
  if (!!!rawVersion)
    throw new Error("The version must be specified in dependabot.yml");

  // Try convert the version to integer
  try {
    version = parseInt(rawVersion, 10);
  } catch (e) {
    throw new Error("Dependabot version specified must be a valid integer");
  }

  // Ensure the version is == 2
  if (version !== 2) {
    throw new Error(
      "Only version 2 of dependabot is supported. Version specified: " + version
    );
  }

  var dependabotConfig: IDependabotConfig = {
    version: version,
    updates: parseUpdates(config),
    registries: parseRegistries(config),
  };

  return dependabotConfig;
}

function parseUpdates(config: any): IDependabotUpdate[] {
  var updates: IDependabotUpdate[] = [];

  // Check the updates parsed
  var rawUpdates = config["updates"];

  // Check if the array of updates exists
  if (!Array.isArray(rawUpdates)) {
    throw new Error(
      "Invalid dependabot config object: Dependency updates config array not found"
    );
  }

  // Parse the value of each of the updates obtained from the file
  rawUpdates.forEach((update) => {
    var dependabotUpdate: IDependabotUpdate = {
      packageEcosystem: update["package-ecosystem"],
      directory: convertPlaceholder(update["directory"]),

      openPullRequestsLimit: update["open-pull-requests-limit"],

      targetBranch: update["target-branch"],
      vendor: update["vendor"] ? JSON.parse(update["vendor"]) : null,
      versioningStrategy: update["versioning-strategy"],
      milestone: update["milestone"],
      branchNameSeparator: update["pull-request-branch-name"]
        ? update["pull-request-branch-name"]["separator"]
        : undefined,
      rejectExternalCode: update["insecure-external-code-execution"] === "deny",

      // Convert to JSON or as required by the script
      allow: update["allow"] ? JSON.stringify(update["allow"]) : undefined,
      labels: update["labels"] ? JSON.stringify(update["labels"]) : undefined,
      reviewers: update["reviewers"]
        ? JSON.stringify(update["reviewers"])
        : undefined,
      assignees: update["assignees"]
        ? JSON.stringify(update["assignees"])
        : undefined,
    };

    if (!dependabotUpdate.packageEcosystem) {
      throw new Error(
        "The value 'package-ecosystem' in dependency update config is missing"
      );
    }

    // zero is a valid value
    if (
      !dependabotUpdate.openPullRequestsLimit &&
      dependabotUpdate.openPullRequestsLimit !== 0
    ) {
      dependabotUpdate.openPullRequestsLimit = 5;
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

  if (rawRegistries == undefined) return registries;

  // Parse the value of each of the registries obtained from the file
  Object.entries(rawRegistries).forEach((item) => {
    var registryConfigKey = item[0];
    var registryConfig = item[1];

    // parse the type
    var rawType = registryConfig["type"]
    if (!rawType) {
      throw new Error(
        `The value for 'type' in dependency registry config '${registryConfigKey}' is missing`
      );
    }

    // ensure the type is a known one
    if (!KnownRegistryTypes.includes(rawType)) {
      throw new Error(
        `The value '${rawType}' for 'type' in dependency registry config '${registryConfigKey}' is not among the supported values.`
      );
    }
    var type = rawType?.replace("-", "_");

    var parsed: IDependabotRegistry = { type: type, };
    registries.push(parsed);

    // handle special fields for 'hex-organization' types
    if (type === 'hex_organization') {
      var organization = registryConfig["organization"];
      if (!organization) {
        throw new Error(
          `The value 'organization' in dependency registry config '${registryConfigKey}' is missing`
        );
      }
      parsed.organization = organization;
    }

    // handle special fields for 'hex-repository' types
    if (type === 'hex_repository') {
      var repo = registryConfig["repo"];
      if (!repo) {
        throw new Error(
          `The value 'repo' in dependency registry config '${registryConfigKey}' is missing`
        );
      }

      parsed.repo = repo;
      parsed["auth-key"] = registryConfig["auth-key"];
      parsed["public-key-fingerprint"] = registryConfig["public-key-fingerprint"];
    }

    // parse username, password, key, and token while replacing tokens where necessary
    parsed.username = registryConfig["username"];
    parsed.password = convertPlaceholder(registryConfig["password"]);
    parsed.key = convertPlaceholder(registryConfig["key"]);
    parsed.token = convertPlaceholder(registryConfig["token"]);

    // add "replaces-base" if present
    var replacesBase = registryConfig["replaces-base"];
    if (replacesBase !== undefined) {
      parsed["replaces-base"] = replacesBase;
    }

    // parse the url
    var url = registryConfig["url"];
    if (!url && type !== 'hex_organization') {
      throw new Error(
        `The value 'url' in dependency registry config '${registryConfigKey}' is missing`
      );
    }
    if (url) {
      // Some credentials do not use the 'url' property in the Ruby updater.
      // npm_registry and docker_registry use 'registry' which should be stripped off the scheme.
      // terraform_registry uses 'host' which is the hostname from the given URL.

      if (type === 'docker_registry' || type === 'npm_registry') {
        parsed.registry = url.replace("https://", "").replace("http://", "");
      }
      else if (type === 'terraform_registry') {
        parsed.host = new URL(url).hostname;
      }
      else if (type === 'python_index') {
        parsed["index-url"] = url;
      }
      else {
        parsed.url = url;
      }
    }
  });
  return registries;
}

const KnownRegistryTypes = [
  "composer-repository",
  "docker-registry",
  "git",
  "hex-organization",
  "hex-repository",
  "maven-repository",
  "npm-registry",
  "nuget-feed",
  "python-index",
  "rubygems-server",
  "terraform-registry",
];

export { parseConfigFile, parseUpdates, parseRegistries, };
