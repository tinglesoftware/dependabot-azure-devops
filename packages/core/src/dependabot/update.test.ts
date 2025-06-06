import { readFile } from 'fs/promises';
import { describe, expect, it } from 'vitest';

import {
  DependabotCreatePullRequestSchema,
  DependabotIncrementMetricSchema,
  DependabotMarkAsProcessedSchema,
  DependabotRecordEcosystemMetaSchema,
  DependabotRecordEcosystemVersionsSchema,
  DependabotUpdateDependencyListSchema,
} from './update';

describe('create_pull_request', () => {
  it('python-pip', async () => {
    const raw = JSON.parse(await readFile('fixtures/create_pull_request/python-pip.json', 'utf-8'));
    const data = DependabotCreatePullRequestSchema.parse(raw['data']);

    expect(data['base-commit-sha']).toEqual('ae42ad6aa4577ec148752865a5edcf2eb2ac2df7');

    expect(data['dependencies']!.length).toEqual(1);
    expect(data['dependencies'][0]!.name).toEqual('openai');
    expect(data['dependencies'][0]!['previous-requirements']!.length).toEqual(1);
    expect(data['dependencies'][0]!['previous-requirements']![0]!.file).toEqual('pyproject.toml');
    expect(data['dependencies'][0]!['previous-requirements']![0]!.groups).toEqual([]);
    expect(data['dependencies'][0]!['previous-requirements']![0]!.requirement).toEqual('==1.63.0');
    expect(data['dependencies'][0]!['previous-requirements']![0]!.source).toBeNull();
    expect(data['dependencies'][0]!['previous-version']).toEqual('1.63.0');

    expect(data['dependencies'][0]!.requirements!.length).toEqual(1);
    expect(data['dependencies'][0]!.requirements![0]!.file).toEqual('pyproject.toml');
    expect(data['dependencies'][0]!.requirements![0]!.groups).toEqual([]);
    expect(data['dependencies'][0]!.requirements![0]!.requirement).toEqual('==1.84.0');
    expect(data['dependencies'][0]!.requirements![0]!.source).toBeNull();
    expect(data['dependencies'][0]!.version).toEqual('1.84.0');
    expect(data['dependencies'][0]!.directory).toEqual('/');

    expect(data['updated-dependency-files'].length).toEqual(1);
    expect(data['updated-dependency-files'][0]!.content.length).toBeGreaterThan(20);
    expect(data['updated-dependency-files'][0]!.content_encoding).toEqual('utf-8');
    expect(data['updated-dependency-files'][0]!.deleted).toEqual(false);
    expect(data['updated-dependency-files'][0]!.directory).toEqual('/');
    expect(data['updated-dependency-files'][0]!.name).toEqual('pyproject.toml');
    expect(data['updated-dependency-files'][0]!.operation).toEqual('update');
    expect(data['updated-dependency-files'][0]!.support_file).toEqual(false);
    expect(data['updated-dependency-files'][0]!.type).toEqual('file');
    expect(data['updated-dependency-files'][0]!.mode).toEqual('');

    expect(data['pr-title']).toEqual('build: bump openai from 1.63.0 to 1.84.0');
    expect(data['pr-body'].length).toBeGreaterThan(20);
    expect(data['commit-message'].length).toBeGreaterThan(20);
    expect(data['dependency-group']).toBeNull();
  });
});

describe('mark_as_processed', () => {
  it('simple', async () => {
    const raw = JSON.parse(await readFile('fixtures/mark_as_processed/simple.json', 'utf-8'));
    const data = DependabotMarkAsProcessedSchema.parse(raw['data']);
    expect(data).toBeDefined();
    expect(data['base-commit-sha']).toEqual('ae42ad6aa4577ec148752865a5edcf2eb2ac2df7');
  });
});

