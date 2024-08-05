# typed: true
# frozen_string_literal: true

require "base64"
require "dependabot/base_command"
require "dependabot/dependency_snapshot"
require "dependabot/updater"
require "octokit"

require "tinglesoftware/dependabot/api_clients/azure_api_client"

#
# This command is a combination of "FileFetcherCommand" and "UpdateFilesCommand" from the dependabot-core updater.
# Normally Dependabot splits dependency updates up asynchronously over multiple smaller jobs and actions.
# e.g. fetch-files, update-dependency-snapshot, update-files, create-pull-request, etc
#
# In Azure DevOps, we want to do everything synchronously in a single job/command because we are expected to perform
# dependency updates in a self-contained environment without delegating or queuing follow-up actions to external APIs.
#
# This command will ensure the entire end-to-end update process is done synchronously.
#
module TingleSoftware
  module Dependabot
    module Commands
      class UpdateAllDependenciesSynchronousCommand < ::Dependabot::BaseCommand
        attr_reader :job

        # BaseCommand does not implement this method, so we should expose
        # the instance variable for error handling to avoid raising a
        # NotImplementedError if it is referenced
        attr_reader :base_commit_sha

        def initialize(job:)
          @job = job
          @service = ::Dependabot::Service.new(
            # Use the Azure DevOps API client rather than the (default) Dependabot Service API.
            # This allows us to perform pull request changes synchronously to within the context of this job.
            client: TingleSoftware::Dependabot::ApiClients::AzureApiClient.new(
              job: job,
              dependency_snapshot_resolver: proc { @dependency_snapshot }
            )
          )
        end

        def perform_job
          # Clone the repo contents then find all files that could contain dependency references
          clone_repo_and_snapshot_dependency_files
          log_what_we_found

          # Update/close any existing pull requests that are out of date or no longer required
          update_all_existing_pull_requests

          # Create new pull requests any dependency [groups] that still need updating (i.e. not in an open PR already)
          update_all_dependencies
        end

        private

        def log_what_we_found
          ::Dependabot.logger.info(
            "Repository scan completed for '#{job.source.url}' at commit '#{@base_commit_sha}'"
          )
          (job.source.directories || [job.source.directory]).each do |dir|
            dependency_snapshot.current_directory = dir
            log_snapshot_dependency_files(dir)
            log_snapshot_dependencies
            log_snapshot_dependency_groups
            dependency_snapshot.current_directory = ""
          end
          log_open_pull_requests
        end

        def log_snapshot_dependency_files(dir)
          ::Dependabot.logger.info(
            "Found directory '#{dir}' with #{dependency_snapshot.dependency_files.count} dependency files:"
          )
          dependency_snapshot.dependency_files.select.each do |f|
            ::Dependabot.logger.info(" - #{f.directory}#{f.name}")
          end
        end

        def log_snapshot_dependencies
          ::Dependabot.logger.info(
            "Found #{dependency_snapshot.dependencies.count(&:top_level?)} top-level dependencies:"
          )
          dependency_snapshot.dependencies.select(&:top_level?).each do |d|
            ::Dependabot.logger.info(" - #{d.name} (#{d.version}) #{job.vulnerable?(d) ? '(VULNERABLE!)' : ''}")
          end
          ::Dependabot.logger.info(
            "Found #{dependency_snapshot.dependencies.count { |d| !d.top_level? }} transitive dependencies:"
          )
          dependency_snapshot.dependencies.reject(&:top_level?).each do |d|
            ::Dependabot.logger.info(" - #{d.name} (#{d.version}) #{job.vulnerable?(d) ? '(VULNERABLE!)' : ''}")
          end
        end

        def log_snapshot_dependency_groups
          return unless dependency_snapshot.groups.any?

          ::Dependabot.logger.info(
            "Found #{dependency_snapshot.groups.count} dependency group(s):"
          )
          dependency_snapshot.groups.select.each do |g|
            ::Dependabot.logger.info(" - #{g.name}")
            g.dependencies.each { |d| ::Dependabot.logger.info("   - #{d.name} (#{d.version})") }
          end
        end

        def log_open_pull_requests
          return unless job.open_pull_requests.any?

          ::Dependabot.logger.info("Found #{job.open_pull_requests.count} open pull requests(s):")
          job.open_pull_requests.select.each do |pr|
            ::Dependabot.logger.info(" - ##{pr['pullRequestId']}: #{pr['title']}")
          end
        end

        def update_all_existing_pull_requests # rubocop:disable Metrics/PerceivedComplexity
          job.open_pull_requests.each do |pr|
            ::Dependabot.logger.info(
              "Checking if PR ##{pr['pullRequestId']}: #{pr['title']} needs to be updated"
            )

            deps = pr["updated_dependencies"]
            next if deps.nil? # Ignore PRs with no updated dependency info as we can't be sure what they are updating

            dependency_group_name = deps.is_a?(Hash) ? deps.fetch("dependency-group-name", nil) : nil
            dependency_names = (deps.is_a?(Array) ? deps : deps["dependencies"])&.map { |d| d["dependency-name"] } || []

            # Refocus our job towards updating this single PR, using the CURRENT snapshot of the dependecneis
            job.for_pull_request_update(
              dependency_group_name: dependency_group_name,
              dependency_names: dependency_snapshot.dependencies
                .select { |d| dependency_names.include?(d.name) }
                .select { |d| job.allowed_update?(d) }
                .map(&:name)
            )

            # Run the update on the PR using a clone our job with the OLD snapshot of the dependencies that existed
            # at the time the PR created. This is important for Dependabot to be able to determine if the PR is still
            # relevant or not.
            run_updates_for(
              job.clone.for_pull_request_update(
                dependency_group_name: dependency_group_name,
                dependency_names: dependency_names
              )
            )
          end
        end

        def update_all_dependencies
          ::Dependabot.logger.info("Checking if any dependencies need a new pull request created")
          run_updates_for(
            job.for_all_updates(
              dependency_names: job.security_updates_only? ? dependencies_allowed_to_update.map(&:name) : nil
            )
          )
        end

        def dependencies_allowed_to_update
          dependency_snapshot.dependencies.select { |d| job.allowed_update?(d) }
        end

        def run_updates_for(job)
          ::Dependabot::Updater.new(
            service: service,
            job: job,
            dependency_snapshot: dependency_snapshot
          ).run
        end

        def clone_repo_and_snapshot_dependency_files
          return unless job.clone?

          ::Dependabot.logger.info(
            "Cloning repository '#{file_fetcher.source.url}' to '#{file_fetcher.repo_contents_path}'"
          )

          # Clone the repo contents
          file_fetcher.clone_repo_contents
          @base_commit_sha = file_fetcher.commit
          raise "Base commit SHA not found for '#{file_fetcher.source.url}'" unless @base_commit_sha

          # Run dependency discovery
          dependency_snapshot.all_dependencies
        end

        def dependency_snapshot
          @dependency_snapshot ||= create_dependency_snapshot
        end

        def create_dependency_snapshot
          ::Dependabot::DependencySnapshot.create_from_job_definition(
            job: job,
            job_definition: {
              "base64_dependency_files" => base64_dependency_files.map(&:to_h),
              "base_commit_sha" => @base_commit_sha
            }
          )
        end

        def file_fetcher
          @file_fetcher ||= create_file_fetcher
        end

        # This method is responsible for creating or retrieving a file fetcher
        # from a cache (@file_fetchers) for the given directory.
        def file_fetcher_for_directory(directory)
          @file_fetchers ||= {}
          @file_fetchers[directory] ||= create_file_fetcher(directory: directory)
        end

        # A method that abstracts the file fetcher creation logic and applies the same settings across all instances
        def create_file_fetcher(directory: nil)
          # Use the provided directory or fallback to job.source.directory if directory is nil.
          directory_to_use = directory || job.source.directory
          args = {
            source: job.source.clone.tap { |s| s.directory = directory_to_use },
            credentials: job.credentials,
            repo_contents_path: job.repo_contents_path,
            options: job.experiments
          }

          ::Dependabot::FileFetchers.for_package_manager(job.package_manager).new(**args)
        end

        def dependency_files_for_multi_directories
          return @dependency_files_for_multi_directories if defined?(@dependency_files_for_multi_directories)

          has_glob = T.let(false, T::Boolean)
          directories = Dir.chdir(job.repo_contents_path) do
            job.source.directories.map do |dir|
              next dir unless glob?(dir)

              has_glob = true
              dir = dir.delete_prefix("/")
              Dir.glob(dir, File::FNM_DOTMATCH).select { |d| File.directory?(d) }.map { |d| "/#{d}" }
            end.flatten
          end.uniq

          @dependency_files_for_multi_directories = directories.flat_map do |dir|
            ff = with_retries { file_fetcher_for_directory(dir) }

            begin
              files = ff.files
            rescue Dependabot::DependencyFileNotFound
              # skip directories that don't contain manifests if globbing is used
              next if has_glob

              raise
            end

            files
          end.compact

          if @dependency_files_for_multi_directories.empty?
            raise Dependabot::DependencyFileNotFound, job.source.directories.join(", ")
          end

          @dependency_files_for_multi_directories
        end

        def dependency_files
          return @dependency_files if defined?(@dependency_files)

          @dependency_files = with_retries { file_fetcher.files }
          @dependency_files
        end

        def base64_dependency_files
          files = job.source.directories ? dependency_files_for_multi_directories : dependency_files
          files.map do |file|
            base64_file = file.dup
            base64_file.content = Base64.encode64(file.content) unless file.binary?
            base64_file
          end
        end

        def with_retries(max_retries: 2)
          retries ||= 0
          begin
            yield
          rescue Octokit::BadGateway
            retries += 1
            retry if retries <= max_retries
            raise
          end
        end

        def glob?(directory)
          # We could tighten this up, but it's probably close enough.
          directory.include?("*") || directory.include?("?") || (directory.include?("[") && directory.include?("]"))
        end
      end
    end
  end
end
