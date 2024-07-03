# typed: strict
# frozen_string_literal: true

$LOAD_PATH.unshift(__dir__ + "/../lib")

# ensure logs are output immediately. Useful when running in certain hosts like ContainerGroups
$stdout.sync = true

require "json"
require "logger"
require "debug" if ENV["DEBUG"]

require "dependabot/logger"
require "dependabot/api_client"
require "dependabot/service"
require "dependabot/job"
require "dependabot/updater"
require "dependabot/base_command"

require "dependabot/bundler"
require "dependabot/cargo"
require "dependabot/composer"
require "dependabot/docker"
require "dependabot/elm"
require "dependabot/git_submodules"
require "dependabot/github_actions"
require "dependabot/go_modules"
require "dependabot/gradle"
require "dependabot/hex"
require "dependabot/maven"
require "dependabot/npm_and_yarn"
require "dependabot/nuget"
require "dependabot/python"
require "dependabot/pub"
require "dependabot/swift"
require "dependabot/devcontainers"
require "dependabot/terraform"

require "tinglesoftware/dependabot/clients/azure"
require "tinglesoftware/dependabot/empty_api_client"
require "tinglesoftware/dependabot/file_fetcher_and_updater_command"
require "tinglesoftware/dependabot/vulnerabilities"

Dependabot.logger = Logger.new($stdout)

Dependabot::SimpleInstrumentor.subscribe do |*args|
  name = args.first

  payload = args.last
  if name == "excon.request" || name == "excon.response"
    puts "🌍 #{name == 'excon.response' ? "<-- #{payload[:status]}" : "--> #{payload[:method].upcase}"}" \
         " #{Excon::Utils.request_uri(payload)}"
  end
end

unless ENV["DEPENDABOT_JOB_ID"].to_i.nonzero?
  ENV["DEPENDABOT_JOB_ID"] = Time.now.to_i.to_s
end

unless ENV["DEPENDABOT_JOB_PATH"].to_s.strip.empty?
  ENV["DEPENDABOT_JOB_PATH"] = "/tmp/dependabot-job-#{ENV.fetch('DEPENDABOT_JOB_ID')}"
end

$options = {
  credentials: [],
  provider: "azure",

  directory: ENV["DEPENDABOT_DIRECTORY"] || "/", # Directory where the base dependency files are.
  branch: ENV["DEPENDABOT_TARGET_BRANCH"] || nil, # Branch against which to create PRs

  allow_conditions: [],
  reject_external_code: ENV["DEPENDABOT_REJECT_EXTERNAL_CODE"] == "true",
  requirements_update_strategy: nil,
  security_advisories: [],
  security_updates_only: false,
  ignore_conditions: [],
  pull_requests_limit: ENV["DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT"].to_i || 5,
  custom_labels: nil, # nil instead of empty array to ensure default labels are passed
  reviewers: nil, # nil instead of empty array to avoid API rejection
  assignees: nil, # nil instead of empty array to avoid API rejection
  branch_name_separator: ENV["DEPENDABOT_BRANCH_NAME_SEPARATOR"] || "/", # Separator used for created branches.
  milestone: ENV["DEPENDABOT_MILESTONE"].to_i || nil, # Get the work item to attach
  vendor_dependencies: ENV["DEPENDABOT_VENDOR"] == "true",
  repo_contents_path: ENV["DEPENDABOT_REPO_CONTENTS_PATH"] || nil,
  updater_options: {},
  author_details: {
    email: ENV["DEPENDABOT_AUTHOR_EMAIL"] || "noreply@github.com",
    name: ENV["DEPENDABOT_AUTHOR_NAME"] || "dependabot[bot]"
  },
  fail_on_exception: ENV["DEPENDABOT_FAIL_ON_EXCEPTION"] == "true", # Stop the job if an exception occurs
  skip_pull_requests: ENV["DEPENDABOT_SKIP_PULL_REQUESTS"] == "true", # Skip creating/updating Pull Requests
  close_unwanted: ENV["DEPENDABOT_CLOSE_PULL_REQUESTS"] == "true", # Close unwanted Pull Requests

  # See description of requirements here:
  # https://github.com/dependabot/dependabot-core/issues/600#issuecomment-407808103
  # https://github.com/wemake-services/kira-dependencies/pull/210
  excluded_requirements: ENV["DEPENDABOT_EXCLUDE_REQUIREMENTS_TO_UNLOCK"]&.split&.map(&:to_sym) || [],

  # Details on the location of the repository
  azure_organization: ENV.fetch("AZURE_ORGANIZATION", nil),
  azure_project: ENV.fetch("AZURE_PROJECT", nil),
  azure_repository: ENV.fetch("AZURE_REPOSITORY", nil),
  azure_hostname: ENV["AZURE_HOSTNAME"] || "dev.azure.com",
  azure_protocol: ENV["AZURE_PROTOCOL"] || "https",
  azure_port: nil,
  azure_virtual_directory: ENV["AZURE_VIRTUAL_DIRECTORY"] || "",

  # Automatic completion
  set_auto_complete: ENV["AZURE_SET_AUTO_COMPLETE"] == "true", # Set auto complete on created pull requests
  auto_complete_ignore_config_ids: JSON.parse(ENV["AZURE_AUTO_COMPLETE_IGNORE_CONFIG_IDS"] || "[]"), # default to empty
  merge_strategy: ENV["AZURE_MERGE_STRATEGY"] || "squash", # default to squash
  trans_work_items: false,

  # Automatic Approval
  auto_approve_pr: ENV["AZURE_AUTO_APPROVE_PR"] == "true",
  auto_approve_user_token: ENV["AZURE_AUTO_APPROVE_USER_TOKEN"] || ENV.fetch("AZURE_ACCESS_TOKEN", nil)
}

