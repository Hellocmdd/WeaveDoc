#!/usr/bin/env bash
set -euo pipefail

PREFIX="[DEBUG-avedit-freeze]"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj"
SAMPLE_PATH="tests/test_doc/markdown/test-symbols.md"
RUNS=3
WAIT_SECONDS=10
OUTPUT_ROOT="_debug/avedit-selection-freeze"
DRY_RUN=0
SKIP_BUILD=0
USE_NO_BUILD=1

usage() {
  cat <<'EOF'
Usage: scripts/hitl_avedit_selection_freeze.sh [options]

Runs a human-in-the-loop repro loop for the AvaloniaEdit horizontal selection freeze.

Options:
  --sample <path>       Markdown sample to open.
  --runs <n>            Number of consecutive manual runs. Default: 3.
  --wait <seconds>      Freeze observation window after drag selection. Default: 10.
  --output <dir>        Output directory for logs. Default: _debug/avedit-selection-freeze.
  --skip-build          Do not build before the first run.
  --no-no-build         Run dotnet without --no-build after the optional build.
  --dry-run             Print the fixed procedure without launching the app.
  -h, --help            Show this help.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --sample)
      SAMPLE_PATH="${2:?Missing value for --sample}"
      shift 2
      ;;
    --runs)
      RUNS="${2:?Missing value for --runs}"
      shift 2
      ;;
    --wait)
      WAIT_SECONDS="${2:?Missing value for --wait}"
      shift 2
      ;;
    --output)
      OUTPUT_ROOT="${2:?Missing value for --output}"
      shift 2
      ;;
    --skip-build)
      SKIP_BUILD=1
      shift
      ;;
    --no-no-build)
      USE_NO_BUILD=0
      shift
      ;;
    --dry-run)
      DRY_RUN=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "$PREFIX Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if ! [[ "$RUNS" =~ ^[0-9]+$ ]] || [[ "$RUNS" -lt 1 ]]; then
  echo "$PREFIX --runs must be a positive integer." >&2
  exit 2
fi

if ! [[ "$WAIT_SECONDS" =~ ^[0-9]+$ ]] || [[ "$WAIT_SECONDS" -lt 1 ]]; then
  echo "$PREFIX --wait must be a positive integer." >&2
  exit 2
fi

cd "$PROJECT_ROOT"

