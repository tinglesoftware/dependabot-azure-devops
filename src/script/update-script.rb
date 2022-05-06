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

  directory: ENV["DEPENDABOT_DIRECTORY"] || "/", # Directory where the base dependency files are.
  branch: ENV["DEPENDABOT_TARGET_BRANCH"] || nil, # Branch against which to create PRs

  allow_conditions: [],
  requirements_update_strategy: nil,
  ignore_conditions: [],
  fail_on_exception: ENV['DEPENDABOT_FAIL_ON_EXCEPTION'] == "true", # Stop the job if an exception occurs
  pull_requests_limit: ENV["DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT"].to_i || 5,

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

  work_item_id: ENV['DEPENDABOT_MILESTONE'] || nil, # Get the work item to attach

  set_auto_complete: ENV["AZURE_SET_AUTO_COMPLETE"] == "true", # Set auto complete on created pull requests
  merge_strategy: ENV["AZURE_MERGE_STRATEGY"] || "2", # default to squash merge

  # Automatically Approve the PR
  auto_approve_pr: ENV["AZURE_AUTO_APPROVE_PR"] == "true",
  auto_approve_user_email: ENV["AZURE_AUTO_APPROVE_USER_EMAIL"],
  auto_approve_user_token: ENV["AZURE_AUTO_APPROVE_USER_TOKEN"],
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
  "npm" => "npm_and_yarn",
  "yarn" => "npm_and_yarn",
  "pipenv" => "pip",
  "pip-compile" => "pip",
  "poetry" => "pip",
  "gomod" => "go_modules",
  "gitsubmodule" => "submodules",
  "mix" => "hex"
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
  "username" => "x-access-token",
  "password" => ENV["AZURE_ACCESS_TOKEN"]
}
unless ENV["GITHUB_ACCESS_TOKEN"].to_s.strip.empty?
  puts "GitHub access token has been provided."
  $options[:credentials] << {
    "type" => "git_source",
    "host" => "github.com",
    "username" => "x-access-token",
    "password" => ENV["GITHUB_ACCESS_TOKEN"] # A GitHub access token with read access to public repos
  }
end
unless ENV["DEPENDABOT_EXTRA_CREDENTIALS"].to_s.strip.empty?
  # For example:
  # "[{\"type\":\"npm_registry\",\"registry\":\
  #     "registry.npmjs.org\",\"token\":\"123\"}]"
  $options[:credentials].concat(JSON.parse(ENV["DEPENDABOT_EXTRA_CREDENTIALS"]))

  # Adding custom private feed removes the public onces so we have to create it
  if $package_manager == "nuget"
    $options[:credentials] << {
      "type" => "nuget_feed",
      "url" => "https://api.nuget.org/v3/index.json",
    }
  end
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
  requirements_update_strategy_raw = ENV["DEPENDABOT_VERSIONING_STRATEGY"] || "auto"
  $options[:requirements_update_strategy] = VERSIONING_STRATEGIES.fetch(requirements_update_strategy_raw)

  # For npm_and_yarn, we must correct the strategy to one allowed
  # https://github.com/dependabot/dependabot-core/blob/5ec858331d11253a30aa15fab25ae22fbdecdee0/npm_and_yarn/lib/dependabot/npm_and_yarn/update_checker/requirements_updater.rb#L18-L19
  # https://github.com/dependabot/dependabot-core/blob/5926b243b2875ad0d8c0a52c09210c4f5f274c5e/composer/lib/dependabot/composer/update_checker/requirements_updater.rb#L23-L24
  if $package_manager == "npm_and_yarn" || $package_manager == "composer"
    strategy = $options[:requirements_update_strategy]
    if strategy == :auto || strategy == :lockfile_only
      $options[:requirements_update_strategy] = :bump_versions
    end
  end
end

####################################################
# Setup the hostname, protocol and port to be used #
####################################################
$options[:azure_port] = ENV["AZURE_PORT"] || ($options[:azure_protocol] == "http" ? "80" : "443")
puts "Using hostname = '#{$options[:azure_hostname]}', protocol = '#{$options[:azure_protocol]}', port = '#{$options[:azure_port]}'."

