# This script is designed to loop through all dependencies in a GHE, GitLab or
# Azure DevOps project, creating PRs where necessary.

require "dependabot/file_fetchers"
require "dependabot/file_parsers"
require "dependabot/update_checkers"
require "dependabot/file_updaters"
require "dependabot/pull_request_creator"
require "dependabot/omnibus"
require "gitlab"
require "json"

credentials = [
  {
    "type" => "git_source",
    "host" => "github.com",
    "username" => "x-access-token",
    "password" => ENV["GITHUB_ACCESS_TOKEN"] # A GitHub access token with read access to public repos
  }
]
Excon.defaults[:ssl_verify_peer] = false
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
  milestone: ENV["DEPENDABOT_MILESTONE"] || nil, # Get the work item to attach
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
  excluded_requirements: ENV["DEPENDABOT_EXCLUDE_REQUIREMENTS_TO_UNLOCK"]&.split(" ")&.map(&:to_sym) || [],

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


# Full name of the repo you want to create pull requests for.
repo_name = "#{$options[:azure_organization]}/#{$options[:azure_project]}/_git/#{$options[:azure_repository]}"
# repo_name = ENV["PROJECT_PATH"] # namespace/project

# Directory where the base dependency files are.
directory = ENV["DIRECTORY_PATH"] || "/"

# Branch to look at. Defaults to repo's default branch
branch = ENV["BRANCH"]

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
package_manager = ENV["PACKAGE_MANAGER"] || "bundler"

# Expected to be a JSON object passed to the underlying components
options = JSON.parse(ENV["OPTIONS"] || "{}", {:symbolize_names => true})
puts "Running with options: #{options}"

if ENV["GITHUB_ENTERPRISE_ACCESS_TOKEN"]
  credentials << {
    "type" => "git_source",
    "host" => ENV["GITHUB_ENTERPRISE_HOSTNAME"], # E.g., "ghe.mydomain.com",
    "username" => "x-access-token",
    "password" => ENV["GITHUB_ENTERPRISE_ACCESS_TOKEN"] # A GHE access token with API permission
  }

  source = Dependabot::Source.new(
    provider: "github",
    hostname: ENV["GITHUB_ENTERPRISE_HOSTNAME"],
    api_endpoint: "https://#{ENV['GITHUB_ENTERPRISE_HOSTNAME']}/api/v3/",
    repo: repo_name,
    directory: directory,
    branch: branch,
  )
elsif ENV["GITLAB_ACCESS_TOKEN"]
  gitlab_hostname = ENV["GITLAB_HOSTNAME"] || "gitlab.com"

  credentials << {
    "type" => "git_source",
    "host" => gitlab_hostname,
    "username" => "x-access-token",
    "password" => ENV["GITLAB_ACCESS_TOKEN"] # A GitLab access token with API permission
  }

  source = Dependabot::Source.new(
    provider: "gitlab",
    hostname: gitlab_hostname,
    api_endpoint: "https://#{gitlab_hostname}/api/v4",
    repo: repo_name,
    directory: directory,
    branch: branch,
  )
elsif ENV["AZURE_ACCESS_TOKEN"]
  azure_hostname = ENV["AZURE_HOSTNAME"] || "dev.azure.com"

  credentials << {
    "type" => "git_source",
    "host" => azure_hostname,
    "username" => "x-access-token",
    "password" => ENV["AZURE_ACCESS_TOKEN"]
  }

  source = Dependabot::Source.new(
    provider: "azure",
    hostname: azure_hostname,
    api_endpoint: "https://#{azure_hostname}/",
    repo: repo_name,
    directory: directory,
    branch: branch,
  )
elsif ENV["BITBUCKET_ACCESS_TOKEN"]
  bitbucket_hostname = ENV["BITBUCKET_HOSTNAME"] || "bitbucket.org"

  credentials << {
    "type" => "git_source",
    "host" => bitbucket_hostname,
    "username" => "x-token-auth",
    "token" => ENV["BITBUCKET_ACCESS_TOKEN"]
  }

  source = Dependabot::Source.new(
    provider: "bitbucket",
    hostname: bitbucket_hostname,
    api_endpoint: ENV["BITBUCKET_API_URL"] || "https://api.bitbucket.org/2.0/",
    repo: repo_name,
    directory: directory,
    branch: branch,
  )
