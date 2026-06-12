#!/usr/bin/env bash
set -euo pipefail

PREFIX="[DEBUG-avedit-xtest]"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj"
SAMPLE_PATH="tests/test_doc/markdown/test-symbols.md"
OUTPUT_ROOT="_debug/avedit-selection-freeze"
WINDOW_TITLE="WeaveDoc Markdown Editor"
WAIT_SECONDS=10
STARTUP_TIMEOUT=25
DRY_RUN=0
SKIP_BUILD=0
USE_NO_BUILD=1
KEEP_OPEN=0
CAPTURE_STACK=0
EDITOR_DIAGNOSTICS=0
FORCE_AVALONIAEDIT=0

LINE_Y_RATIO="0.245"
SCROLL_Y_RATIO="0.925"
SCROLL_START_X_RATIO="0.140"
SCROLL_END_X_RATIO="0.720"
DRAG_START_X_RATIO="0.260"
DRAG_END_X_RATIO="0.960"

usage() {
  cat <<'EOF'
Usage: scripts/xtest_avedit_selection_probe.sh [options]

Runs a semi-automated X11/XTest probe for the AvaloniaEdit horizontal selection freeze.
The probe injects real pointer events into the desktop session:
  1. focus the editor line area,
  2. drag the editor horizontal scrollbar to create non-zero horizontal offset,
  3. drag-select across the long display-math line toward the visible line end,
  4. wait for the freeze window, then send Alt+F4 and record whether the app closes.

Options:
  --sample <path>              Markdown sample to open. Default: tests/test_doc/markdown/test-symbols.md.
  --wait <seconds>             Freeze observation window after drag selection. Default: 10.
  --output <dir>               Output directory. Default: _debug/avedit-selection-freeze.
  --window-title <text>        Window title substring. Default: WeaveDoc Markdown Editor.
  --startup-timeout <seconds>  Time to wait for the app window. Default: 25.
  --skip-build                 Do not build before launching.
  --no-no-build                Run dotnet without --no-build after the optional build.
  --keep-open                  Do not send Alt+F4 after the observation window.
  --capture-stack              Launch under gdb and capture thread stacks if close is unresponsive.
  --editor-diagnostics         Enable AvaloniaEdit event/layout diagnostics in the app process.
  --force-avaloniaedit         Keep the AvaloniaEdit editor visible even for fallback-triggering samples.
  --dry-run                    Print environment and planned coordinate ratios without launching.

Coordinate tuning options, expressed as ratios inside the detected app window:
  --line-y-ratio <n>           Display-math line y coordinate. Default: 0.245.
  --scroll-y-ratio <n>         Horizontal scrollbar y coordinate. Default: 0.925.
  --scroll-start-x-ratio <n>   Horizontal scrollbar drag start x. Default: 0.140.
  --scroll-end-x-ratio <n>     Horizontal scrollbar drag end x. Default: 0.720.
  --drag-start-x-ratio <n>     Selection drag start x. Default: 0.260.
  --drag-end-x-ratio <n>       Selection drag end x. Default: 0.960.
  -h, --help                   Show this help.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --sample)
      SAMPLE_PATH="${2:?Missing value for --sample}"
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
    --window-title)
      WINDOW_TITLE="${2:?Missing value for --window-title}"
      shift 2
      ;;
    --startup-timeout)
      STARTUP_TIMEOUT="${2:?Missing value for --startup-timeout}"
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
    --keep-open)
      KEEP_OPEN=1
      shift
      ;;
    --capture-stack)
      CAPTURE_STACK=1
      USE_NO_BUILD=1
      shift
      ;;
    --editor-diagnostics)
      EDITOR_DIAGNOSTICS=1
      shift
      ;;
    --force-avaloniaedit)
      FORCE_AVALONIAEDIT=1
      EDITOR_DIAGNOSTICS=1
      shift
      ;;
    --dry-run)
      DRY_RUN=1
      shift
      ;;
    --line-y-ratio)
      LINE_Y_RATIO="${2:?Missing value for --line-y-ratio}"
      shift 2
      ;;
    --scroll-y-ratio)
      SCROLL_Y_RATIO="${2:?Missing value for --scroll-y-ratio}"
      shift 2
      ;;
    --scroll-start-x-ratio)
      SCROLL_START_X_RATIO="${2:?Missing value for --scroll-start-x-ratio}"
      shift 2
      ;;
    --scroll-end-x-ratio)
      SCROLL_END_X_RATIO="${2:?Missing value for --scroll-end-x-ratio}"
      shift 2
      ;;
    --drag-start-x-ratio)
      DRAG_START_X_RATIO="${2:?Missing value for --drag-start-x-ratio}"
      shift 2
      ;;
    --drag-end-x-ratio)
      DRAG_END_X_RATIO="${2:?Missing value for --drag-end-x-ratio}"
      shift 2
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

