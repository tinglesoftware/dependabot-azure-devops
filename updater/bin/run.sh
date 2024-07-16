#!/bin/bash
set -e

command="$1"
if [ -z "$command" ]; then
  echo "usage: run [fetch_files|update_files|update_script|update_script_vnext]"
  exit 1
fi

# Tell hex to use the system-wide CA bundle
export HEX_CACERTS_PATH=/etc/ssl/certs/ca-certificates.crt

# Tell python to use the system-wide CA bundle
export REQUESTS_CA_BUNDLE=/etc/ssl/certs/ca-certificates.crt

# This is a WORKAROUND for fixing various quirks that might exist within the container environment that we don't have much control over.
# see: https://github.com/ruby/resolv/issues/23 (Ruby)
#      https://github.com/tinglesoftware/dependabot-azure-devops/pull/369 (Ruby)
#      https://github.com/tinglesoftware/dependabot-azure-devops/pull/834 (Ruby)
#      https://github.com/tinglesoftware/dependabot-azure-devops/issues/921#issuecomment-2162273558 (NuGet)
if [ -n "$WORKAROUND_CMD" ]; then
  eval "$WORKAROUND_CMD"
fi

bundle exec ruby "bin/${command}.rb"
