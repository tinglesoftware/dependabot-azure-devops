# typed: strict
# frozen_string_literal: true

require "dependabot/job"

#
# Represents a Dependabot job; A single unit of work that Dependabot can perform (e.g. "update all dependencies").
# This class contains all the user configuration needed to perform the job.
#
module TingleSoftware
  module Dependabot
    class Job < ::Dependabot::Job
      extend T::Sig

      attr_reader :azure_client

      attr_reader :pr_author_name

      attr_reader :pr_author_email

      attr_reader :pr_message_header

      attr_reader :pr_message_footer

      attr_reader :pr_custom_labels

      def initialize(attributes, azure_client)
        @azure_client = azure_client
        @pr_author_name = T.let(attributes.fetch(:pr_author_name, "dependabot[bot]"), T.nilable(String))
        @pr_author_email = T.let(attributes.fetch(:pr_author_email, "noreply@github.com"), T.nilable(String))
        @pr_message_header = T.let(attributes.fetch(:pr_message_header, nil), T.nilable(String))
        @pr_message_footer = T.let(attributes.fetch(:pr_message_footer, nil), T.nilable(String))
        @pr_custom_labels = T.let(attributes.fetch(:pr_message_footer, nil), T.nilable(T::Array[String]))
        super(attributes)
      end
    end
  end
end
