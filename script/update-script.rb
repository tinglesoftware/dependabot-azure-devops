require "json"
require "logger"
require "dependabot/logger"

Dependabot.logger = Logger.new($stdout)

require "dependabot/file_fetchers"
require "dependabot/file_parsers"
require "dependabot/update_checkers"
require "dependabot/file_updaters"
require "dependabot/pull_request_creator"
require "dependabot/pull_request_updater"
require "dependabot/config/file_fetcher"
require "dependabot/omnibus"

require_relative "azure_helpers"

# These options try to follow the dry-run.rb script.
# https://github.com/dependabot/dependabot-core/blob/main/bin/dry-run.rb

$options = {
  credentials: [],
  provider: "azure",
  github_token: nil,

  directory: ENV["DEPENDABOT_DIRECTORY"] || "/", # Directory where the base dependency files are.
  branch: ENV["DEPENDABOT_TARGET_BRANCH"] || nil, # Branch against which to create PRs

  allow_conditions: [],
  reject_external_code: ENV['DEPENDABOT_REJECT_EXTERNAL_CODE'] == "true",
  requirements_update_strategy: nil,
  security_advisories: [],
  security_updates_only: false,
  ignore_conditions: [],
  pull_requests_limit: ENV["DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT"]&.to_i || 5,
  custom_labels: nil, # nil instead of empty array to ensure default labels are passed
  reviewers: nil, # nil instead of empty array to avoid API rejection
  assignees: nil, # nil instead of empty array to avoid API rejection
  branch_name_separator: ENV["DEPENDABOT_BRANCH_NAME_SEPARATOR"] || "/", # Separator used for created branches.
  milestone: ENV['DEPENDABOT_MILESTONE'] || nil, # Get the work item to attach
  updater_options: {},
  author_details: {
    email: ENV["DEPENDABOT_AUTHOR_EMAIL"] || "noreply@github.com",
    name: ENV["DEPENDABOT_AUTHOR_NAME"] || "dependabot[bot]",
  },
  fail_on_exception: ENV['DEPENDABOT_FAIL_ON_EXCEPTION'] == "true", # Stop the job if an exception occurs
  skip_pull_requests: ENV['DEPENDABOT_SKIP_PULL_REQUESTS'] == "true", # Skip creating/updating Pull Requests

  # See description of requirements here:
  # https://github.com/dependabot/dependabot-core/issues/600#issuecomment-407808103
  # https://github.com/wemake-services/kira-dependencies/pull/210
  excluded_requirements: ENV['DEPENDABOT_EXCLUDE_REQUIREMENTS_TO_UNLOCK']&.split(" ")&.map(&:to_sym) || [],

  # Details on the location of the repository
  azure_organization: ENV["AZURE_ORGANIZATION"],
  azure_project: ENV["AZURE_PROJECT"],
  azure_repository: ENV["AZURE_REPOSITORY"],
  azure_hostname: ENV["AZURE_HOSTNAME"] || "dev.azure.com",
  azure_protocol: ENV["AZURE_PROTOCOL"] || "https",
  azure_port: nil,
  azure_virtual_directory: ENV["AZURE_VIRTUAL_DIRECTORY"] || "",

  # Automatic completion
  set_auto_complete: ENV["AZURE_SET_AUTO_COMPLETE"] == "true", # Set auto complete on created pull requests
  auto_complete_ignore_config_ids: JSON.parse(ENV['AZURE_AUTO_COMPLETE_IGNORE_CONFIG_IDS'] || '[]'), # default to empty array
  merge_strategy: ENV["AZURE_MERGE_STRATEGY"] || "2", # default to squash merge

  # Automatic Approval
  auto_approve_pr: ENV["AZURE_AUTO_APPROVE_PR"] == "true",
  auto_approve_user_email: ENV["AZURE_AUTO_APPROVE_USER_EMAIL"],
  auto_approve_user_token: ENV["AZURE_AUTO_APPROVE_USER_TOKEN"] || ENV["AZURE_ACCESS_TOKEN"],
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
PACKAGE_ECOSYSTEM_MAPPING = { # [Hash<String, String>]
  "github-actions" => "github_actions",
  "gitsubmodule" => "submodules",
  "gomod" => "go_modules",
  "mix" => "hex",
  "npm" => "npm_and_yarn",
  # Additional ones
  "yarn" => "npm_and_yarn",
  "pipenv" => "pip",
  "pip-compile" => "pip",
  "poetry" => "pip",
}.freeze
$package_manager = PACKAGE_ECOSYSTEM_MAPPING.fetch($package_manager, $package_manager)

#########################################################
# Setup credentials for source code,                    #
# Add GitHub Access Token (PAT) to avoid rate limiting, #
# Setup extra credentials                               #
########################################################
$options[:credentials] << {
  "type" => "git_source",
  "host" => $options[:azure_hostname],
  "username" => ENV["AZURE_ACCESS_USERNAME"] || "x-access-token",
  "password" => ENV["AZURE_ACCESS_TOKEN"]
}
unless ENV["GITHUB_ACCESS_TOKEN"].to_s.strip.empty?
  puts "GitHub access token has been provided."
  $options[:github_token] = ENV["GITHUB_ACCESS_TOKEN"] # A GitHub access token with read access to public repos
  $options[:credentials] << {
    "type" => "git_source",
    "host" => "github.com",
    "username" => "x-access-token",
    "password" => $options[:github_token]
  }
end
# DEPENDABOT_EXTRA_CREDENTIALS, for example:
# "[{\"type\":\"npm_registry\",\"registry\":\"registry.npmjs.org\",\"token\":\"123\"}]"
unless ENV["DEPENDABOT_EXTRA_CREDENTIALS"].to_s.strip.empty?
  $options[:credentials] += JSON.parse(ENV["DEPENDABOT_EXTRA_CREDENTIALS"])
end

##########################################
# Setup the requirements update strategy #
##########################################
# GitHub native implementation modifies some of the names in the config file
unless ENV["DEPENDABOT_VERSIONING_STRATEGY"].to_s.strip.empty?
  VERSIONING_STRATEGIES = { # [Hash<String, Symbol>]
    "auto" => :auto,
    "lockfile-only" => :lockfile_only,
    "widen" => :widen_ranges,
    "increase" => :bump_versions,
    "increase-if-necessary" => :bump_versions_if_necessary
  }.freeze
  strategy_raw = ENV["DEPENDABOT_VERSIONING_STRATEGY"] || "auto"
  $options[:requirements_update_strategy] = VERSIONING_STRATEGIES.fetch(strategy_raw)

  # For npm_and_yarn & composer, we must correct the strategy to one allowed
  # https://github.com/dependabot/dependabot-core/blob/5ec858331d11253a30aa15fab25ae22fbdecdee0/npm_and_yarn/lib/dependabot/npm_and_yarn/update_checker/requirements_updater.rb#L18-L19
  # https://github.com/dependabot/dependabot-core/blob/5926b243b2875ad0d8c0a52c09210c4f5f274c5e/composer/lib/dependabot/composer/update_checker/requirements_updater.rb#L23-L24
  if $package_manager == "npm_and_yarn" || $package_manager == "composer"
    strategy = $options[:requirements_update_strategy]
    if strategy == :auto || strategy == :lockfile_only
      $options[:requirements_update_strategy] = :bump_versions
    end
  end

  # For pub, we also correct the strategy
  # https://github.com/dependabot/dependabot-core/blob/ca9f236591ba49fa6e2a8d5f06e538614033a628/pub/lib/dependabot/pub/update_checker.rb#L110
  if $package_manager == "pub"
    strategy = $options[:requirements_update_strategy]
    if strategy == :auto
      $options[:requirements_update_strategy] = nil
    elsif strategy == :lockfile_only
      $options[:requirements_update_strategy] = "bump_versions"
    else
      $options[:requirements_update_strategy] = strategy.to_s
    end
  end
end

#################################################################
#                     Setup Allow conditions                    #
# DEPENDABOT_ALLOW_CONDITIONS Example:
# [{"dependency-name":"sphinx","dependency-type":"production"}]
#################################################################
unless ENV["DEPENDABOT_ALLOW_CONDITIONS"].to_s.strip.empty?
  $options[:allow_conditions] = JSON.parse(ENV["DEPENDABOT_ALLOW_CONDITIONS"])
end

# Get allow versions for a dependency
TYPE_HANDLERS = { # [Hash<String, Proc>] handlers for type allow rules
  "all" => proc { true },
  "direct" => proc { |dep| dep.top_level? },
  "indirect" => proc { |dep| !dep.top_level? },
  "production" => proc { |dep| dep.production? },
  "development" => proc { |dep| !dep.production? },
  "security" => proc { |_, checker| checker.vulnerable? }
}.freeze
def allow_conditions_for(dep)
  # Find where the name matches then get the type e.g. production, direct, etc
  found = $options[:allow_conditions].find { |al| dep.name.match?(al['dependency-name']) }
  found ? found['dependency-type'] : nil
end

#################################################################
#                   Setup Security Advisories                   #
# File contents example:
# [{"dependency-name":"name","patched-versions":[],"unaffected-versions":[],"affected-versions":["< 0.10.0"]}]
#################################################################
unless ENV["DEPENDABOT_SECURITY_ADVISORIES_FILE"].to_s.strip.empty?
  security_advisories_file_name = ENV["DEPENDABOT_SECURITY_ADVISORIES_FILE"]
  if File.exists?(security_advisories_file_name)
    $options[:security_advisories] += JSON.parse(File.read(security_advisories_file_name))
  end
end

##################################################################################################
#                                     Setup Ignore conditions                                   #
# DEPENDABOT_IGNORE_CONDITIONS Example: [{"dependency-name":"ruby","versions":[">= 3.a", "< 4"]}]
##################################################################################################
unless ENV["DEPENDABOT_IGNORE_CONDITIONS"].to_s.strip.empty?
  $options[:ignore_conditions] = JSON.parse(ENV["DEPENDABOT_IGNORE_CONDITIONS"])
end

#################################################################
#                        Setup Labels                           #
# DEPENDABOT_LABELS Example: ["npm dependencies","triage-board"]
#################################################################
unless ENV["DEPENDABOT_LABELS"].to_s.strip.empty?
  $options[:custom_labels] = JSON.parse(ENV["DEPENDABOT_LABELS"])
end

#########################################################################
#                         Setup Reviewers                               #
# DEPENDABOT_REVIEWERS Example: ["be9321e2-f404-4ffa-8d6b-44efddb04865"]
#########################################################################
unless ENV["DEPENDABOT_REVIEWERS"].to_s.strip.empty?
  $options[:reviewers] = JSON.parse(ENV["DEPENDABOT_REVIEWERS"])
end

#########################################################################
#                           Setup Assignees                             #
# DEPENDABOT_ASSIGNEES Example: ["be9321e2-f404-4ffa-8d6b-44efddb04865"]
#########################################################################
unless ENV["DEPENDABOT_ASSIGNEES"].to_s.strip.empty?
  $options[:assignees] = JSON.parse(ENV["DEPENDABOT_ASSIGNEES"])
end

# Get ignore versions for a dependency
def ignored_versions_for(dep)
  if $options[:ignore_conditions].any?
    ignore_conditions = $options[:ignore_conditions].map do |ic|
      Dependabot::Config::IgnoreCondition.new(
        dependency_name: ic["dependency-name"],
        versions: ic["versions"],
        update_types: ic["update-types"]
      )
    end
    Dependabot::Config::UpdateConfig.new(ignore_conditions: ignore_conditions).
      ignored_versions_for(
        dep,
        security_updates_only: $options[:security_updates_only])
  else
    $update_config.ignored_versions_for(
      dep,
      security_updates_only: $options[:security_updates_only])
  end
end

def security_advisories_for(dep)
  relevant_advisories =
    $options[:security_advisories].
      select { |adv| adv.fetch("dependency-name").casecmp(dep.name).zero? }

  relevant_advisories.map do |adv|
    vulnerable_versions = adv["affected-versions"] || []
    safe_versions = (adv["patched-versions"] || []) +
                    (adv["unaffected-versions"] || [])

    Dependabot::SecurityAdvisory.new(
      dependency_name: dep.name,
      package_manager: $package_manager,
      vulnerable_versions: vulnerable_versions,
      safe_versions: safe_versions
    )
  end
end

# Create an update checker
def update_checker_for(dependency, files)
  Dependabot::UpdateCheckers.for_package_manager($package_manager).new(
    dependency: dependency,
    dependency_files: files,
    credentials: $options[:credentials],
    requirements_update_strategy: $options[:requirements_update_strategy],
    ignored_versions: ignored_versions_for(dependency),
    security_advisories: security_advisories_for(dependency),
    options: $options[:updater_options],
    )
end

def log_conflicting_dependencies(conflicting)
  return unless conflicting.any?

  puts "The update is not possible because of the following conflicting dependencies:"
  conflicting.each do |conflicting_dep|
    puts " - #{conflicting_dep['explanation']}"
  end
end

def security_fix?(dependency)
  security_advisories_for(dependency).any? do |advisory|
    advisory.fixed_by?(dependency)
  end
end

# If a version update for a peer dependency is possible we should
# defer to the PR that will be created for it to avoid duplicate PRS
def peer_dependency_should_update_instead?(dependency_name, updated_deps, files)
  # # This doesn't apply to security updates as we can't rely on the
  # # peer dependency getting updated
  # return false if $options[:security_updated_only]

  updated_deps
    .reject { |dep| dep.name == dependency_name }
    .any? do |dep|
      original_peer_dep = ::Dependabot::Dependency.new(
        name: dep.name,
        version: dep.previous_version,
        requirements: dep.previous_requirements,
        package_manager: dep.package_manager
      )
      update_checker_for(original_peer_dep, files)
        .can_update?(requirements_to_unlock: :own)
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
if !$options[:security_updates_only] && $options[:pull_requests_limit] == 0
  puts "Pull requests limit is set to zero. Security only updates are implied."
  $options[:security_updates_only] = true
end

####################################################
# Setup the hostname, protocol and port to be used #
####################################################
$options[:azure_port] = ENV["AZURE_PORT"] || ($options[:azure_protocol] == "http" ? "80" : "443")
$api_endpoint = "#{$options[:azure_protocol]}://#{$options[:azure_hostname]}:#{$options[:azure_port]}/"
$api_endpoint = $api_endpoint + "#{$options[:azure_virtual_directory]}/" unless $options[:azure_virtual_directory].empty?
puts "Using '#{$api_endpoint}' as API endpoint"
puts "Pull Requests shall be linked to milestone (work item) #{$options[:milestone]}" if $options[:milestone]
puts "Pull Requests shall be labeled #{$options[:custom_labels]}" if $options[:custom_labels]
puts "Working in #{$repo_name}, '#{$options[:branch] || 'default'}' branch under '#{$options[:directory]}' directory"

# Full name of the repo targeted.
$repo_name = "#{$options[:azure_organization]}/#{$options[:azure_project]}/_git/#{$options[:azure_repository]}"

$source = Dependabot::Source.new(
  provider: $options[:provider],
  hostname: $options[:azure_hostname],
  api_endpoint: $api_endpoint,
  repo: $repo_name,
  directory: $options[:directory],
  branch: $options[:branch],
)

## Read the update configuration if present
fetcher_args = {
  source: $source,
  credentials: $options[:credentials],
  options: $options[:updater_options]
}
puts "Looking for configuration file in the repository ..."
$config_file = begin
  # Using fetcher_args as before or in the examples will result in the
  # config file not being found if the directory specified is not the root.
  # This happens because the files are checked relative to the supplied directory.
  # https://github.com/dependabot/dependabot-core/blob/c5cd618812b07ece4a4b53ea18d80ad213b077e7/common/lib/dependabot/config/file_fetcher.rb#L29
  #
  # To solve this, the FileFetcher for the Config should have its own source
  # with the directory pointing to the root. Cloning makes it much easier
  # since we are only making the change for fetching the config file.
  #
  # See https://github.com/tinglesoftware/dependabot-azure-devops/issues/399
  cfg_source = $source.clone
  cfg_source.directory = "/"
  cfg_file = Dependabot::Config::FileFetcher.new(
    source: cfg_source,
    credentials: $options[:credentials],
    options: $options[:updater_options],
  ).config_file
  puts "Using configuration file at '#{cfg_file.path}' 😎"
  Dependabot::Config::File.parse(cfg_file.content)
rescue Dependabot::RepoNotFound, Dependabot::DependencyFileNotFound
  puts "Configuration file was not found, a default config will be used. 😔"
  Dependabot::Config::File.new(updates: [])
end
$update_config = $config_file.update_config(
  $package_manager,
  directory: $options[:directory],
  target_branch: $options[:branch]
)

##############################
# Fetch the dependency files #
##############################
puts "Using '#{$options[:requirements_update_strategy]}' requirements update strategy" if $options[:requirements_update_strategy]
fetcher = Dependabot::FileFetchers.for_package_manager($package_manager).new(**fetcher_args)
puts "Fetching #{$package_manager} dependency files ..."
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
  credentials: $options[:credentials],
  reject_external_code: $options[:reject_external_code]
)

