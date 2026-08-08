#!/usr/bin/env bash
# Build gate — TODO(stack): no build target until the engine lands.
# Android/Google Play target: expect an AAB assembly step here (engine batch build or gradle bundle).
set -euo pipefail

# CM-C10 criterion 9: fail closed on staged-tree drift before any build step. Check mode
# ONLY — the build gate never writes the staged tree, so the write-mode flag is refused
# outright. Forwarded args (e.g. --root <dir>) reach the stager untouched.
# NOTE: this does NOT cover the real device build path (Unity GUI Build And Run /
# unity/Assets/Editor/CatMetroCliBuild.cs:10); the shelved assert-only ContentSync re-cut
# (CM-C10 R1) is the follow-up that will.
for arg in "$@"; do
  if [ "$arg" = "--apply" ]; then
    echo "build: refusing --apply — the build gate runs stage-content.sh check-only" >&2
    exit 1
  fi
done
bash "$(dirname "$0")/stage-content.sh" "$@"

echo "build: nothing to build yet — engine scaffold lands with the specs (TODO(stack))"
