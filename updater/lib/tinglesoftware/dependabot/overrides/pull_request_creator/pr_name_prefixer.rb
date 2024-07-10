# typed: strict
# frozen_string_literal: true

require "dependabot/pull_request_creator"

module Dependabot
  class PullRequestCreator
    class PrNamePrefixer
      extend T::Sig

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