if [[ "$SAMPLE_PATH" != /* ]]; then
  SAMPLE_ABS="$PROJECT_ROOT/$SAMPLE_PATH"
else
  SAMPLE_ABS="$SAMPLE_PATH"
fi

if [[ ! -f "$SAMPLE_ABS" ]]; then
  echo "$PREFIX Sample file not found: $SAMPLE_ABS" >&2
  exit 1
fi

print_manual_steps() {
  cat <<EOF
$PREFIX Fixed manual procedure:
1. Wait until "WeaveDoc Markdown Editor" opens the sample.
2. Click inside the Markdown editing area on the left, not the preview pane.
3. Use the editor's bottom horizontal scrollbar to move to the middle/back half of the long display-math line near line 6.
4. Press on formula text, hold the mouse button, drag right to the formula line end, then release.
5. Wait $WAIT_SECONDS seconds after release before deciding whether the UI froze.
6. Try normal window close. If it does not respond within 5 seconds, report close as failed; the script will clean up the process.

Fail standard:
- FAIL: after the drag selection, the UI does not respond for $WAIT_SECONDS seconds and the process remains alive.
- PASS: after the drag selection, the UI responds and normal close completes promptly.
EOF
}

if [[ "$DRY_RUN" -eq 1 ]]; then
  run_cmd=(dotnet run --project "$PROJECT_PATH")
  if [[ "$USE_NO_BUILD" -eq 1 ]]; then
    run_cmd+=(--no-build)
  fi
  run_cmd+=(-- "$SAMPLE_ABS")
  echo "$PREFIX Dry run only. Command:"
  printf '%q ' "${run_cmd[@]}"
  printf '\n\n'
  print_manual_steps
  exit 0
fi

if [[ ! -t 0 ]]; then
  echo "$PREFIX This HITL loop requires an interactive terminal for manual result prompts." >&2
  exit 1
fi

session_id="$(date +%Y%m%d-%H%M%S)"
session_dir="$PROJECT_ROOT/$OUTPUT_ROOT/$session_id"
mkdir -p "$session_dir"
summary="$session_dir/summary.tsv"

cat > "$summary" <<EOF
run	started_at	result	froze_10s	last_step	close_responded	process_alive_after_close	stdout_log	stderr_log	cpu_log	notes
EOF

cat > "$session_dir/README.md" <<EOF
# $PREFIX HITL session $session_id

- Project: $PROJECT_PATH
- Sample: $SAMPLE_ABS
- Runs: $RUNS
- Wait seconds: $WAIT_SECONDS
- Started at: $(date -Is)

EOF
print_manual_steps >> "$session_dir/README.md"

if [[ "$SKIP_BUILD" -eq 0 ]]; then
  echo "$PREFIX Building $PROJECT_PATH before manual runs..."
  dotnet build "$PROJECT_PATH" > "$session_dir/build.stdout.log" 2> "$session_dir/build.stderr.log"
fi

quote_tsv() {
  local value="${1:-}"
  value="${value//$'\t'/ }"
  value="${value//$'\n'/ }"
  printf '%s' "$value"
}

process_group_alive() {
  local pgid="$1"
  ps -eo pgid=,stat= |
    awk -v pgid="$pgid" '$1 == pgid && $2 !~ /^Z/ { found=1 } END { exit found ? 0 : 1 }'
}

terminate_group() {
  local pgid="$1"
  local label="$2"
  if process_group_alive "$pgid"; then
    echo "$PREFIX $label: sending SIGTERM to process group $pgid"
    kill -TERM "-$pgid" 2>/dev/null || true
    sleep 2
  fi
  if process_group_alive "$pgid"; then
    echo "$PREFIX $label: sending SIGKILL to process group $pgid"
    kill -KILL "-$pgid" 2>/dev/null || true
  fi
}

wait_for_group_exit() {
  local pgid="$1"
  local seconds="$2"
  local elapsed=0
  while [[ "$elapsed" -lt "$seconds" ]]; do
    if ! process_group_alive "$pgid"; then
      return 0
    fi
    sleep 1
    elapsed=$((elapsed + 1))
  done
  return 1
}

for run in $(seq 1 "$RUNS"); do
  run_dir="$session_dir/run-$run"
  mkdir -p "$run_dir"
  stdout_log="$run_dir/stdout.log"
  stderr_log="$run_dir/stderr.log"
  cpu_log="$run_dir/cpu.tsv"
  started_at="$(date -Is)"

  run_cmd=(dotnet run --project "$PROJECT_PATH")
  if [[ "$USE_NO_BUILD" -eq 1 ]]; then
    run_cmd+=(--no-build)
  fi
  run_cmd+=(-- "$SAMPLE_ABS")

  echo
  echo "$PREFIX Run $run/$RUNS starting at $started_at"
  echo "$PREFIX Command: ${run_cmd[*]}"
  setsid "${run_cmd[@]}" > "$stdout_log" 2> "$stderr_log" &
  launcher_pid=$!
  sleep 1
  pgid="$(ps -o pgid= -p "$launcher_pid" | tr -d ' ' || true)"
  if [[ -z "$pgid" ]]; then
    pgid="$launcher_pid"
  fi

  {
    echo -e "timestamp\tpid\tppid\tpgid\tstat\tpcpu\tpmem\tetime\tcomm\targs"
    while process_group_alive "$pgid"; do
      timestamp="$(date -Is)"
      ps -eo pid=,ppid=,pgid=,stat=,pcpu=,pmem=,etime=,comm=,args= |
        awk -v pgid="$pgid" -v ts="$timestamp" '$3 == pgid { print ts "\t" $0 }'
      sleep 1
    done
  } > "$cpu_log" &
  monitor_pid=$!

  print_manual_steps
  echo
  read -r -p "$PREFIX Press Enter after completing the drag-selection step and waiting $WAIT_SECONDS seconds..."
  read -r -p "$PREFIX Did the UI freeze for at least $WAIT_SECONDS seconds? [y/N] " froze
  read -r -p "$PREFIX Last step before freeze or slowdown (none/during-drag/after-release/close-attempt/other): " last_step
  echo "$PREFIX Now try closing the window normally. If it does not close within 5 seconds, answer n."
  read -r -p "$PREFIX Did normal close respond within 5 seconds? [y/N] " close_responded
  read -r -p "$PREFIX Optional notes for this run: " notes

  if wait_for_group_exit "$pgid" 5; then
    process_alive_after_close="no"
  else
    process_alive_after_close="yes"
    terminate_group "$pgid" "run $run cleanup"
    wait_for_group_exit "$pgid" 3 || true
  fi

  if kill -0 "$monitor_pid" 2>/dev/null; then
    wait "$monitor_pid" 2>/dev/null || true
  fi
  wait "$launcher_pid" 2>/dev/null || true

  froze_normalized="no"
  if [[ "$froze" =~ ^[Yy]$ ]]; then
    froze_normalized="yes"
  fi

  close_normalized="no"
  if [[ "$close_responded" =~ ^[Yy]$ ]]; then
    close_normalized="yes"
  fi

  result="pass"
  if [[ "$froze_normalized" == "yes" || "$process_alive_after_close" == "yes" || "$close_normalized" == "no" ]]; then
    result="fail"
  fi

  {
    quote_tsv "$run"; printf '\t'
    quote_tsv "$started_at"; printf '\t'
    quote_tsv "$result"; printf '\t'
    quote_tsv "$froze_normalized"; printf '\t'
    quote_tsv "$last_step"; printf '\t'
    quote_tsv "$close_normalized"; printf '\t'
    quote_tsv "$process_alive_after_close"; printf '\t'
    quote_tsv "$stdout_log"; printf '\t'
    quote_tsv "$stderr_log"; printf '\t'
    quote_tsv "$cpu_log"; printf '\t'
    quote_tsv "$notes"; printf '\n'
  } >> "$summary"

  echo "$PREFIX Run $run recorded as $result."
done

echo
echo "$PREFIX HITL session complete."
echo "$PREFIX Summary: $summary"
