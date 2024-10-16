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
  dependencyNames: string[],
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
  // To speed up the process, we can make the requests in parallel. We batch the requests to avoid hitting the rate limit too quickly.
  // https://docs.github.com/en/graphql/overview/rate-limits-and-node-limits-for-the-graphql-api
  console.log(`Fetching security advisories for ${dependencyNames.length} dependencies...`);
  const securityAdvisories = await batch(100, dependencyNames, async (dependencyName) => {
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

  console.log(`Found ${securityAdvisories.length} security advisories`);
  return securityAdvisories;
}

async function batch(batchSize: number, items: string[], callback: (item: string) => Promise<ISecurityAdvisory[]>) {
  const results: ISecurityAdvisory[] = [];
  for (let i = 0; i < items.length; i += batchSize) {
    const batch = items.slice(i, i + batchSize);
    const batchResults = await Promise.all(batch.map(callback));
    results.push(...batchResults.flat());
  }
  return results;
}
