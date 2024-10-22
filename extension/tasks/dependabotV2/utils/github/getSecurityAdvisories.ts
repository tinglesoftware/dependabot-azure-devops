import axios from 'axios';

const GITHUB_ADVISORY_GRAPHQL_API = 'https://api.github.com/graphql';
const GITHUB_ADVISORY_SOURCE_NAME = 'GitHub Advisory Database';
const GITHUB_ECOSYSTEM: { [key: string]: string } = {
  github_actions: 'ACTIONS',
  composer: 'COMPOSER',
  elm: 'ERLANG',
  go_modules: 'GO',
  maven: 'MAVEN',
  npm_and_yarn: 'NPM',
  nuget: 'NUGET',
  pip: 'PIP',
  pub: 'PUB',
  bundler: 'RUBYGEMS',
  cargo: 'RUST',
  swift: 'SWIFT',
};

/**
 * Dependency object
 */
export interface IDependency {
  name: string;
  version?: string;
}

/**
 * Security advisory object
 */
export interface ISecurityAdvisory {
  'dependency-name': string;
  'affected-versions': string[];
  'patched-versions': string[];
  'unaffected-versions': string[];
  'title': string;
  'description': string;
  'source-name': string;
  'source-url': string;
}

/**
 * Get the list of security advisories from the GitHub Security Advisory API using GraphQL
 * @param accessToken
 * @param packageEcosystem
 * @param dependencyNames
 */
export async function getSecurityAdvisories(
  accessToken: string,
  packageEcosystem: string,
  dependencies: IDependency[],
): Promise<ISecurityAdvisory[]> {
  const ecosystem = GITHUB_ECOSYSTEM[packageEcosystem];
  const query = `
    query($ecosystem: SecurityAdvisoryEcosystem, $package: String) {
      securityVulnerabilities(first: 100, ecosystem: $ecosystem, package: $package) {
        nodes {
          advisory {
            summary,
            description,
            permalink
          }
          firstPatchedVersion {
            identifier
          }
          vulnerableVersionRange
        }
      }
    }
  `;

  // GitHub API doesn't support querying multiple dependencies at once, so we need to make a request for each dependency individually.
  // To speed up the process, we can make the requests in parallel, 100 at a time. We batch the requests to avoid hitting the rate limit too quickly.
  // https://docs.github.com/en/graphql/overview/rate-limits-and-node-limits-for-the-graphql-api
  console.info(`Checking security advisories for ${dependencies.length} dependencies...`);
  const dependencyNames = dependencies.map((dependency) => dependency.name);
  const securityAdvisories = await batchSecurityAdvisoryQuery(100, dependencyNames, async (dependencyName) => {
    const variables = {
      ecosystem: ecosystem,
      package: dependencyName,
    };
    const response = await axios.post(
      GITHUB_ADVISORY_GRAPHQL_API,
      JSON.stringify({
        query,
        variables,
      }),
      {
        headers: {
          'Authorization': `Bearer ${accessToken}`,
          'Content-Type': 'application/json',
        },
      },
    );
    if (response.status < 200 || response.status > 299) {
      throw new Error(
        `Failed to fetch security advisories for '${dependencyName}': ${response.status} ${response.statusText}`,
      );
    }
    const vulnerabilities = response.data?.data?.securityVulnerabilities?.nodes;
    return vulnerabilities.map((vulnerabilitity: any) => {
      return {
        'dependency-name': dependencyName,
        'affected-versions': vulnerabilitity.vulnerableVersionRange
          ? [vulnerabilitity.vulnerableVersionRange?.trim()]
          : [],
        'patched-versions': vulnerabilitity.firstPatchedVersion
          ? [vulnerabilitity.firstPatchedVersion?.identifier]
          : [],
        'unaffected-versions': [],
        'title': vulnerabilitity.advisory?.summary,
        'description': vulnerabilitity.advisory?.description,
        'source-name': GITHUB_ADVISORY_SOURCE_NAME,
        'source-url': vulnerabilitity.advisory?.permalink,
      };
    });
  });

  // Filter out advisories that are not relevant the version of the dependency we are using
  const affectedAdvisories = securityAdvisories.filter((advisory) => {
    const dependency = dependencies.find((d) => d.name === advisory['dependency-name']);
    if (!dependency) {
      return false;
    }
    const isAffected =
      advisory['affected-versions'].length > 0 &&
      advisory['affected-versions'].find((range) => versionIsInRange(dependency.version, range));
    const isPatched =
      advisory['patched-versions'].length > 0 &&
      advisory['patched-versions'].find((range) => versionIsInRange(dependency.version, range));
    const isUnaffected =
      advisory['unaffected-versions'].length > 0 &&
      advisory['unaffected-versions'].find((range) => versionIsInRange(dependency.version, range));
    return (isAffected && !isPatched) || isUnaffected;
  });

  const vulnerableDependencyCount = new Set(affectedAdvisories.map((advisory) => advisory['dependency-name'])).size;
  console.info(
    `Found ${affectedAdvisories.length} vulnerabilities; affecting ${vulnerableDependencyCount} dependencies`,
  );
  return affectedAdvisories;
}

async function batchSecurityAdvisoryQuery(
  batchSize: number,
  items: string[],
  action: (item: string) => Promise<ISecurityAdvisory[]>,
) {
  const results: ISecurityAdvisory[] = [];
  for (let i = 0; i < items.length; i += batchSize) {
    const batch = items.slice(i, i + batchSize);
    const batchResults = await Promise.all(batch.map(action));
    results.push(...batchResults.flat());
  }
  return results;
}

function versionIsInRange(version: string, range: string): boolean {
  // TODO: Parse the major/minor/patch version and do a proper comparison, taking in to considerations version ranges (e.g. ">=1.0.0")
  return true;
}
