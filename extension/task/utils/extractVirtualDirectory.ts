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
export default function extractVirtualDirectory(organizationUrl: URL): string {
  // extract the pathname from the url then split
  //pathname takes the shape '/tfs/x/'
  let path = organizationUrl.pathname.split("/");

  // Virtual Directories are sometimes used in on-premises
  // URLs typically are like this: https://server.domain.com/tfs/x/
  // The pathname extracted looks like this: '/tfs/x/'
  if (path.length == 4) {
    return path[1];
  }
  return "";
}
