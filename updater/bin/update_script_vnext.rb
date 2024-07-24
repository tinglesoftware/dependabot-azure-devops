# typed: strict
# frozen_string_literal: true

$LOAD_PATH.unshift(__dir__ + "/../lib")

# Ensure logs are output immediately. Useful when running in certain hosts like ContainerGroups
$stdout.sync = true

require "tinglesoftware/dependabot/setup"
require "tinglesoftware/dependabot/job"
require "tinglesoftware/dependabot/commands/update_all_dependencies_synchronous_command"

require "tinglesoftware/azure/artifacts_credential_provider"

ENV["UPDATER_ONE_CONTAINER"] = "true" # The full end-to-end update will happen in a single container
ENV["UPDATER_DETERMINISTIC"] = "true" # The list of dependencies to update will be consistent across multiple runs

begin
  TingleSoftware::Dependabot::Commands::UpdateAllDependenciesSynchronousCommand.new(
    job: TingleSoftware::Dependabot::Job.new(
      # Override Dependabot updater options (feature flags) required by this job
      experiments: {
        # Required for correctly detecting existing PRs when refreshing group dependency updates.
        # Without this, Dependabot::DependencyGroup.matches_existing_pr? will always return false for group updates.
        "dependency_has_directory" => true
      }
    )
  ).run
rescue ::Dependabot::RunFailure
  exit 1
end