if ! [[ "$WAIT_SECONDS" =~ ^[0-9]+$ ]] || [[ "$WAIT_SECONDS" -lt 1 ]]; then
  echo "$PREFIX --wait must be a positive integer." >&2
  exit 2
fi

if ! [[ "$STARTUP_TIMEOUT" =~ ^[0-9]+$ ]] || [[ "$STARTUP_TIMEOUT" -lt 1 ]]; then
  echo "$PREFIX --startup-timeout must be a positive integer." >&2
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

require_command() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "$PREFIX Missing required command: $cmd" >&2
    return 1
  fi
}

python_xtest() {
  python3 - "$@" <<'PY'
import ctypes
import ctypes.util
import os
import sys
import time

BOOL = ctypes.c_int

def load_library(name, fallback):
    path = ctypes.util.find_library(name) or fallback
    return ctypes.CDLL(path)

def open_display():
    x11 = load_library("X11", "libX11.so.6")
    xtst = load_library("Xtst", "libXtst.so.6")

    x11.XOpenDisplay.argtypes = [ctypes.c_char_p]
    x11.XOpenDisplay.restype = ctypes.c_void_p
    x11.XDefaultScreen.argtypes = [ctypes.c_void_p]
    x11.XDefaultScreen.restype = ctypes.c_int
    x11.XFlush.argtypes = [ctypes.c_void_p]
    x11.XFlush.restype = ctypes.c_int
    x11.XKeysymToKeycode.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
    x11.XKeysymToKeycode.restype = ctypes.c_uint
    x11.XCloseDisplay.argtypes = [ctypes.c_void_p]
    x11.XCloseDisplay.restype = ctypes.c_int

    xtst.XTestQueryExtension.argtypes = [
        ctypes.c_void_p,
        ctypes.POINTER(ctypes.c_int),
        ctypes.POINTER(ctypes.c_int),
        ctypes.POINTER(ctypes.c_int),
        ctypes.POINTER(ctypes.c_int),
    ]
    xtst.XTestQueryExtension.restype = BOOL
    xtst.XTestFakeMotionEvent.argtypes = [
        ctypes.c_void_p,
        ctypes.c_int,
        ctypes.c_int,
        ctypes.c_int,
        ctypes.c_ulong,
    ]
    xtst.XTestFakeMotionEvent.restype = BOOL
    xtst.XTestFakeButtonEvent.argtypes = [
        ctypes.c_void_p,
        ctypes.c_uint,
        BOOL,
        ctypes.c_ulong,
    ]
    xtst.XTestFakeButtonEvent.restype = BOOL
    xtst.XTestFakeKeyEvent.argtypes = [
        ctypes.c_void_p,
        ctypes.c_uint,
        BOOL,
        ctypes.c_ulong,
    ]
    xtst.XTestFakeKeyEvent.restype = BOOL

    display_name = os.environ.get("DISPLAY")
    display = x11.XOpenDisplay(display_name.encode() if display_name else None)
    if not display:
        raise RuntimeError("cannot open X display")

    event_base = ctypes.c_int()
    error_base = ctypes.c_int()
    major = ctypes.c_int()
    minor = ctypes.c_int()
    if not xtst.XTestQueryExtension(
        display,
        ctypes.byref(event_base),
        ctypes.byref(error_base),
        ctypes.byref(major),
        ctypes.byref(minor),
    ):
        x11.XCloseDisplay(display)
        raise RuntimeError("XTest extension is not available on this display")

    return x11, xtst, display, x11.XDefaultScreen(display), major.value, minor.value

def flush(x11, display):
    x11.XFlush(display)
    time.sleep(0.08)

def move(x11, xtst, display, screen, x, y, delay=0.05):
    xtst.XTestFakeMotionEvent(display, screen, int(x), int(y), 0)
    x11.XFlush(display)
    time.sleep(delay)

def button(xtst, display, button_number, pressed):
    xtst.XTestFakeButtonEvent(display, button_number, 1 if pressed else 0, 0)

def drag(x11, xtst, display, screen, start_x, start_y, end_x, end_y, steps):
    move(x11, xtst, display, screen, start_x, start_y)
    button(xtst, display, 1, True)
    flush(x11, display)
    for step in range(1, steps + 1):
        t = step / steps
        x = start_x + (end_x - start_x) * t
        y = start_y + (end_y - start_y) * t
        move(x11, xtst, display, screen, x, y, delay=0.025)
    button(xtst, display, 1, False)
    flush(x11, display)

def alt_f4(x11, xtst, display):
    XK_Alt_L = 0xFFE9
    XK_F4 = 0xFFC1
    alt = x11.XKeysymToKeycode(display, XK_Alt_L)
    f4 = x11.XKeysymToKeycode(display, XK_F4)
    if not alt or not f4:
        raise RuntimeError("cannot resolve Alt/F4 keycodes")
    xtst.XTestFakeKeyEvent(display, alt, 1, 0)
    xtst.XTestFakeKeyEvent(display, f4, 1, 0)
    xtst.XTestFakeKeyEvent(display, f4, 0, 0)
    xtst.XTestFakeKeyEvent(display, alt, 0, 0)
    flush(x11, display)

mode = sys.argv[1]
try:
    x11, xtst, display, screen, major, minor = open_display()
    try:
        if mode == "check":
            print(f"XTest available: {major}.{minor}")
        elif mode == "probe":
            if len(sys.argv) != 9:
                raise RuntimeError("probe requires 7 coordinates plus wait seconds")
            focus_x, line_y, scroll_start_x, scroll_y, scroll_end_x, drag_start_x, drag_end_x = [
                int(float(value)) for value in sys.argv[2:9]
            ]
            move(x11, xtst, display, screen, focus_x, line_y)
            button(xtst, display, 1, True)
            flush(x11, display)
            button(xtst, display, 1, False)
            flush(x11, display)
            drag(x11, xtst, display, screen, scroll_start_x, scroll_y, scroll_end_x, scroll_y, 24)
            time.sleep(0.25)
            drag(x11, xtst, display, screen, drag_start_x, line_y, drag_end_x, line_y, 48)
        elif mode == "close":
            alt_f4(x11, xtst, display)
        else:
            raise RuntimeError(f"unknown mode: {mode}")
    finally:
        x11.XCloseDisplay(display)
except Exception as exc:
    print(f"{exc}", file=sys.stderr)
    sys.exit(1)
PY
}

