import { readFile } from 'fs/promises';
import * as yaml from 'js-yaml';
import { describe, expect, it } from 'vitest';

import {
  DependabotConfigSchema,
  parseRegistries,
  type DependabotGroup,
  type DependabotIgnoreCondition,
  type DependabotUpdate,
} from './config';
import {
  makeCredentialsMetadata,
  mapAllowedUpdatesFromDependabotConfigToJobConfig,
  mapExperiments,
  mapGroupsFromDependabotConfigToJobConfig,
  mapIgnoreConditionsFromDependabotConfigToJobConfig,
  mapSourceFromDependabotConfigToJobConfig,
  type DependabotSourceInfo,
} from './job-builder';

describe('mapExperiments', () => {
  it('should return an empty object if experiments is undefined', () => {
    const result = mapExperiments(undefined);
    expect(result).toEqual({});
  });

  it('should return an empty object if experiments is an empty object', () => {
    const result = mapExperiments({});
    expect(result).toEqual({});
  });

  it('should convert string experiment value "true" to boolean `true`', () => {
    const experiments = {
      experiment1: 'true',
    };
    const result = mapExperiments(experiments);
    expect(result).toEqual({
      experiment1: true,
    });
  });

  it('should convert string experiment value "false" to boolean `false`', () => {
    const experiments = {
      experiment1: 'false',
    };
    const result = mapExperiments(experiments);
    expect(result).toEqual({
      experiment1: false,
    });
  });

  it('should keep boolean experiment values as is', () => {
    const experiments = {
      experiment1: true,
      experiment2: false,
    };
    const result = mapExperiments(experiments);
    expect(result).toEqual({
      experiment1: true,
      experiment2: false,
    });
  });

  it('should keep string experiment values other than "true" or "false" as is', () => {
    const experiments = {
      experiment1: 'someString',
    };
    const result = mapExperiments(experiments);
    expect(result).toEqual({
      experiment1: 'someString',
    });
  });
});

describe('mapSourceFromDependabotConfigToJobConfig', () => {
  it('should map source correctly for Azure DevOps Services', () => {
    const sourceInfo: DependabotSourceInfo = {
      'provider': 'azure',
      'hostname': 'dev.azure.com',
      'api-endpoint': 'https://dev.azure.com',
      'repository-slug': 'my-org/my-project/_git/my-repo',
    };
    const update = {
      'package-ecosystem': 'nuget',
      'directory': '/',
      'directories': [],
    } as DependabotUpdate;

    const result = mapSourceFromDependabotConfigToJobConfig(sourceInfo, update);
    expect(result).toMatchObject({
      'provider': 'azure',
      'api-endpoint': 'https://dev.azure.com',
      'hostname': 'dev.azure.com',
      'repo': 'my-org/my-project/_git/my-repo',
    });
  });

  it('should map source correctly for Azure DevOps Server', () => {
    const sourceInfo: DependabotSourceInfo = {
      'provider': 'azure',
      'api-endpoint': 'https://my-org.com:8443/tfs',
      'hostname': 'my-org.com',
      'repository-slug': 'tfs/my-collection/my-project/_git/my-repo',
    };
    const update = {
      'package-ecosystem': 'nuget',
      'directory': '/',
      'directories': [],
    } as DependabotUpdate;

    const result = mapSourceFromDependabotConfigToJobConfig(sourceInfo, update);
    expect(result).toMatchObject({
      'provider': 'azure',
      'api-endpoint': 'https://my-org.com:8443/tfs',
      'hostname': 'my-org.com',
      'repo': 'tfs/my-collection/my-project/_git/my-repo',
    });
  });
});

describe('mapAllowedUpdatesFromDependabotConfigToJobConfig', () => {
  it('should allow direct dependency updates if rules are undefined', () => {
    const result = mapAllowedUpdatesFromDependabotConfigToJobConfig(undefined);
    expect(result).toEqual([{ 'dependency-type': 'direct', 'update-type': 'all' }]);
  });

  it('should allow direct dependency security updates if rules are undefined and securityOnlyUpdate is true', () => {
    const result = mapAllowedUpdatesFromDependabotConfigToJobConfig(undefined, true);
    expect(result).toEqual([{ 'dependency-type': 'direct', 'update-type': 'security' }]);
  });
});

describe('mapIgnoreConditionsFromDependabotConfigToJobConfig', () => {
  it('should return undefined if rules are undefined', () => {
    const result = mapIgnoreConditionsFromDependabotConfigToJobConfig(undefined);
    expect(result).toBeUndefined();
  });

  it('should handle single version string correctly', () => {
    const ignore: DependabotIgnoreCondition[] = [{ 'dependency-name': 'dep1', 'versions': '>3' }];
    const result = mapIgnoreConditionsFromDependabotConfigToJobConfig(ignore);
    expect(result).toEqual([{ 'dependency-name': 'dep1', 'version-requirement': '>3' }]);
  });

  it('should handle single version string array correctly', () => {
    const ignore: DependabotIgnoreCondition[] = [{ 'dependency-name': 'dep1', 'versions': ['>1.0.0'] }];
    const result = mapIgnoreConditionsFromDependabotConfigToJobConfig(ignore);
    expect(result).toEqual([{ 'dependency-name': 'dep1', 'version-requirement': '>1.0.0' }]);
  });

  it('should handle multiple version strings correctly', () => {
    const ignore: DependabotIgnoreCondition[] = [{ 'dependency-name': 'dep1', 'versions': ['>1.0.0', '<2.0.0'] }];
    const result = mapIgnoreConditionsFromDependabotConfigToJobConfig(ignore);
    expect(result).toEqual([{ 'dependency-name': 'dep1', 'version-requirement': '>1.0.0, <2.0.0' }]);
  });

  it('should handle empty versions array correctly', () => {
    const ignore: DependabotIgnoreCondition[] = [{ 'dependency-name': 'dep1', 'versions': [] }];
    const result = mapIgnoreConditionsFromDependabotConfigToJobConfig(ignore);
    expect(result).toEqual([{ 'dependency-name': 'dep1', 'version-requirement': '' }]);
  });
});

