#!/usr/bin/env bash
# scripts/stage-content.sh — CM-C10: the single automated author of the staged derived tree
# unity/Assets/StreamingAssets/** (R1/HC-1; state/backlog.md staged-derived-tree exception class).
#
# Modes
#   (default)      CHECK — read-only: names every drifted path, exits non-zero on drift,
#                  writes nothing. Safe anywhere; scripts/build.sh calls exactly this.
#   --apply        the ONLY writing mode: byte-verbatim copy + scoped prune + .meta fill-in.
#   --root <dir>   treat <dir> as the repo root (the scripts/check.sh:14-19 convention).
#
# Rules — an explicit allowlist, never a directory sweep (criterion 2). content/ also holds
# generator inputs and config/ holds non-shipping tool config (pins, validator thresholds,
# daily pipeline) — none of that may ever ship (docs/architecture/overview.md:388):
#   rule 1: content/levels/*.json      -> unity/Assets/StreamingAssets/content/levels/
#           (ADR-0008:31-45 — authored levels are the source of truth; staged copy ships)
#   rule 2: config/runtime_bounds.json -> unity/Assets/StreamingAssets/config/
#           (ADR-0008:31-45,57-77 — bounds ship beside content, unindexed by any catalog)
#
# Byte copy only: no re-serialization, no key reordering, no formatting pass (ADR-0008:64-66).
# The byte-identity gate at tests/unity/editmode.test.sh:18-26 and the credential-free ci
# clause (ADR-0009:33) VERIFY this tree; this script is the only thing that AUTHORS it.
# Neither gate ever calls this script and this script never runs a gate (stop condition 2).
#
# .meta policy (R1/HC-2, criterion 6): an existing .meta is never rewritten — guid stability
# outranks uniformity (the six editor-generated guids on main are preserved forever). A staged
# payload with no .meta gets one generated in the shipped DefaultImporter shape with a
# deterministic path-derived guid (A-5 — properties, no pinned literal):
#     guid = first 32 lowercase hex chars of sha256("catmetro-meta-v1:" + payload path
#            relative to unity/Assets/StreamingAssets)
# Root-independent by construction, so every machine and every run derives the same guid;
# the wrapper re-derives it with an independent implementation (python3 hashlib).
#
# Prune (criterion 5): a destination file whose NAME matches a rule pattern but has no source
# counterpart is removed together with its .meta — write mode only. Anything else inside a
# rule directory is reported as drift and never deleted; anything outside the two rule
# directories is untouched and unreported (the future catalog artifacts live there, H2).
set -uo pipefail

usage() { echo "usage: stage-content.sh [--root <dir>] [--apply]   (default: check mode, read-only)"; }

apply=0; root=""
while [ $# -gt 0 ]; do
  case "$1" in
    --apply) apply=1; shift ;;
    --root)  root="${2:?stage-content.sh: --root needs a directory}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "stage-content.sh: unknown argument '$1'" >&2; usage >&2; exit 2 ;;
  esac
done

