using System.Text.Json;
using NuGet.Versioning;
using Tingle.Dependabot.Models.GitHub;
using Tingle.Extensions.Processing;
using SC = Tingle.Dependabot.DependabotSerializerContext;

namespace Tingle.Dependabot.Workflow;


public class GitHubGraphClient(HttpClient httpClient)
{
    private const string Endpoint = "https://api.github.com/graphql";

    private const string SecurityVulnerabilitiesQuery = @"
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
    ";

    /// <summary>
    /// Get the list of security vulnerabilities for a given package ecosystem and list of packages
    /// </summary>
    public async Task<List<GitHubSecurityVulnerability>> GetSecurityVulnerabilitiesAsync(string token,
                                                                                         string ecosystem,
                                                                                         List<GitHubPackage> packages,
                                                                                         CancellationToken cancellationToken = default)
    {
        // GitHub API doesn't support querying multiple package at once, so we need to make a request for each package individually.
        // To speed up the process, we can make the requests in parallel, 100 at a time. We batch the requests to avoid hitting the rate limit too quickly.
        // https://docs.github.com/en/graphql/overview/rate-limits-and-node-limits-for-the-graphql-api
        var vulnerabilities = new List<GitHubSecurityVulnerability>();
        var processor = new SequentialBatchProcessor<GitHubPackage>(
            concurrencyLimit: 100,
            handler: (pkg, ct) => GetSecurityVulnerabilitiesAsync(token, ecosystem, pkg, vulnerabilities, ct));
        await processor.ProcessAsync(packages, cancellationToken);

        // Filter out vulnerabilities that have been withdrawn or that are not relevant the current version of the package
        var affected = vulnerabilities.Where(v =>
        {
            if (v.Advisory.WithdrawnAt == null) return false;

            var pkg = v.Package;
            if (string.IsNullOrEmpty(pkg.Version) || string.IsNullOrEmpty(v.VulnerableVersionRange)) return false;

            var ranges = v.VulnerableVersionRange
                .Split(',')
                .Select(r => r.Trim());

            return ranges.All(range => NuGetVersion.TryParse(pkg.Version, out var currentVersion) &&
                                       VersionRange.TryParse(range, out var versionRange) &&
                                       versionRange.Satisfies(currentVersion));
        }).ToList();

        return affected;
    }

    private async Task GetSecurityVulnerabilitiesAsync(string token,
                                                       string ecosystem,
                                                       GitHubPackage pkg,
                                                       List<GitHubSecurityVulnerability> destination,
                                                       CancellationToken cancellationToken = default)
    {
        var query = new GhsaGraphQlRequest(
            Query: SecurityVulnerabilitiesQuery,
            Variables: new GhsaGraphQlRequestVariables(
                Ecosystem: ecosystem,
                Package: pkg.Name
            ));

        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new("Bearer", token);
        request.Headers.Accept.Add(new("application/json"));
        request.Content = JsonContent.Create(query, SC.Default.GhsaGraphQlRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var graph = (await response.Content.ReadFromJsonAsync(SC.Default.GhsaGraphQlResponse, cancellationToken))!;
        if (graph.Errors is not null && graph.Errors.Count > 0)
        {
            var json = JsonSerializer.Serialize(graph.Errors, SC.Default.ListGhsaGraphQlError);
            throw new Exception($"GHSA GraphQL request failed with errors: {json}");
        }

        var result = graph.Data!.SecurityVulnerabilities.Nodes
            .Where(v => v.Advisory != null)
            .Select(v => new GitHubSecurityVulnerability(Ecosystem: ecosystem,
                                                         Package: pkg,
                                                         Advisory: v.Advisory,
                                                         VulnerableVersionRange: v.VulnerableVersionRange,
                                                         FirstPatchedVersion: v.FirstPatchedVersion?.Identifier))
            .ToList();

        destination.AddRange(result);
    }
}
