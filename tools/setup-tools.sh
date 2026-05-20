#!/usr/bin/env bash
set -euo pipefail

PANDOC_VERSION="3.9.0.2"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS_DIR="$SCRIPT_DIR"
PANDOC_DIR="$TOOLS_DIR/pandoc"
PANDOC_BIN="$PANDOC_DIR/pandoc"
PANDOC_BIN_ALT="$PANDOC_DIR/bin/pandoc"
PANDOC_FALLBACK_MARKER="$PANDOC_DIR/.fallback"
TMP_ARCHIVE="$TOOLS_DIR/pandoc.tar.gz"
TMP_EXTRACT="$TOOLS_DIR/pandoc-temp"

if [[ -x "$PANDOC_BIN" ]]; then
    echo "[Pandoc] Already installed: $("$PANDOC_BIN" --version | head -n 1)"
    exit 0
fi

if [[ -x "$PANDOC_BIN_ALT" ]]; then
    echo "[Pandoc] Already installed: $("$PANDOC_BIN_ALT" --version | head -n 1)"
    exit 0
fi

if [[ -f "$PANDOC_FALLBACK_MARKER" ]]; then
    echo "[Pandoc] Fallback marker detected. Skipping download and using internal converter fallback."
    exit 0
fi

case "$(uname -s)" in
    Linux*)
        PLATFORM="linux-amd64"
        ;;
    Darwin*)
        PLATFORM="macOS"
        ;;
    *)
        echo "[Pandoc] Unsupported platform: $(uname -s)" >&2
        exit 1
        ;;
esac

URL="https://github.com/jgm/pandoc/releases/download/${PANDOC_VERSION}/pandoc-${PANDOC_VERSION}-${PLATFORM}.tar.gz"

echo "[Pandoc] Downloading v${PANDOC_VERSION} for ${PLATFORM} ..."
rm -rf "$TMP_EXTRACT"
mkdir -p "$TMP_EXTRACT"
if ! curl -fsSL --connect-timeout 10 --max-time 60 "$URL" -o "$TMP_ARCHIVE"; then
    mkdir -p "$PANDOC_DIR"
    touch "$PANDOC_FALLBACK_MARKER"
    echo "[Pandoc] Download unavailable, using internal converter fallback for this environment."
    exit 0
fi

echo "[Pandoc] Extracting ..."
if ! tar -xzf "$TMP_ARCHIVE" -C "$TMP_EXTRACT"; then
    mkdir -p "$PANDOC_DIR"
    touch "$PANDOC_FALLBACK_MARKER"
    rm -f "$TMP_ARCHIVE"
    rm -rf "$TMP_EXTRACT"
    echo "[Pandoc] Extraction failed, using internal converter fallback for this environment."
    exit 0
fi
rm -f "$TMP_ARCHIVE"

INNER_DIR="$(find "$TMP_EXTRACT" -mindepth 1 -maxdepth 1 -type d | head -n 1)"
if [[ -z "${INNER_DIR:-}" ]]; then
    mkdir -p "$PANDOC_DIR"
    touch "$PANDOC_FALLBACK_MARKER"
    rm -rf "$TMP_EXTRACT"
    echo "[Pandoc] Failed to locate extracted directory, using internal converter fallback for this environment."
    exit 0
fi

mkdir -p "$PANDOC_DIR"
find "$INNER_DIR" -mindepth 1 -maxdepth 1 -exec mv {} "$PANDOC_DIR"/ \;
rm -rf "$TMP_EXTRACT"
if [[ ! -f "$PANDOC_BIN" && ! -f "$PANDOC_BIN_ALT" ]]; then
    touch "$PANDOC_FALLBACK_MARKER"
    echo "[Pandoc] Downloaded archive did not contain a usable pandoc binary, using internal converter fallback."
    exit 0
fi

if [[ -f "$PANDOC_BIN" ]]; then
    chmod +x "$PANDOC_BIN"
fi

if [[ -f "$PANDOC_BIN_ALT" ]]; then
    chmod +x "$PANDOC_BIN_ALT"
fi

echo
echo "=== Installation Summary ==="
if [[ -x "$PANDOC_BIN" ]]; then
    echo "  pandoc : $("$PANDOC_BIN" --version | head -n 1)"
    echo
    echo "All tools ready."
elif [[ -x "$PANDOC_BIN_ALT" ]]; then
    echo "  pandoc : $("$PANDOC_BIN_ALT" --version | head -n 1)"
    echo
    echo "All tools ready."
else
    echo "  pandoc : MISSING" >&2
    exit 1
fi
