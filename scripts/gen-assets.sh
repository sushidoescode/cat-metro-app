#!/usr/bin/env bash
# gen-assets.sh — Meshy / Tripo text-to-3D generation pipeline (RICH-ASSETS contract).
# Docs + license record + the human arming one-liner: docs/design/assets/PIPELINE.md
#
# ============================= KEY CUSTODY (READ FIRST) ==============================
# API keys are read from the PROCESS ENVIRONMENT ONLY:
#
#     MESHY_API_KEY   — Meshy key   (required for live 'meshy' / 'queue' runs)
#     TRIPO_API_KEY   — Tripo key   (required for live 'tripo' / 'queue' runs)
#
# This script NEVER reads keys from any file, never echoes/logs/persists a key, and
# never enables xtrace. The auth header is fed to curl as a config stream on stdin
# (process substitution), so the key appears in no argv and no file. All printed
# output passes through a redactor that strips the key values. When a required env
# var is unset, the script fails naming that variable. The HUMAN arms live runs by
# exporting the keys in their own session — agents never see the key material.
# =====================================================================================
#
# Usage:
#   bash scripts/gen-assets.sh meshy <prompt> <out.glb> [--dry-run]
#   bash scripts/gen-assets.sh tripo <prompt> <out.glb> [--dry-run]
#   bash scripts/gen-assets.sh queue <manifest.json>    [--dry-run]
#
# Outputs land under unity/Assets/Art/Generated/incoming/ (gitignored; candidates are
# LOCAL until curated + provenance'd). Each <out>.glb gets a sidecar <out>.glb.json
# (prompt, service, task id, timestamp, sha256). An existing output is SKIPped —
# delete the .glb to regenerate. --dry-run prints the requests it WOULD make
# (auth redacted) and needs no keys.
#
# Tunables (env, all optional):
#   MESHY_AI_MODEL (latest) MESHY_POLYCOUNT (30000) MESHY_TEXTURE_RES (2k)
#   MESHY_ENABLE_PBR (false) MESHY_POLL_INTERVAL (10s) MESHY_STAGE_TIMEOUT (1200s)
#   TRIPO_MODEL_VERSION (server default) TRIPO_POLL_INTERVAL (5s) TRIPO_TIMEOUT (900s)
#   DOWNLOAD_TIMEOUT (900s) GEN_ASSETS_OUT_DIR (unity/Assets/Art/Generated/incoming)

set -uo pipefail
set +x # custody discipline: xtrace stays off no matter what the caller inherited
cd "$(git rev-parse --show-toplevel)" || exit 1

MESHY_BASE="https://api.meshy.ai/openapi/v2"
TRIPO_BASE="${TRIPO_API_BASE:-https://openapi.tripo3d.ai/v3}"
OUT_DIR="${GEN_ASSETS_OUT_DIR:-unity/Assets/Art/Generated/incoming}"

MESHY_AI_MODEL="${MESHY_AI_MODEL:-latest}"
MESHY_POLYCOUNT="${MESHY_POLYCOUNT:-30000}"
MESHY_TEXTURE_RES="${MESHY_TEXTURE_RES:-2k}"
MESHY_ENABLE_PBR="${MESHY_ENABLE_PBR:-false}"
MESHY_POLL_INTERVAL="${MESHY_POLL_INTERVAL:-10}"
MESHY_STAGE_TIMEOUT="${MESHY_STAGE_TIMEOUT:-1200}"
TRIPO_POLL_INTERVAL="${TRIPO_POLL_INTERVAL:-5}"
TRIPO_TIMEOUT="${TRIPO_TIMEOUT:-900}"
DOWNLOAD_TIMEOUT="${DOWNLOAD_TIMEOUT:-900}"

DRY_RUN=0

# --- custody helpers -----------------------------------------------------------------

