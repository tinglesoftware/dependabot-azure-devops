# typed: strict
# frozen_string_literal: true

# src: https://github.com/dependabot/dependabot-core/blob/8441dbad1bb13149f897cdbe92c11d36f98c8248/common/lib/dependabot/pull_request_creator/pr_name_prefixer.rb

#
# This module overrides the dependabot-core PR name prefixer with our own implementation that allows setting the styler.
# Without this override, there is no way for the end-user to explicitly set the PR name prefix styler implementation.
#

# TODO: Remove this if/when dependabot makes this user-configurable or removes this feature entirely.

require "dependabot/pull_request_creator"

module Dependabot
  class PullRequestCreator
    class PrNamePrefixer
      def using_angular_commit_messages?
        ENV.fetch("DEPENDABOT_PR_NAME_PREFIX_STYLE", nil) == "angular"
      end

      def using_eslint_commit_messages?
        ENV.fetch("DEPENDABOT_PR_NAME_PREFIX_STYLE", nil) == "eslint"
      end

      def using_gitmoji_commit_messages?
        ENV.fetch("DEPENDABOT_PR_NAME_PREFIX_STYLE", nil) == "gitmoji"
      end
    end
  end
end
