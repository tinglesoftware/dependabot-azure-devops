# typed: strict
# frozen_string_literal: true

require "dependabot/api_client"
require "dependabot/pull_request_creator"
require "dependabot/pull_request_updater"
require "dependabot/credential"

#
# Custom API client that bridges the internal Dependabot Service API to Azure DevOps.
#
# This API is normally only available to Dependabot jobs being executed within the official
# hosted infrastructure and is not available to external users.
#
module TingleSoftware
  module Dependabot
    module ApiClients
      class AzureApiClient < ::Dependabot::ApiClient
        extend T::Sig
        attr_reader :job

        def initialize(job:)
          @job = job
        end

        sig { params(dependency_change: ::Dependabot::DependencyChange, base_commit_sha: String).void }
        def create_pull_request(dependency_change, base_commit_sha)
          conflict_pull_request_commit = nil
          conflict_pull_request_id = nil
          existing_pull_request = nil
          job.existing_pull_requests.each do |pr|
            pr_id = pr["id"]
            title = pr["title"]
            source_ref_name = pr["source-ref-name"]
            dep = dependency_change.updated_dependencies.first

            # Filter those containing "#{dep.display_name} from #{dep.version}"
            # The format avoids taking PRS for dependencies named in a similar manner.
            # For instance 'Tingle.EventBus' and 'Tingle.EventBus.Transports.Azure.ServiceBus'
            #
            # display_name is used instead of name because some titles do not have the full dependency name.
            # For instance 'org.junit.jupiter:junit-jupiter' will only read 'junit-jupiter' in the title.
            #
            # Sample Titles:
            # Bump Tingle.Extensions.Logging.LogAnalytics from 3.4.2-ci0005 to 3.4.2-ci0006
            # Bump Tingle.EventBus from 0.4.2-ci0005 to 0.4.2-ci0006
            # Bump Tingle.EventBus.Transports.Azure.ServiceBus from 0.4.2-ci0005 to 0.4.2-ci0006
            # chore(deps): bump dotenv from 9.0.1 to 9.0.2 in /server
            next unless title.include?(" #{dep.display_name} from #{dep.version} to ")

            # In case the Pull Request title contains an explicit path, check that path
            # to make sure it is the same Pull Request. For example:
            # 'Bump hashicorp/azurerm from 3.1.0 to 3.12.3 in /projectA' denotes a different
            # Pull Request from 'Bump hashicorp/azurerm from 3.1.0 to 3.12.3 in /projectB'
            next if updated_files.first.directory != "/" && !title.end_with?(" in #{updated_files.first.directory}")

            # If the title does not contain the updated version, we need to abandon the PR and delete
            # it's branch, because there is a newer version available.
            # Using the format " to #{updated_deps[0].version}" handles both root and nested updates.
            # For example:
            # Bump Tingle.EventBus from 0.4.2-ci0005 to 0.4.2-ci0006
            # chore(deps): bump dotenv from 9.0.1 to 9.0.2 in /server
            unless title.include?(" to #{updated_deps[0].version}")
              # Abandon old version PR
              job.azure_client.branch_delete(source_ref_name) # do this first to avoid hanging branches
              job.azure_client.pull_request_abandon(pr_id)
              puts "Abandoned Pull Request ##{pr_id}"
              next
            end

            existing_pull_request = pr

            # If the merge status of the current PR is not succeeded,
            # we need to resolve the merge conflicts
            next unless pr["mergeStatus"] != "succeeded"

            # ignore pull request manually edited
            next if job.azure_client.pull_request_commits(pr_id).length > 1

            # keep pull request for updating later
            conflict_pull_request_commit = pr["lastMergeSourceCommit"]["commitId"]
            conflict_pull_request_id = pr_id
            break
          end

          pull_request_id = nil
          created_or_updated = false
          if conflict_pull_request_commit && conflict_pull_request_id

            ##############################################
            # Update pull request with conflict resolved #
            ##############################################
            pr_updater = ::Dependabot::PullRequestUpdater.new(
              source: $source,
              base_commit: commit,
              old_commit: conflict_pull_request_commit,
              files: updated_files,
              credentials: $options[:credentials],
              pull_request_number: conflict_pull_request_id,
              author_details: $options[:author_details]
            )

            puts "Submitting pull request (##{conflict_pull_request_id}) update for #{dep.name}."
            pr_updater.update
            pull_request = existing_pull_request
            pull_request_id = conflict_pull_request_id

            created_or_updated = true
          elsif !existing_pull_request # Only create PR if there is none existing

            ########################################
            # Create a pull request for the update #
            ########################################
            pr_creator = ::Dependabot::PullRequestCreator.new(
              source: job.source,
              base_commit: base_commit_sha,
              dependencies: dependency_change.updated_dependencies,
              files: dependency_change.updated_dependency_files,
              credentials: job.credentials,
              author_details: {
                email: ENV["DEPENDABOT_AUTHOR_EMAIL"] || "noreply@github.com",
                name: ENV["DEPENDABOT_AUTHOR_NAME"] || "dependabot[bot]"
              },
              commit_message_options: job.commit_message_options,
              custom_labels: [],
              reviewers: [],
              assignees: [],
              milestone: [],
              branch_name_separator: "/",
              label_language: true,
              automerge_candidate: true,
              github_redirection_service: ::Dependabot::PullRequestCreator::DEFAULT_GITHUB_REDIRECTION_SERVICE,
              provider_metadata: {
                #work_item: $options[:milestone]
              },
              message: dependency_change.pr_message
            )

            puts "Submitting '#{dependency_change.pr_message.pr_name}' pull request for creation."
            pull_request = pr_creator.create

            if pull_request
              req_status = pull_request&.status
              if req_status == 201
                pull_request = JSON[pull_request.body]
                pull_request_id = pull_request["pullRequestId"]
                puts "Created pull request for '#{dependency_change.pr_message.pr_name}'(##{pull_request_id})."
              else
                content = JSON[pull_request.body]
                message = content["message"]
                puts "Failed! PR already exists or an error has occurred."
                # throw exception here because pull_request.create does not throw
                raise StandardError, "Pull Request creation failed with status #{req_status}. Message: #{message}"
              end
            else
              puts "Seems PR is already present."
            end

            created_or_updated = true
          else
            pull_request = existing_pull_request # One already existed
            pull_request_id = pull_request["pullRequestId"]
            puts "Pull request for #{dep.version} already exists (##{pull_request_id}) and does not need updating."
          end
        end

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
