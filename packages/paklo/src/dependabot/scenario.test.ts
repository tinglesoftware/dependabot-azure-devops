import { createReadStream } from 'fs';
import { readFile } from 'fs/promises';
import * as yaml from 'js-yaml';
import * as readline from 'readline';
import { describe, expect, it } from 'vitest';

import { DependabotDataSchema, DependabotInputSchema, DependabotScenarioSchema, type DependabotData } from './scenario';

describe('input', () => {
  it('python-pip', async () => {
    const raw = yaml.load(await readFile('fixtures/jobs/python-pip.yaml', 'utf-8'));
    const input = DependabotInputSchema.parse(raw);

    // parsing is enough to test that we generated the correct job schema
    // but we test a few fields to be sure
    expect(input.job.id).toEqual('update-0-pip-all');
    expect(input.job['package-manager']).toEqual('pip');
    expect(input.job['credentials-metadata']).toBeDefined();
    expect(input.credentials[0]!.type).toEqual('git_source');
    expect(input.credentials[0]!.host).toEqual('dev.azure.com');
    expect(input.credentials[0]!.username).toEqual('x-access-token');
    expect(input.credentials[0]!.password).toEqual('01');
  });
});

describe('scenario', () => {
  it('python-pip', async () => {
    const raw = yaml.load(await readFile('fixtures/scenarios/python-pip.yaml', 'utf-8'));
    const scenario = DependabotScenarioSchema.parse(raw);

    // parsing is enough to test that we have the right schema
    // but we test a few fields to be sure
    expect(scenario.input.job.id).toBeUndefined();
    expect(scenario.input.job['package-manager']).toEqual('pip');
    expect(scenario.input.job['credentials-metadata']).toBeUndefined();
    expect(scenario.input.credentials[0]!.type).toEqual('git_source');
    expect(scenario.input.credentials[0]!.host).toEqual('dev.azure.com');
    expect(scenario.input.credentials[0]!.username).toEqual('x-access-token');
    expect(scenario.input.credentials[0]!.password).toEqual('01');
    expect(scenario.output.length).toBe(46);
    expect(scenario.output.filter((o) => o.type == 'create_pull_request').length).toBe(18);
    expect(scenario.output.filter((o) => o.type == 'update_pull_request').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'close_pull_request').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'record_update_job_error').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'record_update_job_unknown_error').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'mark_as_processed').length).toBe(1);
    expect(scenario.output.filter((o) => o.type == 'update_dependency_list').length).toBe(1);
    expect(scenario.output.filter((o) => o.type == 'record_ecosystem_versions').length).toBe(1);
    expect(scenario.output.filter((o) => o.type == 'record_ecosystem_meta').length).toBe(25);
    expect(scenario.output.filter((o) => o.type == 'increment_metric').length).toBe(0);
  });

  it('nuget', async () => {
    const raw = yaml.load(await readFile('fixtures/scenarios/nuget.yaml', 'utf-8'));
    const scenario = DependabotScenarioSchema.parse(raw);

    // parsing is enough to test that we have the right schema
    // but we test a few fields to be sure
    expect(scenario.input.job.id).toBeUndefined();
    expect(scenario.input.job['package-manager']).toEqual('nuget');
    expect(scenario.input.job['credentials-metadata']).toBeUndefined();
    expect(scenario.input.credentials[0]!.type).toEqual('git_source');
    expect(scenario.input.credentials[0]!.host).toEqual('dev.azure.com');
    expect(scenario.input.credentials[0]!.username).toEqual('x-access-token');
    expect(scenario.input.credentials[0]!.password).toEqual('01');
    expect(scenario.output.length).toBe(2);
    expect(scenario.output.filter((o) => o.type == 'create_pull_request').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'update_pull_request').length).toBe(1);
    expect(scenario.output.filter((o) => o.type == 'close_pull_request').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'record_update_job_error').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'record_update_job_unknown_error').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'mark_as_processed').length).toBe(1);
    expect(scenario.output.filter((o) => o.type == 'update_dependency_list').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'record_ecosystem_versions').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'record_ecosystem_meta').length).toBe(0);
    expect(scenario.output.filter((o) => o.type == 'increment_metric').length).toBe(0);
  });
});

describe('result data', () => {
  it('python-pip.jsonl', async () => {
    const data = await readDependabotData('fixtures/scenarios/python-pip.jsonl');

    // parsing is enough to test that we have the right schema
    // but we test a few fields to be sure
    expect(data.length).toBe(46);
    expect(data.filter((o) => o.type == 'create_pull_request').length).toBe(18);
    expect(data.filter((o) => o.type == 'update_pull_request').length).toBe(0);
    expect(data.filter((o) => o.type == 'close_pull_request').length).toBe(0);
    expect(data.filter((o) => o.type == 'record_update_job_error').length).toBe(0);
    expect(data.filter((o) => o.type == 'record_update_job_unknown_error').length).toBe(0);
    expect(data.filter((o) => o.type == 'mark_as_processed').length).toBe(1);
    expect(data.filter((o) => o.type == 'update_dependency_list').length).toBe(1);
    expect(data.filter((o) => o.type == 'record_ecosystem_versions').length).toBe(1);
    expect(data.filter((o) => o.type == 'record_ecosystem_meta').length).toBe(25);
    expect(data.filter((o) => o.type == 'increment_metric').length).toBe(0);
  });

  it('nuget.jsonl', async () => {
    const data = await readDependabotData('fixtures/scenarios/nuget.jsonl');

    // parsing is enough to test that we have the right schema
    // but we test a few fields to be sure
    expect(data.length).toBe(2);
    expect(data.filter((o) => o.type == 'create_pull_request').length).toBe(0);
    expect(data.filter((o) => o.type == 'update_pull_request').length).toBe(1);
    expect(data.filter((o) => o.type == 'close_pull_request').length).toBe(0);
    expect(data.filter((o) => o.type == 'record_update_job_error').length).toBe(0);
    expect(data.filter((o) => o.type == 'record_update_job_unknown_error').length).toBe(0);
    expect(data.filter((o) => o.type == 'mark_as_processed').length).toBe(1);
    expect(data.filter((o) => o.type == 'update_dependency_list').length).toBe(0);
    expect(data.filter((o) => o.type == 'record_ecosystem_versions').length).toBe(0);
    expect(data.filter((o) => o.type == 'record_ecosystem_meta').length).toBe(0);
    expect(data.filter((o) => o.type == 'increment_metric').length).toBe(0);
  });
});

async function readDependabotData(path: string): Promise<DependabotData[]> {
  const rl = readline.createInterface({
    input: createReadStream(path, { encoding: 'utf-8' }),
    crlfDelay: Infinity,
  });

  const outputArray: DependabotData[] = [];
  for await (const line of rl) {
    const json = JSON.parse(line);
    const output = await DependabotDataSchema.parseAsync(json);
    outputArray.push(output);
  }

  return outputArray;
}