# Name of the package manager you'd like to do the update for. Options are:
# - bundler
# - pip (includes pipenv)
# - npm_and_yarn
# - maven
# - gradle
# - cargo
# - hex
# - composer
# - nuget
# - dep
# - go_modules
# - elm
# - submodules
# - docker
# - terraform
$package_manager = ENV["DEPENDABOT_PACKAGE_MANAGER"] || "bundler"

# GitHub native implementation modifies some of the names in the config file
# https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#package-ecosystem
# [Hash<String, String>]
PACKAGE_ECOSYSTEM_MAPPING = {
  "github-actions" => "github_actions",
  "gitsubmodule" => "submodules",
  "gomod" => "go_modules",
  "mix" => "hex",
  "npm" => "npm_and_yarn",
  # Additional ones
  "yarn" => "npm_and_yarn",
  "pipenv" => "pip",
  "pip-compile" => "pip",
  "poetry" => "pip"
}.freeze
$package_manager = PACKAGE_ECOSYSTEM_MAPPING.fetch($package_manager, $package_manager)

#########################################################
# Setup credentials for source code,                    #
# Add GitHub Access Token (PAT) to avoid rate limiting, #
# Setup extra credentials                               #
########################################################
$options[:credentials] << Dependabot::Credential.new({
  "type" => "git_source",
  "host" => $options[:azure_hostname],
  "username" => ENV["AZURE_ACCESS_USERNAME"] || "x-access-token",
  "password" => ENV.fetch("AZURE_ACCESS_TOKEN", nil)
})

$vulnerabilities_fetcher = nil
unless ENV["GITHUB_ACCESS_TOKEN"].to_s.strip.empty?
  puts "GitHub access token has been provided."
  github_token = ENV.fetch("GITHUB_ACCESS_TOKEN", nil) # A GitHub access token with read access to public repos
  $options[:credentials] << Dependabot::Credential.new({
    "type" => "git_source",
    "host" => "github.com",
    "username" => "x-access-token",
    "password" => github_token
  })
  $vulnerabilities_fetcher =
    Dependabot::Vulnerabilities::Fetcher.new($package_manager, github_token)
