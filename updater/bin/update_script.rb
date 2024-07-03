# frozen_string_literal: true

# rubocop:disable Style/GlobalVars

require "json"
require "logger"
require "dependabot/logger"

Dependabot.logger = Logger.new($stdout)

# ensure logs are output immediately. Useful when running in certain hosts like ContainerGroups
$stdout.sync = true

require "dependabot/credential"
require "dependabot/file_fetchers"
require "dependabot/file_parsers"
require "dependabot/update_checkers"
require "dependabot/file_updaters"
require "dependabot/pull_request_creator"
require "dependabot/pull_request_updater"
require "dependabot/requirements_update_strategy"
require "dependabot/simple_instrumentor"

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

require_relative "azure_helpers"
require_relative "vulnerabilities"

# These options try to follow the dry-run.rb script.
# https://github.com/dependabot/dependabot-core/blob/main/bin/dry-run.rb

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
    "lockfile-only" => Dependabot::RequirementsUpdateStrategy::LockfileOnly,
    "widen" => Dependabot::RequirementsUpdateStrategy::WidenRanges,
    "increase" => Dependabot::RequirementsUpdateStrategy::BumpVersions,
    "increase-if-necessary" => Dependabot::RequirementsUpdateStrategy::BumpVersionsIfNecessary
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
    if strategy.nil? || strategy == Dependabot::RequirementsUpdateStrategy::LockfileOnly
      $options[:requirements_update_strategy] = Dependabot::RequirementsUpdateStrategy::BumpVersions
    end
  end

  # For pub, we also correct the strategy
  # https://github.com/dependabot/dependabot-core/blob/ca9f236591ba49fa6e2a8d5f06e538614033a628/pub/lib/dependabot/pub/update_checker.rb#L110
  if $package_manager == "pub"
    strategy = $options[:requirements_update_strategy]
    if strategy == Dependabot::RequirementsUpdateStrategy::LockfileOnly
      $options[:requirements_update_strategy] = Dependabot::RequirementsUpdateStrategy::BumpVersions
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

def allow_conditions_for(dep)
  # Find where the name matches then get the type e.g. production, direct, etc
  found = $options[:allow_conditions].find { |al| dep.name.match?(al["dependency-name"]) }
  found ? found["dependency-type"] : "all" # when not specified, allow all types
end

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

# Get ignore versions for a dependency
def ignored_versions_for(dep)
  $update_config.ignored_versions_for(
    dep,
    security_updates_only: $options[:security_updates_only]
  )
end

# rubocop:disable Metrics/PerceivedComplexity
def security_advisories_for(dep)
  relevant_advisories =
    $options[:security_advisories]
    .select { |adv| adv.fetch("dependency-name").casecmp(dep.name).zero? }

  # add relevant advisories from the fetcher if present
  relevant_advisories += $vulnerabilities_fetcher&.fetch(dep.name) || []

  relevant_advisories.map do |adv|
    vulnerable_versions = adv["affected-versions"] || []
    safe_versions = (adv["patched-versions"] || []) +
                    (adv["unaffected-versions"] || [])

    # Filter out nil (using .compact), white spaces and empty strings which is necessary for situations
    # where the API response contains null that is converted to nil, or it is an empty
    # string. For example, npm package named faker does not have patched version as of 2023-01-16
    # See: https://github.com/advisories/GHSA-5w9c-rv96-fr7g for npm package
    # This ideally fixes
    # https://github.com/tinglesoftware/dependabot-azure-devops/issues/453#issuecomment-1383587644
    vulnerable_versions = vulnerable_versions.compact.reject { |v| v.strip.empty? }
    safe_versions = safe_versions.compact.reject { |v| v.strip.empty? }
    next if vulnerable_versions.empty? && safe_versions.empty?

    Dependabot::SecurityAdvisory.new(
      dependency_name: dep.name,
      package_manager: $package_manager,
      vulnerable_versions: vulnerable_versions,
      safe_versions: safe_versions
    )
  end
end
# rubocop:enable Metrics/PerceivedComplexity

# Create an update checker
def update_checker_for(dependency, files, security_advisories)
  Dependabot::UpdateCheckers.for_package_manager($package_manager).new(
    dependency: dependency,
    dependency_files: files,
    credentials: $options[:credentials],
    requirements_update_strategy: $options[:requirements_update_strategy],
    ignored_versions: ignored_versions_for(dependency),
    security_advisories: security_advisories,
    options: $options[:updater_options]
  )
