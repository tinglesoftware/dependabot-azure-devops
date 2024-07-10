# typed: strict
# frozen_string_literal: true

require "dependabot/job"
require "tinglesoftware/dependabot/vulnerabilities"

#
# Represents a Dependabot job; A single unit of work that Dependabot can perform (e.g. "update all dependencies").
# This class contains all the user configuration needed to perform the job.
#
module TingleSoftware
  module Dependabot
    class Job < ::Dependabot::Job
      extend T::Sig

      attr_reader :azure_client

      attr_reader :azure_set_auto_complete

      attr_reader :azure_auto_complete_ignore_config_ids

      attr_reader :azure_set_auto_approve

      attr_reader :azure_auto_approve_user_token

      attr_reader :azure_merge_strategy

      attr_reader :active_pull_requests

      attr_reader :pr_author_name

      attr_reader :pr_author_email

      attr_reader :pr_signature_key

      attr_reader :pr_message_header

      attr_reader :pr_message_footer

      attr_reader :pr_custom_labels

      attr_reader :pr_reviewers

      attr_reader :pr_assignees

      attr_reader :pr_milestone

      attr_reader :pr_branch_name_separator

      def initialize(attributes, azure_client)
        @azure_client = azure_client
        @azure_set_auto_complete = T.let(attributes.fetch(:azure_set_auto_complete), T::Boolean)
        @azure_auto_complete_ignore_config_ids = T.let(attributes.fetch(:azure_auto_complete_ignore_config_ids), T.nilable(T::Array[String]))
        @azure_set_auto_approve = T.let(attributes.fetch(:azure_set_auto_approve), T::Boolean)
        @azure_auto_approve_user_token = T.let(attributes.fetch(:azure_auto_approve_user_token), T.nilable(String))
        @azure_merge_strategy = T.let(attributes.fetch(:azure_merge_strategy), String)
        @active_pull_requests = T.let(attributes.fetch(:active_pull_requests), T.nilable(T::Array[T::Hash[String, T.untyped]]))
        @pr_author_name = T.let(attributes.fetch(:pr_author_name), String)
        @pr_author_email = T.let(attributes.fetch(:pr_author_email), String)
        @pr_signature_key = T.let(attributes.fetch(:pr_signature_key, nil), T.nilable(String))
        @pr_message_header = T.let(attributes.fetch(:pr_message_header, nil), T.nilable(String))
        @pr_message_footer = T.let(attributes.fetch(:pr_message_footer, nil), T.nilable(String))
        @pr_custom_labels = T.let(attributes.fetch(:pr_custom_labels, nil), T.nilable(T::Array[String]))
        @pr_reviewers = T.let(attributes.fetch(:pr_reviewers, nil), T.nilable(T::Array[String]))
        @pr_assignees = T.let(attributes.fetch(:pr_assignees, nil), T.nilable(T.any(T::Array[String], T::Array[Integer])))
        @pr_milestone = T.let(attributes.fetch(:pr_milestone, nil), T.nilable(Integer))
        @pr_branch_name_separator = T.let(attributes.fetch(:pr_branch_name_separator), String)

        super(attributes)
      end

      def vulnerabilities_fetcher
        @vulnerabilities_fetcher ||= TingleSoftware::Dependabot::Vulnerabilities::Fetcher.new(package_manager, token) if token
      end

      sig { params(dependency: ::Dependabot::Dependency).returns(T::Array[::Dependabot::SecurityAdvisory]) }
      def security_advisories_for(dependency)
        # If configured, fetch security advisories from GitHub's Security Advisory API
        fetch_missing_advisories_for_dependency(dependency) if vulnerabilities_fetcher
        super
      end

      def fetch_missing_advisories_for_dependency(dependency)
        ::Dependabot.logger.info("Checking if #{dependency.name} has any security advisories")
        advisories = vulnerabilities_fetcher.fetch(dependency.name)
        @security_advisories.push(
          *advisories.map do |advisory|
            # Filter out nil (using .compact), white spaces and empty strings which is necessary for situations
            # where the API response contains null that is converted to nil, or it is an empty
            # string. For example, npm package named faker does not have patched version as of 2023-01-16
            # See: https://github.com/advisories/GHSA-5w9c-rv96-fr7g for npm package
            # This ideally fixes
            # https://github.com/tinglesoftware/dependabot-azure-devops/issues/453#issuecomment-1383587644
            {
              "dependency-name" => dependency.name,
              "patched-versions" => (advisory["patched-versions"] || []).compact.reject { |v| v.strip.empty? },
              "unaffected-versions" => (advisory["unaffected-versions"] || []).compact.reject { |v| v.strip.empty? },
              "affected-versions" => (advisory["affected-versions"] || []).compact.reject { |v| v.strip.empty? }
            }
          end
        )
        ::Dependabot.logger.debug("##{JSON.pretty_generate(@security_advisories)}")
      end
    end
  end
end
