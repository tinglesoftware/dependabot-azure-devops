/**
 * Extract organization name from organization URL
 *
 * @param organizationUrl
 *
 * @returns organization name
 */
export default function extractOrganization(organizationUrl: string): string {
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
