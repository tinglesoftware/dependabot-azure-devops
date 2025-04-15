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
  'nuget-use-direct-discovery': true,
  'enable-file-parser-python-local': true,
  'npm-fallback-version-above-v6': true,
  'lead-security-dependency': true,
  'enable-record-ecosystem-meta': true,
  'enable-shared-helpers-command-timeout': true,
  'enable-engine-version-detection': true,
  'avoid-duplicate-updates-package-json': true,
  'allow-refresh-for-existing-pr-dependencies': true,
  'enable-bun-ecosystem': true,
  'exclude-local-composer-packages': true,
};
