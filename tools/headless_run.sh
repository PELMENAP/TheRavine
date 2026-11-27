#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: tools/headless_run.sh --out DIR (--seconds S | --ticks N) [options]

Runs the simulation in Unity batchmode, one Unity process per replicate, then
runs tools/simlog_analyze.py over the logs of each replicate.

Required:
  --out DIR           output directory (created if missing)
  --seconds S         stop when SimulationClock reaches S simulated seconds
  --ticks N           stop after N frames; either or both of --seconds / --ticks

Options:
  --replicates N      number of independent runs (default 1)
  --scene PATH        scene asset path (default Assets/Scenes/TestScene/FreeScene.unity)
  --rules PATH        SimulationRules asset path (default: the one assigned in the scene)
  --fps N             fixed frame rate of simulated time (default 30)
  --wall S            fail a replicate after S wall-clock seconds (default: no limit)
  --no-analyze        skip tools/simlog_analyze.py
  -h, --help          this text

Environment:
  UNITY_PATH          path to the Unity editor executable (required)

Output files are named r<replicate>_<original name>. Replicates share no seed:
each run draws its own randomness.
USAGE
}

fail() {
  echo "headless_run: $*" >&2
  exit 2
}

out=""
seconds=""
ticks=""
replicates=1
scene=""
rules=""
fps=""
wall=""
analyze=1

while [ $# -gt 0 ]; do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    --out) [ $# -ge 2 ] || fail "--out needs a value"; out="$2"; shift 2 ;;
    --seconds) [ $# -ge 2 ] || fail "--seconds needs a value"; seconds="$2"; shift 2 ;;
    --ticks) [ $# -ge 2 ] || fail "--ticks needs a value"; ticks="$2"; shift 2 ;;
    --replicates) [ $# -ge 2 ] || fail "--replicates needs a value"; replicates="$2"; shift 2 ;;
    --scene) [ $# -ge 2 ] || fail "--scene needs a value"; scene="$2"; shift 2 ;;
    --rules) [ $# -ge 2 ] || fail "--rules needs a value"; rules="$2"; shift 2 ;;
    --fps) [ $# -ge 2 ] || fail "--fps needs a value"; fps="$2"; shift 2 ;;
    --wall) [ $# -ge 2 ] || fail "--wall needs a value"; wall="$2"; shift 2 ;;
    --no-analyze) analyze=0; shift ;;
    *) usage >&2; fail "unknown argument: $1" ;;
  esac
done

[ -n "$out" ] || { usage >&2; fail "--out is required"; }
[ -n "$seconds$ticks" ] || { usage >&2; fail "--seconds or --ticks is required"; }
case "$replicates" in ''|*[!0-9]*) fail "--replicates must be a positive integer" ;; esac
[ "$replicates" -ge 1 ] || fail "--replicates must be a positive integer"
[ -n "${UNITY_PATH:-}" ] || fail "UNITY_PATH is not set"
[ -x "$UNITY_PATH" ] || fail "UNITY_PATH is not executable: $UNITY_PATH"

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "$out"
out="$(cd "$out" && pwd)"

status=0
for ((i = 0; i < replicates; i++)); do
  args=(-batchmode -nographics -projectPath "$root" -logFile "$out/unity_r$(printf '%03d' "$i").log"
        -executeMethod HeadlessSimulation.Run
        -simOut "$out" -simReplicates 1 -simFirstReplicate "$i")
  [ -z "$seconds" ] || args+=(-simSeconds "$seconds")
  [ -z "$ticks" ] || args+=(-simTicks "$ticks")
  [ -z "$scene" ] || args+=(-simScene "$scene")
  [ -z "$rules" ] || args+=(-simRules "$rules")
  [ -z "$fps" ] || args+=(-simFps "$fps")
  [ -z "$wall" ] || args+=(-simWallSeconds "$wall")

  echo "headless_run: replicate $i"
  if ! "$UNITY_PATH" "${args[@]}"; then
    echo "headless_run: replicate $i failed, see $out/unity_r$(printf '%03d' "$i").log" >&2
    status=1
  fi
done

if [ "$analyze" -eq 1 ]; then
  for ((i = 0; i < replicates; i++)); do
    tag="r$(printf '%03d' "$i")"
    log="$(ls "$out/${tag}_simulation_log_"*.csv 2>/dev/null | head -n 1 || true)"
    [ -n "$log" ] || continue
    stamp="${log##*simulation_log_}"
    stamp="${stamp%.csv}"
    extra=()
    [ ! -f "$out/${tag}_behaviour_descriptors_${stamp}.csv" ] || descriptors="$out/${tag}_behaviour_descriptors_${stamp}.csv"
    [ ! -f "$out/${tag}_life_log_${stamp}.csv" ] || extra+=(--life "$out/${tag}_life_log_${stamp}.csv")
    [ ! -f "$out/${tag}_behaviour_motifs_${stamp}.csv" ] || extra+=(--motifs "$out/${tag}_behaviour_motifs_${stamp}.csv")
    [ ! -f "$out/${tag}_speech_tables_${stamp}.csv" ] || extra+=(--speech-tables "$out/${tag}_speech_tables_${stamp}.csv")
    if ! python3 "$root/tools/simlog_analyze.py" "$log" ${descriptors:+"$descriptors"} ${extra[@]+"${extra[@]}"} > "$out/${tag}_analysis.txt"; then
      echo "headless_run: analysis of replicate $i failed" >&2
      status=1
    fi
    unset descriptors
  done
fi

exit "$status"
