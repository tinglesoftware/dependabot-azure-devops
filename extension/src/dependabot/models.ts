import { IDependabotUpdate } from './config';

/**
 * Represents the Dependabot CLI update job.yaml configuration file options.
 */
export interface IDependabotUpdateJobConfig {
  // The dependabot "updater" job configuration
  // See: https://github.com/dependabot/cli/blob/main/internal/model/job.go
  //      https://github.com/dependabot/dependabot-core/blob/main/updater/lib/dependabot/job.rb
  job: {
    'id': string;
    'package-manager': string;
    'update-subdependencies'?: boolean;
    'updating-a-pull-request'?: boolean;
    'dependency-group-to-refresh'?: string;
    'dependency-groups'?: {
      'name': string;
      'applies-to'?: string;
      'rules': {
        'patterns'?: string[];
        'exclude-patterns'?: string[];
        'dependency-type'?: string;
        'update-types'?: string[];
      };
    }[];
    'dependencies'?: string[];
    'allowed-updates'?: {
      'dependency-name'?: string;
      'dependency-type'?: string;
      'update-type'?: string;
    }[];
    'ignore-conditions'?: {
      'dependency-name'?: string;
      'source'?: string;
      'update-types'?: string[];
      'updated-at'?: string;
      'version-requirement'?: string;
    }[];
    'security-updates-only'?: boolean;
    'security-advisories'?: {
      'dependency-name': string;
      'affected-versions': string[];
      'patched-versions': string[];
      'unaffected-versions': string[];
    }[];
    'source': {
      'provider': string;
      'api-endpoint'?: string;
      'hostname': string;
      'repo': string;
      'branch'?: string;
      'commit'?: string;
      'directory'?: string;
      'directories'?: string[];
    };
    'existing-pull-requests'?: {
      'dependency-name': string;
      'dependency-version': string;
      'directory': string;
    }[][];
    'existing-group-pull-requests'?: {
      'dependency-group-name': string;
      'dependencies': {
        'dependency-name': string;
        'dependency-version': string;
        'directory': string;
      }[];
    }[];
    'commit-message-options'?: {
      'prefix'?: string;
      'prefix-development'?: string;
      'include-scope'?: boolean;
    };
    'cooldown'?: {
      'default-days'?: number;
      'semver-major-days'?: number;
      'semver-minor-days'?: number;
      'semver-patch-days'?: number;
      'include'?: string[];
      'exclude'?: string[];
    };
    'experiments'?: Record<string, string | boolean>;
    'max-updater-run-time'?: number;
    'reject-external-code'?: boolean;
    'repo-private'?: boolean;
    'repo-contents-path'?: string;
    'requirements-update-strategy'?: string;
    'lockfile-only'?: boolean;
    'vendor-dependencies'?: boolean;
    'debug'?: boolean;
  };

  // The dependabot "proxy" registry credentials
  // See: https://github.com/dependabot/dependabot-core/blob/main/common/lib/dependabot/credential.rb
  credentials: {
    'type': string;
    'host'?: string;
    'url'?: string;
    'registry'?: string;
    'region'?: string;
    'username'?: string;
    'password'?: string;
    'token'?: string;
    'replaces-base'?: boolean;
  }[];
}

/**
 * Represents a single Dependabot CLI update operation
 */
export interface IDependabotUpdateOperation extends IDependabotUpdateJobConfig {
  config: IDependabotUpdate;
}

/**
 * Represents the output of a Dependabot CLI update operation
 */
export interface IDependabotUpdateOperationResult {
  success: boolean;
  error: Error;
  output: {
    type: string;
    data: any;
  };
}
