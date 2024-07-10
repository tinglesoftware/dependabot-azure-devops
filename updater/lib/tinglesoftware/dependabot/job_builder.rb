# typed: strict
# frozen_string_literal: true

require "json"
require "dependabot/job"
require "dependabot/source"
require "dependabot/credential"

require "tinglesoftware/dependabot/clients/azure"

#
# Parse Dependabot user configuration from environment variables and convert them in to a Dependabot job object.
#
module TingleSoftware
  module Dependabot
    class JobBuilder
      def self.from_env_vars
        ::Dependabot.logger.info("Parsing job info from host environment variables.")
        options = {
          id: id,
          allowed_updates: allowed_updates,
          commit_message_options: commit_message_options,
          credentials: credentials,
          dependencies: nil, # TODO: Implement this
          existing_pull_requests: existing_pull_requests,
          existing_group_pull_requests: existing_group_pull_requests,
          experiments: {}, # TODO: Fix experiments,
          ignore_conditions: ignore_conditions,
          package_manager: package_manager,
          reject_external_code: reject_external_code,
          repo_contents_path: repo_contents_path,
          requirements_update_strategy: nil, # TODO: Fix requirements_update_strategy,
          lockfile_only: lockfile_only,
          security_advisories: security_advisories,
          security_updates_only: security_updates_only,
          source: source,
          token: github_access_token,
          update_subdependencies: true,
          updating_a_pull_request: false, # TODO: Implement this
          vendor_dependencies: false, # TODO: Implement this
          dependency_groups: dependency_groups,
          dependency_group_to_refresh: nil, # TODO: Implement this
          # TinglingSoftware::Dependabot::Job specific options
          azure_set_auto_complete: azure_set_auto_complete,
          azure_auto_complete_ignore_config_ids: azure_auto_complete_ignore_config_ids,
          azure_set_auto_approve: azure_set_auto_approve,
          azure_auto_approve_user_token: azure_auto_approve_user_token,
          azure_merge_strategy: azure_merge_strategy,
          active_pull_requests: active_pull_requests,
          pr_author_name: pr_author_name,
          pr_author_email: pr_author_email,
          pr_signature_key: pr_signature_key,
          pr_message_header: pr_message_header,
          pr_message_footer: pr_message_footer,
          pr_custom_labels: pr_custom_labels,
          pr_reviewers: pr_reviewers,
          pr_assignees: pr_assignees,
          pr_milestone: pr_milestone,
          pr_branch_name_separator: pr_branch_name_separator,
          pr_branch_name_prefix: pr_branch_name_prefix
        }
        validate(options)
        ::Dependabot.logger.debug("Parsed job info: #{JSON.pretty_generate(options)}")
        TingleSoftware::Dependabot::Job.new(options, azure_client)
      end

      def self.validate(options)
        if options["security_updates_only"] && !options["token"]
          raise StandardError, "Security only updates are enabled but a GitHub token is not supplied! Cannot proceed"
        end
      end

      # TODO: DEPENDABOT_VENDOR
      # TODO: DEPENDABOT_FAIL_ON_EXCEPTION
      # TODO: DEPENDABOT_SKIP_PULL_REQUESTS
      # TODO: DEPENDABOT_CLOSE_PULL_REQUESTS
      # TODO: DEPENDABOT_EXCLUDE_REQUIREMENTS_TO_UNLOCK

      def self.id
        ENV.fetch("DEPENDABOT_JOB_ID", Time.now.to_i.to_s)
      end

      # TODO: Implement this
      def self.open_pull_requests_limit
        ENV.fetch("DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT", "5").to_i
      end

      def self.allowed_updates
        conditions = JSON.parse(ENV.fetch("DEPENDABOT_ALLOW_CONDITIONS", "[]")).compact
        return conditions if conditions.count.nonzero?

        # If no conditions are specified, default to updating all dependencies
        conditions << {
          "dependency-type" => "all"
        }
      end

      def self.commit_message_options
        JSON.parse(ENV.fetch("DEPENDABOT_COMMIT_MESSAGE_OPTIONS", "{}"), symbolize_names: true)
      end

      def self.github_access_token
        ENV.fetch("GITHUB_ACCESS_TOKEN", nil)
      end

      def self.credentials
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

      # GitHub native implementation modifies some of the names in the config file
      # https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#package-ecosystem
      PACKAGE_ECOSYSTEM_MAPPING = {
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
      }.freeze

      def self.package_manager
        pkg_mgr = ENV.fetch("DEPENDABOT_PACKAGE_MANAGER", "bundler")
        PACKAGE_ECOSYSTEM_MAPPING.fetch(pkg_mgr, pkg_mgr)
      end

      def self.reject_external_code
        ENV.fetch("DEPENDABOT_REJECT_EXTERNAL_CODE", nil) == "true"
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
        ENV.fetch("DEPENDABOT_LOCKFILE_ONLY", nil) == "true"
      end

      def self.dependency_groups
        groups = JSON.parse(ENV.fetch("DEPENDABOT_DEPENDENCY_GROUPS", "[]")).compact
        return groups if groups.count.nonzero?

        nil
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
        ENV.fetch("DEPENDABOT_TARGET_BRANCH", nil)
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

      def self.azure_set_auto_complete
        ENV.fetch("AZURE_SET_AUTO_COMPLETE", nil) == "true"
      end

      def self.azure_auto_complete_ignore_config_ids
        JSON.parse(ENV.fetch("AZURE_AUTO_COMPLETE_IGNORE_CONFIG_IDS", "[]")).compact
      end

      def self.azure_set_auto_approve
        ENV.fetch("AZURE_AUTO_APPROVE_PR", nil) == "true"
      end

      def self.azure_auto_approve_user_token
        ENV.fetch("AZURE_AUTO_APPROVE_USER_TOKEN", nil) || ENV.fetch("AZURE_ACCESS_TOKEN", nil)
      end

      def self.azure_merge_strategy
        ENV.fetch("AZURE_MERGE_STRATEGY", "squash")
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

      def self.active_pull_requests
        @active_pull_requests ||= fetch_active_pull_requests
      end

      def self.fetch_active_pull_requests
        ::Dependabot.logger.info(
          "Fetching pull request info for existing dependency updates."
        )
        user_id = azure_client.get_user_id
        target_branch_name = branch || azure_client.fetch_default_branch(repo_name)
        azure_client.pull_requests_active(user_id, target_branch_name)
      end

      def self.active_pull_requests_property_sets
        @active_pull_requests_property_sets ||= fetch_active_pull_requests_property_sets
      end

      def self.fetch_active_pull_requests_property_sets
        active_pull_requests.map do |pr|
          pull_request_id = pr["pullRequestId"].to_s
          azure_client.pull_request_properties_list(pull_request_id).map do |k,v|
            [k, v["$value"]]
          end.to_h
        end
      end

      def self.existing_pull_requests
        dependencies = active_pull_requests_property_sets.map do |props|
          JSON.parse(props[ApiClients::AzureApiClient::PullRequest::Properties::UPDATED_DEPENDENCIES] || nil.to_json)
        end&.compact
        dependencies.select { |d| d.is_a?(Array) }
      end

      def self.existing_group_pull_requests
        dependencies = active_pull_requests_property_sets.map do |props|
          JSON.parse(props[ApiClients::AzureApiClient::PullRequest::Properties::UPDATED_DEPENDENCIES] || nil.to_json)
        end&.compact
        dependencies.select { |d| d.is_a?(Hash) }
      end

      def self.security_updates_only
        # If the pull request limit is set to zero, we assume that the user just wants security updates
        return true if open_pull_requests_limit.zero?

        ENV.fetch("DEPENDABOT_SECURITY_UPDATES_ONLY", nil) == "true"
      end

      def self.security_advisories
        @security_advisories ||= load_security_advisories
      end

      def self.load_security_advisories
        security_advisories_file_path = ENV.fetch("DEPENDABOT_SECURITY_ADVISORIES_FILE", nil)
        return [] unless security_advisories_file_path && File.exist?(security_advisories_file_path)
        JSON.parse(File.read(security_advisories_file_path))
      end

      def self.pr_author_name
        ENV.fetch("DEPENDABOT_AUTHOR_NAME", "dependabot[bot]")
      end

      def self.pr_author_email
        ENV.fetch("DEPENDABOT_AUTHOR_EMAIL", "noreply@github.com")
      end

      def self.pr_signature_key
        ENV.fetch("DEPENDABOT_SIGNATURE_KEY", nil)
      end

      def self.pr_message_header
        ENV.fetch("DEPENDABOT_MESSAGE_HEADER", nil)
      end

      def self.pr_message_footer
        ENV.fetch("DEPENDABOT_MESSAGE_FOOTER", nil)
      end

      def self.pr_custom_labels
        labels = JSON.parse(ENV.fetch("DEPENDABOT_LABELS", "[]")).compact
        return labels if labels.count.nonzero?

        nil # nil instead of empty array to ensure default labels are passed
      end

      def self.pr_reviewers
        reviewers = JSON.parse(ENV.fetch("DEPENDABOT_REVIEWERS", "[]")).compact
        return reviewers if reviewers.count.nonzero?

        nil # nil instead of empty array to avoid API rejection
      end

      def self.pr_assignees
        assignees = JSON.parse(ENV.fetch("DEPENDABOT_ASSIGNEES", "[]")).compact
        return assignees if assignees.count.nonzero?

        nil # nil instead of empty array to avoid API rejection
      end

      def self.pr_milestone
        milestone = ENV.fetch("DEPENDABOT_MILESTONE", nil).to_i
        return milestone if milestone.nonzero?

        nil
      end

      def self.pr_branch_name_separator
        ENV.fetch("DEPENDABOT_BRANCH_NAME_SEPARATOR", "/")
      end

      def self.pr_branch_name_prefix
        ENV.fetch("DEPENDABOT_BRANCH_NAME_PREFIX", "dependabot")
      end
    end
  end
end
