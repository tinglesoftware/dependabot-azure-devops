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
# However, in Azure DevOps, we (normally) run inside a pipeline agent and don't have a remote API to defer actions to,
# so we instead perform pull request changes here synchronously. This keeps the entire end-to-end update process
# contained within a single self-contained job.
#
module TingleSoftware
  module Dependabot
    module ApiClients
      class AzureApiClient < ::Dependabot::ApiClient
        extend T::Sig
        attr_reader :job

        # Custom properties used to store dependabot metadata in Azure DevOps pull requests.
        # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-properties
        module PullRequest
          module Properties
            PACKAGE_MANAGER = "dependabot.package_manager"
            BASE_COMMIT_SHA = "dependabot.base_commit_sha"
            UPDATED_DEPENDENCIES = "dependabot.updated_dependencies"
          end
        end

        def initialize(job:, dependency_snapshot_resolver:)
          @job = job
          @dependency_snapshot_resolver = dependency_snapshot_resolver
        end

        sig { params(dependency_change: ::Dependabot::DependencyChange, base_commit_sha: String).void }
        def create_pull_request(dependency_change, base_commit_sha) # rubocop:disable Metrics/AbcSize, Metrics/MethodLength
          return log_skipped_pull_request("creation", dependency_change) if job.skip_pull_requests
          return log_open_limit_reached_for_pull_requests if job.open_pull_request_limit_reached?

          ::Dependabot.logger.info(
            "Creating pull request for '#{dependency_change.pr_message.pr_name}'."
          )

          # Create the pull request
          pull_request = ::Dependabot::PullRequestCreator.new(
            source: job.source,
            base_commit: base_commit_sha,
            dependencies: dependency_change.updated_dependencies,
            dependency_group: dependency_change.dependency_group,
            files: dependency_change.updated_dependency_files,
            credentials: job.credentials,
            pr_message_header: pull_request_header_with_compatibility_scores(dependency_change.updated_dependencies),
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
          ).create

          # Parse the response and log the result
          if pull_request
            req_status = pull_request&.status
            if req_status == 201
              pull_request = JSON[pull_request.body]
              pull_request_id = pull_request["pullRequestId"]
              pull_request_title = pull_request["title"]
              ::Dependabot.logger.info(
                "Created PR ##{pull_request_id}: #{pull_request_title}"
              )
            else
              content = JSON[pull_request.body]
              message = content["message"]
              ::Dependabot.logger.error("Failed! PR already exists or an error has occurred.")
              # throw exception here because pull_request.create does not throw
              raise StandardError, "Pull Request creation failed with status #{req_status}. Message: #{message}"
            end
          else
            ::Dependabot.logger.warn("Seems PR is already present.")
          end

          # Update the pull request property metadata, auto-complete, auto-approve, and refresh active pull requests
          pull_request_sync_state_data(pull_request, dependency_change, base_commit_sha, true) if pull_request_id
        end

        sig { params(dependency_change: ::Dependabot::DependencyChange, base_commit_sha: String).void }
        def update_pull_request(dependency_change, base_commit_sha) # rubocop:disable Metrics/AbcSize, Metrics/MethodLength
          return log_skipped_pull_request("update", dependency_change) if job.skip_pull_requests

          # Find the pull request to update
          dependency_names = dependency_change.updated_dependencies.map(&:name).join(",")
          dependency_group_name = dependency_change.dependency_group.name if dependency_change.grouped_update?
          pull_request = job.existing_pull_request_with_updated_dependencies(
            pull_request_updated_dependencies_property_data(dependency_change)
          )
          pull_request_id = pull_request["pullRequestId"].to_s if pull_request
          pull_request_title = pull_request["title"].to_s if pull_request
          unless pull_request_id
            raise StandardError, "Unable to find pull request for #{dependency_group_name || dependency_names}"
          end

          # Ignore pull requests that don't have conflicts. If there is no conflict, we don't need to update anything
          if pull_request["mergeStatus"] != "conflicts"
            ::Dependabot.logger.info(
              "Skipping pull request update for PR ##{pull_request_id}: #{pull_request_title}, " \
              "there are no merge conflicts to resolve (i.e. nothing actually needs updating)."
            )
            return
          end

          # Ignore the pull request if it has been manually edited
          if job.azure_client.pull_request_commits(pull_request_id).length > 1
            ::Dependabot.logger.info(
              "Skipping pull request update for PR ##{pull_request_id}: #{pull_request_title}, " \
              "it has been manually edited. It is assumed that somebody else is working on it already."
            )
            return
          end

          ::Dependabot.logger.info(
            "Updating pull request for PR ##{pull_request_id}: #{pull_request_title}, " \
            "resolving merge conflict(s)."
          )

          # Update the pull request
          ::Dependabot::PullRequestUpdater.new(
            source: job.source,
            base_commit: base_commit_sha,
            old_commit: pull_request["lastMergeSourceCommit"]["commitId"],
            files: dependency_change.updated_dependency_files,
            credentials: job.credentials,
            pull_request_number: pull_request_id.to_i,
            author_details: {
              name: job.pr_author_name,
              email: job.pr_author_email
            },
            signature_key: job.pr_signature_key
          ).update

          # Update the pull request property metadata, auto-complete, auto-approve, and refresh active pull requests
          pull_request_sync_state_data(pull_request, dependency_change, base_commit_sha, false)
        end

        sig { params(dependency_names: T.any(String, T::Array[String]), reason: T.any(String, Symbol)).void }
        def close_pull_request(dependency_names, reason)
          return log_skipped_pull_request("close", nil) if job.skip_pull_requests

          # Find the pull request to close
          pull_request = job.existing_pull_request_for_dependency_names(dependency_names)
          pull_request_id = pull_request["pullRequestId"].to_s if pull_request
          raise StandardError, "Unable to find pull request for #{dependency_names.join(',')}" unless pull_request_id

          # Comment on the PR explaining why it was closed
          pull_request_add_comment_with_close_reason(pull_request, dependency_names, reason)

          return log_skipped_pull_request("close", nil) unless job.close_pull_requests

          # Delete the source branch
          # Do this first to avoid hanging branches
          pull_request_delete_source_branch(pull_request)

          # Close the pull request
          job.azure_client.pull_request_abandon(pull_request_id)
        end

        sig { params(action: String, dependency_change: T.nilable(::Dependabot::DependencyChange)).void }
        def log_skipped_pull_request(action, dependency_change)
          ::Dependabot.logger.info("Skipping pull request #{action} as it is disabled for this job.")
          return unless job.debug_enabled?

          ::Dependabot.logger.debug("Staged file changes were:") if dependency_change
          dependency_snapshot = @dependency_snapshot_resolver.call
          dependency_change&.updated_dependency_files&.each do |updated_file|
            log_file_diff(
              dependency_snapshot.dependency_files.find { |f| f.name == updated_file.name },
              updated_file
            )
          end
        end

        def log_file_diff(original_file, updated_file)
          return unless original_file
          return if original_file.content == updated_file.content

          summary = case updated_file.operation
                    when ::Dependabot::DependencyFile::Operation::CREATE
                      " + Created '#{updated_file.name}' in '#{updated_file.directory}'"
                    when ::Dependabot::DependencyFile::Operation::UPDATE
                      " ± Updated '#{updated_file.name}' in '#{updated_file.directory}'"
                    when ::Dependabot::DependencyFile::Operation::DELETE
                      " - Deleted '#{updated_file.name}' in '#{updated_file.directory}'"
                    end

          original_tmp_file = Tempfile.new("original")
          original_tmp_file.write(original_file.content)
          original_tmp_file.close

          updated_tmp_file = Tempfile.new("updated")
          updated_tmp_file.write(updated_file.content)
          updated_tmp_file.close

          diff = `diff -u #{original_tmp_file.path} #{updated_tmp_file.path}`.lines
          added_lines = diff.count { |line| line.start_with?("+") }
          removed_lines = diff.count { |line| line.start_with?("-") }

          ::Dependabot.logger.debug(
            summary + "\n" \
                      "~~~\n" \
                      "#{diff.join}\n" \
                      "~~~\n" \
                      "#{added_lines} insertions (+), #{removed_lines} deletions (-)"
          )
        end

        def log_open_limit_reached_for_pull_requests
          ::Dependabot.logger.info(
            "Skipping pull request creation as the open pull request limit (#{job.open_pull_requests_limit}) " \
            "has been reached."
          )
        end

        sig { params(error_type: T.any(String, Symbol), error_details: T.nilable(T::Hash[T.untyped, T.untyped])).void }
        def record_update_job_error(error_type:, error_details:)
          # No implementation required for Azure DevOps, errors are dumped to output console already
        end

        sig { params(error_type: T.any(Symbol, String), error_details: T.nilable(T::Hash[T.untyped, T.untyped])).void }
        def record_update_job_unknown_error(error_type:, error_details:)
          # No implementation required for Azure DevOps, errors are dumped to output console already
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
          # No implementation required for Azure DevOps
        end

        private

        def pull_request_sync_state_data(pull_request, dependency_change, base_commit_sha, is_new_pr)
          # Update the pull request properties with our dependabot metadata
          pull_request_replace_property_metadata(pull_request, job.package_manager, dependency_change, base_commit_sha)

          # Apply auto-complete and auto-approve settings
          pull_request_auto_complete(pull_request) if job.azure_set_auto_complete
          pull_request_auto_approve(pull_request) if job.azure_set_auto_approve

          # Refresh active pull requests to include the new PR
          # Required to ensure that the new PR is included in the next action (if any) and duplicates are avoided
          job.refresh_open_pull_requests if is_new_pr
        end

        def pull_request_add_comment_with_close_reason(pull_request, dependency_names, reason)
          return unless job.comment_pull_requests

          # Generate a user-friendly comment based on the reason for closing the PR
          # The first dependency is the "lead" dependency in a multi-dependency update
          lead_dep_name = dependency_names.first
          reason_for_close_comment = {
            dependencies_changed: "Looks like the dependencies have changed",
            dependency_group_empty: "Looks like the dependencies in this group are now empty",
            dependency_removed: "Looks like #{lead_dep_name} is no longer a dependency",
            up_to_date: "Looks like #{lead_dep_name} is up-to-date now",
            update_no_longer_possible: "Looks like #{lead_dep_name} can no longer be updated"
            # ??? => "Looks like these dependencies are updatable in another way, so this is no longer needed"
            # ??? => "Superseded by ##{new_pull_request_id}"
          }.freeze.fetch(reason) + ", so this is no longer needed."

          return unless reason_for_close_comment

          # Comment on the PR explaining why it was closed
          pull_request_id = pull_request["pullRequestId"].to_s
          job.azure_client.pull_request_thread_with_comments(
            pull_request_id, "system", [reason_for_close_comment], "fixed"
          )
          rescue StandardError => e
            # This has most likely happened because our access token does not have permission to comment on PRs
            # Commenting on the PR is not critical to the process, so continue on
            ::Dependabot.logger.warn(
              "Failed to comment on PR ##{pull_request_id} with close reason. The error was: #{e.message}"
            )
        end

        def pull_request_delete_source_branch(pull_request)
          pull_request_id = pull_request["pullRequestId"]
          pull_request_source_ref_name = pull_request["sourceRefName"]
          job.azure_client.branch_delete(pull_request_source_ref_name)
          rescue StandardError => e
            # This has most likely happened because the branch has already been deleted or our access token does
            # not have permission to manage branches. Deleting the branch is not critical to the process, so continue on
            ::Dependabot.logger.warn(
              "Failed to delete source branch for PR ##{pull_request_id}. The error was: #{e.message}"
            )
        end

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
          #   Merged PR 24093: Bump Tingle.Extensions.Logging.LogAnalytics from 3.4.2-ci0005 to 3.4.2-ci0006
          #
          #   Bumps [Tingle.Extensions.Logging.LogAnalytics](...) from 3.4.2-ci0005 to 3.4.2-ci0006
          #   - [Release notes](....)
          #   - [Changelog](....)
          #   - [Commits](....)
          #
          # There appears to be a DevOps bug when setting "completeOptions" with a "mergeCommitMessage" that is
          # truncated to 4000 characters. The error message is:
          #   Invalid argument value.
          #   Parameter name: Completion options have exceeded the maximum encoded length (4184/4000)
          #
          # Most users seem to agree that the effective limit is about 3500 characters.
          # https://developercommunity.visualstudio.com/t/raise-the-character-limit-for-pull-request-descrip/365708
          #
          # Until this is fixed, we hard cap the max length to 3500 characters
          #
          merge_commit_message_max_length = 3500 # ::Dependabot::PullRequestCreator::Azure::PR_DESCRIPTION_MAX_LENGTH
          merge_commit_message_encoding = ::Dependabot::PullRequestCreator::Azure::PR_DESCRIPTION_ENCODING
          merge_commit_message = "Merged PR #{pull_request_id}: #{pull_request_title}\n\n#{pull_request_description}"
                                 .force_encoding(merge_commit_message_encoding)

          if merge_commit_message.length > merge_commit_message_max_length
            merge_commit_message = merge_commit_message[0..merge_commit_message_max_length]
          end

          ::Dependabot.logger.info("Setting auto complete on PR ##{pull_request_id}.")
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
          rescue StandardError => e
            # This has most likely happened because merge_commit_message exceeded 4000 characters (see comments above)
            # Auto-completing the PR is not critical to the process, so continue on
            ::Dependabot.logger.warn(
              "Failed to set auto-complete status for PR ##{pull_request_id}. The error was: #{e.message}"
            )
        end

        def pull_request_auto_approve(pull_request)
          pull_request_id = pull_request["pullRequestId"]
          ::Dependabot.logger.info("Setting auto approval on PR ##{pull_request_id}")
          job.azure_client.pull_request_approve(
            # Adding argument names will fail! Maybe because there is no spec?
            pull_request_id.to_i,
            job.azure_auto_approve_user_token
          )
          rescue StandardError => e
            # This has most likely happened because the auto-approve user token is invalid
            # Auto-approving the PR is not critical to the process, so continue on
            ::Dependabot.logger.warn(
              "Failed to set auto-approve status for PR ##{pull_request_id}. The error was: #{e.message}"
            )
        end

        def pull_request_replace_property_metadata(pull_request, package_manager, dependency_change, base_commit_sha)
          # Update the pull request property metadata with info about the updated dependencies.
          # This is used in `job.rb` to calculate "existing_pull_requests" in future jobs.
          pull_request_id = pull_request["pullRequestId"]
          ::Dependabot.logger.info("Setting Dependabot metadata properties for PR ##{pull_request_id}")
          job.azure_client.pull_request_properties_update(
            pull_request_id.to_s,
            {
              PullRequest::Properties::PACKAGE_MANAGER =>
                package_manager,
              PullRequest::Properties::BASE_COMMIT_SHA =>
                base_commit_sha.to_s,
              PullRequest::Properties::UPDATED_DEPENDENCIES =>
                pull_request_updated_dependencies_property_data(dependency_change).to_json
            }
          )
        end

        def pull_request_updated_dependencies_property_data(dependency_change)
          updated_dependencies = dependency_change.updated_dependencies.map do |dep|
            {
              "dependency-name" => dep.name,
              "dependency-version" => dep.version,
              "dependency-removed" => dep.removed? ? true : nil,
              "directory" => dep.directory
            }.compact
          end
          if dependency_change.grouped_update?
            {
              "dependency-group-name" => dependency_change.dependency_group.name,
              "dependencies" => updated_dependencies.compact
            }
          else
            updated_dependencies
          end
        end

        def pull_request_header_with_compatibility_scores(dependencies)
          return job.pr_message_header unless dependencies.any? && job.pr_compatibility_scores_badge

          # Compatibility score badges are intended for single dependency security updates, not group updates.
          # https://docs.github.com/en/github/managing-security-vulnerabilities/about-dependabot-security-updates#about-compatibility-scores
          # In group updates, the compatibility score is not very useful and can easily exceed the max message length,
          # so we don't show it.
          return job.pr_message_header if dependencies.length > 1

          compatibility_score_badges = dependencies.map do |dep|
            "[![Dependabot compatibility score](https://dependabot-badges.githubapp.com/badges/compatibility_score?" \
              "dependency-name=#{dep.name}&package-manager=#{job.package_manager}&" \
              "previous-version=#{dep.previous_version}&new-version=#{dep.version})]" \
              "(https://docs.github.com/en/github/managing-security-vulnerabilities/about-dependabot-security-updates#about-compatibility-scores)"
          end&.join(" ")

          ((job.pr_message_header || "") + "\n\n" + compatibility_score_badges).strip
        end
      end
    end
  end
end
