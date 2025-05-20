/**
 * Extract a dependabot compatible hostname from a TeamFoundationCollection URL
 * @param organizationUrl A URL object constructed from the `System.TeamFoundationCollectionUri` variable.
 * @returns The hostname component of the {@see organizationUrl} parameter or `dev.azure.com` if the parameter points to an old `*.visualstudio.com` URL.
 */
export function extractHostname(organizationUrl: URL): string {
  const visualStudioUrlRegex = /^(?<prefix>\S+)\.visualstudio\.com$/iu;
  let hostname = organizationUrl.hostname;
  if (visualStudioUrlRegex.test(hostname)) {
    return 'dev.azure.com';
  }
  return hostname;
}

/**
 * Extract organization name from organization URL
 *
 * @param organizationUrl
 *
 * @returns organization name
 */
export function extractOrganization(organizationUrl: string): string {
  let parts = organizationUrl.split('/');

  // Check for on-premise style: https://server.domain.com/tfs/x/
  if (parts.length === 6) {
    return parts[4];
  }

  // Check for new style: https://dev.azure.com/x/
  if (parts.length === 5) {
    return parts[3];
  }

  // Check for old style: https://x.visualstudio.com/
  if (parts.length === 4) {
    // Get x.visualstudio.com part.
    let part = parts[2];

    // Return organization part (x).
    return part.split('.')[0];
  }

  throw new Error(`Error parsing organization from organization url: '${organizationUrl}'.`);
}

/**
 * Extract virtual directory from organization URL
 *
 * Virtual Directories are sometimes used in on-premises
 * @param organizationUrl
 *
 * @returns virtual directory
 *
 * @example URLs typically are like this:`https://server.domain.com/tfs/x/` and `tfs` is the virtual directory
 */
export function extractVirtualDirectory(organizationUrl: URL): string {
  // extract the pathname from the url then split
  // pathname takes the shape '/tfs/x/'
  let path = organizationUrl.pathname.split('/');

  // Virtual Directories are sometimes used in on-premises
  // URLs typically are like this: https://server.domain.com/tfs/x/
  // The pathname extracted looks like this: '/tfs/x/'
  if (path.length == 4) {
    return path[1];
  }
  return '';
}
