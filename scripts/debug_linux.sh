#!/usr/bin/env bash
set -euo pipefail

# Build in Debug and run under gdb to catch native crashes (Linux)
# Usage: ./scripts/debug_linux.sh

dotnet build -c Debug
DLL_PATH=$(ls -d bin/Debug/net10.0/*.dll 2>/dev/null | head -n1)
if [ -z "$DLL_PATH" ]; then
  echo "Could not find built dll in bin/Debug/net10.0" >&2
  exit 1
fi

echo "Setting core dump size to unlimited"
ulimit -c unlimited

echo "Run under gdb: gdb --args dotnet $DLL_PATH"
echo "Starting gdb..."
gdb --args dotnet "$DLL_PATH"
