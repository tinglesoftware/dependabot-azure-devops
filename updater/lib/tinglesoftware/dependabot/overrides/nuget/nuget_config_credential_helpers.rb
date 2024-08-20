# typed: strong
# frozen_string_literal: true

# src: https://github.com/dependabot/dependabot-core/blob/8441dbad1bb13149f897cdbe92c11d36f98c8248/nuget/lib/dependabot/nuget/nuget_config_credential_helpers.rb

#
# This module overrides the dependabot-core nuget.config credential helper with our own implementation that fixes:
#  - https://github.com/tinglesoftware/dependabot-azure-devops/issues/1243
#
# Without this override, updates via MSBuild/NuGet.exe to .NET Framework projects (i.e. packages.config projects)
# fail to authenticate with private NuGet feeds when:
#  - The NuGet feed is hosted in Azure DevOps; The password is invalid if token notation is "PAT:12345"
#  - The NuGet feed is secured with basic auth (e.g. nuget.telerik.com); The username was hardcoded to "user"
#  - The source repository already contains a nuget.config file; All feed config and package source mappings are ignored
#
# This module primarily fixes auth in .NET Framework projects; .NET [Core] projects auth is handled differently.
# See: tinglesoftware/azure/artifacts_credential_provider.rb for more info.
#
# This credential provider is required for ALL private NuGet feeds, even if they are not hosted in Azure DevOps.
# See README.md (Credentials for private registries and feeds) for more details.
#

# TODO: Remove this once auth can be moved out to the "proxy" component, like dependabot-cli does

module Dependabot
  module Nuget
    module NuGetConfigCredentialHelpers
      def self.add_credentials_to_nuget_config(credentials)
        return unless File.exist?(user_nuget_config_path)

        nuget_credentials = credentials.select { |cred| cred["type"] == "nuget_feed" }
        return if nuget_credentials.empty?

        # When updating via MSBuild/NuGet.exe, dependabot temporarily overrides $HOME/.nuget/NuGet/NuGet.Config.
        # The idea here is that we should only add missing package sources and missing credentials.

        File.rename(user_nuget_config_path, temporary_nuget_config_path)
        File.write(
          user_nuget_config_path,
          <<~NUGET_XML
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                #{package_sources_xml_lines(nuget_credentials).join("\n    ").strip}
              </packageSources>
              <packageSourceCredentials>
                #{package_source_credentials_xml_lines(nuget_credentials).join("\n    ").strip}
              </packageSourceCredentials>
            </configuration>
          NUGET_XML
        )
      end

      def self.package_sources_xml_lines(credentials)
        credentials.each_with_index.filter_map do |c, i|
          # Ignore package sources that have a key (name), as these are already defined in the user's nuget.config file.
          # Ensures that package source ordering and package source mappings in the user's nuget.config are respected.
          next if c["key"]

          "<add key=\"nuget_source_#{i + 1}\" value=\"#{c['url']}\" />"
        end
      end

      def self.package_source_credentials_xml_lines(credentials) # rubocop:disable Metrics/PerceivedComplexity
        credentials.each_with_index.flat_map do |c, i|
          # Ignore public package sources, no credentials required
          next unless c["token"] || c["username"] || c["password"]

          # Use the package source key (name) if provided, otherwise fallback to a auto-generated key.
          # We want preserve the package source key (name) if it is already defined in the user's nuget.config file.
          # This ensures that package source mappings in the user's nuget.config are respected.
          source_key = c["key"] || "nuget_source_#{i + 1}"
          # Use username/password auth if provided, otherwise fallback to token auth.
          # This provides maximum compatibility with Azure DevOps, DevOps Server, and other third-party feeds.
          # When using DevOps PATs, the token is split into username/password parts; Username is not significant.
          # e.g. token "PAT:12345" --> { "username": "PAT", "password": "12345" }
          #            ":12345"    --> { "username": "", "password": "12345" }
          #            "12345"     --> { "username": "12345", "password": "12345" } # username gets redacted to "user"
          source_username = c["username"] || c["token"]&.split(":")&.first
          source_password = c["password"] || c["token"]&.split(":")&.last
          # NuGet.exe will log the username in plain text to the console, which is not great for security!
          # If the username and password are the same value, we can assume that "token" auth is being used and that the
          # username is not significant, so redact it to something generic to avoid leaking sensitive information.
          # e.g. { "username": "12345", "password": "12345" } --> { "username": "user", "password": "12345" }
          source_username = "user" if source_username == source_password
          [
            "<#{source_key}>",
            "  <add key=\"Username\" value=\"#{source_username}\" />",
            "  <add key=\"ClearTextPassword\" value=\"#{source_password}\" />",
            "</#{source_key}>"
          ]
        end
      end
    end
  end
end
