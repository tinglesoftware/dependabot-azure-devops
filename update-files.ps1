# Find the current version for dependabot-omnibus
$gemfileContent = Get-Content -Path "updater/Gemfile" -Raw
$versionLine = $gemfileContent | Select-String 'gem "dependabot-omnibus", "(.*)"' | Select-Object -ExpandProperty Line
$version = [regex]::Match($versionLine, '"~>(\d+\.\d+\.\d+)"').Groups[1].Value
Write-Output "Found dependabot-omnibus version: $version"

# # Update the version in the Dockerfile
# $dockerfile = Get-Content -Path "updater/Dockerfile" -Raw
# $dockerfile = ($dockerfile -replace '(?<=ARG DEPENDABOT_VERSION=)(\d+\.\d+\.\d+)', $version).Trim()
# $dockerfile | Set-Content -Path "updater/Dockerfile"

# Prepare the list of files to be downloaded
$files = @(
    ".ruby-version"
    # ".rubocop.yml"
    # "Rakefile"
    "updater/.rubocop.yml"

    "updater/bin/fetch_files.rb"
    "updater/bin/update_files.rb"

    "updater/config/.npmrc"
    "updater/config/.yarnrc"

    "updater/lib/dependabot/logger/formats.rb"
    "updater/lib/dependabot/updater/operations/create_group_security_update_pull_request.rb"
    "updater/lib/dependabot/updater/operations/create_group_update_pull_request.rb"
    "updater/lib/dependabot/updater/operations/create_security_update_pull_request.rb"
    "updater/lib/dependabot/updater/operations/group_update_all_versions.rb"
    "updater/lib/dependabot/updater/operations/refresh_group_security_update_pull_request.rb"
    "updater/lib/dependabot/updater/operations/refresh_group_update_pull_request.rb"
    "updater/lib/dependabot/updater/operations/refresh_security_update_pull_request.rb"
    "updater/lib/dependabot/updater/operations/refresh_version_update_pull_request.rb"
    "updater/lib/dependabot/updater/operations/update_all_versions.rb"
    "updater/lib/dependabot/updater/dependency_group_change_batch.rb"
    "updater/lib/dependabot/updater/error_handler.rb"
    "updater/lib/dependabot/updater/errors.rb"
    "updater/lib/dependabot/updater/group_update_creation.rb"
    "updater/lib/dependabot/updater/group_update_refreshing.rb"
    "updater/lib/dependabot/updater/operations.rb"
    "updater/lib/dependabot/updater/security_update_helpers.rb"
    "updater/lib/dependabot/api_client.rb"
    "updater/lib/dependabot/base_command.rb"
    "updater/lib/dependabot/dependency_change_builder.rb"
    "updater/lib/dependabot/dependency_change.rb"
    "updater/lib/dependabot/dependency_group_engine.rb"
    "updater/lib/dependabot/dependency_snapshot.rb"
    "updater/lib/dependabot/environment.rb"
    "updater/lib/dependabot/file_fetcher_command.rb"
    "updater/lib/dependabot/job.rb"
    "updater/lib/dependabot/opentelemetry.rb"
    "updater/lib/dependabot/sentry.rb"
    "updater/lib/dependabot/service.rb"
    "updater/lib/dependabot/setup.rb"
    "updater/lib/dependabot/update_files_command.rb"
    "updater/lib/dependabot/updater.rb"

    # "updater/spec/dependabot/updater/operations/group_update_all_versions_spec.rb"
    # "updater/spec/dependabot/updater/operations/refresh_group_update_pull_request_spec.rb"
    # "updater/spec/dependabot/updater/operations/update_all_versions_spec.rb"
    "updater/spec/dependabot/updater/dependency_group_change_batch_spec.rb"
    "updater/spec/dependabot/updater/error_handler_spec.rb"
    "updater/spec/dependabot/updater/operations_spec.rb"
    "updater/spec/dependabot/api_client_spec.rb"
    # "updater/spec/dependabot/dependency_change_builder_spec.rb"
    "updater/spec/dependabot/dependency_change_spec.rb"
    "updater/spec/dependabot/dependency_group_engine_spec.rb"
    # "updater/spec/dependabot/dependency_snapshot_spec.rb"
    "updater/spec/dependabot/environment_spec.rb"
    # "updater/spec/dependabot/file_fetcher_command_spec.rb"
    # "updater/spec/dependabot/integration_spec.rb"
    "updater/spec/dependabot/job_spec.rb"
    "updater/spec/dependabot/sentry_spec.rb"
    "updater/spec/dependabot/service_spec.rb"
    # "updater/spec/dependabot/update_files_command_spec.rb"
    # "updater/spec/dependabot/updater_spec.rb"

    "updater/spec/fixtures/rubygems-index"
    "updater/spec/fixtures/rubygems-info-a"
    "updater/spec/fixtures/rubygems-versions-a.json"
    "updater/spec/fixtures/rubygems-info-b"
    "updater/spec/fixtures/rubygems-versions-b.json"
    "updater/spec/fixtures/bundler/original/Gemfile"
    "updater/spec/fixtures/bundler/original/Gemfile.lock"
    "updater/spec/fixtures/bundler/updated/Gemfile"
    "updater/spec/fixtures/bundler/updated/Gemfile.lock"
    "updater/spec/fixtures/bundler_gemspec/original/Gemfile"
    "updater/spec/fixtures/bundler_gemspec/original/Gemfile.lock"
    "updater/spec/fixtures/bundler_gemspec/original/library.gemspec"
    "updater/spec/fixtures/bundler_git/original/Gemfile"
    "updater/spec/fixtures/bundler_git/original/Gemfile.lock"
    "updater/spec/fixtures/bundler_grouped_by_types/original/Gemfile"
    "updater/spec/fixtures/bundler_grouped_by_types/original/Gemfile.lock"
    "updater/spec/fixtures/bundler_vendored/original/Gemfile"
    "updater/spec/fixtures/bundler_vendored/original/Gemfile.lock"
    "updater/spec/fixtures/docker/original/Dockerfile.bundler"
    "updater/spec/fixtures/docker/original/Dockerfile.cargo"
    "updater/spec/fixtures/jobs/job_with_credentials.json"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_all.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_all_by_dependency_type.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_all_empty_group.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_all_overlapping_groups.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_all_with_existing_pr.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_all_with_ungrouped.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_all_with_vendoring.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_all_semver_grouping_with_global_ignores.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_all_semver_grouping.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh_dependencies_changed.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh_empty_group.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh_missing_group.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh_versions_changed.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh_similar_pr.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/update_all_simple.yaml"
    "updater/spec/fixtures/job_definitions/docker/version_updates/group_update_peer_manifests.yaml"
    "updater/spec/fixtures/job_definitions/README.md"

    "updater/spec/support/dependency_file_helpers.rb"
    "updater/spec/support/dummy_pkg_helpers.rb"

    # "updater/spec/spec_helper.rb"
)

# Download each file listed
$baseUrl = "https://raw.githubusercontent.com/dependabot/dependabot-core"
foreach ($name in $files) {
    $sourceUrl = "$baseUrl/v$version/$($name)"
    $destinationPath = Join-Path -Path '.' -ChildPath "$name"

    # Write-Host "`Downloading $name ..."
    # [System.IO.Directory]::CreateDirectory("$(Split-Path -Path "$destinationPath")") | Out-Null
    # Invoke-WebRequest -Uri $sourceUrl -OutFile $destinationPath

    echo "Downloading $($name) ..."
    mkdir -p "$(dirname "$destinationPath")"
    curl -sL "$sourceUrl" -o "$destinationPath"
}
