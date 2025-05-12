// The default experiments known to be used by the GitHub Dependabot service.
// This changes often, update as needed by extracting them from a Dependabot GitHub Action run.
//  e.g. https://github.com/tinglesoftware/dependabot-azure-devops/actions/workflows/dependabot/dependabot-updates
export const DEFAULT_EXPERIMENTS: Record<string, string | boolean> = {
  'record-ecosystem-versions': true,
  'record-update-job-unknown-error': true,
  'proxy-cached': true,
  'move-job-token': true,
  'dependency-change-validation': true,
  'nuget-install-dotnet-sdks': true,
  'nuget-native-analysis': true,
  'nuget-native-updater': true,
  'nuget-use-direct-discovery': true,
  'nuget-use-legacy-updater-when-updating-pr': true,
  'enable-file-parser-python-local': true,
  'npm-fallback-version-above-v6': true,
  'lead-security-dependency': true,
  // NOTE: 'enable-record-ecosystem-meta' is not currently implemented in Dependabot-CLI.
  //       This experiment is primarily for GitHub analytics and doesn't add much value in the DevOps implementation.
  //       See: https://github.com/dependabot/dependabot-core/pull/10905
  // TODO: Revisit this if/when Dependabot-CLI supports it.
  //'enable-record-ecosystem-meta': true,
  'enable-shared-helpers-command-timeout': true,
  'enable-dependabot-setting-up-cronjob': true,
  'enable-engine-version-detection': true,
  'avoid-duplicate-updates-package-json': true,
  'allow-refresh-for-existing-pr-dependencies': true,
  'allow-refresh-group-with-all-dependencies': true,
  'exclude-local-composer-packages': true,
  'enable-enhanced-error-details-for-updater': true,
  'enable-cooldown-for-python': true,
  'enable-cooldown-for-uv': true,
  'enable-cooldown-for-npm-and-yarn': true,
  'enable-cooldown-for-bun': true,
  'enable-cooldown-for-bundler': true,
  'enable-cooldown-for-cargo': true,
  'enable-cooldown-for-maven': true,
  'enable-cooldown-for-gomodules': true,
  'enable-cooldown-metrics-collection': true,
};
