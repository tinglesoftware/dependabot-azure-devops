import * as fs from "fs";
import * as path from "path";
import * as tl from "azure-pipelines-task-lib/task"

/**
 * Extract the docker image tag from `dockerImageTag` input or the `task.json` file.
 * @returns {string} the version
 */
export default function getDockerImageTag(): string {

  let dockerImageTag: string | undefined = tl.getInput("dockerImageTag");

  if (!dockerImageTag) {
    tl.debug("Getting dockerImageTag from task.json file. If you want to override, specify the dockerImageTag input");

    // Ensure we have the file. Otherwise throw a well readable error.
    const filePath = path.join(__dirname, "..", "task.json");
    if (!fs.existsSync(filePath)) {
      throw new Error(`task.json could not be found at '${filePath}'`);
    }

    // Ensure the file parsed to an object
    let obj: any = JSON.parse(fs.readFileSync(filePath, "utf-8"));
    if (obj === null || typeof obj !== "object") {
      throw new Error("Invalid dependabot config object");
    }

    // we only pull the minor version, the major is fixed for two reasons:
    // 1. The docker version and the task version are not aligned
    // 2. Major docker changes mostly require major changes in the task
    const versionMinor = obj["version"]["Minor"];
    if (!!!versionMinor) throw new Error("Minor version could not be parsed from the file");

    dockerImageTag = `0.${versionMinor}`;
  }

  return dockerImageTag;
}