##########################
# Setup Allow conditions #
##########################
unless ENV["DEPENDABOT_ALLOW_CONDITIONS"].to_s.strip.empty?
  # For example:
  # [{"dependency-name":"sphinx","dependency-type":"production"}]
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

###########################
# Setup Ignore conditions #
###########################
unless ENV["DEPENDABOT_IGNORE_CONDITIONS"].to_s.strip.empty?
  # For example:
  # [{"dependency-name":"ruby","versions":[">= 3.a", "< 4"]}]
  $options[:ignore_conditions] = JSON.parse(ENV["DEPENDABOT_IGNORE_CONDITIONS"])
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
    # Dependabot::Config::UpdateConfig.new(ignore_conditions: ignore_conditions).
    #   ignored_versions_for(dep, security_updates_only: $options[:security_updates_only])
    Dependabot::Config::UpdateConfig.new(ignore_conditions: ignore_conditions).ignored_versions_for(dep)
  else
    $update_config.ignored_versions_for(dep)
  end
end


$api_endpoint = "#{$options[:azure_protocol]}://#{$options[:azure_hostname]}:#{$options[:azure_port]}/"
$api_endpoint = $api_endpoint + "#{$options[:azure_virtual_directory]}/" if !$options[:azure_virtual_directory].empty?
puts "Using '#{$api_endpoint}' as API endpoint"
puts "Pull Requests shall be linked to work item #{$options[:work_item_id]}" if $options[:work_item_id]

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

## Make an empty update_config
$config_file = Dependabot::Config::File.new(updates: [])
$update_config = $config_file.update_config(
  $package_manager,
  directory: $options[:directory],
  target_branch: $options[:branch]
)

##############################
# Fetch the dependency files #
##############################
puts "Fetching #{$package_manager} dependency files for #{$repo_name}"
puts "Targeting '#{$options[:branch] || 'default'}' branch under '#{$options[:directory]}' directory"
puts "Using '#{$options[:requirements_update_strategy]}' requirements update strategy" if $options[:requirements_update_strategy]
fetcher = Dependabot::FileFetchers.for_package_manager($package_manager).new(
  source: $source,
  credentials: $options[:credentials],
)

files = fetcher.files
commit = fetcher.commit

##############################
# Parse the dependency files #
##############################
puts "Parsing dependencies information"
parser = Dependabot::FileParsers.for_package_manager($package_manager).new(
  dependency_files: files,
  source: $source,
  credentials: $options[:credentials],
)

dependencies = parser.parse

