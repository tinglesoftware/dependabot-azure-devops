import * as yaml from 'js-yaml';
import { URL } from 'url';
import { z } from 'zod/v4';

import { convertPlaceholder, type VariableFinderFn } from './placeholder';

export const DependabotRegistrySchema = z
  .object({
    'type': z.enum([
      'composer-repository',
      'docker-registry',
      'git',
      'hex-organization',
      'hex-repository',
      'maven-repository',
      'npm-registry',
      'nuget-feed',
      'python-index',
      'rubygems-server',
      'terraform-registry',
    ]),
    'url': z.string().optional(),
    'username': z.string().optional(),
    'password': z.string().optional(),
    'key': z.string().optional(),
    'token': z.string().optional(),
    'replaces-base': z.boolean().optional(),
    'host': z.string().optional(), // for terraform and composer only
    'registry': z.string().optional(), // for npm only
    'organization': z.string().optional(), // for hex-organisation only
    'repo': z.string().optional(), // for hex-repository only
    'public-key-fingerprint': z.string().optional(), // for hex-repository only
    'index-url': z.string().optional(), // for python-index only
    'auth-key': z.string().optional(), // used by composer-repository, docker-registry, etc
  })
  // change underscore to dash in the registry key/type
  .transform((value) => ({ ...value, type: value.type.replace('-', '_') }));
export type DependabotRegistry = z.infer<typeof DependabotRegistrySchema>;

export const DependabotGroupSchema = z.object({
  // Define an identifier for the group to use in branch names and pull request titles.
  // This must start and end with a letter, and can contain letters, pipes |, underscores _, or hyphens -.
  'IDENTIFIER': z
    .string()
    .check(
      z.regex(/^[a-zA-Z][a-zA-Z0-9|_-]*[a-zA-Z]$/, {
        message:
          'Group identifier must start and end with a letter, and can contain letters, pipes |, underscores _, or hyphens -.',
      }),
    )
    .optional(),
  'applies-to': z.enum(['version-updates', 'security-updates']).optional(),
  'dependency-type': z.enum(['development', 'production']).optional(),
  'patterns': z.string().array().optional(),
  'exclude-patterns': z.string().array().optional(),
  'update-types': z.enum(['major', 'minor', 'patch']).array().optional(),
});
export type DependabotGroup = z.infer<typeof DependabotGroupSchema>;

export const DependabotAllowConditionSchema = z.object({
  'dependency-name': z.string().optional(),
  'dependency-type': z.enum(['direct', 'indirect', 'all', 'production', 'development']).optional(),
});
export type DependabotAllowCondition = z.infer<typeof DependabotAllowConditionSchema>;

export const DependabotIgnoreConditionSchema = z.object({
  'dependency-name': z.string().optional(),
  'versions': z.string().array().optional(),
  'update-types': z
    .enum(['version-update:semver-major', 'version-update:semver-minor', 'version-update:semver-patch'])
    .array()
    .optional(),
});
export type DependabotIgnoreCondition = z.infer<typeof DependabotIgnoreConditionSchema>;

export const DependabotScheduleSchema = z.object({
  interval: z.enum(['daily', 'weekly', 'monthly', 'quarterly', 'semiannually', 'yearly', 'cron']),

  day: z
    .enum(['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'])
    .optional()
    .default('monday'),

  time: z
    .string()
    .default('02:00')
    .check(z.regex(/^(0[0-9]|1[0-9]|2[0-3]):[0-5][0-9]$/, { message: 'Time must be in HH:MM format' }))
    .optional(),

  timezone: z
    .string()
    .optional()
    .default('Etc/UTC')
    .refine(
      (value) => {
        try {
          // If tz is not a valid IANA name, this throws a RangeError
          Intl.DateTimeFormat(undefined, { timeZone: value });
          return true;
        } catch {
          return false;
        }
      },
      { message: 'Invalid IANA time zone' },
    ),
  cronjob: z
    .string()
    .check(z.regex(/^\S+ \S+ \S+ \S+ \S+$/, { message: 'Cronjob must be in standard cron format' }))
    .optional(),
});
export type DependabotSchedule = z.infer<typeof DependabotScheduleSchema>;

export const DependabotCommitMessageSchema = z.object({
  'prefix': z.string().optional(),
  'prefix-development': z.string().optional(),
  'include': z.string().optional(),
});
export type DependabotCommitMessage = z.infer<typeof DependabotCommitMessageSchema>;

