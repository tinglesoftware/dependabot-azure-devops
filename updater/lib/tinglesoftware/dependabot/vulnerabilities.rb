# frozen_string_literal: true

require "octokit"

module Dependabot
  module Vulnerabilities
    class Fetcher
      class QueryError < StandardError; end

      ECOSYSTEM_LOOKUP = {
        "github_actions" => "ACTIONS",
        "composer" => "COMPOSER",
        "elm" => "ERLANG",
        "go_modules" => "GO",
        "maven" => "MAVEN",
        "npm_and_yarn" => "NPM",
        "nuget" => "NUGET",
        "pip" => "PIP",
        "pub" => "PUB",
        "bundler" => "RUBYGEMS",
        "cargo" => "RUST"
      }.freeze

      GRAPHQL_QUERY = <<-GRAPHQL
          query($ecosystem: SecurityAdvisoryEcosystem, $package: String) {
            securityVulnerabilities(first: 100, ecosystem: $ecosystem, package: $package) {
              nodes {
                firstPatchedVersion {
                  identifier
                }
                vulnerableVersionRange
              }
            }
          }
      GRAPHQL

      def initialize(package_manager, github_token)
        @ecosystem = ECOSYSTEM_LOOKUP.fetch(package_manager, nil)
        @client ||= Octokit::Client.new(access_token: github_token)
      end

      def fetch(dependency_name)
        [] unless @ecosystem

        variables = { ecosystem: @ecosystem, package: dependency_name }
        response = @client.post "/graphql", { query: GRAPHQL_QUERY, variables: variables }.to_json
        raise(QueryError, response[:errors]&.map(&:message)&.join(", ")) if response[:errors]

        vulnerabilities = []
        response.data[:securityVulnerabilities][:nodes].map do |node|
          vulnerable_version_range = node[:vulnerableVersionRange]
          first_patched_version = node.dig :firstPatchedVersion, :identifier
          vulnerabilities << {
            "dependency-name" => dependency_name,
            "affected-versions" => [vulnerable_version_range],
            "patched-versions" => [first_patched_version],
            "unaffected-versions" => []
          }
        end

        vulnerabilities
      end
    end
  end
end
