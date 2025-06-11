import { describe, expect, it } from 'vitest';

import { type DependabotGroup, type DependabotIgnoreCondition, type DependabotUpdate } from 'paklo/dependabot';
import {
  mapAllowedUpdatesFromDependabotConfigToJobConfig,
  mapExperiments,
  mapGroupsFromDependabotConfigToJobConfig,
  mapIgnoreConditionsFromDependabotConfigToJobConfig,
  mapSourceFromDependabotConfigToJobConfig,
} from '../../src/dependabot/job-builder';
import { type ISharedVariables } from '../../src/utils/shared-variables';

describe('mapSourceFromDependabotConfigToJobConfig', () => {
  it('should map source correctly for Azure DevOps Services', () => {
    const taskInputs = {
      apiEndpointUrl: 'https://dev.azure.com',
      hostname: 'dev.azure.com',
      organization: 'my-org',
      project: 'my-project',
      repository: 'my-repo',
      virtualDirectory: '',
      port: '443',
      protocol: 'https',
    } as ISharedVariables;
    const update = {
      'package-ecosystem': 'nuget',
      'directory': '/',
      'directories': [],
    } as DependabotUpdate;

    const result = mapSourceFromDependabotConfigToJobConfig(taskInputs, update);
    expect(result).toMatchObject({
      'provider': 'azure',
      'api-endpoint': 'https://dev.azure.com',
      'hostname': 'dev.azure.com',
      'repo': 'my-org/my-project/_git/my-repo',
    });
  });

  it('should map source correctly for Azure DevOps Server', () => {
    const taskInputs = {
      apiEndpointUrl: 'https://my-org.com:8443/tfs',
      hostname: 'my-org.com',
      organization: 'my-collection',
      project: 'my-project',
      repository: 'my-repo',
      virtualDirectory: 'tfs',
      port: '8443',
      protocol: 'https',
    } as ISharedVariables;
    const update = {
      'package-ecosystem': 'nuget',
      'directory': '/',
      'directories': [],
    } as DependabotUpdate;

    const result = mapSourceFromDependabotConfigToJobConfig(taskInputs, update);
    expect(result).toMatchObject({
      'provider': 'azure',
      'api-endpoint': 'https://my-org.com:8443/tfs',
      'hostname': 'my-org.com',
      'repo': 'tfs/my-collection/my-project/_git/my-repo',
    });
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
