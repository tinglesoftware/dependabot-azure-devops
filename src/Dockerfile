FROM dependabot/dependabot-core:0.125.4

# Copy the Ruby and Gem files
COPY ["update-script.rb", "Gemfile", "Gemfile.lock", "./"]

# Install dependencies
RUN bundle install -j 3 --path vendor

# Run dependabot
ENTRYPOINT ["bundle", "exec", "ruby", "./update-script.rb"]
