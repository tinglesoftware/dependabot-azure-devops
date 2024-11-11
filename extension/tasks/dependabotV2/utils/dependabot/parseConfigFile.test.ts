import * as tl from 'azure-pipelines-task-lib/task';

import { parseUpdates } from './parseConfigFile';

describe('parseUpdates', () => {
  it('should throw error if the updates key is undefined', () => {
    const config = {};
    expect(() => parseUpdates(config)).toThrow();
  });

  it('should return an empty array if the updates key is an empty array', () => {
    const config = { updates: [] };
    const updates = parseUpdates(config);
    expect(updates).toEqual([]);
  });

  it('should return an array of updates if the updates key is a non-zero array', () => {
    const config = {
      updates: [
        {
          'package-ecosystem': 'npm_and_yarn',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'nuget',
          'directory': '/app',
        },
      ],
    };
    const updates = parseUpdates(config);
    expect(updates).toEqual([
      {
        'package-ecosystem': 'npm_and_yarn',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'nuget',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
    ]);
  });

  it('should throw error if package-ecosystem is missing', () => {
    const config = {
      updates: [
        {
          directory: '/app',
        },
      ],
    };
    expect(() => parseUpdates(config)).toThrow("The value 'package-ecosystem' in dependency update config is missing");
  });

  it('should throw error if both directory and directories are missing', () => {
    const config = {
      updates: [
        {
          'package-ecosystem': 'npm_and_yarn',
        },
      ],
    };
    expect(() => parseUpdates(config)).toThrow(
      "The values 'directory' and 'directories' in dependency update config is missing, you must specify at least one",
    );
  });

  it('should remap package-ecosystem values correctly', () => {
    const config = {
      updates: [
        {
          'package-ecosystem': 'devcontainer',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'github-actions',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'gitsubmodule',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'gomod',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'mix',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'npm',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'pipenv',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'pip-compile',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'poetry',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'pnpm',
          'directory': '/app',
        },
        {
          'package-ecosystem': 'yarn',
          'directory': '/app',
        },
      ],
    };
    const updates = parseUpdates(config);
    expect(updates).toEqual([
      {
        'package-ecosystem': 'devcontainers',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'github_actions',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'submodules',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'go_modules',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'hex',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'npm_and_yarn',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'pip',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'pip',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'pip',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'npm_and_yarn',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
      {
        'package-ecosystem': 'npm_and_yarn',
        'directory': '/app',
        'open-pull-requests-limit': 5,
      },
    ]);
  });

  it('should replace placeholders in assignees', () => {
    const config = {
      updates: [
        {
          'package-ecosystem': 'bundler',
          'directory': '/app',
          'assignees': ['${{ MY_ASSIGNEE_VAR }}'],
        },
      ],
    };

    jest.spyOn(tl, 'getVariable').mockImplementation((variable: string) => {
      if (variable === 'MY_ASSIGNEE_VAR') {
        return 'user1@myorg.com';
      }
      return undefined;
    });

    const updates = parseUpdates(config);
    expect(updates).toEqual([
      {
        'package-ecosystem': 'bundler',
        'directory': '/app',
        'open-pull-requests-limit': 5,
        'assignees': ['user1@myorg.com'],
      },
    ]);
  });

  it('should replace placeholders in reviewers', () => {
    const config = {
      updates: [
        {
          'package-ecosystem': 'bundler',
          'directory': '/app',
          'reviewers': ['${{ MY_REVIEWER_VAR }}'],
        },
      ],
    };

    jest.spyOn(tl, 'getVariable').mockImplementation((variable: string) => {
      if (variable === 'MY_REVIEWER_VAR') {
        return 'user1@myorg.com';
      }
      return undefined;
    });

    const updates = parseUpdates(config);
    expect(updates).toEqual([
      {
        'package-ecosystem': 'bundler',
        'directory': '/app',
        'open-pull-requests-limit': 5,
        'reviewers': ['user1@myorg.com'],
      },
    ]);
  });

  it('should replace placeholders in milestone', () => {
    const config = {
      updates: [
        {
          'package-ecosystem': 'bundler',
          'directory': '/app',
          'milestone': '${{ MY_MILESTONE_VAR }}',
        },
      ],
    };

    jest.spyOn(tl, 'getVariable').mockImplementation((variable: string) => {
      if (variable === 'MY_MILESTONE_VAR') {
        return '12345';
      }
      return undefined;
    });

    const updates = parseUpdates(config);
    expect(updates).toEqual([
      {
        'package-ecosystem': 'bundler',
        'directory': '/app',
        'open-pull-requests-limit': 5,
        'milestone': '12345',
      },
    ]);
  });

  it('should replace placeholders in target-branch', () => {
    const config = {
      updates: [
        {
          'package-ecosystem': 'bundler',
          'directory': '/app',
          'target-branch': '${{ MY_TARGET_BRANCH_VAR }}',
        },
      ],
    };

    jest.spyOn(tl, 'getVariable').mockImplementation((variable: string) => {
      if (variable === 'MY_TARGET_BRANCH_VAR') {
        return 'dev';
      }
      return undefined;
    });

    const updates = parseUpdates(config);
    expect(updates).toEqual([
      {
        'package-ecosystem': 'bundler',
        'directory': '/app',
        'open-pull-requests-limit': 5,
        'target-branch': 'dev',
      },
    ]);
  });
});
