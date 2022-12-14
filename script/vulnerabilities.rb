require "graphql/client"
require "graphql/client/http"

module Dependabot
  module Vulnerabilities
    class Fetcher
      class QueryError < StandardError; end

      GITHUB_GQL_API_ENDPOINT = "https://api.github.com/graphql"

      ECOSYSTEM_LOOKUP = { # [Hash<String, String>]
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
        @ecosystem = ECOSYSTEM_LOOKUP.fetch(package_manager, nil)
        @http_adapter = CustomHttp.new(GITHUB_GQL_API_ENDPOINT, github_token)

        # Fetch latest schema on init, this will make a network request
        @schema = GraphQL::Client.load_schema(@http_adapter)

        # Parse the query that will be used
        @parsed_query = client.parse <<~GRAPHQL
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

        client.allow_dynamic_queries = true

      end

      def fetch(dependency_name)
        [] unless @ecosystem

        variables = { ecosystem: @ecosystem, package: dependency_name }#.compact_blank
        response = client.query(@parsed_query, variables: variables)
        raise(QueryError, response.errors[:data].join(", ")) if response.errors.any?

        [] # TODO parse the response
      end

      private

      def client
        @client ||= GraphQL::Client.new(schema: @schema, execute: @http_adapter)
      end

      class CustomHttp < GraphQL::Client::HTTP
        def initialize(uri, token)
          super(uri)
          @token = token
        end

        def headers(context)
          # { "Authorization" => "Bearer #{@token}" }
          {
            "Authorization" => "Bearer #{@token}",
            "User-Agent" => "dependabot-azure-devops/1.0"
          }
        end
      end

    end
  end
end
