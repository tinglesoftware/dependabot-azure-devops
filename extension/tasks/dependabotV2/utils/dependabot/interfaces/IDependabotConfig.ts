/**
 * Represents the dependabot.yaml configuration file options.
 * See: https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#configuration-options-for-dependabotyml
 */
export interface IDependabotConfig {
  /**
   * Mandatory. configuration file version.
   **/
  'version': number;

  /**
   * Mandatory. Configure how Dependabot updates the versions or project dependencies.
   * Each entry configures the update settings for a particular package manager.
   */
  'updates': IDependabotUpdate[];

  /**
   * Optional.
   * Specify authentication details to access private package registries.
   */
  'registries'?: Record<string, IDependabotRegistry>;

  /**
   * Optional. Enables updates for ecosystems that are not yet generally available.
   * https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#enable-beta-ecosystems
   */
  'enable-beta-ecosystems'?: boolean;
}

export interface IDependabotUpdate {
  'package-ecosystem': string;
  'directory': string;
  'directories': string[];
  'allow'?: IDependabotAllowCondition[];
  'assignees'?: string[];
  'commit-message'?: IDependabotCommitMessage;
  'groups'?: Record<string, IDependabotGroup>;
  'ignore'?: IDependabotIgnoreCondition[];
  'insecure-external-code-execution'?: string;
  'labels'?: string[];
  'milestone'?: string;
  'open-pull-requests-limit'?: number;
  'pull-request-branch-name'?: IDependabotPullRequestBranchName;
  'rebase-strategy'?: string;
  'registries'?: string[];
  'reviewers'?: string[];
  'schedule'?: IDependabotSchedule;
  'target-branch'?: string;
  'vendor'?: boolean;
  'versioning-strategy'?: string;
}

export interface IDependabotRegistry {
  'type': string;
  'url'?: string;
  'username'?: string;
  'password'?: string;
  'key'?: string;
  'token'?: string;
  'replaces-base'?: boolean;
  'host'?: string; // for terraform and composer only
  'registry'?: string; // for npm only
  'organization'?: string; // for hex-organisation only
  'repo'?: string; // for hex-repository only
  'public-key-fingerprint'?: string; // for hex-repository only
}

export interface IDependabotGroup {
  'applies-to'?: string;
  'dependency-type'?: string;
  'patterns'?: string[];
  'exclude-patterns'?: string[];
  'update-types'?: string[];
}

export interface IDependabotAllowCondition {
  'dependency-name'?: string;
  'dependency-type'?: string;
}

export interface IDependabotIgnoreCondition {
  'dependency-name'?: string;
  'versions'?: string[];
  'update-types'?: string[];
}

export interface IDependabotSchedule {
  interval?: string;
  day?: string;
  time?: string;
  timezone?: string;
}

export interface IDependabotCommitMessage {
  'prefix'?: string;
  'prefix-development'?: string;
  'include'?: string;
}

export interface IDependabotPullRequestBranchName {
  separator?: string;
}