redact() { # every byte this script prints goes through here
  local s="$1"
  [ -n "${MESHY_API_KEY:-}" ] && s=${s//"$MESHY_API_KEY"/[REDACTED]}
  [ -n "${TRIPO_API_KEY:-}" ] && s=${s//"$TRIPO_API_KEY"/[REDACTED]}
  printf '%s\n' "$s"
}

say() { redact "$*"; }
err() { redact "$*" >&2; }

die() { err "gen-assets: ERROR: $*"; exit 1; }

need_key() { # $1 = env var NAME; only its name is ever printed
  local name="$1"
  if [ -z "${!name:-}" ]; then
    die "$name is not set in the environment. Live generation is armed by the human: export $name in your own session (see docs/design/assets/PIPELINE.md), then re-run. Use --dry-run to preview requests without keys."
  fi
}

api_curl() { # $1 = key value, rest = curl args. Key travels via stdin config: no argv, no file.
  local key="$1"; shift
  curl -sS --fail-with-body --max-time 120 \
    --config <(printf 'header = "Authorization: Bearer %s"\nheader = "Content-Type: application/json"\n' "$key") \
    "$@"
}

# --- small utilities -----------------------------------------------------------------

json_get() { # $1 = dotted path; JSON on stdin; exits 1 if path missing/null
  python3 -c '
import json, sys
try:
    obj = json.load(sys.stdin)
    for p in sys.argv[1].split("."):
        obj = obj[p]
    if obj is None:
        sys.exit(1)
    print(obj)
except Exception:
    sys.exit(1)
' "$1"
}

sha256_of() {
  if command -v shasum >/dev/null 2>&1; then shasum -a 256 "$1" | awk '{print $1}'
  else sha256sum "$1" | awk '{print $1}'; fi
}

utc_now() { date -u +%Y-%m-%dT%H:%M:%SZ; }

resolve_out() { # bare filename -> incoming dir; explicit paths honored
  case "$1" in
    */*) printf '%s\n' "$1" ;;
    *)   printf '%s/%s\n' "$OUT_DIR" "$1" ;;
  esac
}

write_sidecar() { # out, service, prompt, task_ids (comma-joined), extra_note
  python3 -c '
import json, sys, hashlib
out, service, prompt, task_ids, sha, ts, note = sys.argv[1:8]
side = {
    "prompt": prompt,
    "service": service,
    "task_id": task_ids,
    "timestamp_utc": ts,
    "sha256": sha,
    "note": note,
}
with open(out + ".json", "w") as f:
    json.dump(side, f, indent=2)
    f.write("\n")
' "$1" "$2" "$3" "$4" "$5" "$6" "$7"
}

download_to() { # url, out — no auth header: model URLs are signed, keys never sent to CDNs
  local url="$1" out="$2" tmp
  tmp="$out.part"
  if ! curl -sS -L --fail --max-time "$DOWNLOAD_TIMEOUT" -o "$tmp" "$url"; then
    rm -f "$tmp"
    return 1
  fi
  mv "$tmp" "$out"
}

finish_asset() { # out, service, prompt, task_ids, note — sidecar + report
  local sha
  sha=$(sha256_of "$1") || die "sha256 failed for $1"
  write_sidecar "$1" "$2" "$3" "$4" "$sha" "$(utc_now)" "$5" || die "sidecar write failed for $1"
  say "DONE  [$2] $1 (sha256 $sha, sidecar $1.json)"
}

# --- Meshy: preview -> refine -> download (docs/design/assets/PIPELINE.md) -----------

meshy_preview_body() {
  python3 -c '
import json, sys
print(json.dumps({
    "mode": "preview",
    "prompt": sys.argv[1],
    "ai_model": sys.argv[2],
    "topology": "triangle",
    "target_polycount": int(sys.argv[3]),
}))' "$1" "$MESHY_AI_MODEL" "$MESHY_POLYCOUNT"
}

meshy_refine_body() { # $1 = preview task id (or placeholder in dry-run)
  python3 -c '
import json, sys
print(json.dumps({
    "mode": "refine",
    "preview_task_id": sys.argv[1],
    "enable_pbr": sys.argv[2] == "true",
    "texture_resolution": sys.argv[3],
}))' "$1" "$MESHY_ENABLE_PBR" "$MESHY_TEXTURE_RES"
}

meshy_poll() { # $1 = task id; prints final task JSON on success
  local id="$1" waited=0 resp status progress
  while :; do
    resp=$(api_curl "$MESHY_API_KEY" "$MESHY_BASE/text-to-3d/$id") || return 1
    status=$(printf '%s' "$resp" | json_get status) || { err "meshy: unparseable poll response for task $id"; return 1; }
    progress=$(printf '%s' "$resp" | json_get progress) || progress="?"
    case "$status" in
      SUCCEEDED) printf '%s\n' "$resp"; return 0 ;;
      FAILED|CANCELED)
        err "meshy: task $id ended $status: $(printf '%s' "$resp" | json_get task_error.message || echo 'no error message')"
        return 1 ;;
      *) say "  meshy task $id: $status ${progress}% (waited ${waited}s)" ;;
    esac
    [ "$waited" -ge "$MESHY_STAGE_TIMEOUT" ] && { err "meshy: task $id timed out after ${MESHY_STAGE_TIMEOUT}s"; return 1; }
    sleep "$MESHY_POLL_INTERVAL"; waited=$((waited + MESHY_POLL_INTERVAL))
  done
}

meshy_generate() { # prompt, out
  local prompt="$1" out="$2" body resp pid rid final glb
  body=$(meshy_preview_body "$prompt")
  if [ "$DRY_RUN" -eq 1 ]; then
    say "DRY-RUN [meshy] would POST $MESHY_BASE/text-to-3d"
    say "  headers: Authorization: Bearer [REDACTED] | Content-Type: application/json"
    say "  body: $body"
    say "  then: poll GET $MESHY_BASE/text-to-3d/<task-id> every ${MESHY_POLL_INTERVAL}s (timeout ${MESHY_STAGE_TIMEOUT}s) until SUCCEEDED"
    say "  then: POST $MESHY_BASE/text-to-3d (refine): $(meshy_refine_body '<preview-task-id>')"
    say "  then: poll refine task, download model_urls.glb -> $out (+ sidecar $out.json)"
    return 0
  fi
  need_key MESHY_API_KEY
  say "MESHY create preview: $prompt"
  resp=$(api_curl "$MESHY_API_KEY" -X POST -d "$body" "$MESHY_BASE/text-to-3d") || { err "meshy: preview create failed: $(redact "$resp")"; return 1; }
  pid=$(printf '%s' "$resp" | json_get result) || { err "meshy: no task id in create response"; return 1; }
  say "  preview task: $pid"
  meshy_poll "$pid" >/dev/null || return 1
  say "MESHY create refine (texture) for $pid"
  resp=$(api_curl "$MESHY_API_KEY" -X POST -d "$(meshy_refine_body "$pid")" "$MESHY_BASE/text-to-3d") || { err "meshy: refine create failed: $(redact "$resp")"; return 1; }
  rid=$(printf '%s' "$resp" | json_get result) || { err "meshy: no refine task id"; return 1; }
  say "  refine task: $rid"
  final=$(meshy_poll "$rid") || return 1
  glb=$(printf '%s' "$final" | json_get model_urls.glb) || { err "meshy: no GLB url on refine task $rid"; return 1; }
  say "  downloading GLB (signed URL) -> $out"
  download_to "$glb" "$out" || { err "meshy: download failed for $rid"; return 1; }
  finish_asset "$out" "meshy" "$prompt" "preview:$pid refine:$rid" "meshy ai_model=$MESHY_AI_MODEL polycount=$MESHY_POLYCOUNT pbr=$MESHY_ENABLE_PBR tex=$MESHY_TEXTURE_RES"
}

# --- Tripo: create -> poll -> download (URLs expire fast: fetch immediately) ---------

tripo_body() {
  python3 -c '
import json, sys
b = {"prompt": sys.argv[1]}
if sys.argv[2]:
    b["model"] = sys.argv[2]
print(json.dumps(b))' "$1" "${TRIPO_MODEL_VERSION:-}"
}

tripo_generate() { # prompt, out
  local prompt="$1" out="$2" body resp tid waited=0 status progress url
  body=$(tripo_body "$prompt")
  if [ "$DRY_RUN" -eq 1 ]; then
    say "DRY-RUN [tripo] would POST $TRIPO_BASE/generation/text-to-model"
    say "  headers: Authorization: Bearer [REDACTED] | Content-Type: application/json"
    say "  body: $body"
    say "  then: poll GET $TRIPO_BASE/tasks/<task-id> every ${TRIPO_POLL_INTERVAL}s (timeout ${TRIPO_TIMEOUT}s) until status=success"
    say "  then: download output.model_url IMMEDIATELY (signed URL, short expiry) -> $out (+ sidecar $out.json)"
    return 0
  fi
  need_key TRIPO_API_KEY
  say "TRIPO create: $prompt"
  resp=$(api_curl "$TRIPO_API_KEY" -X POST -d "$body" "$TRIPO_BASE/generation/text-to-model") || { err "tripo: create failed: $(redact "$resp")"; return 1; }
  tid=$(printf '%s' "$resp" | json_get data.task_id) || { err "tripo: no task id in create response"; return 1; }
  say "  task: $tid"
  while :; do
    resp=$(api_curl "$TRIPO_API_KEY" "$TRIPO_BASE/tasks/$tid") || return 1
    status=$(printf '%s' "$resp" | json_get data.status) || { err "tripo: unparseable poll response for $tid"; return 1; }
    progress=$(printf '%s' "$resp" | json_get data.progress) || progress="?"
    case "$status" in
      success) break ;;
      failed|cancelled|banned|expired) err "tripo: task $tid ended $status"; return 1 ;;
      *) say "  tripo task $tid: $status ${progress}% (waited ${waited}s)" ;;
    esac
    [ "$waited" -ge "$TRIPO_TIMEOUT" ] && { err "tripo: task $tid timed out after ${TRIPO_TIMEOUT}s"; return 1; }
    sleep "$TRIPO_POLL_INTERVAL"; waited=$((waited + TRIPO_POLL_INTERVAL))
  done
  url=$(printf '%s' "$resp" | json_get data.output.model_url) \
    || url=$(printf '%s' "$resp" | json_get data.output.pbr_model) \
    || url=$(printf '%s' "$resp" | json_get data.output.model) \
    || { err "tripo: no model url on task $tid"; return 1; }
  say "  downloading GLB (signed URL, short expiry) -> $out"
  download_to "$url" "$out" || { err "tripo: download failed for $tid (signed URLs expire in minutes — re-run to regenerate)"; return 1; }
  finish_asset "$out" "tripo" "$prompt" "$tid" "tripo model=${TRIPO_MODEL_VERSION:-server-default}"
}

# --- one asset / batch ---------------------------------------------------------------

generate_one() { # service, prompt, out_arg
  local service="$1" prompt="$2" out
  out=$(resolve_out "$3")
  case "$out" in *.glb) ;; *) die "output must end in .glb (got: $out)";; esac
  if [ "$DRY_RUN" -eq 0 ]; then
    mkdir -p "$(dirname "$out")" || die "cannot create output dir for $out"
    if [ -f "$out" ]; then say "SKIP  [$service] $out exists (delete it to regenerate)"; return 0; fi
  fi
  case "$service" in
    meshy) meshy_generate "$prompt" "$out" ;;
    tripo) tripo_generate "$prompt" "$out" ;;
    *) die "unknown service '$service' (want: meshy | tripo)" ;;
  esac
}

queue_run() { # manifest path
  local manifest="$1" total=0 failed=0 line id service out prompt
  [ -f "$manifest" ] || die "manifest not found: $manifest"
  while IFS=$'\t' read -r id service out prompt; do
    total=$((total + 1))
    say "--- [$total] $id ($service) ---"
    if ! generate_one "$service" "$prompt" "$out"; then
      err "queue: entry '$id' FAILED (continuing)"
      failed=$((failed + 1))
    fi
  done < <(python3 -c '
import json, sys
d = json.load(open(sys.argv[1]))
for a in d["assets"]:
    prompt = " ".join(a["prompt"].split())
    print("\t".join([a["id"], a["service"], a["out"], prompt]))
' "$manifest")
  [ "$total" -gt 0 ] || die "manifest has no assets: $manifest"
  say "queue: $((total - failed))/$total succeeded"
  [ "$failed" -eq 0 ]
}

# --- entrypoint ----------------------------------------------------------------------

usage() {
  cat <<'EOF'
gen-assets.sh — Meshy / Tripo text-to-3D generation (RICH-ASSETS pipeline)

KEY CUSTODY — keys come from the PROCESS ENVIRONMENT ONLY (never from any file):
    MESHY_API_KEY   Meshy key   (required for live 'meshy' / 'queue' runs)
    TRIPO_API_KEY   Tripo key   (required for live 'tripo' / 'queue' runs)
The HUMAN arms live runs by exporting these in their own session; the script never
echoes, logs, or persists a key. See docs/design/assets/PIPELINE.md.

Usage:
  bash scripts/gen-assets.sh meshy <prompt> <out.glb> [--dry-run]
  bash scripts/gen-assets.sh tripo <prompt> <out.glb> [--dry-run]
  bash scripts/gen-assets.sh queue <manifest.json>    [--dry-run]

Outputs land under unity/Assets/Art/Generated/incoming/ (gitignored: candidates stay
local until curated + provenance'd), each with a sidecar <out>.glb.json (prompt,
service, task id, timestamp, sha256). Existing outputs are SKIPped — delete the .glb
to regenerate. --dry-run prints the requests it WOULD make (auth redacted), no keys
needed.

Tunables (env, optional): MESHY_AI_MODEL MESHY_POLYCOUNT MESHY_TEXTURE_RES
  MESHY_ENABLE_PBR MESHY_POLL_INTERVAL MESHY_STAGE_TIMEOUT TRIPO_MODEL_VERSION
  TRIPO_POLL_INTERVAL TRIPO_TIMEOUT DOWNLOAD_TIMEOUT GEN_ASSETS_OUT_DIR
EOF
}

main() {
  local args=() a cmd
  for a in "$@"; do
    case "$a" in
      --dry-run) DRY_RUN=1 ;;
      *) args+=("$a") ;;
    esac
  done
  [ "${#args[@]}" -ge 1 ] || { usage; exit 1; }
  cmd="${args[0]}"
  case "$cmd" in
    meshy|tripo)
      [ "${#args[@]}" -eq 3 ] || die "usage: gen-assets.sh $cmd <prompt> <out.glb> [--dry-run]"
      generate_one "$cmd" "${args[1]}" "${args[2]}"
      ;;
    queue)
      [ "${#args[@]}" -eq 2 ] || die "usage: gen-assets.sh queue <manifest.json> [--dry-run]"
      queue_run "${args[1]}"
      ;;
    -h|--help|help) usage ;;
    *) die "unknown subcommand '$cmd' (want: meshy | tripo | queue)" ;;
  esac
}

main "$@"
