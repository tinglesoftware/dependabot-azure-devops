# typed: strict
# frozen_string_literal: true

require "json"
require "dependabot/api_client"
require "dependabot/pull_request_creator"
require "dependabot/pull_request_updater"
require "dependabot/credential"

#
# Azure DevOps implementation of the [internal] Dependabot Service API client.
#
# This API is normally reserved for Dependabot internal use and is used to send job data to Dependabots [internal] APIs.
# Actions like creating/updating/closing pull requests are deferred to this API to be actioned later asynchronously.
# However, in Azure DevOps, we don't have a remote API to defer actions to, so we instead perform pull request changes
# here synchronously. This keeps the entire end-to-end update process contained within a single self-contained job.
#
module TingleSoftware
  module Dependabot
    module ApiClients
      class AzureApiClient < ::Dependabot::ApiClient
        extend T::Sig
        attr_reader :job

        # The names of all custom properties use dto store dependabot metadata in Azure DevOps pull requests.
        # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-properties?view=azure-devops-rest-7.1
        module PullRequest
          module Properties
            BASE_COMMIT_SHA = "dependabot.base_commit_sha"
            UPDATED_DEPENDENCIES = "dependabot.updated_dependencies"
          end
        end

        def initialize(job:)
          @job = job
        end

        sig { params(dependency_change: ::Dependabot::DependencyChange, base_commit_sha: String).void }
        def create_pull_request(dependency_change, base_commit_sha) # rubocop:disable Metrics/AbcSize, Metrics/MethodLength
          pr_creator = ::Dependabot::PullRequestCreator.new(
            source: job.source,
            base_commit: base_commit_sha,
            dependencies: dependency_change.updated_dependencies,
            dependency_group: dependency_change.dependency_group,
            files: dependency_change.updated_dependency_files,
            credentials: job.credentials,
            pr_message_header: job.pr_message_header,
            pr_message_footer: job.pr_message_footer,
            author_details: {
              name: job.pr_author_name,
              email: job.pr_author_email
            },
            signature_key: job.pr_signature_key,
            commit_message_options: job.commit_message_options,
            custom_labels: job.pr_custom_labels,
            reviewers: job.pr_reviewers,
            assignees: job.pr_assignees,
            milestone: job.pr_milestone,
            vulnerabilities_fixed: job.vulnerabilities_fixed_for(dependency_change.updated_dependencies),
            branch_name_separator: job.pr_branch_name_separator,
            branch_name_prefix: job.pr_branch_name_prefix,
            label_language: true,
            automerge_candidate: true,
            github_redirection_service: ::Dependabot::PullRequestCreator::DEFAULT_GITHUB_REDIRECTION_SERVICE,
            provider_metadata: {
              work_item: job.pr_milestone
            }
          )

          # Publish the pull request
          ::Dependabot.logger.info("Submitting '#{dependency_change.pr_message.pr_name}' pull request for creation.")
          pull_request = pr_creator.create
          if pull_request
            req_status = pull_request&.status
            if req_status == 201
              pull_request = JSON[pull_request.body]
              pull_request_id = pull_request["pullRequestId"]
              pull_request_title = pull_request["title"]
              ::Dependabot.logger.info(
                "Created pull request for '#{pull_request_title}'(##{pull_request_id})."
              )

              # Update the pull request property metadata with the updated dependencies info.
              pull_request_replace_property_metadata(pull_request, dependency_change, base_commit_sha)

              # Apply auto-complete and auto-approve settings
              pull_request_auto_complete(pull_request) if job.azure_set_auto_complete
              pull_request_auto_approve(pull_request) if job.azure_set_auto_approve

            else
              content = JSON[pull_request.body]
              message = content["message"]
              ::Dependabot.logger.error("Failed! PR already exists or an error has occurred.")
              # throw exception here because pull_request.create does not throw
              raise StandardError, "Pull Request creation failed with status #{req_status}. Message: #{message}"
            end
          else
            ::Dependabot.logger.info("Seems PR is already present.")
          end
        end

        sig { params(dependency_change: ::Dependabot::DependencyChange, base_commit_sha: String).void }
        def update_pull_request(_dependency_change, _base_commit_sha)
          raise "not yet implemented"
        end

        sig { params(dependency_names: T.any(String, T::Array[String]), reason: T.any(String, Symbol)).void }
        def close_pull_request(_dependency_names, _reason)
          raise "not yet implemented"
          # TODO: Implement this
          # job.azure_client.pull_request_comment(pr_id, reason)
          # job.azure_client.branch_delete(source_ref_name) # do this first to avoid hanging branches
          # job.azure_client.pull_request_abandon(pr_id)
        end

        sig { params(error_type: T.any(String, Symbol), error_details: T.nilable(T::Hash[T.untyped, T.untyped])).void }
        def record_update_job_error(error_type:, error_details:)
          # TODO: Implement this
          # raise e if job.fail_on_exception
        end

        sig { params(error_type: T.any(Symbol, String), error_details: T.nilable(T::Hash[T.untyped, T.untyped])).void }
        def record_update_job_unknown_error(error_type:, error_details:)
          # TODO: Implement this
          # raise e if job.fail_on_exception
        end

        sig { params(base_commit_sha: String).void }
        def mark_job_as_processed(base_commit_sha)
          # No implementation required for Azure DevOps
        end

        sig { params(dependencies: T::Array[T::Hash[Symbol, T.untyped]], dependency_files: T::Array[String]).void }
        def update_dependency_list(dependencies, dependency_files)
          # No implementation required for Azure DevOps
        end

        sig { params(ecosystem_versions: T::Hash[Symbol, T.untyped]).void }
        def record_ecosystem_versions(ecosystem_versions)
          # No implementation required for Azure DevOps
        end

        sig { params(metric: String, tags: T::Hash[String, String]).void }
        def increment_metric(metric, tags:)
          puts "📊 METRIC '#{metric}' #{JSON.pretty_generate(tags)}"
        end

        private

        def pull_request_auto_complete(pull_request)
          pull_request_id = pull_request["pullRequestId"]
          pull_request_title = pull_request["title"]
          pull_request_description = pull_request["description"]

          auto_complete_user_id = pull_request["createdBy"]["id"].to_s

          #
          # Pull requests that pass all policies will be merged automatically.
          # Optional policies can be ignored by passing their identifiers
          #
          # The merge commit message should contain the PR number and title for tracking.
          # This is the default behaviour in Azure DevOps
          # Example:
          # Merged PR 24093: Bump Tingle.Extensions.Logging.LogAnalytics from 3.4.2-ci0005 to 3.4.2-ci0006
          #
          # Bumps [Tingle.Extensions.Logging.LogAnalytics](...) from 3.4.2-ci0005 to 3.4.2-ci0006
          # - [Release notes](....)
          # - [Changelog](....)
          # - [Commits](....)
          #
          # TODO: Figure out why request fails if commit message is 4000 characters long
          merge_commit_message_max_length = ::Dependabot::PullRequestCreator::Azure::PR_DESCRIPTION_MAX_LENGTH - 300
          merge_commit_message_encoding = ::Dependabot::PullRequestCreator::Azure::PR_DESCRIPTION_ENCODING
          merge_commit_message = "Merged PR #{pull_request_id}: #{pull_request_title}\n\n#{pull_request_description}"
                                 .force_encoding(merge_commit_message_encoding)
          if merge_commit_message.length > merge_commit_message_max_length
            merge_commit_message = merge_commit_message[0..merge_commit_message_max_length]
          end

          ::Dependabot.logger.info("Setting auto complete on ##{pull_request_id}.")
          job.azure_client.autocomplete_pull_request(
            # Adding argument names will fail! Maybe because there is no spec?
            pull_request_id.to_i,
            auto_complete_user_id,
            merge_commit_message,
            true, # delete_source_branch
            true, # squash_merge
            job.azure_merge_strategy,
            false, # trans_work_items
            job.azure_auto_complete_ignore_config_ids
          )
        end

        def pull_request_auto_approve(pull_request)
          pull_request_id = pull_request["pullRequestId"]
          ::Dependabot.logger.info("Auto Approving PR #{pull_request_id}")
          job.azure_client.pull_request_approve(
            # Adding argument names will fail! Maybe because there is no spec?
            pull_request_id.to_i,
            job.azure_auto_approve_user_token
          )
        end

        def pull_request_replace_property_metadata(pull_request, dependency_change, base_commit_sha)
          # Update the pull request property metadata with info about the updated dependencies.
          # This is used in `job_builder.rb` to calculate "existing_pull_requests" in future jobs.
          job.azure_client.pull_request_properties_update(
            pull_request["pullRequestId"].to_s,
            {
              PullRequest::Properties::BASE_COMMIT_SHA => base_commit_sha.to_s,
              PullRequest::Properties::UPDATED_DEPENDENCIES =>
                pull_request_updated_dependencies_property_data(dependency_change).to_json
            }
          )
        end

        def pull_request_updated_dependencies_property_data(dependency_change)
          if dependency_change.grouped_update?
            {
              "dependency-group-name" => dependency_change.dependency_group.name,
              "dependencies" => dependency_change.updated_dependencies.map do |dep|
                {
                  "dependency-name" => dep.name,
                  "dependency-version" => dep.version,
                  "directory" => dep.directory,
                  "dependency-removed" => dep.removed? ? true : nil
                }.compact
              end
            }
          else
            dependency_change.updated_dependencies.map do |dep|
              {
                "dependency-name" => dep.name,
                "dependency-version" => dep.version,
                "dependency-removed" => dep.removed? ? true : nil
              }.compact
            end
          end
        end
      end
    end
  end
end