check_environment() {
  local status=0
  echo "$PREFIX Environment check:"

  if [[ -z "${DISPLAY:-}" ]]; then
    echo "$PREFIX - DISPLAY: missing"
    status=1
  else
    echo "$PREFIX - DISPLAY: $DISPLAY"
  fi

  for cmd in dotnet python3 xwininfo xprop xdpyinfo ps awk; do
    if command -v "$cmd" >/dev/null 2>&1; then
      echo "$PREFIX - $cmd: $(command -v "$cmd")"
    else
      echo "$PREFIX - $cmd: missing"
      status=1
    fi
  done

  if [[ "$CAPTURE_STACK" -eq 1 ]]; then
    if command -v gdb >/dev/null 2>&1; then
      echo "$PREFIX - gdb: $(command -v gdb)"
    else
      echo "$PREFIX - gdb: missing"
      status=1
    fi

    if command -v dotnet-dump >/dev/null 2>&1; then
      echo "$PREFIX - dotnet-dump: $(command -v dotnet-dump)"
    else
      echo "$PREFIX - dotnet-dump: missing (gdb stack capture will be used)"
    fi

    if command -v createdump >/dev/null 2>&1; then
      echo "$PREFIX - createdump: $(command -v createdump)"
    else
      local runtime_createdump
      runtime_createdump="$(find /usr/lib/dotnet /usr/share/dotnet -path '*/Microsoft.NETCore.App/*/createdump' -type f -perm -u=x 2>/dev/null | head -n 1 || true)"
      if [[ -n "$runtime_createdump" ]]; then
        echo "$PREFIX - createdump: $runtime_createdump (not on PATH)"
      else
        echo "$PREFIX - createdump: missing"
      fi
    fi

    if [[ -r /proc/sys/kernel/yama/ptrace_scope ]]; then
      echo "$PREFIX - ptrace_scope: $(cat /proc/sys/kernel/yama/ptrace_scope)"
    fi
  fi

  for optional in xdotool ydotool dotool; do
    if command -v "$optional" >/dev/null 2>&1; then
      echo "$PREFIX - $optional: $(command -v "$optional")"
    else
      echo "$PREFIX - $optional: missing (not required; XTest ctypes path is used)"
    fi
  done

  if xdpyinfo 2>/dev/null | grep -qi 'XTEST'; then
    echo "$PREFIX - XTEST extension: reported by xdpyinfo"
  else
    echo "$PREFIX - XTEST extension: not reported by xdpyinfo; checking by XTestQueryExtension"
  fi

  if python_xtest check; then
    echo "$PREFIX - XTestQueryExtension: available"
  else
    status=1
  fi

  return "$status"
}

