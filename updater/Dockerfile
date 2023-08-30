#TODO: find out how to lock the base image version
ARG ECOSYSTEM
FROM ghcr.io/dependabot/dependabot-updater-$ECOSYSTEM

# ENV DEPENDABOT_HOME /home/dependabot
WORKDIR ${DEPENDABOT_HOME}

COPY --chown=dependabot:dependabot updater/Gemfile updater/Gemfile.lock dependabot-updater/

WORKDIR $DEPENDABOT_HOME/dependabot-updater

RUN bundle config set --local path 'vendor' && \
    bundle config set --local frozen 'true' && \
    bundle config set --local without 'development' && \
    bundle install

# Add project
COPY --chown=dependabot:dependabot LICENSE $DEPENDABOT_HOME
COPY --chown=dependabot:dependabot updater $DEPENDABOT_HOME/dependabot-updater

# This entrypoint exists to solve specific setup problems.
# It is only used with the extension and directly on Docker.
# Hosted version does not allow this.
ENTRYPOINT ["bin/entrypoint.sh"]

CMD ["bin/run.sh"]
