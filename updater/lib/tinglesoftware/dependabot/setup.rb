# typed: strict
# frozen_string_literal: true

require "json"
require "logger"

require "dependabot"
require "dependabot/logger"
require "dependabot/logger/formats"
require "dependabot/simple_instrumentor"
require "dependabot/environment"

ENV["DEPENDABOT_JOB_ID"] = Time.now.to_i.to_s unless ENV["DEPENDABOT_JOB_ID"]

Dependabot.logger = Logger.new($stdout).tap do |logger|
  logger.level = ENV["DEPENDABOT_DEBUG"].to_s == "true" ? :debug : :info
  logger.formatter = Dependabot::Logger::BasicFormatter.new
end

Dependabot::SimpleInstrumentor.subscribe do |*args|
  name = args.first
  payload = args.last
  if name == "excon.request" || name == "excon.response"
    puts "🌍 #{name == 'excon.response' ? "<-- #{payload[:status]}" : "--> #{payload[:method].upcase}"}" \
         " #{Excon::Utils.request_uri(payload)}"
  end
end

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
#require "dependabot/silent"
require "dependabot/swift"
require "dependabot/devcontainers"
