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
   * Reviewers.
   */
  reviewers?: string;
  /**
   * Assignees.
   */
  assignees?: string;
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
  openPullRequestsLimit?: number;
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
  /** Identifies the type of registry*/
  type: string;

  /**
   * The URL to use to access the dependencies.
   * Dependabot adds or ignores trailing slashes as required.
   * The protocol is optional. If not specified, `https://` is assumed.
   */
  url?: string | null | undefined;
  /**
   * The URL of the registry to use to access the dependencies.
   * Dependabot adds or ignores trailing slashes as required.
   * It should not have the scheme.
   */
  registry?: string | null | undefined;
  /** The hostname for 'terraform_registry' types */
  host?: string | null | undefined;

  /** The username to access the registry */
  username?: string | null | undefined;
  /** A password for the username to access this registry */
  password?: string | null | undefined;
  /** An access key for this registry */
  key?: string | null | undefined;
  /** An access token for this registry */
  token?: string | null | undefined;

  /** Organization for 'hex_organization' types */
  organization?: string | null | undefined;

  /** Repository for 'hex_repository' types */
  repo?: string | null | undefined;
  /** Repository for 'hex_repository' types */
  "auth-key"?: string | null | undefined;
  /** Fingerprint of the public key for the Hex repository */
  "public-key-fingerprint"?: string | null | undefined;

  /**
   * 	For registries with type: python-index,
   *  if the boolean value is `true`, pip resolves dependencies by using the specified URL
   *  rather than the base URL of the Python Package Index (by default https://pypi.org/simple).
   */
  "replaces-base"?: boolean | null | undefined;
}

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
