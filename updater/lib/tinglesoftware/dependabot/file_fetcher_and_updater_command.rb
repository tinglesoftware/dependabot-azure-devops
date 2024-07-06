# typed: true
# frozen_string_literal: true

require "base64"
require "dependabot/base_command"
require "dependabot/dependency_snapshot"
require "dependabot/opentelemetry"
require "dependabot/updater"
require "octokit"

#
# This command attempts to combine the "FileFetcherCommand" and "UpdateFilesCommand" in to a single command.
#
# Normally Dependabot chunks the dependency update job over multiple commands (fetch-files, update-files).
# However, for Azure DevOps, we want to do everything in a single update job.
#
module TingleSoftware
  module Dependabot
    class FileFetcherAndUpdaterCommand < ::Dependabot::BaseCommand
      attr_reader :base_commit_sha

      attr_reader :job

      attr_reader :credentials

      def initialize(job:, credentials:)
        @api_client = TingleSoftware::Dependabot::EmptyApiClient.new
        @service = ::Dependabot::Service.new(client: api_client)
        @job = job
        @credentials = credentials
      end

      def perform_job # rubocop:disable Metrics/PerceivedComplexity,Metrics/AbcSize
        @base_commit_sha = nil

        begin
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
        rescue StandardError => e
          @base_commit_sha ||= "unknown"
          if Octokit::RATE_LIMITED_ERRORS.include?(e.class)
            remaining = rate_limit_error_remaining(e)
            ::Dependabot.logger.error("Repository is rate limited, attempting to retry in " \
                                      "#{remaining}s")
          else
            ::Dependabot.logger.error("Error during file fetching; aborting: #{e.message}")
          end
          handle_file_fetcher_error(e)
          service.mark_job_as_processed(@base_commit_sha)
          return
        end

        begin
          dependency_snapshot = ::Dependabot::DependencySnapshot.create_from_job_definition(
            job: job,
            job_definition: JSON.parse(
              JSON.dump(
                base64_dependency_files: base64_dependency_files.map(&:to_h),
                base_commit_sha: @base_commit_sha
              )
            )
          )
        rescue StandardError => e
          handle_parser_error(e)
          # If dependency file parsing has failed, there's nothing more we can do,
          # so let's mark the job as processed and stop.
          return service.mark_job_as_processed(base_commit_sha)
        end

        puts "Found #{dependency_snapshot.dependencies.count(&:top_level?)} dependencies"
        dependency_snapshot.dependencies.select(&:top_level?).each { |d| puts " - #{d.name} (#{d.version})" }
        puts "Found #{dependency_snapshot.groups.count} groups"
        dependency_snapshot.groups.select.each do |g|
          puts " - #{g.name}"
          g.dependencies.select(&:top_level?).each { |d| puts "   - #{d.name} (#{d.version})" }
        end
        puts "Found #{dependency_snapshot.ungrouped_dependencies.count(&:top_level?)} ungrouped dependencies"
        dependency_snapshot.ungrouped_dependencies.select(&:top_level?).each { |d| puts " - #{d.name} (#{d.version})" }

        # Update the service's metadata about this project
        service.update_dependency_list(dependency_snapshot: dependency_snapshot)

        # TODO: Pull fatal error handling handling up into this class
        #
        # As above, we can remove the responsibility for handling fatal/job halting
        # errors from Dependabot::Updater entirely.
        ::Dependabot::Updater.new(
          service: service,
          job: job,
          dependency_snapshot: dependency_snapshot
        ).run

        # Finally, mark the job as processed. The Dependabot::Updater may have
        # reported errors to the service, but we always consider the job as
        # successfully processed unless it actually raises.
        service.mark_job_as_processed(dependency_snapshot.base_commit_sha)
      end

      private

      # A method that abstracts the file fetcher creation logic and applies the same settings across all instances
      def create_file_fetcher(directory: nil)
        # Use the provided directory or fallback to job.source.directory if directory is nil.
        directory_to_use = directory || job.source.directory

        args = {
          source: job.source.clone.tap { |s| s.directory = directory_to_use },
          credentials: credentials,
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
          post_ecosystem_versions(ff) if should_record_ecosystem_versions?
          files
        end
      end

      def dependency_files
        return @dependency_files if defined?(@dependency_files)

        @dependency_files = with_retries { file_fetcher.files }
        post_ecosystem_versions(file_fetcher) if should_record_ecosystem_versions?
        @dependency_files
      end

      def should_record_ecosystem_versions?
        # We don't set this flag in GHES because there's no point in recording versions since we can't access that data.
        ::Dependabot::Experiments.enabled?(:record_ecosystem_versions)
      end

      def post_ecosystem_versions(file_fetcher)
        ecosystem_versions = file_fetcher.ecosystem_versions
        api_client.record_ecosystem_versions(ecosystem_versions) unless ecosystem_versions.nil?
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

      def handle_file_fetcher_error(error)
        error_details = ::Dependabot.fetcher_error_details(error)

        error_details ||= begin
          log_error(error)

          unknown_error_details = {
            "error-class" => error.class.to_s,
            "error-message" => error.message,
            "error-backtrace" => error.backtrace.join("\n"),
            "package-manager" => job.package_manager,
            "job-id" => job.id,
            "job-dependencies" => job.dependencies,
            "job-dependency_group" => job.dependency_groups
          }.compact

          service.capture_exception(error: error, job: job)
          {
            "error-type": "file_fetcher_error",
            "error-detail": unknown_error_details
          }
        end

        record_error(error_details)
      end

      # rubocop:disable Metrics/AbcSize
      def handle_parser_error(error)
        # This happens if the repo gets removed after a job gets kicked off.
        # The service will handle the removal without any prompt from the updater,
        # so no need to add an error to the errors array
        return if error.is_a? ::Dependabot::RepoNotFound

        error_details = ::Dependabot.parser_error_details(error)

        error_details ||=
          # Check if the error is a known "run halting" state we should handle
          if (error_type = Updater::ErrorHandler::RUN_HALTING_ERRORS[error.class])
            { "error-type": error_type }
          else
            # If it isn't, then log all the details and let the application error
            # tracker know about it
            ::Dependabot.logger.error error.message
            error.backtrace.each { |line| ::Dependabot.logger.error line }
            unknown_error_details = {
              "error-class" => error.class.to_s,
              "error-message" => error.message,
              "error-backtrace" => error.backtrace.join("\n"),
              "package-manager" => job.package_manager,
              "job-id" => job.id,
              "job-dependencies" => job.dependencies,
              "job-dependency_group" => job.dependency_groups
            }.compact

            service.capture_exception(error: error, job: job)

            # Set an unknown error type as update_files_error to be added to the job
            {
              "error-type": "update_files_error",
              "error-detail": unknown_error_details
            }
          end

        service.record_update_job_error(
          error_type: error_details.fetch(:"error-type"),
          error_details: error_details[:"error-detail"]
        )
        # We don't set this flag in GHES because there older GHES version does not support reporting unknown errors.
        return unless ::Dependabot::Experiments.enabled?(:record_update_job_unknown_error)
        return unless error_details.fetch(:"error-type") == "update_files_error"

        service.record_update_job_unknown_error(
          error_type: error_details.fetch(:"error-type"),
          error_details: error_details[:"error-detail"]
        )
      end
      # rubocop:enable Metrics/AbcSize

      def rate_limit_error_remaining(error)
        # Time at which the current rate limit window resets in UTC epoch secs.
        expires_at = error.response_headers["X-RateLimit-Reset"].to_i
        remaining = Time.at(expires_at) - Time.now
        remaining.positive? ? remaining : 0
      end

      def log_error(error)
        ::Dependabot.logger.error(error.message)
        error.backtrace.each { |line| ::Dependabot.logger.error line }
      end

      def record_error(error_details)
        service.record_update_job_error(
          error_type: error_details.fetch(:"error-type"),
          error_details: error_details[:"error-detail"]
        )

        # We don't set this flag in GHES because there older GHES version does not support reporting unknown errors.
        return unless ::Dependabot::Experiments.enabled?(:record_update_job_unknown_error)
        return unless error_details.fetch(:"error-type") == "file_fetcher_error"

        service.record_update_job_unknown_error(
          error_type: error_details.fetch(:"error-type"),
          error_details: error_details[:"error-detail"]
        )
      end
    end
  end
end