end
# DEPENDABOT_EXTRA_CREDENTIALS, for example:
# "[{\"type\":\"npm_registry\",\"registry\":\"registry.npmjs.org\",\"token\":\"123\"}]"
unless ENV["DEPENDABOT_EXTRA_CREDENTIALS"].to_s.strip.empty?
  $options[:credentials].concat(
    JSON.parse(ENV.fetch("DEPENDABOT_EXTRA_CREDENTIALS", nil)).map do |cred|
      Dependabot::Credential.new(cred)
    end
  )
end

##########################################
# Setup the requirements update strategy #
##########################################
# GitHub native implementation modifies some of the names in the config file
unless ENV["DEPENDABOT_VERSIONING_STRATEGY"].to_s.strip.empty?
  # [Hash<String, Symbol>]
  VERSIONING_STRATEGIES = {
    "lockfile-only" => RequirementsUpdateStrategy::LockfileOnly,
    "widen" => RequirementsUpdateStrategy::WidenRanges,
    "increase" => RequirementsUpdateStrategy::BumpVersions,
    "increase-if-necessary" => RequirementsUpdateStrategy::BumpVersionsIfNecessary
  }.freeze
  strategy_raw = ENV.fetch("DEPENDABOT_VERSIONING_STRATEGY", nil)
  $options[:requirements_update_strategy] = case strategy_raw
                                            when nil then nil
                                            when "auto" then nil
                                            else VERSIONING_STRATEGIES.fetch(strategy_raw)
                                            end

  # For npm_and_yarn & composer, we must correct the strategy to one allowed
  # https://github.com/dependabot/dependabot-core/blob/5ec858331d11253a30aa15fab25ae22fbdecdee0/npm_and_yarn/lib/dependabot/npm_and_yarn/update_checker/requirements_updater.rb#L18-L19
  # https://github.com/dependabot/dependabot-core/blob/5926b243b2875ad0d8c0a52c09210c4f5f274c5e/composer/lib/dependabot/composer/update_checker/requirements_updater.rb#L23-L24
  if $package_manager == "npm_and_yarn" || $package_manager == "composer"
    strategy = $options[:requirements_update_strategy]
    if strategy.nil? || strategy == RequirementsUpdateStrategy::LockfileOnly
      $options[:requirements_update_strategy] = RequirementsUpdateStrategy::BumpVersions
    end
  end

  # For pub, we also correct the strategy
  # https://github.com/dependabot/dependabot-core/blob/ca9f236591ba49fa6e2a8d5f06e538614033a628/pub/lib/dependabot/pub/update_checker.rb#L110
  if $package_manager == "pub"
    strategy = $options[:requirements_update_strategy]
    if strategy == RequirementsUpdateStrategy::LockfileOnly
      $options[:requirements_update_strategy] = RequirementsUpdateStrategy::BumpVersions
    end
  end
end

#################################################################
#                     Setup Allow conditions                    #
# DEPENDABOT_ALLOW_CONDITIONS Example:
# [{"dependency-name":"sphinx","dependency-type":"production"}]
#################################################################
unless ENV["DEPENDABOT_ALLOW_CONDITIONS"].to_s.strip.empty?
  $options[:allow_conditions] = JSON.parse(ENV.fetch("DEPENDABOT_ALLOW_CONDITIONS", nil))
end
unless $options[:allow_conditions].count.nonzero?
  $options[:allow_conditions] = [{
    "dependency-type" => "all"
  }]
end

# Get allow versions for a dependency
# [Hash<String, Proc>] handlers for type allow rules
TYPE_HANDLERS = {
  "all" => proc { true },
  "direct" => proc(&:top_level?),
  "indirect" => proc { |dep| !dep.top_level? },
  "production" => proc(&:production?),
  "development" => proc { |dep| !dep.production? },
  "security" => proc { |_, checker| checker.vulnerable? }
}.freeze

