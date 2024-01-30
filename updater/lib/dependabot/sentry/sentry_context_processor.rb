# typed: strict
# frozen_string_literal: true

require "sorbet-runtime"

require "dependabot/sentry/processor"

class SentryContext < ::Dependabot::Sentry::Processor
  sig do
    override
      .params(
        event: Sentry::Event,
        hint: T::Hash[Symbol, T.untyped]
      )
      .returns(Sentry::Event)
  end
  def process(event, hint)
    if (exception = hint[:exception])
      exception.raven_context.each do |key, value|
        # rubocop:disable Performance/StringIdentifierArgument
        event.send("#{key}=", value)
        # rubocop:enable Performance/StringIdentifierArgument
      end
    end
    event
  end
end
