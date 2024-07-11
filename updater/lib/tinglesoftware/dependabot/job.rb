# typed: strict
# frozen_string_literal: true

require "dependabot/job"
require "tinglesoftware/dependabot/clients/azure"
require "tinglesoftware/dependabot/vulnerabilities"

#
# Represents a Dependabot job; A single unit of work that Dependabot can perform (e.g. "update all dependencies").
# This class contains all the user configuration needed to perform the job, extracted from environment variables.
#
module TingleSoftware
  module Dependabot
    class Job < ::Dependabot::Job
      extend T::Sig

      # TODO: DEPENDABOT_VENDOR
      # TODO: DEPENDABOT_FAIL_ON_EXCEPTION
      # TODO: DEPENDABOT_SKIP_PULL_REQUESTS
      # TODO: DEPENDABOT_CLOSE_PULL_REQUESTS
      # TODO: DEPENDABOT_EXCLUDE_REQUIREMENTS_TO_UNLOCK

      def initialize(azure_client: nil)
        @azure_client = azure_client
        super(
          id: fetch_id,
          allowed_updates: fetch_allowed_updates,
          commit_message_options: fetch_commit_message_options,
          credentials: fetch_credentials,
          dependencies: nil, # TODO: Implement this
          existing_pull_requests: fetch_existing_pull_requests,
          existing_group_pull_requests: fetch_existing_group_pull_requests,
          experiments: {}, # TODO: Fix experiments,
          ignore_conditions: fetch_ignore_conditions,
          package_manager: fetch_package_manager,
          reject_external_code: fetch_reject_external_code,
          repo_contents_path: fetch_repo_contents_path,
          requirements_update_strategy: nil, # TODO: Fix requirements_update_strategy,
          lockfile_only: fetch_lockfile_only,
          security_advisories: security_advisories,
          security_updates_only: security_updates_only,
          source: fetch_source,
          token: github_access_token,
          update_subdependencies: true,
          updating_a_pull_request: false, # TODO: Implement this
          vendor_dependencies: false, # TODO: Implement this
          dependency_groups: fetch_dependency_groups,
          dependency_group_to_refresh: nil # TODO: Implement this
        )
      end

      def validate_job
        super
        return unless security_updates_only && !token

        raise StandardError,
              "Security only updates are enabled but a GitHub token is not supplied! Cannot proceed"
      end

      def vulnerabilities_fetcher
        return unless token

        @vulnerabilities_fetcher ||= TingleSoftware::Dependabot::Vulnerabilities::Fetcher.new(package_manager,
                                                                                              token)
      end

      def vulnerabilities_fixed_for(updated_dependencies)
        updated_dependencies.filter_map do |dep|
          { dep.name => @security_advisories.select { |adv| adv["dependency-name"] == dep.name }
                                            .map { |adv| adv.transform_keys { |key| key.tr("-", "_") } } }
        end&.reduce(:merge)
      end

      def security_advisories_for(dependency)
        # If configured, fetch security advisories from GitHub's Security Advisory API
        fetch_missing_advisories_for_dependency(dependency) if vulnerabilities_fetcher
        super
      end

      def fetch_missing_advisories_for_dependency(dependency)
        ::Dependabot.logger.info("Checking if #{dependency.name} has any security advisories")
        @security_advisories.push(
          *vulnerabilities_fetcher.fetch(dependency.name)
        )
      end

      def fetch_id
        ENV.fetch("DEPENDABOT_JOB_ID", Time.now.to_i.to_s)
      end

      def fetch_allowed_updates
        conditions = JSON.parse(ENV.fetch("DEPENDABOT_ALLOW_CONDITIONS", "[]")).compact
        return conditions if conditions.count.nonzero?

        # If no conditions are specified, default to updating all dependencies
        conditions << {
          "dependency-type" => "all"
        }
      end

      def fetch_commit_message_options
        JSON.parse(ENV.fetch("DEPENDABOT_COMMIT_MESSAGE_OPTIONS", "{}"), symbolize_names: true)
      end

      def fetch_credentials
        creds = [
          {
            "type" => "git_source",
            "host" => azure_hostname,
            "username" => ENV.fetch("AZURE_ACCESS_USERNAME", nil) || "x-access-token",
            "password" => ENV.fetch("AZURE_ACCESS_TOKEN", nil)
          },
          *JSON.parse(ENV.fetch("DEPENDABOT_EXTRA_CREDENTIALS", "[]"))
        ]
        if github_access_token
          creds << {
            "type" => "git_source",
            "host" => "github.com",
            "username" => "x-access-token",
            "password" => github_access_token
          }
        end

        creds.compact
      end

      def fetch_experiments
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

      def fetch_ignore_conditions
        JSON.parse(ENV.fetch("DEPENDABOT_IGNORE_CONDITIONS", "[]"), symbolize_names: true).compact
      end

      def fetch_package_manager
        # GitHub native implementation modifies some of the names in the config file
        # https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#package-ecosystem
        pkg_mgr = ENV.fetch("DEPENDABOT_PACKAGE_MANAGER", "bundler")
        {
          "github-actions" => "github_actions",
          "gitsubmodule" => "submodules",
          "gomod" => "go_modules",
          "mix" => "hex",
          "npm" => "npm_and_yarn",
          # Additional ones
          "yarn" => "npm_and_yarn",
          "pipenv" => "pip",
          "pip-compile" => "pip",
          "poetry" => "pip"
        }.freeze.fetch(pkg_mgr, pkg_mgr)
      end

      def fetch_reject_external_code
        ENV.fetch("DEPENDABOT_REJECT_EXTERNAL_CODE", nil) == "true"
      end

      def fetch_repo_contents_path
        ENV.fetch("DEPENDABOT_REPO_CONTENTS_PATH", nil) ||
          File.expand_path(File.join("tmp", azure_repository_path.split("/")))
        # TODO: File.expand_path(File.join("job", id, "repo", azure_repository_path.split("/")))
      end

      def fetch_requirements_update_strategy
        ENV.fetch("DEPENDABOT_VERSIONING_STRATEGY", nil)
      end

      def fetch_lockfile_only
        ENV.fetch("DEPENDABOT_LOCKFILE_ONLY", nil) == "true"
      end

      def fetch_dependency_groups
        groups = JSON.parse(ENV.fetch("DEPENDABOT_DEPENDENCY_GROUPS", "[]")).compact
        return groups if groups.count.nonzero?

        nil
      end

      def fetch_existing_pull_requests
        dependencies = active_pull_requests_property_sets.filter_map do |props|
          JSON.parse(props[ApiClients::AzureApiClient::PullRequest::Properties::UPDATED_DEPENDENCIES] || nil.to_json)
        end
        dependencies.select { |d| d.is_a?(Array) }
      end

      def fetch_existing_group_pull_requests
        dependencies = active_pull_requests_property_sets.filter_map do |props|
          JSON.parse(props[ApiClients::AzureApiClient::PullRequest::Properties::UPDATED_DEPENDENCIES] || nil.to_json)
        end
        dependencies.select { |d| d.is_a?(Hash) }
      end

      def fetch_source
        {
          "provider" => provider,
          "hostname" => azure_hostname,
          "api-endpoint" => azure_api_endpoint,
          "repo" => azure_repository_path,
          "directory" => directory,
          "branch" => branch
        }
      end

      def directory
        ENV.fetch("DEPENDABOT_DIRECTORY", "/")
      end

      def branch
        ENV.fetch("DEPENDABOT_TARGET_BRANCH", nil)
      end

      def github_access_token
        ENV.fetch("GITHUB_ACCESS_TOKEN", nil)
      end

      def provider
        "azure"
      end

      def azure_client
        @azure_client ||= TingleSoftware::Dependabot::Clients::Azure.for_source(
          source: ::Dependabot::Source.new(
            provider: provider,
            hostname: azure_hostname,
            api_endpoint: azure_api_endpoint,
            repo: azure_repository_path,
            directory: directory,
            branch: branch
          ),
          credentials: fetch_credentials.map { |c| ::Dependabot::Credential.new(c) }
        )
      end

      def azure_protocol
        ENV.fetch("AZURE_PROTOCOL", "https")
      end

      def azure_hostname
        ENV.fetch("AZURE_HOSTNAME", "dev.azure.com")
      end

      def azure_port
        ENV.fetch("AZURE_PORT", azure_protocol == "http" ? "80" : "443")
      end

      def azure_virtual_directory
        ENV.fetch("AZURE_VIRTUAL_DIRECTORY", "")
      end

      def azure_api_endpoint
        virual_directory = azure_virtual_directory.empty? ? "" : "#{azure_virtual_directory}/}"
        "#{azure_protocol}://#{azure_hostname}:#{azure_port}/#{virual_directory}"
      end

      def azure_organization
        ENV.fetch("AZURE_ORGANIZATION", nil)
      end

      def azure_project
        ENV.fetch("AZURE_PROJECT", nil)
      end

      def azure_repository
        ENV.fetch("AZURE_REPOSITORY", nil)
      end

      def azure_repository_path
        "#{azure_organization}/#{azure_project}/_git/#{azure_repository}"
      end

      def azure_set_auto_complete
        ENV.fetch("AZURE_SET_AUTO_COMPLETE", nil) == "true"
      end

      def azure_auto_complete_ignore_config_ids
        JSON.parse(ENV.fetch("AZURE_AUTO_COMPLETE_IGNORE_CONFIG_IDS", "[]")).compact
      end

      def azure_set_auto_approve
        ENV.fetch("AZURE_AUTO_APPROVE_PR", nil) == "true"
      end

      def azure_auto_approve_user_token
        ENV.fetch("AZURE_AUTO_APPROVE_USER_TOKEN", nil) || ENV.fetch("AZURE_ACCESS_TOKEN", nil)
      end

      def azure_merge_strategy
        ENV.fetch("AZURE_MERGE_STRATEGY", "squash")
      end

      def active_pull_requests
        @active_pull_requests ||= fetch_active_pull_requests
      end

      def fetch_active_pull_requests
        ::Dependabot.logger.info(
          "Fetching pull request info for existing dependency updates."
        )
        user_id = azure_client.get_user_id
        target_branch_name = branch || azure_client.fetch_default_branch(azure_repository_path)
        azure_client.pull_requests_active(user_id, target_branch_name)
      end

      def active_pull_requests_property_sets
        @active_pull_requests_property_sets ||= fetch_active_pull_requests_property_sets
      end

      def fetch_active_pull_requests_property_sets
        active_pull_requests.map do |pr|
          pull_request_id = pr["pullRequestId"].to_s
          azure_client.pull_request_properties_list(pull_request_id).to_h do |k, v|
            [k, v["$value"]]
          end
        end
      end

      # TODO: Implement this
      def open_pull_requests_limit
        ENV.fetch("DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT", "5").to_i
      end

      def security_updates_only
        # If the pull request limit is set to zero, we assume that the user just wants security updates
        return true if open_pull_requests_limit.zero?

        ENV.fetch("DEPENDABOT_SECURITY_UPDATES_ONLY", nil) == "true"
      end

      def security_advisories
        @security_advisories ||= parse_security_advisories_file
      end

      def parse_security_advisories_file
        security_advisories_file_path = ENV.fetch("DEPENDABOT_SECURITY_ADVISORIES_FILE", nil)
        return [] unless security_advisories_file_path && File.exist?(security_advisories_file_path)

        JSON.parse(File.read(security_advisories_file_path))
      end

      def pr_author_name
        ENV.fetch("DEPENDABOT_AUTHOR_NAME", "dependabot[bot]")
      end

      def pr_author_email
        ENV.fetch("DEPENDABOT_AUTHOR_EMAIL", "noreply@github.com")
      end

      def pr_signature_key
        ENV.fetch("DEPENDABOT_SIGNATURE_KEY", nil)
      end

      def pr_message_header
        ENV.fetch("DEPENDABOT_MESSAGE_HEADER", nil)
      end

      def pr_message_footer
        ENV.fetch("DEPENDABOT_MESSAGE_FOOTER", nil)
      end

      def pr_custom_labels
        labels = JSON.parse(ENV.fetch("DEPENDABOT_LABELS", "[]")).compact
        return labels if labels.count.nonzero?

        nil # nil instead of empty array to ensure default labels are passed
      end

      def pr_reviewers
        reviewers = JSON.parse(ENV.fetch("DEPENDABOT_REVIEWERS", "[]")).compact
        return reviewers if reviewers.count.nonzero?

        nil # nil instead of empty array to avoid API rejection
      end

      def pr_assignees
        assignees = JSON.parse(ENV.fetch("DEPENDABOT_ASSIGNEES", "[]")).compact
        return assignees if assignees.count.nonzero?

        nil # nil instead of empty array to avoid API rejection
      end

      def pr_milestone
        milestone = ENV.fetch("DEPENDABOT_MILESTONE", nil).to_i
        return milestone if milestone.nonzero?

        nil
      end

      def pr_branch_name_separator
        ENV.fetch("DEPENDABOT_BRANCH_NAME_SEPARATOR", "/")
      end

      def pr_branch_name_prefix
        ENV.fetch("DEPENDABOT_BRANCH_NAME_PREFIX", "dependabot")
      end
    end
  end
end
