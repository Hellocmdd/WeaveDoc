#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DB_PATH="${1:-$ROOT_DIR/rag_store.db}"
SQLITE_BIN="${SQLITE_BIN:-}"

if [[ -z "$SQLITE_BIN" ]]; then
  if command -v sqlite3 >/dev/null 2>&1; then
    SQLITE_BIN="$(command -v sqlite3)"
  elif [[ -x "$ROOT_DIR/.conda/bin/sqlite3" ]]; then
    SQLITE_BIN="$ROOT_DIR/.conda/bin/sqlite3"
  else
    echo "sqlite3 not found. Install sqlite3 or set SQLITE_BIN=/path/to/sqlite3." >&2
    exit 1
  fi
fi

MARKDOWN_DIR="$ROOT_DIR/doc/markdown"
JSON_DIR="$ROOT_DIR/doc/json"

if [[ ! -d "$MARKDOWN_DIR" && ! -d "$JSON_DIR" ]]; then
  echo "No markdown/json document directories found under doc/." >&2
  exit 1
fi

"$SQLITE_BIN" "$DB_PATH" <<'SQL'
CREATE TABLE IF NOT EXISTS rag_chunks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source TEXT NOT NULL,
    chunk_index INTEGER NOT NULL,
    content TEXT NOT NULL,
    embedding BLOB NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_rag_chunks_source ON rag_chunks(source);
DELETE FROM rag_chunks;
SQL

find "$MARKDOWN_DIR" "$JSON_DIR" -type f \( -name '*.md' -o -name '*.json' \) -print0 2>/dev/null |
  sort -z |
  while IFS= read -r -d '' file; do
    rel="${file#$ROOT_DIR/}"
    rel_sql="${rel//\'/\'\'}"
    file_sql="${file//\'/\'\'}"
    "$SQLITE_BIN" "$DB_PATH" \
      "INSERT INTO rag_chunks (source, chunk_index, content, embedding) VALUES ('$rel_sql', 0, readfile('$file_sql'), zeroblob(1536));"
  done

"$SQLITE_BIN" "$DB_PATH" "VACUUM;"
"$SQLITE_BIN" "$DB_PATH" \
  "SELECT source, COUNT(*) AS chunks, SUM(length(content)) AS bytes FROM rag_chunks GROUP BY source ORDER BY source;"
