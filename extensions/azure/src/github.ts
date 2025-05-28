import axios from 'axios';
import * as semver from 'semver';
import { z } from 'zod';

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

export const PackageEcosystemSchema = z.enum([
  'COMPOSER',
  'ERLANG',
  'GO',
  'ACTIONS',
  'MAVEN',
  'NPM',
  'NUGET',
  'PIP',
  'PUB',
  'RUBYGEMS',
  'RUST',
  'SWIFT',
]);
export type PackageEcosystem = z.infer<typeof PackageEcosystemSchema>;

export const PackageSchema = z.object({
  name: z.string(),
  version: z.string().optional(),
});
export type Package = z.infer<typeof PackageSchema>;

export const SecurityAdvisoryIdentifierSchema = z.enum(['CVE', 'GHSA']);
export type SecurityAdvisoryIdentifierType = z.infer<typeof SecurityAdvisoryIdentifierSchema>;

export const SecurityAdvisorySeveritySchema = z.enum(['LOW', 'MODERATE', 'HIGH', 'CRITICAL']);
export type SecurityAdvisorySeverity = z.infer<typeof SecurityAdvisorySeveritySchema>;

export const SecurityAdvisorySchema = z.object({
  identifiers: z.array(
    z.object({
      type: z.union([SecurityAdvisoryIdentifierSchema, z.string()]),
      value: z.string(),
    }),
  ),
  severity: SecurityAdvisorySeveritySchema.optional(),
  summary: z.string(),
  description: z.string().optional(),
  references: z.array(z.object({ url: z.string() })).optional(),
  cvss: z
    .object({
      score: z.number(),
      vectorString: z.string(),
    })
    .optional(),
  epss: z
    .object({
      percentage: z.number(),
      percentile: z.number(),
    })
    .optional(),
  cwes: z
    .array(
      z.object({
        cweId: z.string(),
        name: z.string(),
        description: z.string(),
      }),
    )
    .optional(),
  publishedAt: z.string().optional(),
  updatedAt: z.string().optional(),
  withdrawnAt: z.string().optional(),
  permalink: z.string().optional(),
});
export type SecurityAdvisory = z.infer<typeof SecurityAdvisorySchema>;

const FirstPatchedVersionSchema = z.object({ identifier: z.string() });
export type FirstPatchedVersion = z.infer<typeof FirstPatchedVersionSchema>;

export const SecurityVulnerabilitySchema = z.object({
  package: PackageSchema,
  advisory: SecurityAdvisorySchema,
  vulnerableVersionRange: z.string(),
  firstPatchedVersion: FirstPatchedVersionSchema.optional(),
});
export type SecurityVulnerability = z.infer<typeof SecurityVulnerabilitySchema>;

export function getGhsaPackageEcosystemFromDependabotPackageManager(
  dependabotPackageManager: string,
): PackageEcosystem {
  switch (dependabotPackageManager) {
    case 'composer':
      return 'COMPOSER';
    case 'elm':
      return 'ERLANG';
    case 'github_actions':
      return 'ACTIONS';
    case 'go_modules':
      return 'GO';
    case 'maven':
      return 'MAVEN';
    case 'npm_and_yarn':
      return 'NPM';
    case 'nuget':
      return 'NUGET';
    case 'pip':
      return 'PIP';
    case 'pub':
      return 'PUB';
    case 'bundler':
      return 'RUBYGEMS';
    case 'cargo':
      return 'RUST';
    case 'swift':
      return 'SWIFT';
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
   * @param privateVulnerabilities Private vulnerabilities
   */
  public async getSecurityVulnerabilitiesAsync(
    packageEcosystem: PackageEcosystem,
    packages: Package[],
    privateVulnerabilities: SecurityVulnerability[],
  ): Promise<SecurityVulnerability[]> {
    // GitHub API doesn't support querying multiple package at once, so we need to make a request for each package individually.
    // To speed up the process, we can make the requests in parallel, 100 at a time. We batch the requests to avoid hitting the rate limit too quickly.
    // https://docs.github.com/en/graphql/overview/rate-limits-and-node-limits-for-the-graphql-api
    const securityVulnerabilities = await this.batchGraphQueryAsync<Package, SecurityVulnerability>(
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
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return vulnerabilities?.filter((v: any) => v?.advisory)?.map((v: any) => ({ package: pkg, ...v }));
      },
    );

    const merged = privateVulnerabilities.concat(securityVulnerabilities);
    return this.filter(merged);
  }

  public filter(securityVulnerabilities: SecurityVulnerability[]): SecurityVulnerability[] {
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
