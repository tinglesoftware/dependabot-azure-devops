# typed: strict
# frozen_string_literal: true

$LOAD_PATH.unshift(__dir__ + "/../lib")

# Ensure logs are output immediately. Useful when running in certain hosts like ContainerGroups
$stdout.sync = true

require "tinglesoftware/dependabot/setup"
require "tinglesoftware/dependabot/job"
require "tinglesoftware/dependabot/commands/update_all_dependencies_command"

begin
  TingleSoftware::Dependabot::Commands::UpdateAllDependenciesCommand.new(
    job: TingleSoftware::Dependabot::Job.new
  ).run
rescue ::Dependabot::RunFailure
  exit 1
end