#################################################################
#                   Setup Security Advisories                   #
# File contents example:
# [{"dependency-name":"name","patched-versions":[],"unaffected-versions":[],"affected-versions":["< 0.10.0"]}]
#################################################################
unless ENV["DEPENDABOT_SECURITY_ADVISORIES_FILE"].to_s.strip.empty?
  security_advisories_file_name = ENV.fetch("DEPENDABOT_SECURITY_ADVISORIES_FILE", nil)
  if File.exist?(security_advisories_file_name)
    $options[:security_advisories] += JSON.parse(File.read(security_advisories_file_name))
  end
end

##################################################################################################
#                                     Setup Ignore conditions                                   #
# DEPENDABOT_IGNORE_CONDITIONS Example: [{"dependency-name":"ruby","versions":[">= 3.a", "< 4"]}]
##################################################################################################
ignores = JSON.parse(ENV.fetch("DEPENDABOT_IGNORE_CONDITIONS", "[]"), symbolize_names: true)
$options[:ignore_conditions] = ignores.map do |ic|
  Dependabot::Config::IgnoreCondition.new(
    dependency_name: ic[:"dependency-name"],
    versions: ic[:versions],
    update_types: ic[:"update-types"]
  )
end

##################################################################################################
#                                   Setup Commit Message Options                                 #
# DEPENDABOT_COMMIT_MESSAGE_OPTIONS Example: {"prefix":"(dependabot)"}
##################################################################################################
commit_message = JSON.parse(ENV.fetch("DEPENDABOT_COMMIT_MESSAGE_OPTIONS", "{}"), symbolize_names: true)
$options[:commit_message_options] = Dependabot::Config::UpdateConfig::CommitMessageOptions.new(
  prefix: commit_message[:prefix],
  prefix_development: commit_message[:"prefix-development"] || commit_message[:prefix],
  include: commit_message[:include]
)

#################################################################
#                        Setup Labels                           #
# DEPENDABOT_LABELS Example: ["npm dependencies","triage-board"]
#################################################################
unless ENV["DEPENDABOT_LABELS"].to_s.strip.empty?
  $options[:custom_labels] = JSON.parse(ENV.fetch("DEPENDABOT_LABELS", nil))
end

#########################################################################
#                         Setup Reviewers                               #
# DEPENDABOT_REVIEWERS Example: ["be9321e2-f404-4ffa-8d6b-44efddb04865"]
#########################################################################
unless ENV["DEPENDABOT_REVIEWERS"].to_s.strip.empty?
  $options[:reviewers] = JSON.parse(ENV.fetch("DEPENDABOT_REVIEWERS", nil))
end

#########################################################################
#                           Setup Assignees                             #
# DEPENDABOT_ASSIGNEES Example: ["be9321e2-f404-4ffa-8d6b-44efddb04865"]
#########################################################################
unless ENV["DEPENDABOT_ASSIGNEES"].to_s.strip.empty?
  $options[:assignees] = JSON.parse(ENV.fetch("DEPENDABOT_ASSIGNEES", nil))
end


# Parse the options e.g. goprivate=true,kubernetes_updates=true
$options[:updater_options] = (ENV["DEPENDABOT_UPDATER_OPTIONS"] || "").split(",").to_h do |o|
  if o.include?("=") # key/value pair, e.g. goprivate=true
    o.split("=", 2).map.with_index do |v, i|
      if i.zero?
        v.strip.downcase.to_sym
      else
        v.strip
      end
    end
  else # just a key, e.g. "vendor"
    [o.strip.downcase.to_sym, true]
  end
end

# Register the options as experiments e.g. kubernetes_updates=true
$options[:updater_options].each do |name, val|
  puts "Registering experiment '#{name}=#{val}'"
  Dependabot::Experiments.register(name, val)
end

# Enable security only updates if not enabled and limits is zero
if !$options[:security_updates_only] && ($options[:pull_requests_limit]).zero?
  puts "Pull requests limit is set to zero. Security only updates are implied."
  $options[:security_updates_only] = true
end

