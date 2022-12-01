/**
 * Extract a dependabot compatible hostname from a TeamFoundationCollection URL
 * @param organizationUrl A URL object constructed from the `System.TeamFoundationCollectionUri` variable.
 * @returns The hostname component of the {@see organizationUrl} parameter or `dev.azure.com` if the parameter points to an old `*.visualstudio.com` URL.
 */
export default function extractHostname(organizationUrl: URL): string {
  const visualStudioUrlRegex = /^(?<prefix>\S+)\.visualstudio\.com$/iu;
  let hostname = organizationUrl.hostname;
  if (visualStudioUrlRegex.test(hostname)) {
    return 'dev.azure.com';
  }
  return hostname;
}
