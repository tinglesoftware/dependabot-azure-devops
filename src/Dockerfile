FROM dependabot/dependabot-core:0.125.4

# Copy the Gemfile and Gemfile.lock
COPY ["Gemfile", "Gemfile.lock", "./"]

# Install dependencies
RUN bundle install -j 3 --path vendor

# Script files are known to change more frequently than Gemfiles.
# It is put after installation of dependencies so that the image
# layers can be cached from be and be reused in subsequent builds

# Copy the Ruby scripts
COPY ["update-script.rb", "./"]

# Run dependabot
ENTRYPOINT ["bundle", "exec", "ruby", "./update-script.rb"]
