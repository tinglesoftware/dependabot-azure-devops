ARG ECOSYSTEM
ARG DEPENDABOT_VERSION=0.232.0
FROM ghcr.io/dependabot/dependabot-updater-$ECOSYSTEM:$DEPENDABOT_VERSION

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

# ENTRYPOINT IS USED instead of CMD so as to avoid adding
# 'bin/run.sh' before the file name when running the image bef
ENTRYPOINT ["bin/run.sh"]
