# typed: strict
# frozen_string_literal: true

require "sorbet-runtime"

require "dependabot/npm_and_yarn/file_updater"

module Dependabot
  module NpmAndYarn
    class FileUpdater < Dependabot::FileUpdaters::Base
      class NpmrcBuilder
        extend T::Sig

        sig { params(token: String, registry: T.nilable(String)).returns(String) }
        def auth_line(token, registry = nil)
          auth = if token.include?(":")
                   encoded_token = Base64.encode64(token).delete("\n")
                   "_auth=#{encoded_token}"
                 elsif Base64.decode64(token).ascii_only? &&
                       Base64.decode64(token).include?(":")
                   "_auth=#{token.delete("\n")}"
                 else
                   "_authToken=#{token}"
                 end

          return auth unless registry

          # We need to ensure the registry uri ends with a trailing slash in the npmrc file
          # but we do not want to add one if it already exists
          registry_with_trailing_slash = registry.sub(%r{\/?$}, "/")

          # ============================================================================================
          # Remove any protocol prefix from the registry
          # This is required for Azure DevOps, auth fails if "https://" is included in the registry URL
          registry_with_trailing_slash = registry_with_trailing_slash.sub(%r{^https?://}, "")
          # ============================================================================================

          "//#{registry_with_trailing_slash}:#{auth}"
        end
      end
    end
  end
end
