import { debug, getInput, getVariable } from "azure-pipelines-task-lib/task";

/**
 * Extract the target repository from the `targetRepositoryName` input
 *
 *
 * If the repository is not found, the default value will be extracted from the `Build.Repository.Name`
 * build pipeline variables
 *
 * @returns the target repository
 */
export default function getTargetRepository() {
  // Prepare the repository
  let repository: string = getInput("targetRepositoryName");
  if (!repository) {
    debug("No custom repository provided. The Pipeline Repository Name shall be used.");
    repository = getVariable("Build.Repository.Name");
  }

  repository = encodeURI(repository); // encode special characters like spaces

  return repository;
}