[ -n "$root" ] || root="$(git rev-parse --show-toplevel)"
[ -d "$root" ] || { echo "stage-content.sh: root '$root' is not a directory" >&2; exit 2; }
case "$root" in /*) ;; *) root="$(cd "$root" && pwd)" ;; esac
dest_base="unity/Assets/StreamingAssets"
sa="$root/$dest_base"
mode=check; [ "$apply" -eq 1 ] && mode=apply
echo "stage-content: root=$root mode=$mode dest=$root/$dest_base"
echo "stage-content: rule 1: content/levels/*.json -> $dest_base/content/levels/ (ADR-0008:31-45)"
echo "stage-content: rule 2: config/runtime_bounds.json -> $dest_base/config/ (ADR-0008:31-45,57-77)"

drift=0
report() { echo "stage-content: DRIFT — $1"; drift=1; }
note()   { echo "stage-content: $1"; }

guid_for() { printf 'catmetro-meta-v1:%s' "$1" | shasum -a 256 | cut -d' ' -f1 | cut -c1-32; }

write_meta() { # $1 = absolute .meta path; $2 = payload path relative to $dest_base
  {
    printf 'fileFormatVersion: 2\n'
    printf 'guid: %s\n' "$(guid_for "$2")"
    printf 'DefaultImporter:\n'
    printf '  externalObjects: {}\n'
    printf '  userData: \n'
    printf '  assetBundleName: \n'
    printf '  assetBundleVariant: \n'
  } > "$1"
}

match() { case "$1" in $2) return 0 ;; *) return 1 ;; esac; }

stage_rule() { # $1 = source dir (root-relative); $2 = filename glob; $3 = dest dir ($dest_base-relative)
  local srcdir="$root/$1" pat="$2" destrel="$3" destdir="$sa/$3" f b dst pay
  if [ ! -d "$srcdir" ]; then report "source dir missing: $1"; return; fi
  # forward leg: every matching source staged byte-identically, with a .meta sibling
  for f in "$srcdir"/$pat; do
    [ -e "$f" ] || continue
    b="$(basename "$f")"
    dst="$destdir/$b"
    if [ ! -f "$dst" ]; then
      if [ "$apply" -eq 1 ]; then mkdir -p "$destdir"; cp "$f" "$dst"; note "staged $destrel/$b"
      else report "missing staged copy: $dest_base/$destrel/$b"; fi
    elif ! cmp -s "$f" "$dst"; then
      if [ "$apply" -eq 1 ]; then cp "$f" "$dst"; note "restaged (byte drift) $destrel/$b"
      else report "byte drift: $dest_base/$destrel/$b"; fi
    fi
    if [ -f "$dst" ] && [ ! -f "$dst.meta" ]; then
      if [ "$apply" -eq 1 ]; then write_meta "$dst.meta" "$destrel/$b"; note "generated .meta for $destrel/$b"
      else report "missing .meta: $dest_base/$destrel/$b.meta"; fi
    fi
  done
  # reverse leg: prune stale pattern-matched files; report anything foreign, delete nothing else
  [ -d "$destdir" ] || return 0
  for d in "$destdir"/* "$destdir"/.[!.]*; do
    [ -e "$d" ] || continue
    b="$(basename "$d")"
    if [ -d "$d" ]; then report "foreign directory in rule dir (left in place): $dest_base/$destrel/$b"; continue; fi
    case "$b" in
      *.meta)
        pay="${b%.meta}"
        if match "$pay" "$pat"; then
          if [ ! -f "$destdir/$pay" ] && [ ! -f "$srcdir/$pay" ]; then
            if [ "$apply" -eq 1 ]; then rm "$d"; note "pruned stale .meta $destrel/$b"
            else report "stale staged .meta: $dest_base/$destrel/$b"; fi
          fi
        else
          report "foreign file in rule dir (left in place): $dest_base/$destrel/$b"
        fi
        ;;
      *)
        if match "$b" "$pat"; then
          if [ ! -f "$srcdir/$b" ]; then
            if [ "$apply" -eq 1 ]; then
              rm "$d"
              [ ! -f "$d.meta" ] || rm "$d.meta"
              note "pruned stale $destrel/$b (+.meta)"
            else
              report "stale staged file: $dest_base/$destrel/$b"
            fi
          fi
        else
          report "foreign file in rule dir (left in place): $dest_base/$destrel/$b"
        fi
        ;;
    esac
  done
  return 0
}

stage_rule "content/levels" "*.json" "content/levels"
stage_rule "config" "runtime_bounds.json" "config"

if [ "$drift" -ne 0 ]; then
  if [ "$mode" = check ]; then
    echo "stage-content: DRIFT detected — check mode wrote nothing"
  else
    echo "stage-content: unresolved drift remains — foreign files are never deleted"
  fi
  exit 1
fi
echo "stage-content: OK ($mode)"
exit 0
