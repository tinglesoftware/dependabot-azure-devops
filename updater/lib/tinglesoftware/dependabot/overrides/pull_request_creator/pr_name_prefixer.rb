# typed: strict
# frozen_string_literal: true

require "dependabot/pull_request_creator"

module Dependabot
  class PullRequestCreator
    class PrNamePrefixer
      extend T::Sig

      #
      # Dependabot currently does provide an extension point for setting the PR name prefix styler.
      # To work around this, we override the dependabot-core definitions and provide our own implementation.
      #
      # TODO: If/when dependabot make this user-configurable, remove this override.
      #

      sig { returns(T::Boolean) }
      def using_angular_commit_messages?
        ENV.fetch("DEPENDABOT_PR_NAME_PREFIX_STYLE", nil) == "angular"
      end

      sig { returns(T::Boolean) }
      def using_eslint_commit_messages?
        ENV.fetch("DEPENDABOT_PR_NAME_PREFIX_STYLE", nil) == "eslint"
      end

      sig { returns(T::Boolean) }
      def using_gitmoji_commit_messages?
        ENV.fetch("DEPENDABOT_PR_NAME_PREFIX_STYLE", nil) == "gitmoji"
      end
    end
  end
end
