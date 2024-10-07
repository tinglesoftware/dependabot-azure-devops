import {
  IDependabotAllowCondition,
  IDependabotGroup,
  IDependabotRegistry,
  IDependabotUpdate,
} from '../dependabot/interfaces/IDependabotConfig';
import { ISharedVariables } from '../getSharedVariables';
import { ISecurityAdvisory } from '../github/getSecurityAdvisories';
import { IDependabotUpdateOperation } from './interfaces/IDependabotUpdateOperation';

/**
 * Wrapper class for building dependabot update job objects
 */
export class DependabotJobBuilder {
  /**
   * Create a dependabot update job that updates nothing, but will discover the dependency list for a package ecyosystem
   * @param taskInputs
   * @param update
   * @param registries
   * @returns
   */
  public static newDiscoverDependencyListJob(
    taskInputs: ISharedVariables,
    id: string,
    update: IDependabotUpdate,
    registries: Record<string, IDependabotRegistry>,
  ): IDependabotUpdateOperation {
    return {
      config: update,
      job: {
        'id': `discover-${id}-${update['package-ecosystem']}-dependency-list`,
        'package-manager': update['package-ecosystem'],
        'ignore-conditions': [{ 'dependency-name': '*' }],
        'source': mapSourceFromDependabotConfigToJobConfig(taskInputs, update),
        'experiments': taskInputs.experiments,
        'debug': taskInputs.debug,
      },
      credentials: mapRegistryCredentialsFromDependabotConfigToJobConfig(taskInputs, registries),
    };
  }

  /**
   * Create a dependabot update job that updates all dependencies for a package ecyosystem
   * @param taskInputs
   * @param update
   * @param registries
   * @param dependencyNamesToUpdate
   * @param existingPullRequests
   * @param securityAdvisories
   * @returns
   */
  public static newUpdateAllJob(
    taskInputs: ISharedVariables,
    id: string,
    update: IDependabotUpdate,
    registries: Record<string, IDependabotRegistry>,
    dependencyNamesToUpdate?: string[],
    existingPullRequests?: any[],
    securityAdvisories?: ISecurityAdvisory[],
  ): IDependabotUpdateOperation {
    const packageEcosystem = update['package-ecosystem'];
    const securityUpdatesOnly = update['open-pull-requests-limit'] == 0;
    return buildUpdateJobConfig(
      `update-${id}-${packageEcosystem}-${securityUpdatesOnly ? 'security-only' : 'all'}`,
      taskInputs,
      update,
      registries,
      false,
      undefined,
      securityUpdatesOnly
        ? dependencyNamesToUpdate?.filter((d) => securityAdvisories?.find((a) => a['dependency-name'] == d))
        : dependencyNamesToUpdate,
      existingPullRequests,
      securityAdvisories,
    );
  }

  /**
   * Create a dependabot update job that updates a single pull request
   * @param taskInputs
   * @param update
   * @param registries
   * @param existingPullRequests
   * @param pullRequestToUpdate
   * @returns
   */
  public static newUpdatePullRequestJob(
    taskInputs: ISharedVariables,
    id: string,
    update: IDependabotUpdate,
    registries: Record<string, IDependabotRegistry>,
    existingPullRequests: any[],
    pullRequestToUpdate: any,
  ): IDependabotUpdateOperation {
    const dependencyGroupName = pullRequestToUpdate['dependency-group-name'];
    const dependencyNames = (dependencyGroupName ? pullRequestToUpdate['dependencies'] : pullRequestToUpdate)?.map(
      (d) => d['dependency-name'],
    );
    return buildUpdateJobConfig(
      `update-pr-${id}`,
      taskInputs,
      update,
      registries,
      true,
      dependencyGroupName,
      dependencyNames,
      existingPullRequests,
    );
  }
}

function buildUpdateJobConfig(
  id: string,
  taskInputs: ISharedVariables,
  update: IDependabotUpdate,
  registries: Record<string, IDependabotRegistry>,
  updatingPullRequest?: boolean | undefined,
  updateDependencyGroupName?: string | undefined,
  updateDependencyNames?: string[] | undefined,
  existingPullRequests?: any[],
  securityAdvisories?: ISecurityAdvisory[],
) {
  return {
    config: update,
    job: {
      'id': id,
      'package-manager': update['package-ecosystem'],
      'update-subdependencies': true, // TODO: add config for this?
      'updating-a-pull-request': updatingPullRequest || false,
      'dependency-group-to-refresh': updateDependencyGroupName,
      'dependency-groups': mapGroupsFromDependabotConfigToJobConfig(update.groups),
      'dependencies': updateDependencyNames,
      'allowed-updates': mapAllowedUpdatesFromDependabotConfigToJobConfig(update.allow),
      'ignore-conditions': mapIgnoreConditionsFromDependabotConfigToJobConfig(update.ignore),
      'security-updates-only': update['open-pull-requests-limit'] == 0,
      'security-advisories': mapSecurityAdvisories(securityAdvisories),
      'source': mapSourceFromDependabotConfigToJobConfig(taskInputs, update),
      'existing-pull-requests': existingPullRequests?.filter((pr) => !pr['dependency-group-name']),
      'existing-group-pull-requests': existingPullRequests?.filter((pr) => pr['dependency-group-name']),
      'commit-message-options':
        update['commit-message'] === undefined
          ? undefined
          : {
              'prefix': update['commit-message']?.['prefix'],
              'prefix-development': update['commit-message']?.['prefix-development'],
              'include-scope': update['commit-message']?.['include'],
            },
      'experiments': taskInputs.experiments,
      'max-updater-run-time': undefined, // TODO: add config for this?
      'reject-external-code': update['insecure-external-code-execution']?.toLocaleLowerCase() == 'allow',
      'repo-private': undefined, // TODO: add config for this?
      'repo-contents-path': undefined, // TODO: add config for this?
      'requirements-update-strategy': mapVersionStrategyToRequirementsUpdateStrategy(update['versioning-strategy']),
      'lockfile-only': update['versioning-strategy'] === 'lockfile-only',
      'vendor-dependencies': update.vendor,
      'debug': taskInputs.debug,
    },
    credentials: mapRegistryCredentialsFromDependabotConfigToJobConfig(taskInputs, registries),
  };
}