end

def log_conflicting_dependencies(conflicting)
  return unless conflicting.any?

  puts "The update is not possible because of the following conflicting dependencies:"
  conflicting.each do |conflicting_dep|
    puts " - #{conflicting_dep['explanation']}"
  end
end

def security_fix?(dependency, security_advisories)
  security_advisories.any? do |advisory|
    advisory.fixed_by?(dependency)
  end
end

# If a version update for a peer dependency is possible we should
# defer to the PR that will be created for it to avoid duplicate PRS
def peer_dependency_should_update_instead?(dependency_name, updated_deps, files, security_advisories)
  # This doesn't apply to security updates as we can't rely on the
  # peer dependency getting updated
  return false if $options[:security_updates_only]

  updated_deps
    .reject { |dep| dep.name == dependency_name }
    .any? do |dep|
    original_peer_dep = ::Dependabot::Dependency.new(
      name: dep.name,
      version: dep.previous_version,
      requirements: dep.previous_requirements,
      package_manager: dep.package_manager
    )
    update_checker_for(original_peer_dep, files, security_advisories)
      .can_update?(requirements_to_unlock: :own)
  end
end

def show_diff(original_file, updated_file)
  return unless original_file

  if original_file.content == updated_file.content
    puts "    No change to #{original_file.name}"
    return
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

  puts
  puts "    ± #{original_file.name}"
  puts "    ~~~"
  puts diff.map { |line| "    " + line }.join
  puts "    ~~~"
  puts "    #{added_lines} insertions (+), #{removed_lines} deletions (-)"
  puts
end

Dependabot::SimpleInstrumentor.subscribe do |*args|
  name = args.first

  payload = args.last
  if name == "excon.request" || name == "excon.response"
    puts "🌍 #{name == 'excon.response' ? "<-- #{payload[:status]}" : "--> #{payload[:method].upcase}"}" \
         " #{Excon::Utils.request_uri(payload)}"
  end
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

$source = Dependabot::Source.new(
  provider: $options[:provider],
  hostname: $options[:azure_hostname],
  api_endpoint: $api_endpoint,
  repo: $repo_name,
  directory: $options[:directory],
  branch: $options[:branch]
)

## Create the update configuration (we no longer parse the file because of BOM and type issues)
$update_config = Dependabot::Config::UpdateConfig.new(
  ignore_conditions: $options[:ignore_conditions],
  commit_message_options: $options[:commit_message_options]
)

if $options[:requirements_update_strategy]
  puts "Using '#{$options[:requirements_update_strategy]}' requirements update strategy"
end

##############################
# Fetch the dependency files #
##############################
clone = true
$options[:repo_contents_path] ||= File.expand_path(File.join("tmp", $repo_name.split("/"))) if clone
fetcher_args = {
  source: $source,
  credentials: $options[:credentials],
  repo_contents_path: $options[:repo_contents_path],
  options: $options[:updater_options]
}
fetcher = Dependabot::FileFetchers.for_package_manager($package_manager).new(**fetcher_args)
if clone
  puts "Cloning repository into #{$options[:repo_contents_path]}"
  fetcher.clone_repo_contents
else
  puts "Fetching #{$package_manager} dependency files ..."
end
files = fetcher.files
commit = fetcher.commit
puts "Found #{files.length} dependency file(s) at commit #{commit}"
files.each { |f| puts " - #{f.path}" }

##############################
# Parse the dependency files #
##############################
puts "Parsing dependencies information"
parser = Dependabot::FileParsers.for_package_manager($package_manager).new(
  dependency_files: files,
  source: $source,
  repo_contents_path: $options[:repo_contents_path],
  credentials: $options[:credentials],
  reject_external_code: $options[:reject_external_code],
  options: $options[:updater_options]
)

dependencies = parser.parse
puts "Found #{dependencies.count(&:top_level?)} dependencies"
dependencies.select(&:top_level?).each { |d| puts " - #{d.name} (#{d.version})" }

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

pull_requests_count = 0

