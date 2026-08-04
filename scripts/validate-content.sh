#!/usr/bin/env bash
# CM-C5 criterion 15: the credential-free validate-content job (ADR-0009:35). No engine licence,
# no network beyond the local restore cache, no secret. Exits 0 iff every BLOCKING stage passes
# on every corpus level. Wiring into a workflow is a separate human-gated .github/** PR (Q-V).
# Usage: validate-content.sh [--corpus <path>]... [--out <report.json>] [--stamp]
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
exec dotnet run --project dotnet/CatMetro.Validator -c Release -- "$@"
