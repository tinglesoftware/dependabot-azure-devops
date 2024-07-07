ARG ECOSYSTEM

# The Dependabot docker images in https://github.com/dependabot/dependabot-core are no longer versioned like the Ruby Gems.
# In production, the build pipeline automatically updates the image tag to match the version in updater/Gemfile (see .github/workflows/updater.yml).
# In local/dev,  the "latest" tag will be used by default. You can override this by specifying a dependabot-core release tag commit SHA as the image tag.
#                e.g. for v0.264.0, use "ghcr.io/dependabot/dependabot-updater-$ECOSYSTEM:e8d8a1268ea61304e939ba9ab963e249cac5b241"
FROM ghcr.io/dependabot/dependabot-updater-$ECOSYSTEM

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

# ENTRYPOINT IS USED instead of CMD so as to avoid adding
# 'bin/run.sh' before the file name when running the image
ENTRYPOINT ["bin/run.sh"]
