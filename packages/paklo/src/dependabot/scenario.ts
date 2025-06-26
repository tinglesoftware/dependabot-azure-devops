import { z } from 'zod/v4';
import { DependabotJobConfigSchema } from './job';
import { DependabotCredentialSchema } from './proxy';
import {
  DependabotClosePullRequestSchema,
  DependabotCreatePullRequestSchema,
  DependabotIncrementMetricSchema,
  DependabotMarkAsProcessedSchema,
  DependabotRecordEcosystemMetaSchema,
  DependabotRecordEcosystemVersionsSchema,
  DependabotRecordUpdateJobErrorSchema,
  DependabotRecordUpdateJobUnknownErrorSchema,
  DependabotUpdateDependencyListSchema,
  DependabotUpdatePullRequestSchema,
} from './update';

export const DependabotInputSchema = z.object({
  job: DependabotJobConfigSchema,
  credentials: DependabotCredentialSchema.array(),
});
export type DependabotInput = z.infer<typeof DependabotInputSchema>;

export const DependabotOutputTypeSchema = z.enum([
  'create_pull_request',
  'update_pull_request',
  'close_pull_request',
  'record_update_job_error',
  'record_update_job_unknown_error',
  'mark_as_processed',
  'update_dependency_list',
  'record_ecosystem_versions',
  'record_ecosystem_meta',
  'increment_metric',
]);
export type DependabotOutputType = z.infer<typeof DependabotOutputTypeSchema>;

export const DependabotOutputSchema = z.discriminatedUnion('type', [
  z.object({ type: z.literal('create_pull_request'), expect: z.object({ data: DependabotCreatePullRequestSchema }) }),
  z.object({ type: z.literal('update_pull_request'), expect: z.object({ data: DependabotUpdatePullRequestSchema }) }),
  z.object({ type: z.literal('close_pull_request'), expect: z.object({ data: DependabotClosePullRequestSchema }) }),
  z.object({
    type: z.literal('record_update_job_error'),
    expect: z.object({ data: DependabotRecordUpdateJobErrorSchema }),
  }),
  z.object({
    type: z.literal('record_update_job_unknown_error'),
    expect: z.object({ data: DependabotRecordUpdateJobUnknownErrorSchema }),
  }),
  z.object({ type: z.literal('mark_as_processed'), expect: z.object({ data: DependabotMarkAsProcessedSchema }) }),
  z.object({
    type: z.literal('update_dependency_list'),
    expect: z.object({ data: DependabotUpdateDependencyListSchema }),
  }),
  z.object({
    type: z.literal('record_ecosystem_versions'),
    expect: z.object({ data: DependabotRecordEcosystemVersionsSchema }),
  }),
  z.object({
    type: z.literal('record_ecosystem_meta'),
    expect: z.object({ data: DependabotRecordEcosystemMetaSchema.array() }),
  }),
  z.object({ type: z.literal('increment_metric'), expect: z.object({ data: DependabotIncrementMetricSchema }) }),
]);
export type DependabotOutput = z.infer<typeof DependabotOutputSchema>;

export const DependabotDataSchema = z.discriminatedUnion('type', [
  z.object({
    type: z.literal('create_pull_request'),
    data: DependabotCreatePullRequestSchema,
  }),
  z.object({
    type: z.literal('update_pull_request'),
    data: DependabotUpdatePullRequestSchema,
  }),
  z.object({
    type: z.literal('close_pull_request'),
    data: DependabotClosePullRequestSchema,
  }),
  z.object({
    type: z.literal('record_update_job_error'),
    data: DependabotRecordUpdateJobErrorSchema,
  }),
  z.object({
    type: z.literal('record_update_job_unknown_error'),
    data: DependabotRecordUpdateJobUnknownErrorSchema,
  }),
  z.object({
    type: z.literal('mark_as_processed'),
    data: DependabotMarkAsProcessedSchema,
  }),
  z.object({
    type: z.literal('update_dependency_list'),
    data: DependabotUpdateDependencyListSchema,
  }),
  z.object({
    type: z.literal('record_ecosystem_versions'),
    data: DependabotRecordEcosystemVersionsSchema,
  }),
  z.object({
    type: z.literal('record_ecosystem_meta'),
    data: DependabotRecordEcosystemMetaSchema.array(),
  }),
  z.object({
    type: z.literal('increment_metric'),
    data: DependabotIncrementMetricSchema,
  }),
]);
export type DependabotData = z.infer<typeof DependabotDataSchema>;

export const DependabotScenarioSchema = z.object({
  input: DependabotInputSchema,
  output: DependabotOutputSchema.array(),
});
export type DependabotScenario = z.infer<typeof DependabotScenarioSchema>;
