import {
  debug,
  getEndpointAuthorizationParameter,
  getInput,
} from "azure-pipelines-task-lib/task";

/**
 * Prepare the access token for Azure DevOps Repos.
 *
 *
 * If the user has not provided one, we use the one from the SystemVssConnection
 *
 * @returns Azure DevOps Access Token
 */
export default function getAzureDevOpsAccessToken() {
  let systemAccessToken: string = getInput("azureDevOpsAccessToken");
  if (!systemAccessToken) {
    debug("No custom token provided. The SystemVssConnection's AccessToken shall be used.");
    systemAccessToken = getEndpointAuthorizationParameter(
      "SystemVssConnection",
      "AccessToken",
      false
    );
  }

  return systemAccessToken;
}
