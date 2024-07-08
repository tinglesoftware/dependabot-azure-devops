# typed: strict
# frozen_string_literal: true

require "dependabot/job"

module TingleSoftware
  module Dependabot
    class Job < ::Dependabot::Job
      extend T::Sig

      attr_reader :azure_client

      def initialize(attributes, azure_client)
        @azure_client = azure_client
        super(attributes)
      end
    end
  end
end
