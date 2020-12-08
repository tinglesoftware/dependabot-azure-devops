FROM dependabot/dependabot-core:0.125.7

# Copy the Gemfile and Gemfile.lock
COPY ["Gemfile", "Gemfile.lock", "./"]

# Install dependencies
RUN bundle install -j 3 --path vendor

# Script files are known to change more frequently than Gemfiles.
# They are copied after installation of dependencies so that the
# image layers that change less frequently are available for caching
# and hence be reused in subsequent builds.
# For more information:
# https://docs.docker.com/develop/develop-images/build_enhancements/
# https://testdriven.io/blog/faster-ci-builds-with-docker-cache/

# Copy the Ruby scripts
COPY ["update-script.rb", "./"]

# Run update script
ENTRYPOINT ["bundle", "exec", "ruby", "./update-script.rb"]