function mapSourceFromDependabotConfigToJobConfig(taskInputs: ISharedVariables, update: IDependabotUpdate): any {
  const hasMultipleDirectories = update.directories?.length > 1;
  return {
    'provider': 'azure',
    'api-endpoint': taskInputs.apiEndpointUrl,
    'hostname': taskInputs.hostname,
    'repo': `${taskInputs.organization}/${taskInputs.project}/_git/${taskInputs.repository}`,
    'branch': update['target-branch'],
    'commit': undefined, // use latest commit of target branch
    'directory': hasMultipleDirectories ? undefined : update.directory || '/',
    'directories': hasMultipleDirectories ? update.directories : undefined,
  };
}

function mapGroupsFromDependabotConfigToJobConfig(dependencyGroups: Record<string, IDependabotGroup>): any[] {
  if (!dependencyGroups) {
    return undefined;
  }
  return Object.keys(dependencyGroups).map((name) => {
    const group = dependencyGroups[name];
    return {
      'name': name,
      'applies-to': group['applies-to'],
      'rules': {
        'patterns': group['patterns'],
        'exclude-patterns': group['exclude-patterns'],
        'dependency-type': group['dependency-type'],
        'update-types': group['update-types'],
      },
    };
  });
}

function mapAllowedUpdatesFromDependabotConfigToJobConfig(allowedUpdates: IDependabotAllowCondition[]): any[] {
  if (!allowedUpdates) {
    // If no allow conditions are specified, update all dependencies by default
    return [{ 'dependency-type': 'all' }];
  }
  return allowedUpdates.map((allow) => {
    return {
      'dependency-name': allow['dependency-name'],
      'dependency-type': allow['dependency-type'],
      //'update-type': allow["update-type"] // TODO: This is missing from dependabot.ymal docs, but is used in the dependabot-core job model!?
    };
  });
}

function mapIgnoreConditionsFromDependabotConfigToJobConfig(ignoreConditions: IDependabotAllowCondition[]): any[] {
  if (!ignoreConditions) {
    return undefined;
  }
  return ignoreConditions.map((ignore) => {
    return {
      'dependency-name': ignore['dependency-name'],
      //'source': ignore["source"], // TODO: This is missing from dependabot.ymal docs, but is used in the dependabot-core job model!?
      'update-types': ignore['update-types'],
      //'updated-at': ignore["updated-at"], // TODO: This is missing from dependabot.ymal docs, but is used in the dependabot-core job model!?
      'version-requirement': (<string[]>ignore['versions'])?.join(', '), // TODO: Test this, not sure how this should be parsed...
    };
  });
}

function mapSecurityAdvisories(securityAdvisories: ISecurityAdvisory[]): any[] {
  if (!securityAdvisories) {
    return undefined;
  }
  return securityAdvisories.map((advisory) => {
    return {
      'dependency-name': advisory['dependency-name'],
      'affected-versions': advisory['affected-versions'],
      'patched-versions': advisory['patched-versions'],
      'unaffected-versions': advisory['unaffected-versions'],
      'title': advisory['title'],
      'description': advisory['description'],
      'source-name': advisory['source-name'],
      'source-url': advisory['source-url'],
    };
  });
}

function mapVersionStrategyToRequirementsUpdateStrategy(versioningStrategy: string): string | undefined {
  if (!versioningStrategy) {
    return undefined;
  }
  switch (versioningStrategy) {
    case 'auto':
      return undefined;
    case 'increase':
      return 'bump_versions';
    case 'increase-if-necessary':
      return 'bump_versions_if_necessary';
    case 'lockfile-only':
      return 'lockfile_only';
    case 'widen':
      return 'widen_ranges';
    default:
      throw new Error(`Invalid dependabot.yaml versioning strategy option '${versioningStrategy}'`);
  }
}

function mapRegistryCredentialsFromDependabotConfigToJobConfig(
  taskInputs: ISharedVariables,
  registries: Record<string, IDependabotRegistry>,
): any[] {
  let registryCredentials = new Array();
  if (taskInputs.systemAccessToken) {
    // Required to authenticate with the Azure DevOps git repository when cloning the source code
    registryCredentials.push({
      type: 'git_source',
      host: taskInputs.hostname,
      username: taskInputs.systemAccessUser?.trim()?.length > 0 ? taskInputs.systemAccessUser : 'x-access-token',
      password: taskInputs.systemAccessToken,
    });
  }
  if (taskInputs.githubAccessToken) {
    // Required to avoid rate-limiting errors when generating pull request descriptions (e.g. fetching release notes, commit messages, etc)
    registryCredentials.push({
      type: 'git_source',
      host: 'github.com',
      username: 'x-access-token',
      password: taskInputs.githubAccessToken,
    });
  }
  if (registries) {
    // Required to authenticate with private package feeds when finding the latest version of dependencies
    for (const key in registries) {
      const registry = registries[key];
      registryCredentials.push({
        'type': registry.type,
        'host': registry.host,
        'url': registry.url,
        'registry': registry.registry,
        'username': registry.username,
        'password': registry.password,
        'token': registry.token,
        'replaces-base': registry['replaces-base'],
      });
    }
  }

  return registryCredentials;
}
