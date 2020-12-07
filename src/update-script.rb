require "dependabot/file_fetchers"
require "dependabot/file_parsers"
require "dependabot/update_checkers"
require "dependabot/file_updaters"
require "dependabot/pull_request_creator"
require "dependabot/omnibus"

# Full name of the GitHub repo you want to create pull requests for.
repo_name = "#{ENV["ORGANIZATION"]}/#{ENV["PROJECT"]}/_git/#{ENV["REPOSITORY"]}"

# Directory where the base dependency files are.
directory = ENV["DIRECTORY"] || "/"

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

credentials = [{
  "type" => "git_source",
  "host" => "dev.azure.com",
  "username" => "x-access-token",
  "password" => ENV["SYSTEM_ACCESSTOKEN"]
}]

##############################
# Add GitHub Access TOken (PAT) to avoid rate limiting #
##############################
if ENV["GITHUB_ACCESS_TOKEN"]
  credentials << {
    "type" => "git_source",
    "host" => "github.com",
    "username" => "x-access-token",
    "password" => ENV["GITHUB_ACCESS_TOKEN"] # A GitHub access token with read access to public repos
  }
end

if ENV["PRIVATE_FEED_NAME"]
  if package_manager == "nuget"
    # Adding custom private feed removes the public onces so we have to create it
    credentials << {
      "type" => "nuget_feed",
      "url" => "https://api.nuget.org/v3/index.json",
    }

    url = "https://pkgs.dev.azure.com/#{ENV["ORGANIZATION"]}/_packaging/#{ENV["PRIVATE_FEED_NAME"]}/nuget/v3/index.json"
    puts "Adding private NuGet feed '#{url}'"
    credentials << {
      "type" => "nuget_feed",
      "url" => url,
      "token" => ":#{ENV["SYSTEM_ACCESSTOKEN"]}", # do not forget the colon
    }
  elsif package_manager == "gradle" || package_manager == "maven"
    url = "https://pkgs.dev.azure.com/#{ENV["ORGANIZATION"]}/_packaging/#{ENV["PRIVATE_FEED_NAME"]}/maven/v1"
    puts "Adding private Maven repository '#{url}'"
    credentials << {
      "type" => "maven_repository",
      "url" => url,
      "username" => "#{ENV["ORGANIZATION"]}",
      "password" => "#{ENV["SYSTEM_ACCESSTOKEN"]}"
    }
  elsif package_manager == "npm_and_yarn"
    url = "pkgs.dev.azure.com/#{ENV["ORGANIZATION"]}/_packaging/#{ENV["PRIVATE_FEED_NAME"]}/npm/registry/"
    puts "Adding private npm registry '#{url}'"
    credentials << {
      "type" => "npm_registry",
      "registry" => url,
      "token" => "#{ENV["PRIVATE_FEED_NAME"]}:#{ENV["SYSTEM_ACCESSTOKEN"]}"
    }
  end
end

source = Dependabot::Source.new(
  provider: "azure",
  hostname: "dev.azure.com",
  api_endpoint: "https://dev.azure.com/",
  repo: repo_name,
  directory: directory,
  branch: ENV["TARGET_BRANCH"] || nil,
)

##############################
# Fetch the dependency files #
##############################
puts "Fetching #{package_manager} dependency files for #{repo_name}"
fetcher = Dependabot::FileFetchers.for_package_manager(package_manager).new(
  source: source,
  credentials: credentials,
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
)

dependencies = parser.parse

dependencies.select(&:top_level?).each do |dep|
  #########################################
  # Get update details for the dependency #
  #########################################
  puts "Checking if #{dep.name} #{dep.version} needs updating"

  checker = Dependabot::UpdateCheckers.for_package_manager(package_manager).new(
    dependency: dep,
    dependency_files: files,
    credentials: credentials,
  )

  if checker.up_to_date?
    puts "No update needed for #{dep.name} #{dep.version}"
    next
  end

  requirements_to_unlock =
    if !checker.requirements_unlocked_or_can_be?
      if checker.can_update?(requirements_to_unlock: :none) then :none
      else :update_not_possible
      end
    elsif checker.can_update?(requirements_to_unlock: :own) then :own
    elsif checker.can_update?(requirements_to_unlock: :all) then :all
    else :update_not_possible
    end

  puts "Requirements to unlock #{requirements_to_unlock}"
  next if requirements_to_unlock == :update_not_possible

  updated_deps = checker.updated_dependencies(
    requirements_to_unlock: requirements_to_unlock
  )

  #####################################
  # Generate updated dependency files #
  #####################################
  puts "Updating #{dep.name} from #{dep.version} to #{checker.latest_version}"
  updater = Dependabot::FileUpdaters.for_package_manager(package_manager).new(
    dependencies: updated_deps,
    dependency_files: files,
    credentials: credentials,
  )

  updated_files = updater.updated_dependency_files

  ########################################
  # Create a pull request for the update #
  ########################################
  pr_creator = Dependabot::PullRequestCreator.new(
    source: source,
    base_commit: commit,
    dependencies: updated_deps,
    files: updated_files,
    credentials: credentials,
    label_language: true,
    author_details: {
      email: "noreply@github.com",
      name: "dependabot[bot]"
    },
  )

  print "Submitting #{dep.name} pull request for creation. "
  pull_request = pr_creator.create

  if pull_request
    content = JSON[pull_request.body]
    if pull_request&.status == 201
      puts "Done (PR ##{content["pullRequestId"]})"
    else
      puts "Failed! PR already exists or an error has occurred."
      puts "Status: #{pull_request&.status}."
      puts "Message #{content["message"]}"
    end
  else
    puts "Seems PR is already present."
  end

  next unless pull_request

end

puts "Done"