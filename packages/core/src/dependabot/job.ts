import { z } from 'zod/v4';
import { DependabotCooldownSchema } from './config';
import { DependabotCredentialSchema } from './proxy';

export const DependabotSourceSchema = z.object({
  'provider': z.string(), // TODO: convert to enum?
  'repo': z.string(),
  'directory': z.string().optional(),
  'directories': z.string().array().optional(),
  'branch': z.string().optional(),
  'commit': z.string().optional(),
  'hostname': z.string().optional(), // Must be provided if api-endpoint is
  'api-endpoint': z.string().optional(), // Must be provided if hostname is
  // TODO: refine to ensure either directory or directories is provided
  // TODO: refine to ensure either both hostname and api-endpoint have a value or both are undefined
});
export type DependabotSource = z.infer<typeof DependabotSourceSchema>;

export const DependabotExistingPRSchema = z.object({
  'dependency-name': z.string(),
  'dependency-version': z.string().optional(),
  'directory': z.string().optional(),
});
export type DependabotExistingPR = z.infer<typeof DependabotExistingPRSchema>;

export const DependabotExistingGroupPRSchema = z.object({
  'dependency-group-name': z.string(),
  'dependencies': DependabotExistingPRSchema.array(),
});
export type DependabotExistingGroupPR = z.infer<typeof DependabotExistingGroupPRSchema>;

export const DependabotAllowedSchema = z.object({
  'dependency-name': z.string().optional(),
  'dependency-type': z.string().optional(),
  'update-type': z.string().optional(),
});
export type DependabotAllowed = z.infer<typeof DependabotAllowedSchema>;

export const DependabotGroupRuleJobSchema = z.object({
  'patterns': z.string().array().optional(),
  'exclude-patterns': z.string().array().optional(),
  'dependency-type': z.string().optional(),
  'update-types': z.string().array().optional(),
});
export type DependabotGroupRuleJob = z.infer<typeof DependabotGroupRuleJobSchema>;

export const DependabotGroupJobSchema = z.object({
  'name': z.string(),
  'applies-to': z.string().optional(),
  'rules': DependabotGroupRuleJobSchema,
});
export type DependabotGroupJob = z.infer<typeof DependabotGroupJobSchema>;

export const DependabotConditionSchema = z.object({
  'dependency-name': z.string(),
  'source': z.string().optional(),
  'update-types': z.string().array().optional(),
  'updated-at': z.string().optional(), // TODO: should we instead use a date here?
  'version-requirement': z.string().optional(),
});
export type DependabotCondition = z.infer<typeof DependabotConditionSchema>;

export const DependabotSecurityAdvisorySchema = z.object({
  'dependency-name': z.string(),
  'affected-versions': z.string().array(),
  'patched-versions': z.string().array(),
  'unaffected-versions': z.string().array(),
});
export type DependabotSecurityAdvisory = z.infer<typeof DependabotSecurityAdvisorySchema>;

export const DependabotRequirementSourceSchema = z.record(z.string(), z.any());
export type DependabotRequirementSource = z.infer<typeof DependabotRequirementSourceSchema>;

export const DependabotRequirementSchema = z.object({
  'file': z.string().optional(),
  'groups': z.any().array().optional(),
  'metadata': z.record(z.string(), z.any()).optional(),
  'requirement': z.string().optional(),
  'source': DependabotRequirementSourceSchema.optional(),
  'version': z.string().optional(),
  'previous-version': z.string().optional(),
});
export type DependabotRequirement = z.infer<typeof DependabotRequirementSchema>;

export const DependabotDependencySchema = z.object({
  'name': z.string(),
  'previous-requirements': DependabotRequirementSchema.array().optional(),
  'previous-version': z.string().optional(),
  'version': z.string().optional(),
  'requirements': DependabotRequirementSchema.array().optional(),
  'removed': z.boolean().optional(),
  'directory': z.string().optional(),
});
export type DependabotDependency = z.infer<typeof DependabotDependencySchema>;

export const DependabotCommitOptionsSchema = z.object({
  'prefix': z.string().optional(),
  'prefix-development': z.string().optional(),
  'include-scope': z.boolean().optional(),
});
export type DependabotCommitOptions = z.infer<typeof DependabotCommitOptionsSchema>;

export const DependabotExperimentsSchema = z.record(z.string(), z.union([z.string(), z.boolean()]));
export type DependabotExperiments = z.infer<typeof DependabotExperimentsSchema>;

export const DependabotPackageManagerSchema = z.enum([
  'bun',
  'bundler',
  'cargo',
  'composer',
  'devcontainers',
  'docker',
  'docker_compose', //  // ecosystem(s): 'docker-compose',
  'dotnet_sdk', // ecosystem(s): 'dotnet-sdk'
  'helm',
  'hex', // ecosystem(s): 'mix'
  'elm',
  'submodules', // ecosystem(s): 'gitsubmodule'
  'github_actions', // ecosystem(s): 'github-actions'
  'go_modules', // ecosystem(s): 'gomod'
  'gradle',
  'maven',
  'npm_and_yarn', // ecosystem(s): 'npm', 'pnpm', 'yarn'
  'nuget',
  'pip', // ecosystem(s): 'pipenv', 'pip-compile', 'poetry'
  'pub',
  'swift',
  'terraform',
  'uv',
]);
export type DependabotPackageManager = z.infer<typeof DependabotPackageManagerSchema>;

// See: https://github.com/dependabot/cli/blob/main/internal/model/job.go
//      https://github.com/dependabot/dependabot-core/blob/main/updater/lib/dependabot/job.rb
export const DependabotJobConfigSchema = z.object({
  'id': z.string(),
  'package-manager': DependabotPackageManagerSchema,
  'allowed-updates': DependabotAllowedSchema.array().optional(),
  'debug': z.boolean().optional(),
  'dependency-groups': DependabotGroupJobSchema.array().optional(),
  'dependencies': z.string().array().optional(),
  'dependency-group-to-refresh': z.string().optional(),
  'existing-pull-requests': DependabotExistingPRSchema.array().array().optional(),
  'existing-group-pull-requests': DependabotExistingGroupPRSchema.array().optional(),
  'experiments': DependabotExperimentsSchema,
  'ignore-conditions': DependabotConditionSchema.array().optional(),
  'lockfile-only': z.boolean().optional(),
  'requirements-update-strategy': z.string().optional(),
  'security-advisories': DependabotSecurityAdvisorySchema.array().optional(),
  'security-updates-only': z.boolean().optional(),
  'source': DependabotSourceSchema,
  'update-subdependencies': z.boolean().optional(),
  'updating-a-pull-request': z.boolean().optional(),
  'vendor-dependencies': z.boolean().optional(),
  'reject-external-code': z.boolean().optional(),
  'repo-private': z.boolean().optional(),
  'commit-message-options': DependabotCommitOptionsSchema.optional(),
  'credentials-metadata': DependabotCredentialSchema.array(),
  'max-updater-run-time': z.int().optional(),
  'cooldown': DependabotCooldownSchema.optional(),
});
export type DependabotJobConfig = z.infer<typeof DependabotJobConfigSchema>;

export const DependabotJobFileSchema = z.object({
  job: DependabotJobConfigSchema,
});
export type DependabotJobFile = z.infer<typeof DependabotJobFileSchema>;
