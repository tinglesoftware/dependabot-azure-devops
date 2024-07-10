# typed: true
# frozen_string_literal: true

require "base64"
require "dependabot/base_command"
require "dependabot/dependency_snapshot"
require "dependabot/updater"
require "octokit"

#
# This command combine the dependabot-core "FileFetcherCommand" and "UpdateFilesCommand" in to a single command
# that can be completed end-to-end in a single self-contained job/script, rather than over multiple jobs/scripts.
#
# Normally Dependabot splits the dependency update process over multiple small job/commands (e.g. fetch-files,
# update-files, create-pull-request). For Azure DevOps, we want to do everything in a single job.
#
module TingleSoftware
  module Dependabot
    module Commands
      class UpdateAllDependenciesCommand < ::Dependabot::BaseCommand
        attr_reader :job

        def initialize(job:)
          @job = job
          @service = ::Dependabot::Service.new(
            # Use our custom API client rather than the internal Dependabot Service API.
            # This allows us to perform pull request changes synchronously within the context of this job.
            client: TingleSoftware::Dependabot::ApiClients::AzureApiClient.new(job: job)
          )
        end

        def perform_job # rubocop:disable Metrics/AbcSize
          @base_commit_sha = nil

          # Clone the repo contents then find all files that could contain dependency references
          perform_file_fetch_and_find_dependency_files

          # Parse the dependency files and extract the full list of dependencies that need updating
          # Output information about all the dependencies we found, for diagnostic purposes
          ::Dependabot.logger.info("Found #{dependency_files.count} dependencies files:")
          dependency_files.select.each { |f| ::Dependabot.logger.info(" - #{f.directory}#{f.name}") }
          ::Dependabot.logger.info("Found #{dependency_snapshot.dependencies.count(&:top_level?)} dependencies:")
          dependency_snapshot.dependencies.select(&:top_level?).each { |d| ::Dependabot.logger.info(" - #{d.name} (#{d.version})") }
          ::Dependabot.logger.info("Found #{dependency_snapshot.groups.count} dependency group(s):")
          dependency_snapshot.groups.select.each do |g|
            ::Dependabot.logger.info(" - #{g.name}")
            g.dependencies.select(&:top_level?).each { |d| ::Dependabot.logger.info("   - #{d.name} (#{d.version})") }
          end

          # Perform the dependency update
          perform_dependency_update
        end

        private

        # Logic copied from `updater/lib/dependabot/file_fetcher_command.rb`` (perform_job)
        def perform_file_fetch_and_find_dependency_files
          clone_repo_contents
          @base_commit_sha = file_fetcher.commit
          raise "base commit SHA not found" unless @base_commit_sha

          # In the older versions of GHES (> 3.11.0) job.source.directories will be nil as source.directories was
          # introduced after 3.11.0 release. So, this also supports backward compatibility for older versions of GHES.
          if job.source.directories
            dependency_files_for_multi_directories
          else
            dependency_files
          end
        end

        def dependency_snapshot
          @dependency_snapshot ||= perform_dependency_snapshot
        end

        # Logic copied from `updater/lib/dependabot/update_files_command.rb` (perform_job)
        def perform_dependency_snapshot
          ::Dependabot::DependencySnapshot.create_from_job_definition(
            job: job,
            job_definition: {
              "base64_dependency_files" => base64_dependency_files.map(&:to_h),
              "base_commit_sha" => @base_commit_sha
            }
          )
        end

        # Logic copied from updater/lib/dependabot/update_files_command.rb (perform_job)
        def perform_dependency_update
          ::Dependabot::Updater.new(
            service: service,
            job: job,
            dependency_snapshot: dependency_snapshot
          ).run
        end

        # =============================================================================================================
        # The below was copied from lib/dependabot/file_fetcher_command.rb and lib/dependabot/update_files_command.rb
        # We want our update logic to match the dependabot-core logic as close as possible, so don't modify this code.
        # =============================================================================================================

        # A method that abstracts the file fetcher creation logic and applies the same settings across all instances
        def create_file_fetcher(directory: nil)
          # Use the provided directory or fallback to job.source.directory if directory is nil.
          directory_to_use = directory || job.source.directory

          args = {
            source: job.source.clone.tap { |s| s.directory = directory_to_use },
            credentials: job.credentials,
            options: job.experiments
          }

          args[:repo_contents_path] = ::Dependabot::Environment.repo_contents_path if job.clone? || already_cloned?
          ::Dependabot::FileFetchers.for_package_manager(job.package_manager).new(**args)
        end

        # The main file fetcher method that now calls the create_file_fetcher method
        # and ensures it uses the same repo_contents_path setting as others.
        def file_fetcher
          @file_fetcher ||= create_file_fetcher
        end

        # This method is responsible for creating or retrieving a file fetcher
        # from a cache (@file_fetchers) for the given directory.
        def file_fetcher_for_directory(directory)
          @file_fetchers ||= {}
          @file_fetchers[directory] ||= create_file_fetcher(directory: directory)
        end

        # Fetch dependency files for multiple directories
        def dependency_files_for_multi_directories
          @dependency_files_for_multi_directories ||= job.source.directories.flat_map do |dir|
            ff = with_retries { file_fetcher_for_directory(dir) }
            files = ff.files
            files
          end
        end

        def dependency_files
          return @dependency_files if defined?(@dependency_files)

          @dependency_files = with_retries { file_fetcher.files }
          @dependency_files
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

        def clone_repo_contents
          return unless job.clone?

          file_fetcher.clone_repo_contents
        end

        def base64_dependency_files
          files = job.source.directories ? dependency_files_for_multi_directories : dependency_files
          files.map do |file|
            base64_file = file.dup
            base64_file.content = Base64.encode64(file.content) unless file.binary?
            base64_file
          end
        end

        def already_cloned?
          return false unless ::Dependabot::Environment.repo_contents_path

          # For testing, the source repo may already be mounted.
          @already_cloned ||= File.directory?(File.join(Environment.repo_contents_path, ".git"))
        end
      end
    end
  end
end
