import axios from 'axios';
import * as semver from 'semver';

import { warning } from 'azure-pipelines-task-lib/task';

const GHSA_GRAPHQL_API = 'https://api.github.com/graphql';

const GHSA_SECURITY_VULNERABILITIES_QUERY = `
  query($ecosystem: SecurityAdvisoryEcosystem, $package: String) {
    securityVulnerabilities(first: 100, ecosystem: $ecosystem, package: $package) {
      nodes {
        advisory {
          identifiers {
            type,
            value
          },
          severity,
          summary,
          description,
          references {
            url
          }
          cvss {
            score
            vectorString
          }
          epss {
            percentage
            percentile
          }
          cwes (first: 100) {
            nodes {
              cweId
              name
              description
            }
          }
          publishedAt
          updatedAt
          withdrawnAt
          permalink
        }
        vulnerableVersionRange
        firstPatchedVersion {
          identifier
        }
      }
    }
  }
`;

export enum PackageEcosystem {
  Composer = 'COMPOSER',
  Erlang = 'ERLANG',
  Go = 'GO',
  Actions = 'ACTIONS',
  Maven = 'MAVEN',
  Npm = 'NPM',
  Nuget = 'NUGET',
  Pip = 'PIP',
  Pub = 'PUB',
  Rubygems = 'RUBYGEMS',
  Rust = 'RUST',
  Swift = 'SWIFT',
}

export interface IPackage {
  name: string;
  version?: string;
}

export interface ISecurityVulnerability {
  ecosystem: PackageEcosystem;
  package: IPackage;
  advisory: ISecurityAdvisory;
  vulnerableVersionRange: string;
  firstPatchedVersion: string;
}

export enum SecurityAdvisoryIdentifierType {
  Cve = 'CVE',
  Ghsa = 'GHSA',
}

export enum SecurityAdvisorySeverity {
  Low = 'LOW',
  Moderate = 'MODERATE',
  High = 'HIGH',
  Critical = 'CRITICAL',
}

export interface ISecurityAdvisory {
  identifiers: {
    type: SecurityAdvisoryIdentifierType | string;
    value: string;
  }[];
  severity: SecurityAdvisorySeverity;
  summary: string;
  description: string;
  references: string[];
  cvss: {
    score: number;
    vectorString: string;
  };
  epss: {
    percentage: number;
    percentile: number;
  };
  cwes: {
    id: string;
    name: string;
    description: string;
  }[];
  publishedAt: string;
  updatedAt: string;
  withdrawnAt: string;
  permalink: string;
}

export function getGhsaPackageEcosystemFromDependabotPackageManager(
  dependabotPackageManager: string,
): PackageEcosystem {
  switch (dependabotPackageManager) {
    case 'composer':
      return PackageEcosystem.Composer;
    case 'elm':
      return PackageEcosystem.Erlang;
    case 'github_actions':
      return PackageEcosystem.Actions;
    case 'go_modules':
      return PackageEcosystem.Go;
    case 'maven':
      return PackageEcosystem.Maven;
    case 'npm_and_yarn':
      return PackageEcosystem.Npm;
    case 'nuget':
      return PackageEcosystem.Nuget;
    case 'pip':
      return PackageEcosystem.Pip;
    case 'pub':
      return PackageEcosystem.Pub;
    case 'bundler':
      return PackageEcosystem.Rubygems;
    case 'cargo':
      return PackageEcosystem.Rust;
    case 'swift':
      return PackageEcosystem.Swift;
    default:
      throw new Error(`Unknown dependabot package manager: ${dependabotPackageManager}`);
  }
}

/**
 * GitHub GraphQL client
 */
export class GitHubGraphClient {
  private readonly accessToken: string;

  constructor(accessToken: string) {
    this.accessToken = accessToken;
  }

