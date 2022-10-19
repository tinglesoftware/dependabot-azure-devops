#!/bin/sh
if [ -n "$WORKAROUND_CMD" ]; then
    eval "$WORKAROUND_CMD"
fi

# This will exec the CMD from Dockerfile
exec "$@"
