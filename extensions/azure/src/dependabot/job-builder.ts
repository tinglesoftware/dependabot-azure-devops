import {
  makeCredentialsMetadata,
  type DependabotAllowCondition,
  type DependabotAllowed,
  type DependabotCondition,
  type DependabotCredential,
  type DependabotExistingGroupPR,
  type DependabotExistingPR,
  type DependabotExperiments,
  type DependabotGroup,
  type DependabotGroupJob,
  type DependabotIgnoreCondition,
  type DependabotPackageManager,
  type DependabotRegistry,
  type DependabotSecurityAdvisory,
  type DependabotUpdate,
  type PackageEcosystem,
} from 'paklo/dependabot';
import { type SecurityVulnerability } from '../github';
import { type ISharedVariables } from '../utils/shared-variables';
import { type IDependabotUpdateOperation } from './models';

/**
 * Wrapper class for building dependabot update job objects
 */
export class DependabotJobBuilder {
  /**
   * Create a dependabot update job that updates nothing, but will discover the dependency list for a package ecosystem
   * @param taskInputs
   * @param update
   * @param registries
   * @returns
   */
  public static listAllDependenciesJob(
    taskInputs: ISharedVariables,
    id: string,
    update: DependabotUpdate,
    registries?: Record<string, DependabotRegistry>,
  ): IDependabotUpdateOperation {
    const credentials = mapCredentials(taskInputs, registries);

    return {
      config: update,
      job: {
        'id': `discover-${id}-${update['package-ecosystem']}-dependency-list`,
        'package-manager': mapPackageEcosystemToPackageManager(update['package-ecosystem']),
        'ignore-conditions': [{ 'dependency-name': '*' }],
        'source': mapSourceFromDependabotConfigToJobConfig(taskInputs, update),
        'experiments': taskInputs.experiments,
        'debug': taskInputs.debug,
        'credentials-metadata': makeCredentialsMetadata(credentials),
      },
      credentials,
    };
  }

