#!/usr/bin/env bash
# forge-metrics: compute the retro metrics from history instead of archaeology.
# This write-capable /forge-retro helper computes retro metrics from git history and writes result files.
# It is not the read-only /forge-metrics skill, which analyzes forge-hybrid RunRecord and risk-gate ledger telemetry.
# Usage: ./scripts/forge-metrics.sh [--attest] [days]   (default 14)
#   default  → evals/results/metrics-<date>.json          (claims tier: informational)
#   --attest → evals/results/attested/metrics-<date>.json (promotion-grade; run BY A HUMAN, commit
#              with FORGE_HUMAN_OVERRIDE=1 — the enforcement belts block agent writes there, which
#              is exactly what makes it evidence forge-trust.sh may promote on)
# Sources: git log (always) · gh CLI (PRs + CI, skipped gracefully if absent) · state/PROJECT_STATE.md tags.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
ATTEST=0; [ "${1:-}" = "--attest" ] && { ATTEST=1; shift; }
DAYS="${1:-14}"; SINCE=$(date -v-"${DAYS}"d +%Y-%m-%d 2>/dev/null || date -d "-${DAYS} days" +%Y-%m-%d)

commits=$(git log --since="$SINCE" --oneline | wc -l | tr -d ' ')
reverts=$(git log --since="$SINCE" --oneline --grep='revert' -i | wc -l | tr -d ' ')

prs_merged="n/a"; median_hours="n/a"; ci_first_pass="n/a"
if command -v gh >/dev/null 2>&1 && gh repo view >/dev/null 2>&1; then
  prs_json=$(gh pr list --state merged --limit 100 --json mergedAt,createdAt 2>/dev/null || echo '[]')
  read -r prs_merged median_hours <<<"$(PRS_JSON="$prs_json" SINCE="$SINCE" python3 - <<'PY'
import json,os,datetime as dt
since=dt.datetime.fromisoformat(os.environ["SINCE"]+"T00:00:00+00:00"); prs=json.loads(os.environ["PRS_JSON"] or "[]")
h=[]
for p in prs:
    m=dt.datetime.fromisoformat(p["mergedAt"].replace("Z","+00:00"))
    if m>=since: h.append((m-dt.datetime.fromisoformat(p["createdAt"].replace("Z","+00:00"))).total_seconds()/3600)
h.sort(); print(len(h), round(h[len(h)//2],1) if h else "n/a")
PY
)"
  runs=$(gh run list --workflow ci --limit 50 --json conclusion,createdAt 2>/dev/null || echo '[]')
  ci_first_pass=$(RUNS_JSON="$runs" SINCE="$SINCE" python3 - <<'PY'
import json,os,datetime as dt
since=dt.datetime.fromisoformat(os.environ["SINCE"]+"T00:00:00+00:00"); rs=json.loads(os.environ["RUNS_JSON"] or "[]")
rs=[r for r in rs if dt.datetime.fromisoformat(r["createdAt"].replace("Z","+00:00"))>=since and r["conclusion"]]
ok=sum(1 for r in rs if r["conclusion"]=="success")
print(f"{100*ok//len(rs)}%" if rs else "n/a")
PY
)
fi

# Human-tagged signals from the state file's metrics section (one tag-word per completed task line)
tags(){ grep -iv 'metrics tags' state/PROJECT_STATE.md 2>/dev/null | grep -io "$1" | wc -l | tr -d ' '; }
interventions=$(tags 'intervention'); escapes=$(tags 'escape'); rework=$(tags 'rework')
costs=$(grep -oE 'cost-\$[0-9]+(\.[0-9]+)?' state/PROJECT_STATE.md 2>/dev/null | grep -oE '[0-9]+(\.[0-9]+)?' | paste -sd+ - | bc 2>/dev/null || echo 0)

if [ "$ATTEST" = "1" ]; then
  out="evals/results/attested/metrics-$(date +%Y-%m-%d).json"; issuer="human:$(git config user.email 2>/dev/null || echo unknown)"
else
  out="evals/results/metrics-$(date +%Y-%m-%d).json"; issuer="claims"
fi
mkdir -p "$(dirname "$out")"
cat > "$out" <<EOF
{ "window_days": $DAYS, "since": "$SINCE", "commits": $commits, "reverts": $reverts,
  "prs_merged": "$prs_merged", "median_hours_to_merge": "$median_hours",
  "ci_first_pass_rate": "$ci_first_pass",
  "tagged": { "interventions": $interventions, "escapes": $escapes, "rework": $rework, "cost_usd_sum": "${costs:-0}" },
  "provenance": { "issuer": "$issuer", "commit_sha": "$(git rev-parse HEAD 2>/dev/null || echo none)", "generated_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)" } }
EOF
[ "$ATTEST" = "1" ] && echo "forge-metrics: ATTESTED output — commit it yourself: FORGE_HUMAN_OVERRIDE=1 git add/commit (agents cannot; that is the point)"
echo "forge-metrics (last $DAYS days → $out)"
printf '  %-26s %s\n' "commits" "$commits" "reverts" "$reverts" "PRs merged" "$prs_merged" \
  "median hrs to merge" "$median_hours" "CI first-pass" "$ci_first_pass" \
  "interventions (tagged)" "$interventions" "escapes (tagged)" "$escapes" "rework (tagged)" "$rework" \
  "cost sum (tagged \$)" "${costs:-0}"
echo "  (tag convention: append 'intervention' / 'escape' / 'rework' / 'cost-\$N' per task line in state/PROJECT_STATE.md)"
