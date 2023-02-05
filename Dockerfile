# The tagged versions are currently slow (sometimes it takes months)
# We temporarily switch to getting the gem from git.
# When the changes to this repository are no longer many/major,
# we can switch back to using the tagged versions.

# FROM dependabot/dependabot-core:0.215.0
FROM dependabot/dependabot-core@sha256:3681373aeb07e29fdf30c7a03713195424636fd1cafd569c424a96af27d37735

ENV DEPENDABOT_HOME /home/dependabot
WORKDIR ${DEPENDABOT_HOME}

COPY --chown=dependabot:dependabot updater/Gemfile updater/Gemfile.lock dependabot-updater/
COPY --chown=dependabot:dependabot dependabot-core dependabot-core/

WORKDIR $DEPENDABOT_HOME/dependabot-updater

RUN bundle config set --local path 'vendor' && \
    bundle config set --local frozen 'true' && \
    bundle config set --local without 'development' && \
    bundle install

# Project files are known to change more frequently than Gemfiles.
# They are copied after installation of dependencies so that the
# image layers that change less frequently are available for caching
# and hence be reused in subsequent builds.
# For more information:
# https://docs.docker.com/develop/develop-images/build_enhancements/
# https://testdriven.io/blog/faster-ci-builds-with-docker-cache/

# Add project
COPY --chown=dependabot:dependabot LICENSE $DEPENDABOT_HOME
COPY --chown=dependabot:dependabot updater $DEPENDABOT_HOME/dependabot-updater

WORKDIR $DEPENDABOT_HOME/dependabot-updater

# This entrypoint exists to solve specific setup problems.
# It is only used with the extension and directly on Docker.
# Hosted version does not allow this.
ENTRYPOINT ["bin/entrypoint.sh"]

# Run update script
CMD ["bundle", "exec", "ruby", "bin/update-script.rb"]