################################################
# Get active pull requests for this repository #
################################################
azure_client = Dependabot::Clients::Azure.for_source(
  source: $source,
  credentials: $options[:credentials],
)
default_branch_name = azure_client.fetch_default_branch($source.repo)
active_pull_requests_for_this_repo = azure_client.pull_requests_active(default_branch_name)

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
    puts "Checking if #{dep.name} #{dep.version} needs updating"

    checker = Dependabot::UpdateCheckers.for_package_manager($package_manager).new(
      dependency: dep,
      dependency_files: files,
      credentials: $options[:credentials],
      requirements_update_strategy: $options[:requirements_update_strategy],
      ignored_versions: ignored_versions_for(dep),
    )

    if checker.up_to_date?
      puts "No update needed for #{dep.name} #{dep.version}"
      next
    end

    requirements_to_unlock =
      if !checker.requirements_unlocked_or_can_be?
        if !$options[:excluded_requirements].include?(:none) && checker.can_update?(requirements_to_unlock: :none) then :none
        else :update_not_possible
        end
      elsif !$options[:excluded_requirements].include?(:own) && checker.can_update?(requirements_to_unlock: :own) then :own
      elsif !$options[:excluded_requirements].include?(:all) && checker.can_update?(requirements_to_unlock: :all) then :all
      else :update_not_possible
      end

    puts "Requirements to unlock #{requirements_to_unlock}"
    next if requirements_to_unlock == :update_not_possible

    # Check if the dependency is allowed
    allow_type = allow_conditions_for(dep)
    allowed = checker.vulnerable? || $options[:allow_conditions].empty? || (allow_type && TYPE_HANDLERS[allow_type].call(dep, checker))
    if !allowed
      puts "Updating #{dep.name} is not allowed"
      next
    end

    updated_deps = checker.updated_dependencies(
      requirements_to_unlock: requirements_to_unlock
    )

    #####################################
    # Generate updated dependency files #
    #####################################
    puts "Updating #{dep.name} from #{dep.version} to #{checker.latest_version}"
    updater = Dependabot::FileUpdaters.for_package_manager($package_manager).new(
      dependencies: updated_deps,
      dependency_files: files,
      credentials: $options[:credentials],
    )

    updated_files = updater.updated_dependency_files

    ###################################
    # Find out if a PR already exists #
    ###################################
    conflict_pull_request_commit_id = nil
    conflict_pull_request_id = nil
    existing_pull_request = nil
    active_pull_requests_for_this_repo.each do |pr|
      pr_id = pr["pullRequestId"]
      title = pr["title"]
      sourceRefName = pr["sourceRefName"]

      # Filter those containing " #{dep.name} "
      # The prefix " " and suffix " " avoids taking PRS for dependencies named the same
      # e.g. Tingle.EventBus and Tingle.EventBus.Transports.Azure.ServiceBus
      next if !title.include?(" #{dep.name} ")

      # Ensure the title contains the current dependency version
      # Sometimes, the dep.version might be null such as in npm
      # when the package.lock.json is not checked into source.
      if title.include?(dep.name) && dep.version && title.include?(dep.version)
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
          azure_client.branch_delete(sourceRefName)
          puts "Closed Pull Request ##{pr_id}"
          next
        end

        # If the merge status of the current PR is not successful,
        # we need to resolve the merge conflicts
        existing_pull_request = pr
        if pr["mergeStatus"] != "succeeded"
          # ignore pull request manully edited
          next if azure_client.pull_request_commits(pr_id).length > 1
          # keep pull request
          conflict_pull_request_commit_id = pr["lastMergeSourceCommit"]["commitId"]
          conflict_pull_request_id = pr_id
          break
        end
      end
    end

    pull_request = nil
    pull_request_id = nil
    if conflict_pull_request_commit_id && conflict_pull_request_id
      ##############################################
      # Update pull request with conflict resolved #
      ##############################################
      pr_updater = Dependabot::PullRequestUpdater.new(
        source: $source,
        base_commit: commit,
        old_commit: conflict_pull_request_commit_id,
        files: updated_files,
        credentials: $options[:credentials],
        pull_request_number: conflict_pull_request_id,
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
        # assignees: assignees,
        author_details: {
          email: "noreply@github.com",
          name: "dependabot[bot]"
        },
        label_language: true,
        provider_metadata: {
          work_item: $options[:work_item_id],
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

    if $options[:auto_approve_pr]
      puts "Auto Approving PR for user #{$options[:auto_approve_user_email]}"

      if not $options[:auto_approve_user_token]
        puts "No dedicated token set for auto approve - using regular Access Token"
        $options[:auto_approve_user_token] = ENV["AZURE_ACCESS_TOKEN"]
      end

      azure_client.pull_request_approve(
        pull_request_id,
        $options[:auto_approve_user_email],
        $options[:auto_approve_user_token]
      )
    end

    # Set auto complete for this Pull Request
    # Pull requests that pass all policies will be merged automatically.
    if $options[:set_auto_complete]
      auto_complete_user_id = pull_request["createdBy"]["id"]
      merge_strategy = $options[:merge_strategy]
      puts "Setting auto complete on ##{pull_request_id}."
      azure_client.pull_request_auto_complete(pull_request_id, auto_complete_user_id, merge_strategy)
    end

  rescue StandardError => e
    raise e if $options[:fail_on_exception]
    puts "Error updating #{dep.name} from #{dep.version} to #{checker.latest_version} (continuing)"
    puts e.full_message
  end
end

puts "Done"