describe('update_dependency_list', () => {
  it('python-pip', async () => {
    const raw = JSON.parse(await readFile('fixtures/update_dependency_list/python-pip.json', 'utf-8'));
    const data = DependabotUpdateDependencyListSchema.parse(raw['data']);

    expect(data['dependency_files']).toEqual(['/requirements.txt']);
    expect(data['dependencies']!.length).toEqual(22);

    expect(data['dependencies'][0]!.name).toEqual('asgiref');
    expect(data['dependencies'][0]!.version).toEqual('3.7.2');
    expect(data['dependencies'][0]!.requirements!.length).toEqual(1);
    expect(data['dependencies'][0]!.requirements![0]!.file).toEqual('requirements.txt');
    expect(data['dependencies'][0]!.requirements![0]!.requirement).toEqual('==3.7.2');
    expect(data['dependencies'][0]!.requirements![0]!.groups).toEqual(['dependencies']);
  });

  it('works for a result from python poetry', async () => {
    const raw = JSON.parse(await readFile('fixtures/update_dependency_list/python-poetry.json', 'utf-8'));
    const data = DependabotUpdateDependencyListSchema.parse(raw['data']);

    expect(data['dependency_files']).toEqual(['/pyproject.toml']);
    expect(data['dependencies']!.length).toEqual(1);

    expect(data['dependencies'][0]!.name).toEqual('requests');
    expect(data['dependencies'][0]!.version).toBeNull();
    expect(data['dependencies'][0]!.requirements!.length).toEqual(1);
    expect(data['dependencies'][0]!.requirements![0]!.file).toEqual('pyproject.toml');
    expect(data['dependencies'][0]!.requirements![0]!.requirement).toEqual('^2.31.0');
    expect(data['dependencies'][0]!.requirements![0]!.groups).toEqual(['dependencies']);
  });

  it('works for a result from nuget', async () => {
    const raw = JSON.parse(await readFile('fixtures/update_dependency_list/nuget.json', 'utf-8'));
    const data = DependabotUpdateDependencyListSchema.parse(raw['data']);

    expect(data['dependency_files']).toEqual(['/Root.csproj']);
    expect(data['dependencies']!.length).toEqual(76);

    expect(data['dependencies'][0]!.name).toEqual('Azure.Core');
    expect(data['dependencies'][0]!.version).toEqual('1.35.0');
    expect(data['dependencies'][0]!.requirements!.length).toEqual(0);

    expect(data['dependencies']![3]!.name).toEqual('GraphQL.Server.Ui.Voyager');
    expect(data['dependencies']![3]!.version).toEqual('8.1.0');
    expect(data['dependencies']![3]!.requirements!.length).toEqual(1);
    expect(data['dependencies']![3]!.requirements![0]!.file).toEqual('/Root.csproj');
    expect(data['dependencies']![3]!.requirements![0]!.requirement).toEqual('8.1.0');
    expect(data['dependencies']![3]!.requirements![0]!.groups).toEqual(['dependencies']);
  });
});

describe('record_ecosystem_versions', () => {
  it('python-pip', async () => {
    const raw = JSON.parse(await readFile('fixtures/record_ecosystem_versions/python-pip.json', 'utf-8'));
    const data = DependabotRecordEcosystemVersionsSchema.parse(raw['data']);
    expect(data).toBeDefined();
    expect(data.ecosystem_versions).toBeDefined();
    expect(Object.keys(data.ecosystem_versions!)).toEqual(['languages']);
    expect(Object.keys(data.ecosystem_versions!['languages'])).toEqual(['python']);
    expect(Object.keys(data.ecosystem_versions!['languages']['python'])).toEqual(['max', 'raw']);
    expect(data.ecosystem_versions?.languages.python.max).toEqual('3.13');
    expect(data.ecosystem_versions?.languages.python.raw).toEqual('unknown');
  });
});

describe('record_ecosystem_meta', () => {
  it('python-pip', async () => {
    const raw = JSON.parse(await readFile('fixtures/record_ecosystem_meta/python-pip.json', 'utf-8'));
    const data = DependabotRecordEcosystemMetaSchema.array().parse(raw['data']);
    expect(data.length).toEqual(1);
    expect(data[0]?.ecosystem.name).toEqual('Python');
    expect(data[0]?.ecosystem.package_manager?.name).toEqual('pip');
    expect(data[0]?.ecosystem.package_manager?.version).toEqual('24.0');
    expect(data[0]?.ecosystem.package_manager?.raw_version).toEqual('24.0');
  });
});

describe('increment_metric', () => {
  it('simple', async () => {
    const raw = JSON.parse(await readFile('fixtures/increment_metric/simple.json', 'utf-8'));
    const data = DependabotIncrementMetricSchema.parse(raw['data']);
    expect(data).toBeDefined();
    expect(data.metric).toEqual('updater.started');
    expect(data.tags).toEqual({ operation: 'group_update_all_versions' });
  });
});
