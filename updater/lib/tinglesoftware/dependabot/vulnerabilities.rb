# typed: false
# frozen_string_literal: true

require "octokit"

#
# Fetches security advisory information from GitHub's Security Advisory API
#
module TingleSoftware
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
        GRAPHQL

        def initialize(package_manager, github_token)
          @ecosystem = ECOSYSTEM_LOOKUP.fetch(package_manager, nil)
          @client ||= Octokit::Client.new(access_token: github_token)
        end

        def fetch(dependency_name)
          [] unless @ecosystem

          response = @client.post "/graphql", {
            query: GRAPHQL_QUERY,
            variables: {
              ecosystem: @ecosystem,
              package: dependency_name
            }
          }.to_json

          raise(QueryError, response[:errors]&.map(&:message)&.join(", ")) if response[:errors]

          response.data[:securityVulnerabilities][:nodes].map do |node|
            # Filter out nil (using .compact), white spaces and empty strings which is necessary for situations
            # where the API response contains null that is converted to nil, or it is an empty
            # string. For example, npm package named faker does not have patched version as of 2023-01-16
            # See: https://github.com/advisories/GHSA-5w9c-rv96-fr7g for npm package
            # This ideally fixes
            # https://github.com/tinglesoftware/dependabot-azure-devops/issues/453#issuecomment-1383587644
            vulnerable_version_range = node[:vulnerableVersionRange]
            affected_versions = [vulnerable_version_range].compact.reject { |v| v.strip.empty? }
            first_patched_version = node.dig :firstPatchedVersion, :identifier
            patched_versions = [first_patched_version].compact.reject { |v| v.strip.empty? }
            {
              "dependency-name" => dependency_name,
              "affected-versions" => affected_versions,
              "patched-versions" => patched_versions,
              "unaffected-versions" => [],
              "title" => node.dig(:advisory, :summary),
              "description" => node.dig(:advisory, :description),
              "source-name" => "GitHub Advisory Database",
              "source-url" => node.dig(:advisory, :permalink)
            }
          end
        end
      end
    end
  end
end