dependencies = parser.parse
puts "Found #{dependencies.length} dependencies"
dependencies.each { |d| puts " - #{d.name} (#{d.version})" }

################################################
# Get active pull requests for this repository #
################################################
azure_client = Dependabot::Clients::Azure.for_source(
  source: $source,
  credentials: $options[:credentials],
)
default_branch_name = azure_client.fetch_default_branch($source.repo)
active_pull_requests = azure_client.pull_requests_active(default_branch_name)

pull_requests_count = 0

dependencies.select(&:top_level?).each do |dep|
  # Check if we have reached maximum number of open pull requests
  if $options[:pull_requests_limit] > 0 && pull_requests_count >= $options[:pull_requests_limit]
    puts "Limit of open pull requests (#{$options[:pull_requests_limit]}) reached."
    break
  end

  begin

    #########################################
    # Get update details for the dependency #
    #########################################
    puts "Checking if #{dep.name} #{dep.version} #{$options[:security_updates_only] ? 'is vulnerable' : 'needs updating'}"
    checker = update_checker_for(dep, files)

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
      print "#{dep.name} #{dep.version} is vulnerable. "
      if checker.lowest_security_fix_version
        puts "Earliest non-vulnerable is #{checker.lowest_security_fix_version}"
      else
        puts "Can't find non-vulnerable version. 🚨"
      end
    end

    if checker.up_to_date?
      puts "No update needed for #{dep.name} #{dep.version}"
      next
    end

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
    allowed = checker.vulnerable? || $options[:allow_conditions].empty? || (allow_type && TYPE_HANDLERS[allow_type].call(dep, checker))
    unless allowed
      puts "Updating #{dep.name} is not allowed"
      next
    end

    updated_deps = checker.updated_dependencies(
      requirements_to_unlock: requirements_to_unlock
    )

    # Skip when there is a peer dependency that can be updated
    if peer_dependency_should_update_instead?(checker.dependency.name, updated_deps, files)
      puts "Skipping update, peer dependency can be updated"
      next
    end

    if $options[:security_updates_only] && updated_deps.none? { |d| security_fix?(d) }
      puts "Updated version is still vulnerable 🚨"
      log_conflicting_dependencies(checker.conflicting_dependencies)
      next
    end

    #####################################
    # Generate updated dependency files #
    #####################################
    latest_allowed_version = checker.vulnerable? ?
                               checker.lowest_resolvable_security_fix_version
                               : checker.latest_resolvable_version
    puts "Updating #{dep.name} from #{dep.version} to #{latest_allowed_version}"
    updater = Dependabot::FileUpdaters.for_package_manager($package_manager).new(
      dependencies: updated_deps,
      dependency_files: files,
      credentials: $options[:credentials],
    )

    updated_files = updater.updated_dependency_files

    # Skip creating/updating PR
    if $options[:skip_pull_requests]
      # We are building a message as a way to test if commit-message in the config
      # and commit_message_options.to_h work correctly when testing issue
      # https://github.com/tinglesoftware/dependabot-azure-devops/issues/410
      # In the future this will be replaced with the simpler line below:
      #
      # puts "Skipping creating/updating Pull Request for #{dep.name} as instructed."

      msg = Dependabot::PullRequestCreator::MessageBuilder.new(
        dependencies: updated_deps,
        files: updated_files,
        credentials: $options[:credentials],
        source: $source,
        commit_message_options: $update_config.commit_message_options.to_h,
        github_redirection_service: Dependabot::PullRequestCreator::DEFAULT_GITHUB_REDIRECTION_SERVICE
      ).message
      puts "Skipping creating/updating Pull Request. Title: #{msg.pr_name}"
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

      # Filter those containing " #{dep.name} "
      # The prefix " " and suffix " " avoids taking PRS for dependencies named the same
      # e.g. Tingle.EventBus and Tingle.EventBus.Transports.Azure.ServiceBus
      next unless title.include?(" #{dep.name} ")

      # Ensure the title contains the current dependency version
      # Sometimes, the dep.version might be null such as in npm
      # when the package.lock.json is not checked into source.
      next unless title.include?(dep.name) && dep.version && title.include?(dep.version)

      # If the title does not contain the updated version,
      # we need to close the PR and delete it's branch,
      # because there is a newer version available
      #
      # Sample Titles:
      # Bump Tingle.Extensions.Logging.LogAnalytics from 3.4.2-ci0005 to 3.4.2-ci0006
      # chore(deps): bump dotenv from 9.0.1 to 9.0.2 in /server
      if !title.include?("#{updated_deps[0].version} ") && !title.end_with?(updated_deps[0].version)
        # Close old version PR
        azure_client.pull_request_abandon(pr_id)
        azure_client.branch_delete(source_ref_name)
        puts "Closed Pull Request ##{pr_id}"
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

    pull_request = nil
    pull_request_id = nil
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
        author_details: $options[:author_details],
      )

      print "Submitting pull request (##{conflict_pull_request_id}) update for #{dep.name}. "
      pr_updater.update
      pull_request = existing_pull_request
      pull_request_id = conflict_pull_request_id
      puts "Done."
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
          work_item: $options[:milestone],
        }
      )

      print "Submitting #{dep.name} pull request for creation. "
      pull_request = pr_creator.create

      if pull_request
        req_status = pull_request&.status
        if req_status == 201
          pull_request = JSON[pull_request.body]
          pull_request_id = pull_request["pullRequestId"]
          puts "Done (PR ##{pull_request_id})."
        else
          content = JSON[pull_request.body]
          message = content["message"]
          puts "Failed! PR already exists or an error has occurred."
          # throw exception here because pull_request.create does not throw
          raise StandardError.new "Pull Request creation failed with status #{req_status}. Message: #{message}"
        end
      else
        puts "Seems PR is already present."
      end
    else
      pull_request = existing_pull_request # One already existed
      pull_request_id = pull_request["pullRequestId"]
      puts "Pull request for #{dep.version} already exists (##{pull_request_id}) and does not need updating."
    end

    pull_requests_count += 1
    next unless pull_request_id

    # Auto approve this Pull Request
    if $options[:auto_approve_pr]
      puts "Auto Approving PR for user #{$options[:auto_approve_user_email]}"

      azure_client.pull_request_approve(
        # Adding argument names will fail! May because there is no spec?
        pull_request_id,
        $options[:auto_approve_user_email],
        $options[:auto_approve_user_token]
      )
    end

    # Set auto complete for this Pull Request
    # Pull requests that pass all policies will be merged automatically.
    # Optional policies can be ignored by passing their identifiers
    if $options[:set_auto_complete]
      auto_complete_user_id = pull_request['createdBy']['id']
      puts "Setting auto complete on ##{pull_request_id}."
      azure_client.pull_request_auto_complete(
        # Adding argument names will fail! May because there is no spec?
        pull_request_id,
        auto_complete_user_id,
        $options[:merge_strategy],
        $options[:auto_complete_ignore_config_ids]
      )
    end

  rescue StandardError => e
    raise e if $options[:fail_on_exception]
    puts "Error working on updates for #{dep.name} #{dep.version} (continuing)"
    puts e.full_message
  end
end

puts "Done"
