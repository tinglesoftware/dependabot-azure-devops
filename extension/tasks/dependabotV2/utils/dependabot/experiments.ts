// The default experiments known to be used by the GitHub Dependabot service.
// This changes often, update as needed by extracting them from a Dependabot GitHub Action run.
//  e.g. https://github.com/tinglesoftware/dependabot-azure-devops/actions/workflows/dependabot/dependabot-updates
export const DEFAULT_EXPERIMENTS: Record<string, string | boolean> = {
  'record-ecosystem-versions': true,
  'record-update-job-unknown-error': true,
  'proxy-cached': true,
  'move-job-token': true,
  'dependency-change-validation': true,
  'nuget-native-analysis': true,
  'nuget-use-direct-discovery': true,
  'enable-file-parser-python-local': true,
  'lead-security-dependency': true,
  // NOTE: 'enable-record-ecosystem-meta' is not currently implemented in Dependabot-CLI.
  //       This experiment is primarily for GitHub analytics and doesn't add much value in the DevOps implementation.
  //       See: https://github.com/dependabot/dependabot-core/pull/10905
  // TODO: Revsit this if/when Dependabot-CLI supports it.
  //'enable-record-ecosystem-meta': true,
};
