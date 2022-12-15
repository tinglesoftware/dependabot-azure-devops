FROM dependabot/dependabot-core:0.215.0

# Copy the Gemfile and Gemfile.lock
ARG CODE_DIR=/home/dependabot/dependabot-script
RUN mkdir -p ${CODE_DIR}
COPY --chown=dependabot:dependabot Gemfile Gemfile.lock ${CODE_DIR}/
WORKDIR ${CODE_DIR}

# Install dependencies
RUN bundle config set --local path "vendor" \
  && bundle install --jobs 4 --retry 3

# Script files are known to change more frequently than Gemfiles.
# They are copied after installation of dependencies so that the
# image layers that change less frequently are available for caching
# and hence be reused in subsequent builds.
# For more information:
# https://docs.docker.com/develop/develop-images/build_enhancements/
# https://testdriven.io/blog/faster-ci-builds-with-docker-cache/

# Copy the Ruby scripts
COPY --chown=dependabot:dependabot update-script.rb ${CODE_DIR}
COPY --chown=dependabot:dependabot azure_helpers.rb ${CODE_DIR}
COPY --chown=dependabot:dependabot vulnerabilities.rb ${CODE_DIR}
COPY --chown=dependabot:dependabot --chmod=755 entrypoint.sh ${CODE_DIR}

# This entrypoint exists to solve specific setup problems.
# It is only used with the extension and directly on Docker.
# Hosted version does not allow this.
#
# If you have trouble when running locally, recreate the file to fix the line endings.
# Ideally should not happen, but it does at times. Oops! This is just to help you, ninja!
ENTRYPOINT ["./entrypoint.sh"]

# Run update script
CMD ["bundle", "exec", "ruby", "./update-script.rb"]
