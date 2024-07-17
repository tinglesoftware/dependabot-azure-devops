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
  registries?: Record<string, IDependabotRegistry>;
}

export interface IDependabotUpdate {
  /**
   * Package manager to use.
   * */
  packageEcosystem: string;
  /**
   * Location of package manifests.
   * https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#directory
   * */
  directory: string;
  /**
   * Locations of package manifests.
   * https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#directories
   * */
  directories?: string[];
  /**
   * Dependency group rules
   * https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#groups
   * */
  groups?: string;
  /**
   * Customize which updates are allowed.
   */
  allow?: string;
  /**
   * Customize which updates are ignored.
   */
  ignore?: string;
  /**
   * Custom labels/tags.
   */
  labels?: string;
  /**
   * Reviewers.
   */
  reviewers?: string[];
  /**
   * Assignees.
   */
  assignees?: string[];
  /**
   * Commit Message.
   */
  commitMessage?: string;
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
  insecureExternalCodeExecution?: string;
  /**
   * 	Limit number of open pull requests for version updates.
   */
  openPullRequestsLimit?: number;
  /**
   * 	Registries configured for this update.
   */
  registries: string[];
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

export interface IDependabotRegistry {
  /** Identifies the type of registry*/
  type: string;

  /**
   * The URL to use to access the dependencies.
   * Dependabot adds or ignores trailing slashes as required.
   * The protocol is optional. If not specified, `https://` is assumed.
   */
  url?: string | null | undefined;
  "index-url"?: string | null | undefined; // only for python_index

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