export const DependabotCooldownSchema = z.object({
  'default-days': z.number().optional(),
  'semver-major-days': z.number().optional(),
  'semver-minor-days': z.number().optional(),
  'semver-patch-days': z.number().optional(),
  'include': z.string().array().optional(),
  'exclude': z.string().array().optional(),
});
export type DependabotCooldown = z.infer<typeof DependabotCooldownSchema>;

const DependabotPullRequestBranchNameSchema = z.object({
  separator: z.string().optional(),
});
export type DependabotPullRequestBranchName = z.infer<typeof DependabotPullRequestBranchNameSchema>;

export const PackageEcosystemSchema = z.enum([
  'bun',
  'bundler',
  'cargo',
  'composer',
  'devcontainers',
  'docker',
  'docker-compose',
  'dotnet-sdk',
  'helm',
  'mix',
  'elm',
  'gitsubmodule',
  'github-actions',
  'gomod',
  'gradle',
  'maven',
  'npm',
  'nuget',
  'pip',
  'pub',
  'swift',
  'terraform',
  'uv',

  // Additional aliases, sometimes used for convenience
  'pipenv',
  'pip-compile',
  'poetry',
  'pnpm',
  'yarn',
]);
export type PackageEcosystem = z.infer<typeof PackageEcosystemSchema>;

export const DependabotUpdateSchema = z
  .object({
    'package-ecosystem': PackageEcosystemSchema.optional(),
    'directory': z.string().optional(),
    'directories': z.string().array().optional(),
    'allow': DependabotAllowConditionSchema.array().optional(),
    'assignees': z.string().array().optional(),
    'commit-message': DependabotCommitMessageSchema.optional(),
    'cooldown': DependabotCooldownSchema.optional(),
    'groups': z.record(z.string(), DependabotGroupSchema).optional(),
    'ignore': DependabotIgnoreConditionSchema.and(z.record(z.string(), z.any())).array().optional(),
    'insecure-external-code-execution': z.enum(['allow', 'deny']).optional(),
    'labels': z.string().array().optional(),
    'milestone': z.coerce.string().optional(),
    'open-pull-requests-limit': z.number().check(z.int(), z.gte(0)).optional(),
    'pull-request-branch-name': DependabotPullRequestBranchNameSchema.optional(),
    'rebase-strategy': z.string().optional(),
    'registries': z.string().array().optional(),
    'schedule': DependabotScheduleSchema.optional(),
    'target-branch': z.string().optional(),
    'vendor': z.boolean().optional(),
    'versioning-strategy': z.string().optional(),
  })
  .transform((value, { addIssue }) => {
    // either 'directory' or 'directories' must be specified
    if (!value.directory && (!value.directories || value.directories.length === 0)) {
      addIssue("Either 'directory' or 'directories' must be specified in the dependency update configuration.");
    }

    value['open-pull-requests-limit'] ??= 5; // default to 5 if not specified

    return value;
  });
export type DependabotUpdate = z.infer<typeof DependabotUpdateSchema>;

/**
 * Represents the dependabot.yaml configuration file options.
 * See: https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#configuration-options-for-dependabotyml
 */
export const DependabotConfigSchema = z.object({
  /**
   * Mandatory. configuration file version.
   **/
  'version': z.number().refine((v) => v === 2, { message: 'Only version 2 of dependabot is supported' }),

  /**
   * Mandatory. Configure how Dependabot updates the versions or project dependencies.
   * Each entry configures the update settings for a particular package manager.
   */
  'updates': DependabotUpdateSchema.array().check(
    z.minLength(1, { message: 'At least one update configuration is required' }),
  ),

  /**
   * Optional.
   * Specify authentication details to access private package registries.
   */
  'registries': z.record(z.string(), DependabotRegistrySchema).optional(),

  /**
   * Optional. Enables updates for ecosystems that are not yet generally available.
   * https://docs.github.com/en/code-security/dependabot/working-with-dependabot/dependabot-options-reference#enable-beta-ecosystems-
   */
  'enable-beta-ecosystems': z.boolean().optional(),
});

export type DependabotConfig = z.infer<typeof DependabotConfigSchema>;

/**
 * Parse the contents of a dependabot config YAML file
 * @returns {DependabotConfig} config - the dependabot configuration
 */
export async function parseDependabotConfig({
  configContents,
  configPath,
  variableFinder,
}: {
  configContents: string;
  configPath: string;
  variableFinder: VariableFinderFn;
}): Promise<DependabotConfig> {
  // Load the config
  const loadedConfig = yaml.load(configContents);
  if (loadedConfig === null || typeof loadedConfig !== 'object') {
    throw new Error('Invalid dependabot config object');
  }

  // Parse the config
  const config = await DependabotConfigSchema.parseAsync(loadedConfig);
  const updates = parseUpdates(config, configPath);
  const registries = parseRegistries(config, variableFinder);
  validateConfiguration(updates, registries);

  return { ...config, updates, registries };
}

