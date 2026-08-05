#!/usr/bin/env bash
set -uo pipefail

if [ $# -lt 1 ] || [ ! -r "$1" ]; then
  echo "devcap-report: missing or unreadable CSV: $1" >&2
  exit 1
fi

CSV="$1"

# Read CSV, skip comments and header, parse fields
# Fields: frameIndex,monotonicMs,simTick,screenState,causeVisible
# We need monotonicMs (field 1), screenState (field 3), causeVisible (field 4)

# Collect all data rows into arrays
declare -a MONOTONIC=()
declare -a STATES=()
declare -a CAUSEVIS=()

while IFS= read -r line || [ -n "$line" ]; do
  # Skip comments
  case "$line" in
    \#*) continue ;;
  esac
  # Skip header line
  case "$line" in
    frameIndex,*) continue ;;
  esac
  # Parse: split on comma, take fields 1, 3, 4 (0-indexed: 1, 3, 4)
  IFS=',' read -ra FIELDS <<< "$line"
  if [ "${#FIELDS[@]}" -lt 5 ]; then
    continue
  fi
  MONOTONIC+=("${FIELDS[1]}")
  STATES+=("${FIELDS[3]}")
  CAUSEVIS+=("${FIELDS[4]}")
done < "$CSV"

NUM_ROWS=${#MONOTONIC[@]}
if [ "$NUM_ROWS" -eq 0 ]; then
  echo "devcap-report: no data rows in CSV" >&2
  exit 1
fi

# Identify FailureReview runs
# A run is a maximal consecutive sequence of rows where screenState==FailureReview
# For each run, check:
#   (a) at least one prior row before first FR row
#   (b) at least one causeVisible==1 row in the run
#   (c) followed later by at least one Playing row

declare -a FR_RUNS_START=()  # index of first FR row in each run
declare -a FR_RUNS_END=()   # index of last FR row in each run

cur_run_start=-1
for (( i=0; i<NUM_ROWS; i++ )); do
  if [ "${STATES[$i]}" = "FailureReview" ]; then
    if [ $cur_run_start -eq -1 ]; then
      cur_run_start=$i
    fi
  else
    if [ $cur_run_start -ne -1 ]; then
      FR_RUNS_START+=("$cur_run_start")
      FR_RUNS_END+=("$((i-1))")
      cur_run_start=-1
    fi
  fi
done
# Handle trailing run
if [ $cur_run_start -ne -1 ]; then
  FR_RUNS_START+=("$cur_run_start")
  FR_RUNS_END+=("$((NUM_ROWS-1))")
fi

# For each FR run, determine if it's a complete cycle
# Complete cycle criteria:
#   (a) at least one prior row before first FR row
#   (b) at least one causeVisible==1 in the run
#   (c) followed later by at least one Playing row

declare -a CAUSE_INTERVALS=()
declare -a RETRY_INTERVALS=()
NUM_CYCLES=0

for (( ri=0; ri<${#FR_RUNS_START[@]}; ri++ )); do
  fstart=${FR_RUNS_START[$ri]}
  fend=${FR_RUNS_END[$ri]}

  # (a) at least one prior row
  if [ $fstart -eq 0 ]; then
    continue
  fi

  # (b) at least one causeVisible==1 in the run
  has_cause=0
  first_cause_idx=-1
  for (( ci=fstart; ci<=fend; ci++ )); do
    if [ "${CAUSEVIS[$ci]}" = "1" ]; then
      has_cause=1
      if [ $first_cause_idx -eq -1 ]; then
        first_cause_idx=$ci
      fi
    fi
  done
  if [ $has_cause -eq 0 ]; then
    continue
  fi

  # (c) followed later by at least one Playing row
  # Find first Playing row after the run
  playing_idx=-1
  for (( pi=fend+1; pi<NUM_ROWS; pi++ )); do
    if [ "${STATES[$pi]}" = "Playing" ]; then
      playing_idx=$pi
      break
    fi
  done
  if [ $playing_idx -eq -1 ]; then
    continue
  fi

  # This is a complete cycle
  NUM_CYCLES=$((NUM_CYCLES + 1))

  # cause interval = monotonicMs(first causeVisible==1 row) - monotonicMs(row before first FR row)
  cause_ms=$(( MONOTONIC[first_cause_idx] - MONOTONIC[fstart-1] ))
  CAUSE_INTERVALS+=("$cause_ms")

  # retry interval = monotonicMs(first Playing after run) - monotonicMs(last FR row)
  retry_ms=$(( MONOTONIC[playing_idx] - MONOTONIC[fend] ))
  RETRY_INTERVALS+=("$retry_ms")
done

# Check if we have >=20 cycles
cause_count=${#CAUSE_INTERVALS[@]}
retry_count=${#RETRY_INTERVALS[@]}

if [ "$cause_count" -lt 20 ] || [ "$retry_count" -lt 20 ]; then
  echo "devcap-report: only $cause_count complete cycles (cause=$cause_count retry=$retry_count) — need 20"
  exit 1
fi

# Sort intervals ascending
IFS=$'\n' CAUSE_SORTED=($(printf '%s\n' "${CAUSE_INTERVALS[@]}" | sort -n)); unset IFS
IFS=$'\n' RETRY_SORTED=($(printf '%s\n' "${RETRY_INTERVALS[@]}" | sort -n)); unset IFS

# p95 index = ceil(0.95 * n) - 1 (0-based)
# For n=20: ceil(19) - 1 = 19 - 1 = 18
p95_idx=$(( (95 * NUM_CYCLES + 99) / 100 - 1 ))

CAUSE_P95=${CAUSE_SORTED[$p95_idx]}
RETRY_P95=${RETRY_SORTED[$p95_idx]}

# Output exactly five lines
printf 'CYCLES=%d\n' "$NUM_CYCLES"
printf 'CAUSE_MS_TABLE=%s\n' "$(IFS=,; echo "${CAUSE_SORTED[*]}")"
printf 'RETRY_MS_TABLE=%s\n' "$(IFS=,; echo "${RETRY_SORTED[*]}")"
printf 'CAUSE_P95=%d\n' "$CAUSE_P95"
printf 'RETRY_P95=%d\n' "$RETRY_P95"

exit 0