# rubocop:disable Metrics/BlockLength
dependencies.select(&:top_level?).each do |dep|
  # Check if we have reached maximum number of open pull requests
  if ($options[:pull_requests_limit]).positive? && pull_requests_count >= $options[:pull_requests_limit]
    puts "Limit of open pull requests (#{$options[:pull_requests_limit]}) reached."
    break
  end

  begin
    #########################################
    # Get update details for the dependency #
    #########################################
    puts "Checking if #{dep.name} #{dep.version} is vulnerable" if $options[:security_updates_only]
    puts "Checking if #{dep.name} #{dep.version} needs updating" unless $options[:security_updates_only]
    security_advisories = security_advisories_for(dep)
    checker = update_checker_for(dep, files, security_advisories)

    # For security only updates, skip dependencies that are not vulnerable
    if $options[:security_updates_only] && !checker.vulnerable?
      if checker.version_class.correct?(checker.dependency.version)
        puts "#{dep.name} #{dep.version} is not vulnerable"
      else
        puts "Unable to update vulnerable dependencies for projects without " \
             "a lockfile as the currently installed version isn't known "
      end
      next
    end

    # For vulnerable dependencies
    if checker.vulnerable?
      if checker.lowest_security_fix_version
        puts "#{dep.name} #{dep.version} is vulnerable. Earliest non-vulnerable is " \
             "#{checker.lowest_security_fix_version}"
      else
        puts "#{dep.name} #{dep.version} is vulnerable. Can't find non-vulnerable version. 🚨"
      end
    end

    if checker.up_to_date?
      puts "No update needed for #{dep.name} #{dep.version}"
      next
    end

    # rubocop:disable Lint/ElseLayout
    requirements_to_unlock =
      if !checker.requirements_unlocked_or_can_be?
        if !$options[:excluded_requirements].include?(:none) &&
           checker.can_update?(requirements_to_unlock: :none) then :none
        else :update_not_possible
        end
      elsif !$options[:excluded_requirements].include?(:own) &&
            checker.can_update?(requirements_to_unlock: :own) then :own
      elsif !$options[:excluded_requirements].include?(:all) &&
            checker.can_update?(requirements_to_unlock: :all) then :all
      else :update_not_possible
      end
    # rubocop:enable Lint/ElseLayout

    puts "Requirements to unlock #{requirements_to_unlock}" unless $options[:security_updates_only]
    if checker.respond_to?(:requirements_update_strategy)
      puts "Requirements update strategy #{checker.requirements_update_strategy}"
    end

    if requirements_to_unlock == :update_not_possible
      log_conflicting_dependencies(checker.conflicting_dependencies)
      next
    end

    # Check if the dependency is allowed
    allow_type = allow_conditions_for(dep)
    allowed = checker.vulnerable? ||
              $options[:allow_conditions].empty? ||
              (allow_type && TYPE_HANDLERS[allow_type].call(dep, checker))
    unless allowed
      puts "Updating #{dep.name} is not allowed"
      next
    end

    updated_deps = checker.updated_dependencies(
      requirements_to_unlock: requirements_to_unlock
    )

    # Skip when there is a peer dependency that can be updated
    if peer_dependency_should_update_instead?(checker.dependency.name, updated_deps, files, security_advisories)
      puts "Skipping update, peer dependency can be updated"
      next
    end

    if $options[:security_updates_only] && updated_deps.none? { |d| security_fix?(d, security_advisories) }
      puts "Updated version is still vulnerable 🚨"
      log_conflicting_dependencies(checker.conflicting_dependencies)
      next
    end

    # Removal is only supported for transitive dependencies which are removed as a
    # side effect of the parent update
    updated_deps = updated_deps.reject(&:removed?)

    #####################################
    # Generate updated dependency files #
    #####################################
    latest_allowed_version = if checker.vulnerable?
                               checker.lowest_resolvable_security_fix_version
                             else
                               checker.latest_resolvable_version
                             end
    if updated_deps.count == 1
      dep_first = updated_deps.first
      prev_v = dep_first.previous_version
      prev_v_msg = prev_v ? "from #{prev_v} " : ""
      puts "Updating #{dep_first.name} #{prev_v_msg}to #{latest_allowed_version}"
    else
      dep_names = updated_deps.map(&:name)
      puts "Updating #{dep_names.join(', ')}"
    end
    updater = Dependabot::FileUpdaters.for_package_manager($package_manager).new(
      dependencies: updated_deps,
      dependency_files: files,
      repo_contents_path: $options[:repo_contents_path],
      credentials: $options[:credentials],
      options: $options[:updater_options]
    )

    updated_files = updater.updated_dependency_files

    # Create shared MessageBuilder
    msg = Dependabot::PullRequestCreator::MessageBuilder.new(
      source: $source,
      dependencies: updated_deps,
      files: updated_files,
      credentials: $options[:credentials],
      commit_message_options: $update_config.commit_message_options.to_h,
      # pr_message_header: ,
      # pr_message_footer: ,
      # vulnerabilities_fixed: ,
      # dependency_group: ,
      github_redirection_service: Dependabot::PullRequestCreator::DEFAULT_GITHUB_REDIRECTION_SERVICE,
      pr_message_max_length: Dependabot::PullRequestCreator::Azure::PR_DESCRIPTION_MAX_LENGTH,
      pr_message_encoding: Dependabot::PullRequestCreator::Azure::PR_DESCRIPTION_ENCODING
    )

    # Skip creating/updating PR
    if $options[:skip_pull_requests]
      # We are building a message as a way to test if commit-message in the config
      # and commit_message_options.to_h work correctly when testing issue
      # https://github.com/tinglesoftware/dependabot-azure-devops/issues/410
      # In the future this will be replaced with the simpler line below:
      #
      # puts "Skipping creating/updating Pull Request for #{dep.name} as instructed."

      puts "Skipping creating/updating Pull Request."
      puts "Title: #{msg.pr_name}"
      puts "Description:\r\n#{msg.pr_message}\r\n"
      puts "Commit:\r\n#{msg.commit_message}\r\n"

      # We are printing the file diffs and a message as a way to test file changes
      # when testing issue
      # https://github.com/tinglesoftware/dependabot-azure-devops/issues/512
      puts "\r\nChanges:\r\n"
      updated_files.each do |updated_file|
        if updated_file.operation == Dependabot::DependencyFile::Operation::DELETE
          puts "    Deleted #{updated_file.name}"
        else
          original_file = files.find { |f| f.name == updated_file.name }
          if original_file
            show_diff(original_file, updated_file)
          else
            puts "    Added #{updated_file.name}"
          end
        end
      end

      puts "------------------------"
      puts ""

      pull_requests_count += 1
      next
    end

    ###################################
    # Find out if a PR already exists #
    ###################################
    conflict_pull_request_commit = nil
    conflict_pull_request_id = nil
    existing_pull_request = nil
    active_pull_requests.each do |pr|
      pr_id = pr["pullRequestId"]
      title = pr["title"]
      source_ref_name = pr["sourceRefName"]

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
        azure_client.branch_delete(source_ref_name) # do this first to avoid hanging branches
        azure_client.pull_request_abandon(pr_id)
        puts "Abandoned Pull Request ##{pr_id}"
        next
      end

      existing_pull_request = pr

      # If the merge status of the current PR is not succeeded,
      # we need to resolve the merge conflicts
      next unless pr["mergeStatus"] != "succeeded"

      # ignore pull request manually edited
      next if azure_client.pull_request_commits(pr_id).length > 1

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
      pr_updater = Dependabot::PullRequestUpdater.new(
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
      pr_creator = Dependabot::PullRequestCreator.new(
        source: $source,
        base_commit: commit,
        dependencies: updated_deps,
        files: updated_files,
        credentials: $options[:credentials],
        author_details: $options[:author_details],
        commit_message_options: $update_config.commit_message_options.to_h,
        custom_labels: $options[:custom_labels],
        reviewers: $options[:reviewers],
        assignees: $options[:assignees],
        milestone: $options[:milestone],
        branch_name_separator: $options[:branch_name_separator],
        label_language: true,
        automerge_candidate: $options[:set_auto_complete],
        github_redirection_service: Dependabot::PullRequestCreator::DEFAULT_GITHUB_REDIRECTION_SERVICE,
        provider_metadata: {
          work_item: $options[:milestone]
        },
        message: msg
      )

      puts "Submitting #{dep.name} pull request for creation."
      pull_request = pr_creator.create

      if pull_request
        req_status = pull_request&.status
        if req_status == 201
          pull_request = JSON[pull_request.body]
          pull_request_id = pull_request["pullRequestId"]
          puts "Created pull request for #{dep.name} (##{pull_request_id})."
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

    pull_requests_count += 1
    next unless pull_request_id

    # Auto approve this Pull Request
    if $options[:auto_approve_pr] && created_or_updated
      puts "Auto Approving PR #{pull_request_id}"

      azure_client.pull_request_approve(
        # Adding argument names will fail! Maybe because there is no spec?
        pull_request_id,
        $options[:auto_approve_user_token]
      )
    end

    # Set auto complete for this Pull Request
    # Pull requests that pass all policies will be merged automatically.
    # Optional policies can be ignored by passing their identifiers
    #
    # The merge commit message should contain the PR number and title for tracking.
    # This is the default behaviour in Azure DevOps
    # Example:
    # Merged PR 24093: Bump Tingle.Extensions.Logging.LogAnalytics from 3.4.2-ci0005 to 3.4.2-ci0006
    #
    # Bumps [Tingle.Extensions.Logging.LogAnalytics](...) from 3.4.2-ci0005 to 3.4.2-ci0006
    # - [Release notes](....)
    # - [Changelog](....)
    # - [Commits](....)
    merge_commit_message = "Merged PR #{pull_request_id}: #{msg.pr_name}\n\n#{msg.commit_message}"
    if $options[:set_auto_complete] && created_or_updated
      auto_complete_user_id = pull_request["createdBy"]["id"]
      puts "Setting auto complete on ##{pull_request_id}."
      azure_client.autocomplete_pull_request(
        # Adding argument names will fail! Maybe because there is no spec?
        pull_request_id,
        auto_complete_user_id,
        merge_commit_message,
        true, # delete_source_branch
        true, # squash_merge
        $options[:merge_strategy],
        $options[:trans_work_items],
        $options[:auto_complete_ignore_config_ids]
      )
    end
  rescue StandardError => e
    raise e if $options[:fail_on_exception]

    puts "Error working on updates for #{dep.name} #{dep.version} (continuing)"
    puts e.full_message
  end
end

# look for pull requests that are no longer needed to be abandoned
if $options[:close_unwanted]
  puts "Looking for pull requests that are no longer needed."
  active_pull_requests = azure_client.pull_requests_active(user_id, target_branch_name)
  active_pull_requests.each do |pr|
    pr_id = pr["pullRequestId"]
    title = pr["title"]
    source_ref_name = pr["sourceRefName"]

    begin
      keep = false
      dependencies.select(&:top_level?).each do |dep|
        # Sometimes, the dep.version might be null such as in npm
        # when the package.lock.json is not checked into source.
        next unless dep.version

        # CHECKING BY VERSION DOESN'T SEEM TO WORK. THE CODE IS HERE FOR REFERENCE

        # Check if the version has since been ignored, it so we do not keep
        requirement_class = Dependabot::Utils.requirement_class_for_package_manager(dep.package_manager)
        version_class = Dependabot::Utils.version_class_for_package_manager(dep.package_manager) # necessary for npm
        next unless version_class.correct?(dep.version) # git_submodules don't work here

        ignore_reqs = ignored_versions_for(dep)
                      .flat_map { |req| requirement_class.requirements_array(req) }
        if ignore_reqs.any? { |req| req.satisfied_by?(version_class.new(dep.version)) }
          puts "Update for #{dep.name} #{dep.version} is no longer required."
          next
        end

        # Ensure the title contains the current dependency name and version.
        #
        # Samples:
        # Bump Tingle.Extensions.Logging.LogAnalytics from 3.4.2-ci0005 to 3.4.2-ci0006
        # chore(deps): bump dotenv from 9.0.1 to 9.0.2 in /server
        #
        # display_name is used instead of name because some titles do not have the full dependency name.
        # For instance 'org.junit.jupiter:junit-jupiter' will only read 'junit-jupiter' in the title.
        keep = title.include?("#{dep.display_name} from #{dep.version} to ")

        # Break if the PR should be kept
        break if keep
      end

      # Abandon the PR unless we should keep it
      unless keep
        if $options[:skip_pull_requests]
          puts "Skipping abandoning PR ##{pr_id} (#{title})"
        else
          puts "Abandoning PR ##{pr_id} (#{title}) as it is no longer needed."
          azure_client.branch_delete(source_ref_name)
          azure_client.pull_request_abandon(pr_id)
        end
      end
    rescue StandardError => e
      raise e if $options[:fail_on_exception]

      puts "Error checking whether to abandon (or abandoning) PR ##{pr_id} (continuing)"
      puts e.full_message
    end
  end
end

puts "Done"

# rubocop:enable Metrics/BlockLength

# rubocop:enable Style/GlobalVars
