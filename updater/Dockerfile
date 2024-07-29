ARG ECOSYSTEM
ARG BASE_VERSION=latest
ARG DEPENDABOT_UPDATER_VERSION=unknown

# The Dependabot docker images in https://github.com/dependabot/dependabot-core are no longer versioned like the Ruby Gems; instead they are versioned by the commit SHA of the release tag.
# In production, the build pipeline automatically calculates BASE_VERSION to match the dependabot-omnibus version set in updater/Gemfile (see .github/workflows/updater.yml).
# In local/dev,  the "latest" tag will be used by default. You can override this by setting BASE_VERSION to the commit SHA of a dependabot-core release tag.
#                e.g. for v0.264.0, use BASE_VERSION="e8d8a1268ea61304e939ba9ab963e249cac5b241"
FROM ghcr.io/dependabot/dependabot-updater-$ECOSYSTEM:$BASE_VERSION

LABEL org.opencontainers.image.source="https://github.com/tinglesoftware/dependabot-azure-devops"

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

# Add ENV
ENV DEPENDABOT_UPDATER_VERSION=$DEPENDABOT_UPDATER_VERSION

# ENTRYPOINT IS USED instead of CMD so as to avoid adding
# 'bin/run.sh' before the file name when running the image
ENTRYPOINT ["bin/run.sh"]