# Security updates cannot be performed without GitHub token
if $options[:security_updates_only] && $vulnerabilities_fetcher.nil?
  raise StandardError, "Security updates are enabled but a GitHub token is not supplied! Cannot proceed"
end

####################################################
# Setup the hostname, protocol and port to be used #
####################################################
$options[:azure_port] = ENV["AZURE_PORT"] || ($options[:azure_protocol] == "http" ? "80" : "443")
$api_endpoint = "#{$options[:azure_protocol]}://#{$options[:azure_hostname]}:#{$options[:azure_port]}/"
unless $options[:azure_virtual_directory].empty?
  $api_endpoint = $api_endpoint + "#{$options[:azure_virtual_directory]}/"
end
# Full name of the repo targeted.
$repo_name = "#{$options[:azure_organization]}/#{$options[:azure_project]}/_git/#{$options[:azure_repository]}"
puts "Using '#{$api_endpoint}' as API endpoint"
puts "Pull Requests shall be linked to milestone (work item) #{$options[:milestone]}" if $options[:milestone]
puts "Pull Requests shall be labeled #{$options[:custom_labels]}" if $options[:custom_labels]
puts "Working in #{$repo_name}, '#{$options[:branch] || 'default'}' branch under '#{$options[:directory]}' directory"

## Create the update configuration (we no longer parse the file because of BOM and type issues)
$update_config = Dependabot::Config::UpdateConfig.new(
  ignore_conditions: $options[:ignore_conditions],
  commit_message_options: $options[:commit_message_options]
)

if $options[:requirements_update_strategy]
  puts "Using '#{$options[:requirements_update_strategy]}' requirements update strategy"
end

$source = Dependabot::Source.new(
  provider: $options[:provider],
  hostname: $options[:azure_hostname],
  api_endpoint: $api_endpoint,
  repo: $repo_name,
  directory: $options[:directory],
  branch: $options[:branch]
)

################################################
# Get active pull requests for this repository #
################################################
azure_client = Dependabot::Clients::Azure.for_source(
  source: $source,
  credentials: $options[:credentials]
)
user_id = azure_client.get_user_id
target_branch_name = $options[:branch] || azure_client.fetch_default_branch($source.repo)
active_pull_requests = azure_client.pull_requests_active(user_id, target_branch_name)

job = Dependabot::Job.new(
  id: "1",
  token: "token",
  dependencies: nil,
  allowed_updates: $options[:allow_conditions],
  existing_pull_requests: active_pull_requests,
  existing_group_pull_requests: [],
  ignore_conditions: ignores,
  security_advisories: $options[:security_advisories],
  package_manager: $package_manager,
  source: {
    "provider" => $options[:provider],
    "hostname" => $options[:azure_hostname],
    "api-endpoint" => $api_endpoint,
    "repo" => $repo_name,
    "directory" => $options[:directory],
    "branch" => $options[:branch]
  },
  credentials: [
    "git" => {
      "type" => "git_source",
      "host" => $options[:azure_hostname],
      "username" => ENV["AZURE_ACCESS_USERNAME"] || "x-access-token",
      "password" => ENV.fetch("AZURE_ACCESS_TOKEN", nil)
    }
  ],
  lockfile_only: false,
  requirements_update_strategy: $options[:requirements_update_strategy],
  update_subdependencies: false,
  updating_a_pull_request: false,
  vendor_dependencies: $options[:vendor_dependencies],
  # experiments: experiments,
  commit_message_options: commit_message,
  security_updates_only: $options[:security_updates_only],
  repo_contents_path: $options[:repo_contents_path] || File.expand_path(File.join("tmp", $repo_name.split("/"))),
  reject_external_code: $options[:reject_external_code],
  dependency_groups: [
    {
      # TODO: Parse this from environment variables
      "name" => "microsoft",
      "rules" => {
        "patterns" => ["Microsoft.*"]
      }
    }
  ]
)

begin
  Dependabot::FileFetcherAndUpdaterCommand.new(job: job, credentials: $options[:credentials]).run
rescue Dependabot::RunFailure
  exit 1
end
