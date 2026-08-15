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
#   MESHY_API_BASE (https://api.meshy.ai/openapi/v2; https:// enforced)
#   TRIPO_MODEL_VERSION (server default) TRIPO_POLL_INTERVAL (5s) TRIPO_TIMEOUT (900s)
#   TRIPO_API_BASE (https://openapi.tripo3d.ai/v3; https:// enforced)
#   DOWNLOAD_TIMEOUT (900s) GEN_ASSETS_OUT_DIR (unity/Assets/Art/Generated/incoming)
#   GEN_ASSETS_ACCOUNT_TIER (unknown; recorded per-asset in the sidecar for the license ADR)

set -uo pipefail
set +x # custody discipline: xtrace stays off no matter what the caller inherited
cd "$(git rev-parse --show-toplevel)" || exit 1

MESHY_BASE="${MESHY_API_BASE:-https://api.meshy.ai/openapi/v2}"
TRIPO_BASE="${TRIPO_API_BASE:-https://openapi.tripo3d.ai/v3}"
# F4 (hardened per fix-round review): MESHY_API_BASE / TRIPO_API_BASE override where each
# bearer token is sent. An https-only check still let a stray base redirect the key to any
# https host; a HOST ALLOWLIST closes the vector entirely — only each vendor's own host (or
# loopback, for the offline custody test) may ever carry the credential. The default hosts
# are covered; a legitimate alt-host is a reviewed edit here, not a runtime surprise. Each
# resolved base is still surfaced in live output below, so even an allowed base is never silent.
case "$MESHY_BASE" in
  https://api.meshy.ai/*|https://127.0.0.1:*|https://localhost:*) ;;
  *) echo "gen-assets: MESHY_API_BASE must be the Meshy host over https (got: $MESHY_BASE)" >&2; exit 1 ;;
esac
case "$TRIPO_BASE" in
  https://openapi.tripo3d.ai/*|https://127.0.0.1:*|https://localhost:*) ;;
  *) echo "gen-assets: TRIPO_API_BASE must be the Tripo host over https (got: $TRIPO_BASE)" >&2; exit 1 ;;
esac
OUT_DIR="${GEN_ASSETS_OUT_DIR:-unity/Assets/Art/Generated/incoming}"
ACCOUNT_TIER="${GEN_ASSETS_ACCOUNT_TIER:-unknown}"

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

