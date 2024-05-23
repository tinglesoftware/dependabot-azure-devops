#!/bin/bash
set -e

# This is a WORKAROUND for https://github.com/ruby/resolv/issues/23
# see also https://github.com/tinglesoftware/dependabot-azure-devops/pull/369
# see also https://github.com/tinglesoftware/dependabot-azure-devops/pull/834
if [ -n "$WORKAROUND_CMD" ]; then
    eval "$WORKAROUND_CMD"
fi

command="$1"
if [ -z "$command" ]; then
  echo "usage: run [update_script|fetch_files|update_files]"
  exit 1
fi

# Tell hex to use the system-wide CA bundle
export HEX_CACERTS_PATH=/etc/ssl/certs/ca-certificates.crt

# Tell python to use the system-wide CA bundle
export REQUESTS_CA_BUNDLE=/etc/ssl/certs/ca-certificates.crt

bundle exec ruby "bin/${command}.rb"
