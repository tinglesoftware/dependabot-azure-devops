export type AzureDevOpsUrl = {
  /** URL of the organisation. This may lack the project name */
  'url': URL;

  /** Organisation URL hostname */
  'hostname': string;

  /** Organisation API endpoint URL */
  'api-endpoint': string;

  /** Project ID or Name */
  'project': string;

  /** Repository ID or Name */
  'repository': string;

  /** Slug of the repository e.g. `contoso/prj1/_git/repo1`, `tfs/contoso/prj1/_git/repo1` */
  'repository-slug': string;
};

export function extractUrlParts({
  organisationUrl,
  project,
  repository,
}: {
  organisationUrl: string;
  project: string;
  repository: string;
}): AzureDevOpsUrl {
  // convert url string into a valid JS URL object
  const url = new URL(organisationUrl);
  const protocol = url.protocol.slice(0, -1);
  let { hostname } = url;
  const visualStudioUrlRegex = /^(?<prefix>\S+)\.visualstudio\.com$/iu;
  if (visualStudioUrlRegex.test(hostname)) hostname = 'dev.azure.com'; // TODO: should we really be converting back to the new hostname?

  const organisation: string = extractOrganisation(organisationUrl);

  const virtualDirectory = extractVirtualDirectory(url);
  const apiEndpoint = `${protocol}://${hostname}${url.port ? `:${url.port}` : ''}/${virtualDirectory ? `${virtualDirectory}/` : ''}`;

  const escapedProject = encodeURI(project); // encode special characters like spaces
  const escapedRepository = encodeURI(repository); // encode special characters like spaces
  const repoSlug = `${virtualDirectory ? `${virtualDirectory}/` : ''}${organisation}/${escapedProject}/_git/${escapedRepository}`;

  return {
    url,
    hostname,
    'api-endpoint': apiEndpoint,

    'project': escapedProject,
    'repository': escapedRepository,
    'repository-slug': repoSlug,
  };
}

/**
 * Extract organisation name from organisation URL
 *
 * @param organisationUrl
 *
 * @returns organisation name
 */
function extractOrganisation(organisationUrl: string): string {
  const parts = organisationUrl.split('/');

  // Check for on-premise style: https://server.domain.com/tfs/x/
  if (parts.length === 6) return parts[4]!;

  // Check for new style: https://dev.azure.com/x/
  if (parts.length === 5) return parts[3]!;

  // Check for old style: https://x.visualstudio.com/
  // Get x.visualstudio.com part; Return organisation part (x).
  if (parts.length === 4) return parts[2]!.split('.')[0]!;

  throw new Error(`Error parsing organisation from organisation url: '${organisationUrl}'.`);
}

/**
 * Extract virtual directory from organisation URL
 *
 * Virtual Directories are sometimes used in on-premises
 * @param organisationUrl
 *
 * @returns virtual directory
 *
 * @example URLs typically are like this:`https://server.domain.com/tfs/x/` and `tfs` is the virtual directory
 */
function extractVirtualDirectory(organisationUrl: URL) {
  // extract the pathname from the url then split
  // pathname takes the shape '/tfs/x/'
  const path = organisationUrl.pathname.split('/');

  // Virtual Directories are sometimes used in on-premises
  // URLs typically are like this: https://server.domain.com/tfs/x/
  // The pathname extracted looks like this: '/tfs/x/'
  return path.length == 4 ? path[1]! : undefined;
}
