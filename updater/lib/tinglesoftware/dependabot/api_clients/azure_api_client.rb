# typed: strict
# frozen_string_literal: true

require "dependabot/api_client"

# Mock client for the internal Dependabot Service's API
#
# This API is only available to Dependabot jobs being executed within the official
# hosted infrastructure and is not available to external users.
#
module TingleSoftware
  module Dependabot
    module ApiClients
      class AzureApiClient < ::Dependabot::ApiClient
        extend T::Sig
        def initialize() end

        sig { params(dependency_change: ::Dependabot::DependencyChange, base_commit_sha: String).void }
        def create_pull_request(dependency_change, base_commit_sha) end

        sig { params(dependency_change: ::Dependabot::DependencyChange, base_commit_sha: String).void }
        def update_pull_request(dependency_change, base_commit_sha) end

        sig { params(dependency_names: T.any(String, T::Array[String]), reason: T.any(String, Symbol)).void }
        def close_pull_request(dependency_names, reason) end

        sig { params(error_type: T.any(String, Symbol), error_details: T.nilable(T::Hash[T.untyped, T.untyped])).void }
        def record_update_job_error(error_type:, error_details:) end

        sig { params(error_type: T.any(Symbol, String), error_details: T.nilable(T::Hash[T.untyped, T.untyped])).void }
        def record_update_job_unknown_error(error_type:, error_details:) end

        sig { params(base_commit_sha: String).void }
        def mark_job_as_processed(base_commit_sha) end

        sig { params(dependencies: T::Array[T::Hash[Symbol, T.untyped]], dependency_files: T::Array[String]).void }
        def update_dependency_list(dependencies, dependency_files) end

        sig { params(ecosystem_versions: T::Hash[Symbol, T.untyped]).void }
        def record_ecosystem_versions(ecosystem_versions) end

        sig { params(metric: String, tags: T::Hash[String, String]).void }
        def increment_metric(metric, tags:) end
      end
    end
  end
end