find_window_id() {
  local deadline=$((SECONDS + STARTUP_TIMEOUT))
  while [[ "$SECONDS" -lt "$deadline" ]]; do
    mapfile -t window_ids < <(
      xprop -root _NET_CLIENT_LIST 2>/dev/null |
        grep -oE '0x[0-9a-fA-F]+' || true
    )

    for window_id in "${window_ids[@]}"; do
      local title
      title="$(xprop -id "$window_id" WM_NAME _NET_WM_NAME 2>/dev/null || true)"
      if [[ "$title" == *"$WINDOW_TITLE"* ]]; then
        printf '%s\n' "$window_id"
        return 0
      fi
    done

    sleep 1
  done

  return 1
}

window_geometry() {
  local window_id="$1"
  xwininfo -id "$window_id" |
    awk '
      /Absolute upper-left X:/ { x=$4 }
      /Absolute upper-left Y:/ { y=$4 }
      /Width:/ { w=$2 }
      /Height:/ { h=$2 }
      END {
        if (x == "" || y == "" || w == "" || h == "") exit 1
        printf "%s %s %s %s\n", x, y, w, h
      }'
}

coord() {
  local origin="$1"
  local size="$2"
  local ratio="$3"
  awk -v origin="$origin" -v size="$size" -v ratio="$ratio" 'BEGIN { printf "%d", origin + (size * ratio) }'
}

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

