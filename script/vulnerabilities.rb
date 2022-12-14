require "graphql/client"
require "graphql/client/http"

module Dependabot
  module Vulnerabilities
    class Fetcher
      include Graphql

      class QueryError < StandardError; end

      GITHUB_GQL_API_ENDPOINT = "https://api.github.com/graphql"

      PACKAGE_MANAGER_LOOKUP = { # [Hash<String, String>]
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
        "cargo" => "RUST",
      }.freeze

      def initialize(package_manager, github_token)
        @ecosystem = PACKAGE_MANAGER_LOOKUP.fetch(package_manager, nil)
        @github_token = github_token

        # Configure GraphQL endpoint using the basic HTTP network adapter.
        @http_adapter = GraphQL::Client::HTTP.new(GITHUB_GQL_API_ENDPOINT) do
            def headers(_context)
                { "Authorization" => "Bearer #{@github_token}" }
                # {
                #   "Authorization" => "Bearer #{@github_token}",
                #   "User-Agent": "dependabot-azure-devops/1.0"
                # }
            end
        end

        # Fetch latest schema on init, this will make a network request
        @schema = GraphQL::Client.load_schema(@http_adapter)

        # Parse the query that will be used
        @parsed_query = client.parse <<~GRAPHQL
          query($ecosystem: SecurityAdvisoryEcosystem, $package: String) {
            securityVulnerabilities(ecosystem: $ecosystem, package: $package) {
              nodes {
                firstPatchedVersion {
                  identifier
                }
                package {
                  name
                }
                vulnerableVersionRange
              }
            }
          }
          GRAPHQL

      end

      def fetch(dependency_name)
        [] unless @ecosystem

        variables = { ecosystem: @ecosystem, package: dependency_name }.compact_blank
        response = client.query(@parsed_query, variables: variables)
        raise(QueryError, response.errors[:data].join(", ")) if response.errors.any?

        [] # TODO parse the response
      end

      private

      def client
        @client ||= GraphQL::Client.new(schema: @schema, execute: @http_adapter)
      end

    end
  end
end