export function parseUpdates(config: DependabotConfig, configPath: string): DependabotUpdate[] {
  const updates: DependabotUpdate[] = [];

  // Parse the value of each of the updates obtained from the file
  for (const update of config['updates']) {
    // populate the 'ignore' conditions 'source' and 'updated-at' properties, if missing
    // NOTE: 'source' and 'updated-at' are not documented in the dependabot.yml config docs, but are defined in the dependabot-core and dependabot-cli models.
    //       Currently they don't appear to add much value to the update process, but are populated here for completeness.
    if (update.ignore) {
      for (const condition of update.ignore) {
        condition['source'] ??= configPath;
        // we don't know the last updated time, so we use the current time
        condition['updated-at'] ??= new Date().toISOString();
      }
    }

    updates.push(update);
  }
  return updates;
}

export function parseRegistries(
  config: DependabotConfig,
  variableFinder: VariableFinderFn,
): Record<string, DependabotRegistry> {
  // Parse the value of each of the registries obtained from the config
  const registries: Record<string, DependabotRegistry> = {};
  for (const [key, registry] of Object.entries(config['registries'] || {})) {
    const updated = { ...registry };
    const { type } = updated;

    // handle special fields for 'hex-organization' types
    if (type === 'hex_organization' && !updated.organization) {
      throw new Error(`The value 'organization' in dependency registry config '${type}' is missing`);
    }

    // handle special fields for 'hex-repository' types
    if (type === 'hex_repository' && !updated.repo) {
      throw new Error(`The value 'repo' in dependency registry config '${key}' is missing`);
    }

    // parse username, password, key, and token while replacing tokens where necessary
    updated.username = convertPlaceholder({ input: updated.username, variableFinder: variableFinder });
    updated.password = convertPlaceholder({ input: updated.password, variableFinder: variableFinder });
    updated.key = convertPlaceholder({ input: updated.key, variableFinder: variableFinder });
    updated.token = convertPlaceholder({ input: updated.token, variableFinder: variableFinder });

    // parse the url
    const url = updated['url'];
    if (!url && type !== 'hex_organization') {
      throw new Error(`The value 'url' in dependency registry config '${key}' is missing`);
    }
    if (url) {
      /*
       * Some credentials do not use the 'url' property in the Ruby updater.
       * The 'host' and 'registry' properties are derived from the given URL.
       * The 'registry' property is derived from the 'url' by stripping off the scheme.
       * The 'host' property is derived from the hostname of the 'url'.
       *
       * 'npm_registry' and 'docker_registry' use 'registry' only.
       * 'terraform_registry' uses 'host' only.
       * 'composer_repository' uses both 'url' and 'host'.
       * 'python_index' uses 'index-url' instead of 'url'.
       */

      if (URL.canParse(url)) {
        const parsedUrl = new URL(url);

        const addRegistry = type === 'docker_registry' || type === 'npm_registry';
        if (addRegistry) updated.registry = url.replace('https://', '').replace('http://', '');

        const addHost = type === 'terraform_registry' || type === 'composer_repository';
        if (addHost) updated.host = parsedUrl.hostname;
      }

      if (type === 'python_index') updated['index-url'] = url;

      const removeUrl =
        type === 'docker_registry' ||
        type === 'npm_registry' ||
        type === 'terraform_registry' ||
        type === 'python_index';
      if (removeUrl) delete updated.url; // remove the url if not needed
    }

    // add to list
    registries[key] = updated;
  }
  return registries;
}

export function validateConfiguration(updates: DependabotUpdate[], registries: Record<string, DependabotRegistry>) {
  const configured = Object.keys(registries);
  const referenced: string[] = [];
  for (const u of updates) referenced.push(...(u.registries ?? []));

  // ensure there are no configured registries that have not been referenced
  const missingConfiguration = referenced.filter((el) => !configured.includes(el));
  if (missingConfiguration.length > 0) {
    throw new Error(
      `Referenced registries: '${missingConfiguration.join(',')}' have not been configured in the root of dependabot.yml`,
    );
  }

  // ensure there are no registries referenced but not configured
  const missingReferences = configured.filter((el) => !referenced.includes(el));
  if (missingReferences.length > 0) {
    throw new Error(`Registries: '${missingReferences.join(',')}' have not been referenced by any update`);
  }
}