elsif ENV["BITBUCKET_APP_USERNAME"] && ENV["BITBUCKET_APP_PASSWORD"]
  bitbucket_hostname = ENV["BITBUCKET_HOSTNAME"] || "bitbucket.org"

  credentials << {
    "type" => "git_source",
    "host" => bitbucket_hostname,
    "username" => ENV["BITBUCKET_APP_USERNAME"],
    "password" => ENV["BITBUCKET_APP_PASSWORD"]
  }

  source = Dependabot::Source.new(
    provider: "bitbucket",
    hostname: bitbucket_hostname,
    api_endpoint: ENV["BITBUCKET_API_URL"] || "https://api.bitbucket.org/2.0/",
    repo: repo_name,
    directory: directory,
    branch: branch,
  )
else
  source = Dependabot::Source.new(
    provider: "github",
    repo: repo_name,
    directory: directory,
    branch: branch,
  )
end

##############################
# Fetch the dependency files #
##############################
puts "Fetching #{package_manager} dependency files for #{repo_name}"
fetcher = Dependabot::FileFetchers.for_package_manager(package_manager).new(
  source: source,
  credentials: credentials,
  options: options,
)

files = fetcher.files
commit = fetcher.commit

##############################
# Parse the dependency files #
##############################
puts "Parsing dependencies information"
parser = Dependabot::FileParsers.for_package_manager(package_manager).new(
  dependency_files: files,
  source: source,
  credentials: credentials,
  options: options,
)

dependencies = parser.parse

dependencies.select(&:top_level?).each do |dep|
  #########################################
  # Get update details for the dependency #
  #########################################
  checker = Dependabot::UpdateCheckers.for_package_manager(package_manager).new(
    dependency: dep,
    dependency_files: files,
    credentials: credentials,
    options: options,
  )

  next if checker.up_to_date?

  requirements_to_unlock =
    if !checker.requirements_unlocked_or_can_be?
      if checker.can_update?(requirements_to_unlock: :none) then :none
      else :update_not_possible
      end
    elsif checker.can_update?(requirements_to_unlock: :own) then :own
    elsif checker.can_update?(requirements_to_unlock: :all) then :all
    else :update_not_possible
    end

  next if requirements_to_unlock == :update_not_possible

  updated_deps = checker.updated_dependencies(
    requirements_to_unlock: requirements_to_unlock
  )

  #####################################
  # Generate updated dependency files #
  #####################################
  print "  - Updating #{dep.name} (from #{dep.version})…"
  updater = Dependabot::FileUpdaters.for_package_manager(package_manager).new(
    dependencies: updated_deps,
    dependency_files: files,
    credentials: credentials,
    options: options,
  )

  updated_files = updater.updated_dependency_files

  ########################################
  # Create a pull request for the update #
  ########################################
  assignee = (ENV["PULL_REQUESTS_ASSIGNEE"] || ENV["GITLAB_ASSIGNEE_ID"])&.to_i
  assignees = assignee ? [assignee] : assignee
  pr_creator = Dependabot::PullRequestCreator.new(
    source: source,
    base_commit: commit,
    dependencies: updated_deps,
    files: updated_files,
    credentials: credentials,
    assignees: assignees,
    author_details: { name: "Dependabot", email: "no-reply@github.com" },
    label_language: true,
  )
  pull_request = pr_creator.create
  puts " submitted"

  next unless pull_request

  # Enable GitLab "merge when pipeline succeeds" feature.
  # Merge requests created and successfully tested will be merge automatically.
  if ENV["GITLAB_AUTO_MERGE"]
    g = Gitlab.client(
      endpoint: source.api_endpoint,
      private_token: ENV["GITLAB_ACCESS_TOKEN"]
    )
    g.accept_merge_request(
      source.repo,
      pull_request.iid,
      merge_when_pipeline_succeeds: true,
      should_remove_source_branch: true
    )
  end
end

puts "Done"
