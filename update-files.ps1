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
    ".rubocop.yml"
    ".rubocop_todo.yml"
    "Rakefile"
    "updater/.rubocop.yml"

    "updater/bin/fetch_files.rb"
    "updater/bin/update_files.rb"

    # "updater/config/.npmrc"
    # "updater/config/.yarnrc"

    "updater/lib/dependabot/logger/formats.rb"
    "updater/lib/dependabot/sentry/exception_sanitizer_processor.rb"
    "updater/lib/dependabot/sentry/processor.rb"
    "updater/lib/dependabot/sentry/sentry_context_processor.rb"
    "updater/lib/dependabot/sorbet/runtime.rb"
    "updater/lib/dependabot/updater/operations/create_group_update_pull_request.rb"
    "updater/lib/dependabot/updater/operations/create_security_update_pull_request.rb"
    "updater/lib/dependabot/updater/operations/group_update_all_versions.rb"
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
    "updater/lib/dependabot/notices_helpers.rb"
    "updater/lib/dependabot/opentelemetry.rb"
    "updater/lib/dependabot/pull_request.rb"
    "updater/lib/dependabot/sentry.rb"
    "updater/lib/dependabot/service.rb"
    "updater/lib/dependabot/setup.rb"
    "updater/lib/dependabot/update_files_command.rb"
    "updater/lib/dependabot/updater.rb"

    "updater/spec/dependabot/sentry/exception_sanitizer_processor_spec.rb"
    "updater/spec/dependabot/sentry/sentry_context_processor_spec.rb"
    # "updater/spec/dependabot/updater/operations/refresh_group_update_pull_request_spec.rb"
    "updater/spec/dependabot/updater/dependency_group_change_batch_spec.rb"
    "updater/spec/dependabot/updater/error_handler_spec.rb"
    "updater/spec/dependabot/updater/operations_spec.rb"
    "updater/spec/dependabot/api_client_spec.rb"
    # "updater/spec/dependabot/dependency_change_builder_spec.rb"
    "updater/spec/dependabot/dependency_change_spec.rb"
    "updater/spec/dependabot/dependency_group_engine_spec.rb"
    # "updater/spec/dependabot/dependency_snapshot_spec.rb"
    "updater/spec/dependabot/environment_spec.rb"
    "updater/spec/dependabot/file_fetcher_command_spec.rb"
    "updater/spec/dependabot/job_spec.rb"
    "updater/spec/dependabot/service_spec.rb"
    "updater/spec/dependabot/update_files_command_spec.rb"
    # "updater/spec/dependabot/updater_spec.rb"

    "updater/spec/fixtures/handle_error.json"
    "updater/spec/fixtures/rubygems-index"
    "updater/spec/fixtures/rubygems-info-a"
    "updater/spec/fixtures/rubygems-info-b"
    "updater/spec/fixtures/rubygems-versions-a.json"
    "updater/spec/fixtures/rubygems-versions-b.json"
    "updater/spec/fixtures/bundler/original/Gemfile"
    "updater/spec/fixtures/bundler/original/Gemfile.lock"
    "updater/spec/fixtures/bundler/original/sub_dep"
    "updater/spec/fixtures/bundler/original/sub_dep.lock"
    "updater/spec/fixtures/bundler/updated/Gemfile"
    "updater/spec/fixtures/bundler/updated/Gemfile.lock"
    "updater/spec/fixtures/bundler2/original/Gemfile"
    "updater/spec/fixtures/bundler2/original/Gemfile.lock"
    "updater/spec/fixtures/bundler2/updated/Gemfile"
    "updater/spec/fixtures/bundler2/updated/Gemfile.lock"
    "updater/spec/fixtures/bundler_gemspec/original/Gemfile"
    "updater/spec/fixtures/bundler_gemspec/original/Gemfile.lock"
    "updater/spec/fixtures/bundler_gemspec/original/library.gemspec"
    "updater/spec/fixtures/bundler_gemspec/updated/Gemfile"
    "updater/spec/fixtures/bundler_gemspec/updated/Gemfile.lock"
    "updater/spec/fixtures/bundler_gemspec/updated/library.gemspec"
    "updater/spec/fixtures/bundler_grouped/original/Gemfile"
    "updater/spec/fixtures/bundler_grouped/original/Gemfile.lock"
    "updater/spec/fixtures/bundler_vendored/original/Gemfile"
    "updater/spec/fixtures/bundler_vendored/original/Gemfile.lock"
    "updater/spec/fixtures/bundler_vendored/original/vendor/cache/dummy-pkg-a-2.0.0.gem"
    "updater/spec/fixtures/bundler_vendored/original/vendor/cache/dummy-pkg-b-1.1.0.gem"
    "updater/spec/fixtures/bundler_vendored/original/vendor/cache/ruby-dummy-git-dependency-20151f9b67c8/.bundlecache"
    "updater/spec/fixtures/bundler_vendored/original/vendor/cache/ruby-dummy-git-dependency-20151f9b67c8/dummy-git-dependency.gemspec"
    "updater/spec/fixtures/bundler_vendored/updated/Gemfile"
    "updater/spec/fixtures/bundler_vendored/updated/Gemfile.lock"
    "updater/spec/fixtures/bundler_vendored/updated/.bundle/config"
    "updater/spec/fixtures/bundler_vendored/updated/vendor/cache/ruby-dummy-git-dependency-c0e25c2eb332/.bundlecache"
    "updater/spec/fixtures/bundler_vendored/updated/vendor/cache/ruby-dummy-git-dependency-c0e25c2eb332/dummy-git-dependency.gemspec"
    "updater/spec/fixtures/bundler_vendored/updated/vendor/cache/dummy-pkg-a-2.0.0.gem"
    "updater/spec/fixtures/bundler_vendored/updated/vendor/cache/dummy-pkg-b-1.2.0.gem"
    "updater/spec/fixtures/composer/original/composer.json"
    "updater/spec/fixtures/composer/original/composer.lock"
    "updater/spec/fixtures/composer/updated/composer.json"
    "updater/spec/fixtures/composer/updated/composer.lock"
    "updater/spec/fixtures/dummy/original/a.dummy"
    "updater/spec/fixtures/dummy/original/b.dummy"
    "updater/spec/fixtures/file_fetcher_output/output.json"
    "updater/spec/fixtures/file_fetcher_output/vendoring_output.json"
    "updater/spec/fixtures/jobs/job_with_credentials.json"
    "updater/spec/fixtures/jobs/job_with_dummy.json"
    "updater/spec/fixtures/jobs/job_with_vendor_dependencies.json"
    "updater/spec/fixtures/jobs/job_without_credentials.json"
    "updater/spec/fixtures/job_definitions/bundler/security_updates/group_update_multi_dir.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh_dependencies_changed.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh_empty_group.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh_missing_group.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh_similar_pr.yaml"
    "updater/spec/fixtures/job_definitions/bundler/version_updates/group_update_refresh_versions_changed.yaml"
    "updater/spec/fixtures/job_definitions/dummy/version_updates/group_update_peer_manifests.yaml"
    "updater/spec/fixtures/job_definitions/README.md"
    "updater/spec/fixtures/vcr_cassettes/Dependabot_FileFetcherCommand/_perform_job/when_the_connectivity_check_is_enabled/when_connectivity_is_broken/logs_connectivity_failed_and_does_not_raise_an_error.yml"
    "updater/spec/fixtures/vcr_cassettes/Dependabot_FileFetcherCommand/_perform_job/when_the_connectivity_check_is_enabled/logs_connectivity_is_successful_and_does_not_raise_an_error.yml"
    "updater/spec/fixtures/vcr_cassettes/Dependabot_FileFetcherCommand/_perform_job/does_not_clone_the_repo.yml"
    "updater/spec/fixtures/vcr_cassettes/Dependabot_FileFetcherCommand/_perform_job/fetches_the_files_and_writes_the_fetched_files_to_output_json.yml"
    "updater/spec/fixtures/vcr_cassettes/Dependabot_Updater_Operations_GroupUpdateAllVersions/when_the_snapshot_contains_a_git_dependency/creates_individual_PRs_since_git_dependencies_cannot_be_grouped_as_semver.yml"
    "updater/spec/fixtures/vcr_cassettes/Dependabot_Updater_Operations_GroupUpdateAllVersions/when_the_snapshot_is_updating_a_gemspec/creates_a_DependencyChange_for_just_the_modified_files_without_reporting_errors.yml"
    "updater/spec/fixtures/vcr_cassettes/Dependabot_Updater_Operations_GroupUpdateAllVersions/when_the_snapshot_is_updating_vendored_dependencies/creates_a_pull_request_that_includes_changes_to_the_vendored_files.yml"

    "updater/spec/support/dummy_package_manager/dummy.rb"
    "updater/spec/support/dummy_package_manager/file_fetcher.rb"
    "updater/spec/support/dummy_package_manager/file_parser.rb"
    "updater/spec/support/dummy_package_manager/file_updater.rb"
    "updater/spec/support/dummy_package_manager/requirement.rb"
    "updater/spec/support/dummy_package_manager/update_checker.rb"
    "updater/spec/support/dummy_package_manager/version.rb"
    "updater/spec/support/dependency_file_helpers.rb"
    "updater/spec/support/dummy_pkg_helpers.rb"

    "updater/spec/spec_helper.rb"
)

# Download each file listed
$baseUrl = "https://raw.githubusercontent.com/dependabot/dependabot-core"
foreach ($name in $files) {
    $sourceUrl = "$baseUrl/v$version/$($name)"
    $destinationPath = Join-Path -Path '.' -ChildPath "$name"

    Write-Host "`Downloading $name ..."

    # [System.IO.Directory]::CreateDirectory("$(Split-Path -Path "$destinationPath")") | Out-Null
    # Invoke-WebRequest -Uri $sourceUrl -OutFile $destinationPath

    mkdir -p "$(dirname "$destinationPath")"
    curl -sL "$sourceUrl" -o "$destinationPath"
}