print_plan() {
  local run_cmd
  if [[ "$CAPTURE_STACK" -eq 1 ]]; then
    run_cmd=(gdb -q -batch -ex run -ex "thread apply all bt" --args dotnet "src/WeaveDoc.MarkdownEditor/bin/Debug/net10.0/WeaveDoc.MarkdownEditor.dll" "$SAMPLE_ABS")
  else
    run_cmd=(dotnet run --project "$PROJECT_PATH")
    if [[ "$USE_NO_BUILD" -eq 1 ]]; then
      run_cmd+=(--no-build)
    fi
    run_cmd+=(-- "$SAMPLE_ABS")
  fi

  echo "$PREFIX Planned command:"
  printf '%q ' "${run_cmd[@]}"
  printf '\n'
  if [[ "$CAPTURE_STACK" -eq 1 ]]; then
    echo "$PREFIX Stack capture: gdb will print 'thread apply all bt' after SIGINT."
  fi
  if [[ "$EDITOR_DIAGNOSTICS" -eq 1 ]]; then
    echo "$PREFIX Editor diagnostics: WEAVEDOC_DEBUG_AVEDIT_SELECTION=1"
  fi
  if [[ "$FORCE_AVALONIAEDIT" -eq 1 ]]; then
    echo "$PREFIX Force AvaloniaEdit: WEAVEDOC_DEBUG_FORCE_AVALONIAEDIT=1"
  fi
  echo "$PREFIX Planned ratios:"
  echo "$PREFIX - line-y=$LINE_Y_RATIO, scrollbar-y=$SCROLL_Y_RATIO"
  echo "$PREFIX - scrollbar drag x=$SCROLL_START_X_RATIO -> $SCROLL_END_X_RATIO"
  echo "$PREFIX - selection drag x=$DRAG_START_X_RATIO -> $DRAG_END_X_RATIO"
  echo "$PREFIX This probe uses XTest pointer events and does not call TextEditor.Select()."
}

if [[ "$DRY_RUN" -eq 1 ]]; then
  print_plan
  check_environment
  exit $?
fi

for cmd in dotnet python3 xwininfo xprop xdpyinfo ps awk; do
  require_command "$cmd"
done
if [[ "$CAPTURE_STACK" -eq 1 ]]; then
  require_command gdb
fi

check_environment

session_id="$(date +%Y%m%d-%H%M%S)-xtest"
session_dir="$PROJECT_ROOT/$OUTPUT_ROOT/$session_id"
mkdir -p "$session_dir"
summary="$session_dir/summary.tsv"
stdout_log="$session_dir/stdout.log"
stderr_log="$session_dir/stderr.log"
cpu_log="$session_dir/cpu.tsv"
stack_log="$session_dir/gdb.stack.log"
managed_stack_log="$session_dir/dotnet-stack.log"
app_dll="$PROJECT_ROOT/src/WeaveDoc.MarkdownEditor/bin/Debug/net10.0/WeaveDoc.MarkdownEditor.dll"

cat > "$summary" <<EOF
started_at	result	froze_or_unresponsive_${WAIT_SECONDS}s	close_responded	process_alive_after_close	stack_captured	managed_stack_captured	gdb_status	window_id	window_geometry	focus_x	line_y	scroll_start_x	scroll_y	scroll_end_x	drag_start_x	drag_end_x	stdout_log	stderr_log	cpu_log	stack_log	managed_stack_log	notes
EOF

cat > "$session_dir/README.md" <<EOF
# $PREFIX XTest session $session_id

