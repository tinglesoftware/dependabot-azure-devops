import { z } from 'zod/v4';
import { DependabotDependencySchema } from './job';

// we use nullish() because it does optional() and allows the value to be set to null

export const DependabotDependencyFileSchema = z.object({
  content: z.string(),
  content_encoding: z.string().nullish(),
  deleted: z.boolean().nullish(),
  directory: z.string(),
  name: z.string(),
  operation: z.string(), // TODO: convert to enum?
  support_file: z.boolean().nullish(),
  symlink_target: z.string().nullish(),
  type: z.string().nullish(), // TODO: convert to enum?
  mode: z.string().nullish(),
});
export type DependabotDependencyFile = z.infer<typeof DependabotDependencyFileSchema>;

export const DependabotUpdateDependencyListSchema = z.object({
  dependencies: DependabotDependencySchema.array(),
  dependency_files: z.string().array().nullish(),
});
export type DependabotUpdateDependencyList = z.infer<typeof DependabotUpdateDependencyListSchema>;

export const DependabotCreatePullRequestSchema = z.object({
  'base-commit-sha': z.string(),
  'dependencies': DependabotDependencySchema.array(),
  'updated-dependency-files': DependabotDependencyFileSchema.array(),
  'pr-title': z.string(),
  'pr-body': z.string().nullish(),
  'commit-message': z.string(),
  'dependency-group': z.record(z.string(), z.any()).nullish(),
});
export type DependabotCreatePullRequest = z.infer<typeof DependabotCreatePullRequestSchema>;

export const DependabotUpdatePullRequestSchema = z.object({
  'base-commit-sha': z.string(),
  'dependency-names': z.string().array(),
  'updated-dependency-files': DependabotDependencyFileSchema.array(),
  'pr-title': z.string(),
  'pr-body': z.string().nullish(),
  'commit-message': z.string(),
  'dependency-group': z.record(z.string(), z.any()).nullish(),
});
export type DependabotUpdatePullRequest = z.infer<typeof DependabotUpdatePullRequestSchema>;

export const DependabotClosePullRequestSchema = z.object({
  'dependency-names': z.string().array(),
  'reason': z.string().nullish(), // TODO: convert to enum?
});
export type DependabotClosePullRequest = z.infer<typeof DependabotClosePullRequestSchema>;

export const DependabotMarkAsProcessedSchema = z.object({
  'base-commit-sha': z.string().nullish(),
});
export type DependabotMarkAsProcessed = z.infer<typeof DependabotMarkAsProcessedSchema>;

export const DependabotRecordUpdateJobErrorSchema = z.object({
  'error-type': z.string(),
  'error-details': z.record(z.string(), z.any()).nullish(),
});
export type DependabotRecordUpdateJobError = z.infer<typeof DependabotRecordUpdateJobErrorSchema>;

export const DependabotRecordUpdateJobUnknownErrorSchema = z.object({
  'error-type': z.string(),
  'error-details': z.record(z.string(), z.any()).nullish(),
});
export type DependabotRecordUpdateJobUnknownError = z.infer<typeof DependabotRecordUpdateJobUnknownErrorSchema>;

export const DependabotRecordEcosystemVersionsSchema = z.object({
  ecosystem_versions: z.record(z.string(), z.any()).nullish(),
});
export type DependabotRecordEcosystemVersions = z.infer<typeof DependabotRecordEcosystemVersionsSchema>;

export const DependabotEcosystemVersionManagerSchema = z.object({
  name: z.string(),
  version: z.string(),
  raw_version: z.string(),
  requirement: z.record(z.string(), z.any()).nullish(),
});
export type DependabotEcosystemVersionManager = z.infer<typeof DependabotEcosystemVersionManagerSchema>;

export const DependabotEcosystemMetaSchema = z.object({
  name: z.string(),
  package_manager: DependabotEcosystemVersionManagerSchema.nullish(),
  version: DependabotEcosystemVersionManagerSchema.nullish(),
});
export type DependabotEcosystemMeta = z.infer<typeof DependabotEcosystemMetaSchema>;

export const DependabotRecordEcosystemMetaSchema = z.object({
  ecosystem: DependabotEcosystemMetaSchema,
});
export type DependabotRecordEcosystemMeta = z.infer<typeof DependabotRecordEcosystemMetaSchema>;

export const DependabotIncrementMetricSchema = z.object({
  metric: z.string(),
  tags: z.record(z.string(), z.any()).nullish(),
});
export type DependabotIncrementMetric = z.infer<typeof DependabotIncrementMetricSchema>;
