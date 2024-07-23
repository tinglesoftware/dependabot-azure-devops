# typed: strict
# frozen_string_literal: true

require "dependabot/shared_helpers"

module TingleSoftware
  module NuGet
    module CredentialsProvider
      def self.install_if_nuget_feeds_are_configured
        credentials = JSON.parse(ENV.fetch("DEPENDABOT_EXTRA_CREDENTIALS", "[]"))
        private_nuget_feeds = credentials.select { |cred| cred["type"] == "nuget_feed" }
        return if private_nuget_feeds.empty?

        ENV.store(
          "VSS_NUGET_EXTERNAL_FEED_ENDPOINTS",
          JSON.dump({
            "endpointCredentials" => private_nuget_feeds.map do |cred|
              {
                "endpoint" => cred["url"],
                "username" => "unused",
                "password" => cred["token"].delete_prefix("PAT:") # Credentials provider expects the raw token
              }
            end
          })
        )

        puts ::Dependabot::SharedHelpers.run_shell_command(
          "sh -c \"$(curl -fsSL https://aka.ms/install-artifacts-credprovider.sh)\"", allow_unsafe_shell_command: true
        )
      end
    end
  end
end

# Automatically install the NuGet credential provider if any of the "extra credentials" are NuGet feeds.
# Without it, private Azure DevOps NuGet feeds fail to auth and users have to deal with complicated workarounds.
# TODO: Remove this entire file once https://github.com/dependabot/dependabot-core/pull/8927 is resolved.
TingleSoftware::NuGet::CredentialsProvider.install_if_nuget_feeds_are_configured
