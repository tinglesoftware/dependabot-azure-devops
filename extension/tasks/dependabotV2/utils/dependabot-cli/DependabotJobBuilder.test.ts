import { IDependabotGroup } from '../dependabot/interfaces/IDependabotConfig';
import { mapGroupsFromDependabotConfigToJobConfig } from './DependabotJobBuilder';

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
    const dependencyGroups: Record<string, any> = {
      group1: undefined,
      group2: {
        patterns: ['pattern2'],
      },
    };

    const result = mapGroupsFromDependabotConfigToJobConfig(dependencyGroups);
    expect(result).toHaveLength(1);
  });

  it('should filter out null groups', () => {
    const dependencyGroups: Record<string, any> = {
      group1: null,
      group2: {
        patterns: ['pattern2'],
      },
    };

    const result = mapGroupsFromDependabotConfigToJobConfig(dependencyGroups);
    expect(result).toHaveLength(1);
  });

  it('should map dependency group properties correctly', () => {
    const dependencyGroups: Record<string, IDependabotGroup> = {
      group: {
        'applies-to': 'all',
        'patterns': ['pattern1', 'pattern2'],
        'exclude-patterns': ['exclude1'],
        'dependency-type': 'direct',
        'update-types': ['security'],
      },
    };

    const result = mapGroupsFromDependabotConfigToJobConfig(dependencyGroups);

    expect(result).toEqual([
      {
        'name': 'group',
        'applies-to': 'all',
        'rules': {
          'patterns': ['pattern1', 'pattern2'],
          'exclude-patterns': ['exclude1'],
          'dependency-type': 'direct',
          'update-types': ['security'],
        },
      },
    ]);
  });

  it('should use pattern "*" if no patterns are provided', () => {
    const dependencyGroups: Record<string, IDependabotGroup> = {
      group: {},
    };

    const result = mapGroupsFromDependabotConfigToJobConfig(dependencyGroups);

    expect(result).toEqual([
      {
        name: 'group',
        rules: {
          patterns: ['*'],
        },
      },
    ]);
  });
});
