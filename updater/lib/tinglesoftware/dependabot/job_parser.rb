# typed: strict
# frozen_string_literal: true

require "json"
require "dependabot/job"
require "dependabot/source"
require "dependabot/credential"

require "tinglesoftware/dependabot/clients/azure"

#
# Parses environment variables into a Dependabot job object
#
module TingleSoftware
  module Dependabot
    class JobParser
      def self.job_from_env_vars
        options = {
          id: id,
          allowed_updates: allowed_updates,
          commit_message_options: commit_message_options,
          credentials: credentials,
          dependencies: nil,
          existing_pull_requests: existing_pull_requests,
          existing_group_pull_requests: [],
          experiments: {}, # experiments,
          ignore_conditions: ignore_conditions,
          package_manager: package_manager,
          reject_external_code: false,
          repo_contents_path: repo_contents_path,
          requirements_update_strategy: nil, # requirements_update_strategy,
          lockfile_only: lockfile_only,
          security_advisories: [],
          security_updates_only: false,
          source: source,
          token: github_token,
          update_subdependencies: true,
          updating_a_pull_request: false,
          vendor_dependencies: false,
          dependency_groups: dependency_groups,
          dependency_group_to_refresh: nil,
          repo_private: nil
        }
        ::Dependabot.logger.debug("Parsed job options from environment: #{JSON.pretty_generate(options)}")
        TingleSoftware::Dependabot::Job.new(options, azure_client)
      end

      def self.id
        ENV.fetch("DEPENDABOT_JOB_ID", Time.now.to_i.to_s)
      end

      def self.allowed_updates
        conditions = JSON.parse(ENV.fetch("DEPENDABOT_ALLOW_CONDITIONS", "[]"), symbolize_names: true).compact
        return if conditions.count.nonzero?

        conditions << {
          "dependency-type" => "all"
        }
      end

      def self.commit_message_options
        JSON.parse(ENV.fetch("DEPENDABOT_COMMIT_MESSAGE_OPTIONS", "{}"), symbolize_names: true)
      end

      def self.github_token
        ENV.fetch("GITHUB_ACCESS_TOKEN", nil)
      end

      def self.credentials
        creds = [
          {
            "type" => "git_source",
            "host" => azure_hostname,
            "username" => ENV["AZURE_ACCESS_USERNAME"] || "x-access-token",
            "password" => ENV.fetch("AZURE_ACCESS_TOKEN", nil)
          },
          *JSON.parse(ENV.fetch("DEPENDABOT_EXTRA_CREDENTIALS", "[]"))
        ]
        if github_token
          creds << {
            "type" => "git_source",
            "host" => "github.com",
            "username" => "x-access-token",
            "password" => github_token
          }
        end

        creds.compact
      end

      def self.experiments
        ENV.fetch("DEPENDABOT_UPDATER_OPTIONS", "").split(",").to_h do |o|
          if o.include?("=") # key/value pair, e.g. goprivate=true
            o.split("=", 2).map.with_index do |v, i|
              if i.zero?
                v.strip.downcase.to_sym
              else
                v.strip
              end
            end
          else # just a key, e.g. "vendor"
            [o.strip.downcase.to_sym, true]
          end
        end
      end

      def self.ignore_conditions
        JSON.parse(ENV.fetch("DEPENDABOT_IGNORE_CONDITIONS", "[]"), symbolize_names: true).compact
      end

      def self.package_manager
        ENV.fetch("DEPENDABOT_PACKAGE_MANAGER", nil)
      end

      def self.repo_contents_path
        ENV.fetch("DEPENDABOT_REPO_CONTENTS_PATH", nil) ||
          File.expand_path(File.join("tmp", repo_name.split("/")))
          # File.expand_path(File.join("job", id, "repo", repo_name.split("/")))
      end

      def self.requirements_update_strategy
        ENV.fetch("DEPENDABOT_VERSIONING_STRATEGY", nil)
      end

      def self.lockfile_only
        ENV.fetch("DEPENDABOT_LOCKFILE_ONLY", "false") == "true"
      end

      def self.dependency_groups
        JSON.parse(ENV.fetch("DEPENDABOT_DEPENDENCY_GROUPS", "[]")).compact
      end

      def self.provider
        "azure"
      end

      def self.source
        {
          "provider" => provider,
          "hostname" => azure_hostname,
          "api-endpoint" => api_endpoint,
          "repo" => repo_name,
          "directory" => directory,
          "branch" => branch
        }
      end

      def self.directory
        ENV.fetch("DEPENDABOT_DIRECTORY", "/")
      end

      def self.branch
        ENV.fetch("DEPENDABOT_BRANCH", nil)
      end

      def self.api_endpoint
        virual_directory = azure_virtual_directory.empty? ? "" : "#{azure_virtual_directory}/}"
        "#{azure_protocol}://#{azure_hostname}:#{azure_port}/#{virual_directory}"
      end

      def self.repo_name
        "#{azure_organization}/#{azure_project}/_git/#{azure_repository}"
      end

      def self.azure_organization
        ENV.fetch("AZURE_ORGANIZATION", nil)
      end

      def self.azure_project
        ENV.fetch("AZURE_PROJECT", nil)
      end

      def self.azure_repository
        ENV.fetch("AZURE_REPOSITORY", nil)
      end

      def self.azure_hostname
        ENV.fetch("AZURE_HOSTNAME", "dev.azure.com")
      end

      def self.azure_protocol
        ENV.fetch("AZURE_PROTOCOL", "https")
      end

      def self.azure_port
        ENV.fetch("AZURE_PORT", azure_protocol == "http" ? "80" : "443")
      end

      def self.azure_virtual_directory
        ENV.fetch("AZURE_VIRTUAL_DIRECTORY", "")
      end

      def self.azure_client
        @azure_client ||= TingleSoftware::Dependabot::Clients::Azure.for_source(
          source: ::Dependabot::Source.new(
            provider: provider,
            hostname: azure_hostname,
            api_endpoint: api_endpoint,
            repo: repo_name,
            directory: directory,
            branch: branch
          ),
          credentials: credentials.map { |c| ::Dependabot::Credential.new(c) }
        )
      end

      def self.existing_pull_requests
        user_id = azure_client.get_user_id
        target_branch_name = branch || azure_client.fetch_default_branch(repo_name)
        azure_client.pull_requests_active(user_id, target_branch_name).map do |pr|
          title = pr["title"]
          {
            "id" => pr["pullRequestId"],
            "title" => pr["title"],
            "source-ref-name" => pr["sourceRefName"],
            "dependency-name" => title.match(/bump (.*) from/i)&.captures&.first,
            "dependency-version" => title.match(/to (.*)\b/i)&.captures&.first,
            "directory" => directory,
            "dependency-removed" => false
          }
        end
      end
    end
  end
end
