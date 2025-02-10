import {
  IDependabotAllowCondition,
  IDependabotGroup,
  IDependabotRegistry,
  IDependabotUpdate,
} from '../dependabot/interfaces/IDependabotConfig';
import { ISharedVariables } from '../getSharedVariables';
import { ISecurityVulnerability } from '../github/ISecurityVulnerability';
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
  public static listAllDependenciesJob(
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
   * @param securityVulnerabilities
   * @returns
   */
  public static updateAllDependenciesJob(
    taskInputs: ISharedVariables,
    id: string,
    update: IDependabotUpdate,
    registries: Record<string, IDependabotRegistry>,
    dependencyNamesToUpdate?: string[],
    existingPullRequests?: any[],
    securityVulnerabilities?: ISecurityVulnerability[],
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
        ? dependencyNamesToUpdate?.filter((d) => securityVulnerabilities?.find((v) => v.package.name == d))
        : dependencyNamesToUpdate,
      existingPullRequests,
      securityVulnerabilities,
    );
  }

  /**
   * Create a dependabot update job that updates a single pull request
   * @param taskInputs
   * @param update
   * @param registries
   * @param existingPullRequests
   * @param pullRequestToUpdate
   * @param securityVulnerabilities
   * @returns
   */
  public static updatePullRequestJob(
    taskInputs: ISharedVariables,
    id: string,
    update: IDependabotUpdate,
    registries: Record<string, IDependabotRegistry>,
    existingPullRequests: any[],
    pullRequestToUpdate: any,
    securityVulnerabilities?: ISecurityVulnerability[],
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
      securityVulnerabilities?.filter((v) => dependencyNames.includes(v.package.name)),
    );
  }
}

export function buildUpdateJobConfig(
  id: string,
  taskInputs: ISharedVariables,
  update: IDependabotUpdate,
  registries: Record<string, IDependabotRegistry>,
  updatingPullRequest?: boolean | undefined,
  updateDependencyGroupName?: string | undefined,
  updateDependencyNames?: string[] | undefined,
  existingPullRequests?: any[],
  securityVulnerabilities?: ISecurityVulnerability[],
): IDependabotUpdateOperation {
  const securityOnlyUpdate = update['open-pull-requests-limit'] == 0;
  return {
    config: update,
    job: {
      'id': id,
      'package-manager': update['package-ecosystem'],
      'updating-a-pull-request': updatingPullRequest || false,
      'dependency-group-to-refresh': updateDependencyGroupName,
      'dependency-groups': mapGroupsFromDependabotConfigToJobConfig(update.groups),
      'dependencies': updateDependencyNames?.length ? updateDependencyNames : undefined,
      'allowed-updates': mapAllowedUpdatesFromDependabotConfigToJobConfig(update.allow, securityOnlyUpdate),
      'ignore-conditions': mapIgnoreConditionsFromDependabotConfigToJobConfig(update.ignore),
      'security-updates-only': securityOnlyUpdate,
      'security-advisories': mapSecurityAdvisories(securityVulnerabilities),
      'source': mapSourceFromDependabotConfigToJobConfig(taskInputs, update),
      'existing-pull-requests': existingPullRequests?.filter((pr) => !pr['dependency-group-name']),
      'existing-group-pull-requests': existingPullRequests?.filter((pr) => pr['dependency-group-name']),
      'commit-message-options':
        update['commit-message'] === undefined
          ? undefined
          : {
              'prefix': update['commit-message']?.['prefix'],
              'prefix-development': update['commit-message']?.['prefix-development'],
              'include-scope':
                update['commit-message']?.['include']?.toLocaleLowerCase()?.trim() == 'scope' ? true : undefined,
            },
      'experiments': Object.keys(taskInputs.experiments || {}).reduce(
        (acc, key) => {
          // Experiment values are known to be either 'true', 'false', or a string value.
          // If the value is 'true' or 'false', convert it to a boolean type so that dependabot-core handles it correctly.
          const value = taskInputs.experiments[key];
          if (typeof value === 'string' && value?.toLocaleLowerCase() === 'true') {
            acc[key] = true;
          } else if (typeof value === 'string' && value?.toLocaleLowerCase() === 'false') {
            acc[key] = false;
          } else {
            acc[key] = value;
          }
          return acc;
        },
        {} as Record<string, string | boolean>,
      ),
      'reject-external-code': update['insecure-external-code-execution']?.toLocaleLowerCase()?.trim() == 'allow',
      'repo-private': undefined, // TODO: add config for this?
      'repo-contents-path': undefined, // TODO: add config for this?
      'requirements-update-strategy': mapVersionStrategyToRequirementsUpdateStrategy(update['versioning-strategy']),
      'lockfile-only': update['versioning-strategy'] === 'lockfile-only',
      'vendor-dependencies': update.vendor,
      'debug': taskInputs.debug,

      // TODO: Investigate if these options are needed or are now obsolete.
      //       These options don't appear to be used by dependabot-core yet/anymore,
      //       but do appear in GitHub Dependabot job logs seen in the wild.
      //'max-updater-run-time': 2700,
      //'proxy-log-response-body-on-auth-failure': true,
      //'update-subdependencies': false,
    },
    credentials: mapRegistryCredentialsFromDependabotConfigToJobConfig(taskInputs, registries),
  };
}

