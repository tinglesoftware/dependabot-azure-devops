import { type SecurityVulnerability } from '@/github';
import {
  type DependabotAllowCondition,
  type DependabotConfig,
  type DependabotGroup,
  type DependabotIgnoreCondition,
  type DependabotRegistry,
  type DependabotUpdate,
  type PackageEcosystem,
} from './config';
import {
  type DependabotAllowed,
  type DependabotCondition,
  type DependabotExistingGroupPR,
  type DependabotExistingPR,
  type DependabotExperiments,
  type DependabotGroupJob,
  type DependabotPackageManager,
  type DependabotSecurityAdvisory,
  type DependabotSource,
  type DependabotSourceProvider,
} from './job';
import { type DependabotCredential } from './proxy';
import { type DependabotData, type DependabotInput } from './scenario';

export type DependabotSourceInfo = {
  'provider': DependabotSourceProvider;
  'hostname': string;
  'api-endpoint': string;
  'repository-slug': string;
};

/** Represents a single Dependabot operation */
export type DependabotOperation = DependabotInput & { config: DependabotUpdate };

/** Represents the output of a Dependabot CLI update operation */
export type DependabotOperationResult = {
  success: boolean;
  error?: Error;
  output?: DependabotData;
  pr?: number;
};

/**
 * Class for building dependabot job objects
 */
export class DependabotJobBuilder {
  private readonly config: DependabotConfig;
  private readonly update: DependabotUpdate;
  private readonly experiments: DependabotExperiments;
  private readonly debug: boolean;

  private readonly packageManager: DependabotPackageManager;
  private readonly source: DependabotSource;
  private readonly credentials: DependabotCredential[];
  private readonly credentialsMetadata: DependabotCredential[];

  constructor({
    source,
    config,
    update,
    systemAccessUser,
    systemAccessToken,
    githubToken,
    experiments,
    debug,
  }: {
    source: DependabotSourceInfo;
    config: DependabotConfig;
    update: DependabotUpdate;
    experiments: DependabotExperiments;
    systemAccessUser?: string;
    systemAccessToken?: string;
    githubToken?: string;
    /** Determines if verbose log messages are logged */
    debug: boolean;
  }) {
    this.config = config;
    this.update = update;
    this.experiments = experiments;
    this.debug = debug;

    this.packageManager = mapPackageEcosystemToPackageManager(update['package-ecosystem']);
    this.source = mapSourceFromDependabotConfigToJobConfig(source, update);
    this.credentials = mapCredentials({
      sourceHostname: source.hostname,
      systemAccessUser,
      systemAccessToken,
      githubToken,
      registries: config.registries,
    });
    this.credentialsMetadata = makeCredentialsMetadata(this.credentials);
  }

  /**
   * Create a dependabot update job that updates nothing, but will discover the dependency list for a package ecosystem
   */
  public forDependenciesList({ id }: { id: string }): DependabotOperation {
    return {
      config: this.update,
      job: {
        'id': id,
        'package-manager': this.packageManager,
        'ignore-conditions': [{ 'dependency-name': '*' }],
        'source': this.source,
        'experiments': this.experiments,
        'debug': this.debug,
        'credentials-metadata': this.credentialsMetadata,
      },
      credentials: this.credentials,
    };
  }