- Project: $PROJECT_PATH
- Sample: $SAMPLE_ABS
- Window title: $WINDOW_TITLE
- Wait seconds: $WAIT_SECONDS
- Started at: $(date -Is)
- Probe: XTest pointer events; no \`TextEditor.Select()\`.
- Required interaction: non-zero horizontal scroll offset, then mouse drag-selection toward line end.
- Stack capture: $([[ "$CAPTURE_STACK" -eq 1 ]] && echo "gdb thread apply all bt" || echo "disabled")
- Editor diagnostics: $([[ "$EDITOR_DIAGNOSTICS" -eq 1 ]] && echo "enabled" || echo "disabled")
- Force AvaloniaEdit: $([[ "$FORCE_AVALONIAEDIT" -eq 1 ]] && echo "enabled" || echo "disabled")

EOF

if [[ "$SKIP_BUILD" -eq 0 ]]; then
  echo "$PREFIX Building $PROJECT_PATH before XTest probe..."
  dotnet build "$PROJECT_PATH" > "$session_dir/build.stdout.log" 2> "$session_dir/build.stderr.log"
fi
if [[ "$CAPTURE_STACK" -eq 1 && ! -f "$app_dll" ]]; then
  echo "$PREFIX Expected built app dll not found: $app_dll" >&2
  exit 1
fi

if [[ "$CAPTURE_STACK" -eq 1 ]]; then
  run_cmd=(dotnet "$app_dll" "$SAMPLE_ABS")
  launch_cmd=(
    gdb -q -batch
    -ex "set pagination off"
    -ex "set confirm off"
    -ex "set debuginfod enabled off"
    -ex "run"
    -ex "thread apply all bt"
    -ex "quit"
    --args "${run_cmd[@]}"
  )
  stdout_log="$stack_log"
  stderr_log="$session_dir/gdb.stderr.log"
else
  run_cmd=(dotnet run --project "$PROJECT_PATH")
  if [[ "$USE_NO_BUILD" -eq 1 ]]; then
    run_cmd+=(--no-build)
  fi
  run_cmd+=(-- "$SAMPLE_ABS")
  launch_cmd=("${run_cmd[@]}")
fi

env_cmd=(env)
if [[ "$EDITOR_DIAGNOSTICS" -eq 1 ]]; then
  env_cmd+=(WEAVEDOC_DEBUG_AVEDIT_SELECTION=1)
fi
if [[ "$FORCE_AVALONIAEDIT" -eq 1 ]]; then
  env_cmd+=(WEAVEDOC_DEBUG_FORCE_AVALONIAEDIT=1)
fi

started_at="$(date -Is)"
echo "$PREFIX Launching app at $started_at"
echo "$PREFIX Command: ${run_cmd[*]}"
if [[ "$CAPTURE_STACK" -eq 1 ]]; then
  echo "$PREFIX GDB log: $stack_log"
fi
setsid "${env_cmd[@]}" "${launch_cmd[@]}" > "$stdout_log" 2> "$stderr_log" &
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

result="fail"
notes=""
froze_or_unresponsive="yes"
close_responded="no"
process_alive_after_close="yes"
stack_captured="no"
managed_stack_captured="no"
gdb_status=""
window_id=""
geometry_text=""
focus_x=""
line_y=""
scroll_start_x=""
scroll_y=""
scroll_end_x=""
drag_start_x=""
drag_end_x=""

cleanup() {
  if [[ -n "${pgid:-}" ]] && process_group_alive "$pgid"; then
    terminate_group "$pgid" "probe cleanup"
  fi
  if [[ -n "${monitor_pid:-}" ]] && kill -0 "$monitor_pid" 2>/dev/null; then
    wait "$monitor_pid" 2>/dev/null || true
  fi
  wait "$launcher_pid" 2>/dev/null || true
}
trap cleanup EXIT

if ! window_id="$(find_window_id)"; then
  notes="window title not found before startup timeout"
else
  geometry_text="$(window_geometry "$window_id")"
  read -r window_x window_y window_w window_h <<<"$geometry_text"

  focus_x="$(coord "$window_x" "$window_w" "$DRAG_START_X_RATIO")"
  line_y="$(coord "$window_y" "$window_h" "$LINE_Y_RATIO")"
  scroll_start_x="$(coord "$window_x" "$window_w" "$SCROLL_START_X_RATIO")"
  scroll_y="$(coord "$window_y" "$window_h" "$SCROLL_Y_RATIO")"
  scroll_end_x="$(coord "$window_x" "$window_w" "$SCROLL_END_X_RATIO")"
  drag_start_x="$(coord "$window_x" "$window_w" "$DRAG_START_X_RATIO")"
  drag_end_x="$(coord "$window_x" "$window_w" "$DRAG_END_X_RATIO")"

  {
    echo "Window id: $window_id"
    echo "Window geometry: $geometry_text"
    echo "Focus click: $focus_x,$line_y"
    echo "Horizontal scrollbar drag: $scroll_start_x,$scroll_y -> $scroll_end_x,$scroll_y"
    echo "Selection drag: $drag_start_x,$line_y -> $drag_end_x,$line_y"
  } | tee -a "$session_dir/README.md"

  python_xtest probe \
    "$focus_x" "$line_y" \
    "$scroll_start_x" "$scroll_y" "$scroll_end_x" \
    "$drag_start_x" "$drag_end_x"

  echo "$PREFIX Waiting $WAIT_SECONDS seconds after selection drag."
  sleep "$WAIT_SECONDS"

  if [[ "$KEEP_OPEN" -eq 1 ]]; then
    notes="keep-open requested; close responsiveness not measured"
    froze_or_unresponsive="unknown"
    close_responded="unknown"
    process_alive_after_close="$(process_group_alive "$pgid" && echo yes || echo no)"
    result="unknown"
  else
    echo "$PREFIX Sending Alt+F4 through XTest to measure close responsiveness."
    python_xtest close

    if wait_for_group_exit "$pgid" 5; then
      close_responded="yes"
      process_alive_after_close="no"
      froze_or_unresponsive="no"
      result="pass"
    else
      close_responded="no"
      process_alive_after_close="yes"
      froze_or_unresponsive="yes"
      result="fail"
      notes="process did not exit within 5 seconds after Alt+F4"
      if [[ "$CAPTURE_STACK" -eq 1 ]]; then
        dotnet_pid="$(ps -eo pid=,pgid=,comm=,args= | awk -v pgid="$pgid" '$2 == pgid && $3 == "dotnet" { print $1; exit }')"
        if [[ -n "$dotnet_pid" && -x "$PROJECT_ROOT/_debug/dotnet-tools/dotnet-stack" ]]; then
          echo "$PREFIX Capturing managed stack from dotnet pid $dotnet_pid."
          if "$PROJECT_ROOT/_debug/dotnet-tools/dotnet-stack" report --process-id "$dotnet_pid" > "$managed_stack_log" 2>&1; then
            if grep -qE 'WeaveDoc|Avalonia|AvaloniaEdit|Thread' "$managed_stack_log"; then
              managed_stack_captured="yes"
            fi
          fi
        fi
        echo "$PREFIX Sending SIGINT to gdb process group $pgid for stack capture."
        kill -INT "-$pgid" 2>/dev/null || true
        if wait_for_group_exit "$pgid" 30; then
          gdb_status="exited-after-sigint"
        else
          gdb_status="timeout-after-sigint"
          terminate_group "$pgid" "gdb stack cleanup"
        fi
        if [[ -f "$stack_log" ]] && grep -qE '^Thread [0-9]+|^#[0-9]+' "$stack_log"; then
          stack_captured="yes"
        fi
      fi
    fi
  fi
fi

{
  quote_tsv "$started_at"; printf '\t'
  quote_tsv "$result"; printf '\t'
  quote_tsv "$froze_or_unresponsive"; printf '\t'
  quote_tsv "$close_responded"; printf '\t'
  quote_tsv "$process_alive_after_close"; printf '\t'
  quote_tsv "$stack_captured"; printf '\t'
  quote_tsv "$managed_stack_captured"; printf '\t'
  quote_tsv "$gdb_status"; printf '\t'
  quote_tsv "$window_id"; printf '\t'
  quote_tsv "$geometry_text"; printf '\t'
  quote_tsv "$focus_x"; printf '\t'
  quote_tsv "$line_y"; printf '\t'
  quote_tsv "$scroll_start_x"; printf '\t'
  quote_tsv "$scroll_y"; printf '\t'
  quote_tsv "$scroll_end_x"; printf '\t'
  quote_tsv "$drag_start_x"; printf '\t'
  quote_tsv "$drag_end_x"; printf '\t'
  quote_tsv "$stdout_log"; printf '\t'
  quote_tsv "$stderr_log"; printf '\t'
  quote_tsv "$cpu_log"; printf '\t'
  quote_tsv "$stack_log"; printf '\t'
  quote_tsv "$managed_stack_log"; printf '\t'
  quote_tsv "$notes"; printf '\n'
} >> "$summary"

echo "$PREFIX Probe recorded as $result."
echo "$PREFIX Summary: $summary"

trap - EXIT
cleanup