  /**
   * Create a dependabot update job that updates all dependencies for a package ecosystem
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
    update: DependabotUpdate,
    registries?: Record<string, DependabotRegistry>,
    enableBetaEcosystems?: boolean | undefined,
    dependencyNamesToUpdate?: string[],
    existingPullRequests?: (DependabotExistingPR[] | DependabotExistingGroupPR)[],
    securityVulnerabilities?: SecurityVulnerability[],
  ): IDependabotUpdateOperation {
    const securityUpdatesOnly = update['open-pull-requests-limit'] == 0;
    return buildUpdateJobConfig(
      `update-${id}-${update['package-ecosystem']}-${securityUpdatesOnly ? 'security-only' : 'all'}`,
      taskInputs,
      update,
      registries,
      enableBetaEcosystems,
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
    update: DependabotUpdate,
    registries: Record<string, DependabotRegistry> | undefined,
    enableBetaEcosystems: boolean | undefined,
    existingPullRequests: (DependabotExistingPR[] | DependabotExistingGroupPR)[],
    pullRequestToUpdate: DependabotExistingPR[] | DependabotExistingGroupPR,
    securityVulnerabilities?: SecurityVulnerability[],
  ): IDependabotUpdateOperation {
    const dependencyGroupName = Array.isArray(pullRequestToUpdate)
      ? undefined
      : pullRequestToUpdate['dependency-group-name'];
    const dependencyNames = (
      Array.isArray(pullRequestToUpdate) ? pullRequestToUpdate : pullRequestToUpdate.dependencies
    )?.map((d) => d['dependency-name']);
    return buildUpdateJobConfig(
      `update-pr-${id}`,
      taskInputs,
      update,
      registries,
      enableBetaEcosystems,
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
  update: DependabotUpdate,
  registries?: Record<string, DependabotRegistry>,
  enableBetaEcosystems?: boolean | undefined,
  updatingPullRequest?: boolean | undefined,
  updateDependencyGroupName?: string | undefined,
  updateDependencyNames?: string[] | undefined,
  existingPullRequests?: (DependabotExistingPR[] | DependabotExistingGroupPR)[],
  securityVulnerabilities?: SecurityVulnerability[],
): IDependabotUpdateOperation {
  const securityOnlyUpdate = update['open-pull-requests-limit'] == 0;
  const credentials = mapCredentials(taskInputs, registries);

  return {
    config: update,
    job: {
      'id': id,
      'package-manager': mapPackageEcosystemToPackageManager(update['package-ecosystem']),
      'updating-a-pull-request': updatingPullRequest || false,
      'dependency-group-to-refresh': updateDependencyGroupName,
      'dependency-groups': mapGroupsFromDependabotConfigToJobConfig(update.groups),
      'dependencies': updateDependencyNames?.length ? updateDependencyNames : undefined,
      'allowed-updates': mapAllowedUpdatesFromDependabotConfigToJobConfig(update.allow, securityOnlyUpdate),
      'ignore-conditions': mapIgnoreConditionsFromDependabotConfigToJobConfig(update.ignore),
      'security-updates-only': securityOnlyUpdate,
      'security-advisories': mapSecurityAdvisories(securityVulnerabilities),
      'source': mapSourceFromDependabotConfigToJobConfig(taskInputs, update),
      'existing-pull-requests': existingPullRequests?.filter((pr) => Array.isArray(pr)),
      'existing-group-pull-requests': existingPullRequests?.filter((pr): pr is DependabotExistingGroupPR => true),
      'commit-message-options':
        update['commit-message'] === undefined
          ? undefined
          : {
              'prefix': update['commit-message']?.prefix,
              'prefix-development': update['commit-message']?.['prefix-development'],
              'include-scope':
                update['commit-message']?.include?.toLocaleLowerCase()?.trim() == 'scope' ? true : undefined,
            },
      'cooldown': update.cooldown,
      'experiments': mapExperiments(taskInputs.experiments),
      'reject-external-code': update['insecure-external-code-execution']?.toLocaleLowerCase()?.trim() == 'allow',
      'requirements-update-strategy': mapVersionStrategyToRequirementsUpdateStrategy(update['versioning-strategy']),
      'lockfile-only': update['versioning-strategy'] === 'lockfile-only',
      'vendor-dependencies': update.vendor,
      'debug': taskInputs.debug,
      'credentials-metadata': makeCredentialsMetadata(credentials),
      'proxy-log-response-body-on-auth-failure': true,
      'max-updater-run-time': 2700,
      'enable-beta-ecosystems': enableBetaEcosystems || false,
      // Updates across ecosystems is still in development
      // See https://github.com/dependabot/dependabot-core/issues/8126
      //     https://github.com/dependabot/dependabot-core/pull/12339
      // It needs to merged in the core repo first before we support it
      // However, to match current job configs and to prevent surprises, we disable it
      'multi-ecosystem-update': false,
    },
    credentials,
  };
}

export function mapSourceFromDependabotConfigToJobConfig(taskInputs: ISharedVariables, update: DependabotUpdate) {
  const isDevOpsServices = !taskInputs.virtualDirectory?.length; // Azure DevOps Services does not use a virtual directory
  return {
    'provider': 'azure',
    'api-endpoint': taskInputs.apiEndpointUrl,
    'hostname': taskInputs.hostname,
    'repo': isDevOpsServices
      ? `${taskInputs.organization}/${taskInputs.project}/_git/${taskInputs.repository}`
      : `${taskInputs.virtualDirectory}/${taskInputs.organization}/${taskInputs.project}/_git/${taskInputs.repository}`,
    'branch': update['target-branch'],
    'commit': undefined, // use latest commit of target branch
    'directory': update.directory,
    'directories': update.directories,
  };
}

export function mapGroupsFromDependabotConfigToJobConfig(
  dependencyGroups?: Record<string, DependabotGroup | undefined | null>,
): DependabotGroupJob[] | undefined {
  if (!dependencyGroups || !Object.keys(dependencyGroups).length) {
    return undefined;
  }
  return Object.keys(dependencyGroups)
    .filter((name) => dependencyGroups[name])
    .map((name) => {
      const group = dependencyGroups[name]!;
      return {
        'name': name,
        'applies-to': group['applies-to'],
        'rules': {
          'patterns': group.patterns?.length ? group.patterns : ['*'],
          'exclude-patterns': group['exclude-patterns'],
          'dependency-type': group['dependency-type'],
          'update-types': group['update-types'],
        },
      } satisfies DependabotGroupJob;
    });
}

export function mapAllowedUpdatesFromDependabotConfigToJobConfig(
  allowedUpdates?: DependabotAllowCondition[],
  securityOnlyUpdate?: boolean,
): DependabotAllowed[] {
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
  ignoreConditions?: DependabotIgnoreCondition[],
): DependabotCondition[] | undefined {
  if (!ignoreConditions) {
    return undefined;
  }
  return ignoreConditions.map((ignore) => {
    return {
      'source': ignore.source,
      'updated-at': ignore['updated-at'],
      'dependency-name': ignore['dependency-name'],
      'update-types': ignore['update-types'],

      // The dependabot.yml config docs are not very clear about acceptable values; after scanning dependabot-core and dependabot-cli,
      // this could either be a single version string (e.g. '>1.0.0'), or multiple version strings separated by commas (e.g. '>1.0.0, <2.0.0')
      'version-requirement': Array.isArray(ignore.versions) ? (<string[]>ignore.versions)?.join(', ') : ignore.versions,
    } satisfies DependabotCondition;
  });
}

export function mapSecurityAdvisories(
  securityVulnerabilities?: SecurityVulnerability[],
): DependabotSecurityAdvisory[] | undefined {
  if (!securityVulnerabilities) {
    return undefined;
  }

  // A single security advisory can cause a vulnerability in multiple versions of a package.
  // We need to map each unique security advisory to a list of affected-versions and patched-versions.
  const vulnerabilitiesGroupedByPackageNameAndAdvisoryId = new Map<string, SecurityVulnerability[]>();
  for (const vuln of securityVulnerabilities) {
    const key = `${vuln.package.name}/${vuln.advisory.identifiers.map((i) => `${i.type}:${i.value}`).join('/')}`;
    if (!vulnerabilitiesGroupedByPackageNameAndAdvisoryId.has(key)) {
      vulnerabilitiesGroupedByPackageNameAndAdvisoryId.set(key, []);
    }
    vulnerabilitiesGroupedByPackageNameAndAdvisoryId.get(key)!.push(vuln);
  }
  return Array.from(vulnerabilitiesGroupedByPackageNameAndAdvisoryId.values()).map((vulns) => {
    return {
      'dependency-name': vulns[0]!.package.name,
      'affected-versions': vulns.map((v) => v.vulnerableVersionRange).filter((v) => v && v.length > 0),
      'patched-versions': vulns
        .map((v) => v.firstPatchedVersion?.identifier)
        .filter((v) => v && v.length > 0)
        .map((v) => v!),
      'unaffected-versions': [],
    } satisfies DependabotSecurityAdvisory;
  });
}

export function mapVersionStrategyToRequirementsUpdateStrategy(versioningStrategy?: string): string | undefined {
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

export function mapCredentials(
  taskInputs: ISharedVariables,
  registries?: Record<string, DependabotRegistry>,
): DependabotCredential[] {
  const credentials = [];
  if (taskInputs.systemAccessToken) {
    // Required to authenticate with the Azure DevOps git repository when cloning the source code
    credentials.push({
      type: 'git_source',
      host: taskInputs.hostname,
      username: (taskInputs.systemAccessUser ?? '').trim()?.length > 0 ? taskInputs.systemAccessUser : 'x-access-token',
      password: taskInputs.systemAccessToken,
    });
  }
  if (taskInputs.githubAccessToken) {
    // Required to avoid rate-limiting errors when generating pull request descriptions (e.g. fetching release notes, commit messages, etc)
    credentials.push({
      type: 'git_source',
      host: 'github.com',
      username: 'x-access-token',
      password: taskInputs.githubAccessToken,
    });
  }
  if (registries) {
    // Required to authenticate with private package feeds when finding the latest version of dependencies.
    // The registries have already been worked on (see parseRegistries) so there is no need to do anything else.
    credentials.push(...Object.values(registries));
  }

  return credentials;
}

export function mapExperiments(experiments?: DependabotExperiments): DependabotExperiments {
  experiments ??= {};
  return Object.keys(experiments).reduce((acc, key) => {
    // Experiment values are known to be either 'true', 'false', or a string value.
    // If the value is 'true' or 'false', convert it to a boolean type so that dependabot-core handles it correctly.
    const value = experiments[key];
    if (typeof value === 'string' && value?.toLocaleLowerCase() === 'true') {
      acc[key] = true;
    } else if (typeof value === 'string' && value?.toLocaleLowerCase() === 'false') {
      acc[key] = false;
    } else {
      if (typeof value === 'string' || typeof value === 'boolean') acc[key] = value;
    }
    return acc;
  }, {} as DependabotExperiments);
}

export function mapPackageEcosystemToPackageManager(ecosystem: PackageEcosystem): DependabotPackageManager {
  // Map the dependabot config "package ecosystem" to the equivalent dependabot-core/cli "package manager".
  // Config values: https://docs.github.com/en/code-security/dependabot/working-with-dependabot/dependabot-options-reference#package-ecosystem-
  // Core/CLI values: https://github.com/dependabot/dependabot-core/blob/main/common/lib/dependabot/config/file.rb#L60-L81
  switch (ecosystem) {
    case 'docker-compose':
      return 'docker_compose';
    case 'dotnet-sdk':
      return 'dotnet_sdk';
    case 'github-actions':
      return 'github_actions';
    case 'gitsubmodule':
      return 'submodules';
    case 'gomod':
      return 'go_modules';
    case 'mix':
      return 'hex';
    case 'npm':
      return 'npm_and_yarn';
    // Additional aliases, sometimes used for convenience
    case 'pipenv':
      return 'pip';
    case 'pip-compile':
      return 'pip';
    case 'poetry':
      return 'pip';
    case 'pnpm':
      return 'npm_and_yarn';
    case 'yarn':
      return 'npm_and_yarn';
    default:
      return ecosystem;
  }
}