describe('mapGroupsFromDependabotConfigToJobConfig', () => {
  it('should return undefined if dependencyGroups is undefined', () => {
    const result = mapGroupsFromDependabotConfigToJobConfig(undefined);
    expect(result).toBeUndefined();
  });

  it('should return undefined if dependencyGroups is an empty object', () => {
    const result = mapGroupsFromDependabotConfigToJobConfig({});
    expect(result).toBeUndefined();
  });

  it('should filter out undefined groups', () => {
    const dependencyGroups: Record<string, DependabotGroup | undefined | null> = {
      group1: undefined,
      group2: {
        patterns: ['pattern2'],
      },
    };

    const result = mapGroupsFromDependabotConfigToJobConfig(dependencyGroups);
    expect(result).toHaveLength(1);
  });

  it('should filter out null groups', () => {
    const dependencyGroups: Record<string, DependabotGroup | undefined | null> = {
      group1: null,
      group2: {
        patterns: ['pattern2'],
      },
    };

    const result = mapGroupsFromDependabotConfigToJobConfig(dependencyGroups);
    expect(result).toHaveLength(1);
  });

  it('should map dependency group properties correctly', () => {
    const dependencyGroups: Record<string, DependabotGroup> = {
      group: {
        'applies-to': 'version-updates',
        'patterns': ['pattern1', 'pattern2'],
        'exclude-patterns': ['exclude1'],
        'dependency-type': 'production',
        'update-types': ['major'],
      },
    };

    const result = mapGroupsFromDependabotConfigToJobConfig(dependencyGroups);

    expect(result).toEqual([
      {
        'name': 'group',
        'applies-to': 'version-updates',
        'rules': {
          'patterns': ['pattern1', 'pattern2'],
          'exclude-patterns': ['exclude1'],
          'dependency-type': 'production',
          'update-types': ['major'],
        },
      },
    ]);
  });

  it('should use pattern "*" if no patterns are provided', () => {
    const dependencyGroups: Record<string, DependabotGroup> = {
      group: {},
    };

    const result = mapGroupsFromDependabotConfigToJobConfig(dependencyGroups);

    expect(result).toEqual([{ name: 'group', rules: { patterns: ['*'] } }]);
  });
});

// TODO: add tests for mapCredentials

describe('makeCredentialsMetadata', () => {
  it('works', async () => {
    const config = await DependabotConfigSchema.parseAsync(
      yaml.load(await readFile('fixtures/config/sample-registries.yml', 'utf-8')),
    );

    let registries = await parseRegistries(config, () => undefined);
    expect(Object.keys(registries).length).toBe(11);

    // TODO: change this to call makeCredentials/mapCredentials once it is moved to this package hence remove this hack
    registries = {
      main_source: {
        type: 'git_source',
        host: 'dev.azure.com',
        username: 'x-access-token',
        password: 'token',
      },
      github_source: {
        type: 'git_source',
        host: 'github.com',
        username: 'x-access-token',
        password: 'github-token',
      },
      ...registries,
    };
    const metadatas = makeCredentialsMetadata(Object.values(registries));
    expect(metadatas.length).toBe(13);

    expect(metadatas[0]).toEqual({ type: 'git_source', host: 'dev.azure.com' });
    expect(metadatas[1]).toEqual({ type: 'git_source', host: 'github.com' });
    expect(metadatas[2]).toEqual({
      type: 'composer_repository',
      host: 'repo.packagist.com',
      url: 'https://repo.packagist.com/example-company/',
    });
    expect(metadatas[3]).toEqual({
      'type': 'docker_registry',
      'replaces-base': true,
      'registry': 'registry.hub.docker.com',
    });
    expect(metadatas[4]).toEqual({ type: 'git', url: 'https://github.com' });
    expect(metadatas[5]).toEqual({ type: 'hex_organization', organization: 'github' });
    expect(metadatas[6]).toEqual({
      'type': 'hex_repository',
      'repo': 'private-repo',
      'public-key-fingerprint': 'pkf_1234567890',
      'url': 'https://private-repo.example.com',
    });
    expect(metadatas[7]).toEqual({
      'type': 'maven_repository',
      'replaces-base': true,
      'url': 'https://artifactory.example.com',
    });
    expect(metadatas[8]).toEqual({ 'type': 'npm_registry', 'replaces-base': true, 'registry': 'npm.pkg.github.com' });
    expect(metadatas[9]).toEqual({
      type: 'nuget_feed',
      url: 'https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json',
    });
    expect(metadatas[10]).toEqual({
      'type': 'python_index',
      'replaces-base': true,
      'index-url': 'https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example',
    });
    expect(metadatas[11]).toEqual({
      'type': 'rubygems_server',
      'replaces-base': false,
      'url': 'https://rubygems.pkg.github.com/octocat/github_api',
    });
    expect(metadatas[12]).toEqual({ type: 'terraform_registry', host: 'terraform.example.com' });
  });
});
