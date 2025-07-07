import * as tl from 'azure-pipelines-task-lib/task';
import { type AzureDevOpsUrl, extractUrlParts } from 'paklo/azure';
import { DEFAULT_EXPERIMENTS, type DependabotExperiments } from 'paklo/dependabot';
import { getAzureDevOpsAccessToken, getGithubAccessToken } from './tokens';

export interface ISharedVariables {
  url: AzureDevOpsUrl;

  /** Whether the repository was overridden via input */
  repositoryOverridden: boolean;

  /** The github token */
  githubAccessToken?: string;
  /** The access User for Azure DevOps Repos */
  systemAccessUser?: string;
  /** The access token for Azure DevOps Repos */
  systemAccessToken: string;

  authorEmail?: string;
  authorName?: string;

  /** Determines if the pull requests that dependabot creates should have auto complete set */
  setAutoComplete: boolean;
  /** Merge strategies which can be used to complete a pull request */
  mergeStrategy?: string;
  /** List of any policy configuration Id's which auto-complete should not wait for */
  autoCompleteIgnoreConfigIds: number[];

  /** Determines if the pull requests that dependabot creates should be automatically approved */
  autoApprove: boolean;
  /** A personal access token of the user that should approve the PR */
  autoApproveUserToken: string;

  experiments: DependabotExperiments;

  /** Determines if verbose log messages are logged */
  debug: boolean;
  /** Determines if secrets are protected */
  secrets: boolean;

  /** List of update identifiers to run */
  targetUpdateIds: number[];

  securityAdvisoriesFile: string | undefined;

  /** Whether to test logic without creating, updating or abandoning pull requests */
  dryRun: boolean;

  /** The dependabot-cli go package to use for updates. e.g. github.com/dependabot/cli/cmd/dependabot@latest */
  dependabotCliPackage?: string;
  /** The apiUrl argument of the dependabot update command */
  dependabotCliApiUrl?: string;
  /** The listening port of the dependabot update command */
  dependabotCliApiListeningPort?: string;
  /** The dependabot-updater docker image to use for updates. e.g. ghcr.io/dependabot/dependabot-updater-{ecosystem}:latest */
  dependabotUpdaterImage?: string;

  /* Path to a certificate the proxy will trust */
  proxyCertPath?: string;
}

/**
 * Extract shared variables
 *
 * @returns shared variables
 */
export default function getSharedVariables(): ISharedVariables {
  let project = tl.getInput('targetProjectName');
  const projectOverridden = typeof project === 'string';
  if (!projectOverridden) {
    // We use the project name because it is very readable.
    // It may not work in all APIs and if it fails, we can switch from `System.TeamProject` to `System.TeamProjectId`.
    project = tl.getVariable('System.TeamProject');
    tl.debug(`No custom project provided; Running update for current project.`);
  } else {
    tl.debug(`Custom project provided; Running update for specified project.`);
  }

  let repository = tl.getInput('targetRepositoryName');
  const repositoryOverridden = typeof repository === 'string';
  if (projectOverridden && !repositoryOverridden) {
    throw new Error(`Target repository must be provided when target project is overridden`);
  }
  if (!repositoryOverridden) {
    repository = tl.getVariable('Build.Repository.Name');
    tl.debug(`No custom repository provided; Running update for local repository.`);
  } else {
    tl.debug(`Custom repository provided; Running update for remote repository.`);
  }

  const organisationUrl = tl.getVariable('System.TeamFoundationCollectionUri')!;
  const urlParts = extractUrlParts({ organisationUrl, project: project!, repository: repository! });

  // Prepare the access credentials
  const githubAccessToken = getGithubAccessToken();
  const systemAccessUser = tl.getInput('azureDevOpsUser');
  const systemAccessToken = getAzureDevOpsAccessToken();

  const authorEmail: string | undefined = tl.getInput('authorEmail');
  const authorName: string | undefined = tl.getInput('authorName');

  // Prepare variables for auto complete
  const setAutoComplete = tl.getBoolInput('setAutoComplete', false);
  const mergeStrategy = tl.getInput('mergeStrategy', true);
  const autoCompleteIgnoreConfigIds = tl.getDelimitedInput('autoCompleteIgnoreConfigIds', ';', false).map(Number);

  // Prepare variables for auto approve
  const autoApprove: boolean = tl.getBoolInput('autoApprove', false);
  const autoApproveUserToken = tl.getInput('autoApproveUserToken')!;

  // Convert experiments from comma separated key value pairs to a record
  let experiments = tl
    .getInput('experiments', false)
    ?.split(',')
    .filter((entry) => entry.trim() !== '') // <-- filter out empty entries
    .reduce((acc, cur) => {
      const [key, value] = cur.split('=', 2);
      acc[key!] = value || true;
      return acc;
    }, {} as DependabotExperiments);

  // If no experiments are defined, use the default experiments
  if (!experiments) {
    experiments = DEFAULT_EXPERIMENTS;
    tl.debug('No experiments provided; Using default experiments.');
  }

  console.log('Experiments:', experiments);

  const debug: boolean = tl.getVariable('System.Debug')?.match(/true/i) ? true : false;
  const secrets: boolean = tl.getVariable('System.Secrets')?.match(/true/i) ? true : false;

  // Get the target identifiers
  const targetUpdateIds = tl.getDelimitedInput('targetUpdateIds', ';', false).map(Number);

  // Prepare other variables
  const securityAdvisoriesFile: string | undefined = tl.getInput('securityAdvisoriesFile');
  const dryRun: boolean = tl.getBoolInput('dryRun', false);

  const dependabotCliPackage: string | undefined = tl.getInput('dependabotCliPackage');
  const dependabotCliApiUrl: string | undefined = tl.getInput('dependabotCliApiUrl', false);
  const dependabotCliApiListeningPort: string | undefined = tl.getInput('dependabotCliApiListeningPort', false);
  const dependabotUpdaterImage: string | undefined = tl.getInput('dependabotUpdaterImage');

  const proxyCertPath: string | undefined = tl.getInput('proxyCertPath');

  return {
    url: urlParts,
    repositoryOverridden,

    githubAccessToken,
    systemAccessUser,
    systemAccessToken,

    authorEmail,
    authorName,

    setAutoComplete,
    mergeStrategy,
    autoCompleteIgnoreConfigIds,

    autoApprove,
    autoApproveUserToken,

    experiments,

    debug,
    secrets,

    targetUpdateIds,
    securityAdvisoriesFile,

    dryRun,

    dependabotCliPackage,
    dependabotCliApiUrl,
    dependabotCliApiListeningPort,
    dependabotUpdaterImage,

    proxyCertPath,
  };
}
