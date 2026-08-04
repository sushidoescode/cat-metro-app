#!/usr/bin/env bash
# CM-C6 criterion 6: the credential-free validate-dailies job (ADR-0009:35). No engine licence,
# no network beyond the local restore cache, no secret, no clock read — date keys come from
# --from/--days or the config anchor row. Exits 0 iff every date resolves a salt whose board
# passes every blocking stage. Wiring into a workflow is a separate human-gated PR (Q-V).
# Usage: validate-dailies.sh [--out <artifact.json>] [--from <yyyy-MM-dd>] [--days <n>]
#                            [--board <level.json>] [--curve <curve.json>]
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
exec dotnet run --project dotnet/CatMetro.DailyTools -c Release -- "$@"
