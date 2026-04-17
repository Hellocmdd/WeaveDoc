#!/usr/bin/env bash
set -euo pipefail

# Attach gdb to a running dotnet process (native crash investigation)
# Usage: ./scripts/attach_gdb.sh <pid>

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <pid>" >&2
  exit 2
fi

PID=$1
echo "Attaching gdb to PID $PID"
sudo gdb -p "$PID"
