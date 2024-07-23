# typed: strict
# frozen_string_literal: true

require "dependabot/shared_helpers"

#
# This module auto installs the Azure Artifacts Credential Provider if any Azure DevOps NuGet feeds are configured.
# Without it, users have to deal with complicated workarounds to get authentication to work with Dependabot updates.
# See: https://github.com/tinglesoftware/dependabot-azure-devops/pull/1233 for more info.
#

# TODO: Remove this once https://github.com/dependabot/dependabot-core/pull/8927 is resolved or auth works natively.

module TingleSoftware
  module Azure
    module ArtifactsCredentialProvider
      def self.install_if_nuget_feeds_are_configured
        credentials = JSON.parse(ENV.fetch("DEPENDABOT_EXTRA_CREDENTIALS", "[]"))
        private_ado_nuget_feeds = credentials.select do |cred|
          cred["type"] == "nuget_feed" && cred["url"].include?("dev.azure.com")
        end

        return if private_ado_nuget_feeds.empty?

        # Configure NuGet feed authentication
        ENV.store(
          "VSS_NUGET_EXTERNAL_FEED_ENDPOINTS",
          JSON.dump({
            "endpointCredentials" => private_ado_nuget_feeds.map do |cred|
              {
                "endpoint" => cred["url"],
                "username" => "unused",
                "password" => cred["token"].delete_prefix("PAT:") # Credentials provider expects the raw token
              }
            end
          })
        )

        # Install cred provider from https://github.com/microsoft/artifacts-credprovider
        puts ::Dependabot::SharedHelpers.run_shell_command(
          "sh -c \"$(curl -fsSL https://aka.ms/install-artifacts-credprovider.sh)\"", allow_unsafe_shell_command: true
        )
      end
    end
  end
end

TingleSoftware::Azure::ArtifactsCredentialProvider.install_if_nuget_feeds_are_configured
