/**
 * To see the supported structure, visit
 *
 * https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#configuration-options-for-dependabotyml
 */
export interface IDependabotConfig {
  /**
   *  Mandatory. configuration file version.
   **/
  version: number;
  /**
   *  Mandatory. Configure how Dependabot updates the versions or project dependencies.
   *  Each entry configures the update settings for a particular package manager.
   */
  updates: IDependabotUpdate[];
  /**
   *  Optional. Specify authentication details to access private package registries.
   */
  registries?: IDependabotRegistry[];
}

export interface IDependabotUpdate {
  /**
   * Location of package manifests.
   * */
  directory: string;
  /**
   * Package manager to use.
   * */
  packageEcosystem: string;
  schedule?: IDependabotUpdateSchedule;
  /**
   * Customize which updates are allowed.
   */
  allow?: string;
  /**
   * Custom labels/tags.
   */
  labels?: string;
  /**
   * The milestone to associate pull requests with.
   */
  milestone?: string;
  /**
   * Separator for the branches created.
   */
  branchNameSeparator?: string;
  /**
   * Whether to reject external code
   */
  rejectExternalCode: boolean;
  /**
   * 	Limit number of open pull requests for version updates.
   */
  openPullRequestLimit?: number;
  /**
   * Branch to create pull requests against.
   */
  targetBranch?: string;
  /**
   * Update vendored or cached dependencies
   */
  vendor?: boolean;
  /**
   * How to update manifest version requirements.
   */
  versioningStrategy?: string;
}

export interface IDependabotUpdateSchedule {
  /**
   * Time of day to check for updates (hh:mm)
   */
  time?: string;
  /**
   * Day of week to check for updates
   */
  day?: string;
  /**
   * Timezone for time of day (zone identifier)
   */
  timezone?: string;
  /**
   * 	How often to check for updates
   */
  interval: string;
}

export interface IDependabotRegistry {
  /**
   * Identifies the type of registry
   */
  type: string;
  /**
   * The URL to use to access the dependencies in this registry.
   * The protocol is optional. If not specified, `https://` is assumed.
   * Dependabot adds or ignores trailing slashes as required.
   */
  url: string;
  /**
   * The username to access the registry
   */
  username?: string;
  /**
   *  A password for the username to access this registry
   */
  password?: string;
  /**
   *  An access key for this registry
   */
  key?: string;
  /**
   *  An access token for this registry
   */
  token?: string;
  /**
   * 	For registries with type: python-index,
   *  if the boolean value is `true`, pip resolves dependencies by using the specified URL
   *  rather than the base URL of the Python Package Index (by default https://pypi.org/simple).
   */
  "replaces-base"?: string;
}

export type DependabotDependencyType =
  | "direct"
  | "all"
  | "production"
  | "development";

export type DependabotPackageEcosystemType =
  | "bundler"
  | "cargo"
  | "composer"
  | "docker"
  | "hex"
  | "elm"
  | "gitsubmodules"
  | "github-actions"
  | "gomod"
  | "gradle"
  | "maven"
  | "mix"
  | "npm"
  | "nuget"
  | "pip"
  | "terraform";

export type DependabotVersioningStrategyType =
  | "lock-file-only"
  | "auto"
  | "widen"
  | "increase"
  | "increase-if-necessary";