export function mapSourceFromDependabotConfigToJobConfig(taskInputs: ISharedVariables, update: IDependabotUpdate): any {
  return {
    'provider': 'azure',
    'api-endpoint': taskInputs.apiEndpointUrl,
    'hostname': taskInputs.hostname,
    'repo': `${taskInputs.organization}/${taskInputs.project}/_git/${taskInputs.repository}`,
    'branch': update['target-branch'],
    'commit': undefined, // use latest commit of target branch
    'directory': update.directory,
    'directories': update.directories,
  };
}

export function mapGroupsFromDependabotConfigToJobConfig(
  dependencyGroups: Record<string, IDependabotGroup>,
): any[] | undefined {
  if (!dependencyGroups || !Object.keys(dependencyGroups).length) {
    return undefined;
  }
  return Object.keys(dependencyGroups)
    .map((name) => {
      const group = dependencyGroups[name];
      if (!group) {
        return;
      }
      return {
        'name': name,
        'applies-to': group['applies-to'],
        'rules': {
          'patterns': group['patterns']?.length ? group['patterns'] : ['*'],
          'exclude-patterns': group['exclude-patterns'],
          'dependency-type': group['dependency-type'],
          'update-types': group['update-types'],
        },
      };
    })
    .filter((g) => g);
}

export function mapAllowedUpdatesFromDependabotConfigToJobConfig(
  allowedUpdates: IDependabotAllowCondition[],
  securityOnlyUpdate?: boolean,
): any[] {
  // If no allow conditions are specified, update direct dependencies by default; This is what GitHub does.
  // NOTE: 'update-type' appears to be a deprecated config, but still appears in the dependabot-core model and GitHub Dependabot job logs.
  //       See: https://github.com/dependabot/dependabot-core/blob/b3a0c1f86c20729494097ebc695067099f5b4ada/updater/lib/dependabot/job.rb#L253C1-L257C78
  if (!allowedUpdates) {
    return [
      {
        'dependency-type': 'direct',
        'update-type': securityOnlyUpdate ? 'security' : 'all',
      },
    ];
  }
  return allowedUpdates.map((allow) => {
    return {
      'dependency-name': allow['dependency-name'],
      'dependency-type': allow['dependency-type'],
      'update-type': allow['update-type'],
    };
  });
}

export function mapIgnoreConditionsFromDependabotConfigToJobConfig(
  ignoreConditions: IDependabotAllowCondition[],
): any[] {
  if (!ignoreConditions) {
    return undefined;
  }
  return ignoreConditions.map((ignore) => {
    return {
      'source': ignore['source'],
      'updated-at': ignore['updated-at'],
      'dependency-name': ignore['dependency-name'],
      'update-types': ignore['update-types'],

      // The dependabot.yml config docs are not very clear about acceptable values; after scanning dependabot-core and dependabot-cli,
      // this could either be a single version string (e.g. '>1.0.0'), or multiple version strings separated by commas (e.g. '>1.0.0, <2.0.0')
      'version-requirement': Array.isArray(ignore['versions'])
        ? (<string[]>ignore['versions'])?.join(', ')
        : <string>ignore['versions'],
    };
  });
}

export function mapSecurityAdvisories(securityVulnerabilities: ISecurityVulnerability[]): any[] {
  if (!securityVulnerabilities) {
    return undefined;
  }

  // A single security advisory can cause a vulnerability in multiple versions of a package.
  // We need to map each unique security advisory to a list of affected-versions and patched-versions.
  const vulnerabilitiesGroupedByPackageNameAndAdvisoryId = new Map<string, ISecurityVulnerability[]>();
  for (const vuln of securityVulnerabilities) {
    const key = `${vuln.package.name}/${vuln.advisory.identifiers.map((i) => `${i.type}:${i.value}`).join('/')}`;
    if (!vulnerabilitiesGroupedByPackageNameAndAdvisoryId.has(key)) {
      vulnerabilitiesGroupedByPackageNameAndAdvisoryId.set(key, []);
    }
    vulnerabilitiesGroupedByPackageNameAndAdvisoryId.get(key).push(vuln);
  }
  return Array.from(vulnerabilitiesGroupedByPackageNameAndAdvisoryId.values()).map((vulns) => {
    return {
      'dependency-name': vulns[0].package.name,
      'affected-versions': vulns.map((v) => v.vulnerableVersionRange).filter((v) => v && v.length > 0),
      'patched-versions': vulns.map((v) => v.firstPatchedVersion).filter((v) => v && v.length > 0),
      'unaffected-versions': [],
    };
  });
}

export function mapVersionStrategyToRequirementsUpdateStrategy(versioningStrategy: string): string | undefined {
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

export function mapRegistryCredentialsFromDependabotConfigToJobConfig(
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