redact() { # routes the script's OWN printed output through key redaction (F9: curl's own
  # stderr — e.g. connection errors — bypasses this; safe because no key is ever passed
  # to curl on argv/stdout, but do not assume curl's stderr is redacted)
  local s="$1"
  [ -n "${MESHY_API_KEY:-}" ] && s=${s//"$MESHY_API_KEY"/[REDACTED]}
  [ -n "${TRIPO_API_KEY:-}" ] && s=${s//"$TRIPO_API_KEY"/[REDACTED]}
  printf '%s\n' "$s"
}

say() { redact "$*"; }
err() { redact "$*" >&2; }

die() { err "gen-assets: ERROR: $*"; exit 1; }

need_key() { # $1 = env var NAME; only its name is ever printed. Returns 1 (soft, F3) so a
             # queue armed with only one service's key continues past the entries it cannot serve.
  local name="$1"
  if [ -z "${!name:-}" ]; then
    err "gen-assets: ERROR: $name is not set in the environment. Live generation is armed by the human: export $name in your own session (see docs/design/assets/PIPELINE.md), then re-run. Use --dry-run to preview requests without keys."
    return 1
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

resolve_out() { # bare filename -> incoming dir. F5: the manifest is external DATA, so an
  # 'out' with a path separator or '..' could steer a network-fetched file into the
  # tracked Unity tree, outside the gitignored candidate area and its curation gate. Only
  # bare filenames are honored; anything with '/' or '..' is refused by the caller.
  case "$1" in
    *..*|*/*) return 1 ;;
    *)        printf '%s/%s\n' "$OUT_DIR" "$1" ;;
  esac
}

write_sidecar() { # out, service, prompt, task_ids (comma-joined), sha, ts, note, tier
  python3 -c '
import json, sys
out, service, prompt, task_ids, sha, ts, note, tier = sys.argv[1:9]
side = {
    "prompt": prompt,
    "service": service,
    "task_id": task_ids,
    "timestamp_utc": ts,
    "sha256": sha,
    "plan_tier": tier,   # F8: the license depends on account tier; recorded from
                         # GEN_ASSETS_ACCOUNT_TIER (the human sets it when arming), so the
                         # asset ADR never has to guess. "unknown" if not supplied.
    "note": note,
}
with open(out + ".json", "w") as f:
    json.dump(side, f, indent=2)
    f.write("\n")
' "$1" "$2" "$3" "$4" "$5" "$6" "$7" "$8"
}

download_to() { # url, out — no auth header (URLs are signed); https-only, size-capped, glTF-verified
  local url="$1" out="$2" tmp
  # F1: the url is vendor-supplied (RK-6 external DATA). A bare curl argument beginning
  # with '-' is an OPTION, and one token (-K/--config) makes curl execute an attacker
  # file — arbitrary local read/write while the human's keys are armed. Two independent
  # defenses: require an https:// scheme (so it can never begin with '-'), and pass the
  # url after '--'. F6: cap the size (a 4GB response must not fill the disk and be
  # certified by a sidecar), pin the protocol across redirects, and verify the payload
  # is a binary glTF before it lands where Unity will auto-import it.
  case "$url" in
    https://*) ;;
    *) err "refusing non-https download url"; return 1 ;;
  esac
  mkdir -p "$(dirname "$out")" || { err "cannot create output dir for $out"; return 1; }
  tmp="$out.part"
  # --globoff: a vendor url with {a,b} / [1-9] must not fan out into multiple requests.
  if ! curl -sS -L --fail --globoff --proto '=https' --proto-redir '=https' \
       --max-filesize 268435456 --max-time "$DOWNLOAD_TIMEOUT" -o "$tmp" -- "$url"; then
    rm -f "$tmp"
    return 1
  fi
  if [ "$(head -c 4 "$tmp" 2>/dev/null)" != "glTF" ]; then
    err "refusing download: payload is not a binary glTF (.glb)"
    rm -f "$tmp"
    return 1
  fi
  mv "$tmp" "$out"
}

finish_asset() { # out, service, prompt, task_ids, note — sidecar + report (soft-fail: F3)
  local sha
  sha=$(sha256_of "$1") || { err "sha256 failed for $1"; return 1; }
  write_sidecar "$1" "$2" "$3" "$4" "$sha" "$(utc_now)" "$5" "$ACCOUNT_TIER" \
    || { err "sidecar write failed for $1"; return 1; }
  say "DONE  [$2] $1 (sha256 $sha, tier $ACCOUNT_TIER, sidecar $1.json)"
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
      # LIVE-VERIFIED 2026-08-14: progress goes to STDERR, never stdout. This function's
      # stdout is its DATA CHANNEL — the caller does `final=$(meshy_poll "$rid")` and parses
      # it as JSON. Emitting progress on stdout put "  meshy task …%" lines in front of the
      # JSON, so json_get died and every Meshy asset failed with "no GLB url on refine task"
      # (7/7 in the human's first live queue; Tripo was immune because it polls inline, which
      # is why the failure split perfectly along service lines). The preview poll hid the bug
      # by discarding stdout with >/dev/null — which also swallowed the progress lines, so the
      # log looked like polling never happened at all.
      *) redact "  meshy task $id: $status ${progress}% (waited ${waited}s)" >&2 ;;
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
  need_key MESHY_API_KEY || return 1
  say "MESHY create preview: $prompt (base $MESHY_BASE)"
  resp=$(api_curl "$MESHY_API_KEY" -X POST -d "$body" "$MESHY_BASE/text-to-3d") || { err "meshy: preview create failed: $(redact "$resp")"; return 1; }
  pid=$(printf '%s' "$resp" | json_get result) || { err "meshy: no task id in create response"; return 1; }
  say "  preview task: $pid"
  # >/dev/null discards only the final JSON (the data channel); progress now goes to stderr
  # so the preview stage is visible in the log exactly like Tripo's inline polling.
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

# LIVE-VERIFIED 2026-08-14: Tripo v3 REQUIRES `model`. Omitting it returns HTTP 400 with
# code 1004 "model is required, allowed values: P1-20260311, v2.5-20250123, v3.0-20250812,
# v3.1-20260211". The original shape sent `model` only when TRIPO_MODEL_VERSION was set, and
# the default was unset — so every live tripo call failed. Caught by the one-asset probe the
# PIPELINE doc prescribes (review finding F12: the v3 shape was documented but never
# executed), which is exactly the 1-asset-instead-of-9 cost that probe exists to pay.
# Default is the newest v-series model, matching this script's /v3 base URL; override with
# TRIPO_MODEL_VERSION for a different one from the allowed list above.
TRIPO_MODEL_DEFAULT="v3.1-20260211"

tripo_body() {
  python3 -c '
import json, sys
print(json.dumps({"prompt": sys.argv[1], "model": sys.argv[2]}))' \
    "$1" "${TRIPO_MODEL_VERSION:-$TRIPO_MODEL_DEFAULT}"
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
  need_key TRIPO_API_KEY || return 1
  say "TRIPO create: $prompt (base $TRIPO_BASE)"
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
  # F3: per-asset failures RETURN non-zero (never exit) so queue_run continues to the
  # next entry and prints its N/M summary; die() is reserved for main-level misuse.
  local service="$1" prompt="$2" out
  out=$(resolve_out "$3") || { err "output name must be a bare filename under $OUT_DIR (got: $3)"; return 1; }
  case "$out" in *.glb) ;; *) err "output must end in .glb (got: $out)"; return 1;; esac
  if [ "$DRY_RUN" -eq 0 ] && [ -f "$out" ]; then
    say "SKIP  [$service] $out exists (delete it to regenerate)"; return 0
  fi
  case "$service" in
    meshy) meshy_generate "$prompt" "$out" ;;
    tripo) tripo_generate "$prompt" "$out" ;;
    *) err "unknown service '$service' (want: meshy | tripo)"; return 1 ;;
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
  TRIPO_POLL_INTERVAL TRIPO_TIMEOUT MESHY_API_BASE TRIPO_API_BASE (both https:// enforced)
  DOWNLOAD_TIMEOUT GEN_ASSETS_OUT_DIR GEN_ASSETS_ACCOUNT_TIER (recorded per-asset in the sidecar)
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
