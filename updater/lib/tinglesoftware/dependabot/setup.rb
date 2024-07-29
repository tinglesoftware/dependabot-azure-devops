# typed: strict
# frozen_string_literal: true

require "json"
require "logger"
require "sentry-ruby"

require "dependabot"
require "dependabot/logger"
require "dependabot/logger/formats"
require "dependabot/simple_instrumentor"
require "dependabot/opentelemetry"
require "dependabot/sentry"
require "dependabot/environment"

ENV["DEPENDABOT_JOB_ID"] = Time.now.to_i.to_s unless ENV["DEPENDABOT_JOB_ID"]

Dependabot.logger = Logger.new($stdout).tap do |logger|
  logger.level = ENV["DEPENDABOT_DEBUG"].to_s == "true" ? :debug : :info
  logger.formatter = Dependabot::Logger::BasicFormatter.new
end

# TODO: connect error handler to the job, operation, or service so that the errors can be reported to Sentry here
Sentry.init do |config|
  config.dsn = "https://0abd35cde5ca89c8dbcfb766a5f5bc50@o4507686465830912.ingest.us.sentry.io/4507686484508672"
  config.release = ENV.fetch("DEPENDABOT_UPDATER_VERSION", "unknown")
  config.logger = Dependabot.logger

  config.before_send = ->(event, hint) { Dependabot::Sentry.process_chain(event, hint) }
  config.propagate_traces = false
  config.instrumenter = ::Dependabot::OpenTelemetry.should_configure? ? :otel : :sentry
end

Dependabot::SimpleInstrumentor.subscribe do |*args|
  name = args.first
  payload = args.last
  if name == "excon.request" || name == "excon.response"
    error_codes = [400, 500].freeze
    puts "🌍 #{name == 'excon.response' ? "<-- #{payload[:status]}" : "--> #{payload[:method].upcase}"}" \
         " #{Excon::Utils.request_uri(payload)}"
    puts "🚨 #{payload[:body]}" if payload[:body] && error_codes.include?(payload[:status])
  end
end

Dependabot::OpenTelemetry.configure

# Ecosystems
require "dependabot/python"
require "dependabot/terraform"
require "dependabot/elm"
require "dependabot/docker"
require "dependabot/git_submodules"
require "dependabot/github_actions"
require "dependabot/composer"
require "dependabot/nuget"
require "dependabot/gradle"
require "dependabot/maven"
require "dependabot/hex"
require "dependabot/cargo"
require "dependabot/go_modules"
require "dependabot/npm_and_yarn"
require "dependabot/bundler"
require "dependabot/pub"
require "dependabot/swift"
require "dependabot/devcontainers"

# Overrides for dependabot core functionality that are currently not extensible
require "tinglesoftware/dependabot/overrides/nuget/nuget_config_credential_helpers"
require "tinglesoftware/dependabot/overrides/pull_request_creator/pr_name_prefixer"