  /**
   * Create a dependabot update job that updates all dependencies for a package ecosystem
   */
  public forUpdate({
    id,
    dependencyNamesToUpdate,
    existingPullRequests,
    pullRequestToUpdate,
    securityVulnerabilities,
  }: {
    id: string;
    dependencyNamesToUpdate?: string[];
    existingPullRequests?: (DependabotExistingPR[] | DependabotExistingGroupPR)[];
    pullRequestToUpdate?: DependabotExistingPR[] | DependabotExistingGroupPR;
    securityVulnerabilities?: SecurityVulnerability[];
  }): DependabotOperation {
    const securityOnlyUpdate = this.update['open-pull-requests-limit'] == 0;

    let updatingPullRequest: boolean;
    let updateDependencyGroupName: string | undefined;
    let updateDependencyNames: string[] | undefined;
    let vulnerabilities: SecurityVulnerability[] | undefined;

    if (pullRequestToUpdate) {
      updatingPullRequest = true;
      updateDependencyGroupName = Array.isArray(pullRequestToUpdate)
        ? undefined
        : pullRequestToUpdate['dependency-group-name'];
      updateDependencyNames = (
        Array.isArray(pullRequestToUpdate) ? pullRequestToUpdate : pullRequestToUpdate.dependencies
      )?.map((d) => d['dependency-name']);
      vulnerabilities = securityVulnerabilities?.filter((v) => updateDependencyNames?.includes(v.package.name));
    } else {
      updatingPullRequest = false;
      updateDependencyNames = securityOnlyUpdate
        ? dependencyNamesToUpdate?.filter((d) => securityVulnerabilities?.find((v) => v.package.name == d))
        : dependencyNamesToUpdate;
    }

    return {
      config: this.update,
      job: {
        'id': id,
        'package-manager': this.packageManager,
        'updating-a-pull-request': updatingPullRequest || false,
        'dependency-group-to-refresh': updateDependencyGroupName,
        'dependency-groups': mapGroupsFromDependabotConfigToJobConfig(this.update.groups),
        'dependencies': updateDependencyNames?.length ? updateDependencyNames : undefined,
        'allowed-updates': mapAllowedUpdatesFromDependabotConfigToJobConfig(this.update.allow, securityOnlyUpdate),
        'ignore-conditions': mapIgnoreConditionsFromDependabotConfigToJobConfig(this.update.ignore),
        'security-updates-only': securityOnlyUpdate,
        'security-advisories': mapSecurityAdvisories(vulnerabilities),
        'source': this.source,
        'existing-pull-requests': existingPullRequests?.filter((pr) => Array.isArray(pr)),
        'existing-group-pull-requests': existingPullRequests?.filter(
          (pr): pr is DependabotExistingGroupPR => !Array.isArray(pr),
        ),
        'commit-message-options':
          this.update['commit-message'] === undefined
            ? undefined
            : {
                'prefix': this.update['commit-message']?.prefix,
                'prefix-development': this.update['commit-message']?.['prefix-development'],
                'include-scope':
                  this.update['commit-message']?.include?.toLocaleLowerCase()?.trim() == 'scope' ? true : undefined,
              },
        'cooldown': this.update.cooldown,
        'experiments': mapExperiments(this.experiments),
        'reject-external-code': this.update['insecure-external-code-execution']?.toLocaleLowerCase()?.trim() == 'allow',
        'requirements-update-strategy': mapVersionStrategyToRequirementsUpdateStrategy(
          this.update['versioning-strategy'],
        ),
        'lockfile-only': this.update['versioning-strategy'] === 'lockfile-only',
        'vendor-dependencies': this.update.vendor,
        'debug': this.debug,
        'credentials-metadata': this.credentialsMetadata,
        'proxy-log-response-body-on-auth-failure': true,
        'max-updater-run-time': 2700,
        'enable-beta-ecosystems': this.config['enable-beta-ecosystems'] || false,
        // Updates across ecosystems is still in development
        // See https://github.com/dependabot/dependabot-core/issues/8126
        //     https://github.com/dependabot/dependabot-core/pull/12339
        // It needs to merged in the core repo first before we support it
        // However, to match current job configs and to prevent surprises, we disable it
        'multi-ecosystem-update': false,
      },
      credentials: this.credentials,
    };
  }
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

export function mapSourceFromDependabotConfigToJobConfig(
  source: DependabotSourceInfo,
  update: DependabotUpdate,
): DependabotSource {
  return {
    'provider': source.provider,
    'api-endpoint': source['api-endpoint'],
    'hostname': source.hostname,
    'repo': source['repository-slug'],
    'branch': update['target-branch'],
    'commit': undefined, // use latest commit of target branch
    'directory': update.directory,
    'directories': update.directories,
  };
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

export function mapCredentials({
  sourceHostname,
  systemAccessUser,
  systemAccessToken,
  githubToken,
  registries,
}: {
  sourceHostname: string;
  systemAccessUser?: string;
  systemAccessToken?: string;
  githubToken?: string;
  registries?: Record<string, DependabotRegistry>;
}): DependabotCredential[] {
  const credentials = [];

  // Required to authenticate with the git repository when cloning the source code
  if (systemAccessToken) {
    credentials.push({
      type: 'git_source',
      host: sourceHostname,
      username: (systemAccessUser ?? '').trim()?.length > 0 ? systemAccessUser : 'x-access-token',
      password: systemAccessToken,
    });
  }

  // Required to avoid rate-limiting errors when generating pull request descriptions (e.g. fetching release notes, commit messages, etc)
  if (githubToken) {
    credentials.push({
      type: 'git_source',
      host: 'github.com',
      username: 'x-access-token',
      password: githubToken,
    });
  }
  if (registries) {
    // TODO: only registries with secure information should be copied otherwise it does not quality to be a credential
    // TODO: only registries for the current update should be set
    // Required to authenticate with private package feeds when finding the latest version of dependencies.
    // The registries have already been worked on (see parseRegistries) so there is no need to do anything else.
    credentials.push(...Object.values(registries));
  }

  return credentials;
}

export function makeCredentialsMetadata(credentials: DependabotCredential[]): DependabotCredential[] {
  const sensitive = ['username', 'token', 'password', 'key', 'auth-key'];
  return credentials.map((cred) =>
    Object.fromEntries(Object.entries(cred).filter(([key]) => !sensitive.includes(key))),
  );
}
