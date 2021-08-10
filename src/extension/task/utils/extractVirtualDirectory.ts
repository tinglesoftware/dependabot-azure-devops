export default function extractVirtualDirectory(organizationUrl: URL): string {
  let path = organizationUrl.pathname.split("/");
  // Virtual Directories are sometimes used in on-premises
  // URLs tipically are like this: https://server.domain.com/tfs/x/
  if (path.length == 4) {
    return path[1];
  }
  return "";
}