  /**
   * Get the list of security vulnerabilities for a given package ecosystem and list of packages
   * @param packageEcosystem
   * @param packages
   */
  public async getSecurityVulnerabilitiesAsync(
    packageEcosystem: PackageEcosystem,
    packages: IPackage[],
  ): Promise<ISecurityVulnerability[]> {
    // GitHub API doesn't support querying multiple package at once, so we need to make a request for each package individually.
    // To speed up the process, we can make the requests in parallel, 100 at a time. We batch the requests to avoid hitting the rate limit too quickly.
    // https://docs.github.com/en/graphql/overview/rate-limits-and-node-limits-for-the-graphql-api
    const securityVulnerabilities = await this.batchGraphQueryAsync<IPackage, ISecurityVulnerability>(
      100,
      packages,
      async (pkg) => {
        const variables = {
          ecosystem: packageEcosystem,
          package: pkg.name,
        };
        const response = await axios.post(
          GHSA_GRAPHQL_API,
          JSON.stringify({
            query: GHSA_SECURITY_VULNERABILITIES_QUERY,
            variables: variables,
          }),
          {
            headers: {
              'Authorization': `Bearer ${this.accessToken}`,
              'Content-Type': 'application/json',
            },
          },
        );
        if (response.status < 200 || response.status > 299) {
          throw new Error(`GHSA GraphQL request failed with response: ${response.status} ${response.statusText}`);
        }
        const errors = response.data?.errors;
        if (errors) {
          throw new Error(`GHSA GraphQL request failed with errors: ${JSON.stringify(errors)}`);
        }

        const vulnerabilities = response.data?.data?.securityVulnerabilities?.nodes;
        return vulnerabilities
          ?.filter((v: any) => v?.advisory)
          ?.map((v: any) => {
            return {
              ecosystem: packageEcosystem,
              package: pkg,
              advisory: {
                identifiers: v.advisory.identifiers?.map((i: any) => {
                  return {
                    type: i.type,
                    value: i.value,
                  };
                }),
                severity: v.advisory.severity,
                summary: v.advisory.summary,
                description: v.advisory.description,
                references: v.advisory.references?.map((r: any) => r.url),
                cvss: !v.advisory.cvss
                  ? undefined
                  : {
                      score: v.advisory.cvss.score,
                      vectorString: v.advisory.cvss.vectorString,
                    },
                epss: !v.advisory.epss
                  ? undefined
                  : {
                      percentage: v.advisory.epss.percentage,
                      percentile: v.advisory.epss.percentile,
                    },
                cwes: v.advisory.cwes?.nodes?.map((c: any) => {
                  return {
                    id: c.cweId,
                    name: c.name,
                    description: c.description,
                  };
                }),
                publishedAt: v.advisory.publishedAt,
                updatedAt: v.advisory.updatedAt,
                withdrawnAt: v.advisory.withdrawnAt,
                permalink: v.advisory.permalink,
              },
              vulnerableVersionRange: v.vulnerableVersionRange,
              firstPatchedVersion: v.firstPatchedVersion?.identifier,
            };
          });
      },
    );

    // Filter out vulnerabilities that have been withdrawn or that are not relevant the current version of the package
    const affectedVulnerabilities = securityVulnerabilities
      .filter((v) => !v.advisory.withdrawnAt)
      .filter((v) => {
        const pkg = v.package;
        if (!pkg || !pkg.version || !v.vulnerableVersionRange) {
          return false;
        }

        /**
         * The vulnerable version range follows a basic syntax with a few forms:
         *   `= 0.2.0` denotes a single vulnerable version
         *   `<= 1.0.8` denotes a version range up to and including the specified version
         *   `< 0.1.11` denotes a version range up to, but excluding, the specified version
         *   `>= 4.3.0, < 4.3.5` denotes a version range with a known minimum and maximum version
         *   `>= 0.0.1` denotes a version range with a known minimum, but no known maximum
         */
        const versionRangeRequirements = v.vulnerableVersionRange.split(',').map((v) => v.trim());
        return versionRangeRequirements.every((r) => pkg.version && semver.satisfies(pkg.version, r));
      });

    return affectedVulnerabilities;
  }

  /**
   * Batch requests in parallel to speed up the process when we are forced to do a N+1 query
   * @param batchSize
   * @param items
   * @param action
   * @returns
   */
  private async batchGraphQueryAsync<T1, T2>(batchSize: number, items: T1[], action: (item: T1) => Promise<T2[]>) {
    const results: T2[] = [];
    for (let i = 0; i < items.length; i += batchSize) {
      const batch = items.slice(i, i + batchSize);
      if (batch?.length) {
        try {
          const batchResults = await Promise.all(batch.map(action));
          if (batchResults?.length) {
            results.push(...batchResults.flat());
          }
        } catch (error) {
          warning(`Request batch [${i}-${i + batchSize}] failed; The data may be incomplete. ${error}`);
        }
      }
    }
    return results;
  }
}
