#!/usr/bin/env bash
set -euo pipefail

# Simple perf wrapper for Linux: record a run and open report
# Requires: perf installed and permissions (sudo may be needed)
# Usage: ./scripts/collect_perf.sh

dotnet build -c Release
DLL_PATH=$(ls -d bin/Release/net10.0/*.dll 2>/dev/null | head -n1)
if [ -z "$DLL_PATH" ]; then
  echo "Could not find built dll in bin/Release/net10.0" >&2
  exit 1
fi

echo "Recording perf.data (may require sudo)..."
sudo perf record -g -- dotnet "$DLL_PATH"
echo "To view report: sudo perf report"
