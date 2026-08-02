# Handoff: rulesets-solo — server-side enforcement rollout (solo posture)
- Date/session: 2026-08-02, forge-init/substrate session (Claude Code) → fresh session after forge-kit PR #2 merges

## Contract
Install the server-side ruleset wall on github.com/sushidoescode/cat-metro-app without locking out a solo maintainer. Kit-side change: forge-kit PR #2 (`feat/solo-ruleset-posture`, head `1a5c556`) — being reviewed/merged by a separate kit-focused agent session, per the human.

## State
Done:
- Kit: solo posture implemented + 2 independent fresh-context reviews + verification round (all 9 findings CLOSED) + round-3 hardening; 27/27 offline scenarios green; kit lint gate PASS; branch pushed; PR #2 open with the two open human decisions in its body.
- This repo: reviewed `scripts/setup-rulesets.sh` + `scripts/forge-doctor.sh` adopted at commit `602959f` (source: kit `1a5c556`). GitHub Pro active; repo private; CI green on main.

Not done (in order):
1. HUMAN: merge kit PR #2 (other session handles). If review there changes the script, re-sync this repo's copies from the merged template (`diff` against `~/forge-kit/template/scripts/` or `/forge-upgrade` after a kit release) — commit `602959f` predates any such changes.
2. HUMAN: declare the posture — append exactly `posture=solo` to `state/mode`, then `FORGE_HUMAN_OVERRIDE=1 git commit` + push. (Hook-protected by design; agents must not do this. Strict parse: whole line, exactly once.)
3. Agent: `bash scripts/setup-rulesets.sh --solo` → expect exit 3, rulesets forge-main-solo/forge-tags/forge-tag-creators created, org leg skipped (personal repo).
4. Agent: live probes the mock couldn't cover: (a) immediate `--check` must return 3 — a DRIFT here means GitHub normalized the 0-approval pull_request params (report upstream); (b) attempt a direct push to main — must be REJECTED (proves the wall); land that commit via branch → PR → green CI → squash self-merge instead; (c) `bash scripts/forge-doctor.sh` → remote line = PARTIAL note (mode=sprint).
5. Agent+HUMAN: add the human (`sushidoescode`) as named User bypass actor on forge-tag-creators (v* tag creation is blocked for everyone until then); verify `--check` still 3.

## Evidence
- Kit review trail + open decisions: forge-kit PR #2 body. Behavioral suite: `bash tests/rulesets-e2e.sh` in the kit (27/27).
- This repo's adoption commit: `602959f`.

## Decisions made
- mode=sprint with production graduation criterion (see PROJECT_STATE Now).
- posture=solo chosen by the human (2026-08-02) after the dual floor proved solo-incompatible; residual (no server-side human gate on main; hook-protected set self-merge-reachable) is DISCLOSED in the kit and awaits formal acceptance in kit PR #2 — mirror an ADR line here at graduation.

## Open questions / risks
- Kit PR #2 open decisions: residual-acceptance ADR + N1c (forge-policy content rule hard-failing state/mode-weakening diffs).
- After rulesets are active, ALL main-bound work goes branch → PR → green CI → squash self-merge; direct pushes (agent and human) bounce.
- Graduation to production (before monetization code): flip posture to dual (needs second human CODEOWNER or kit solo→dual path), re-run setup-rulesets without --solo, delete forge-main-solo in GitHub settings, re-enable claude-review workflow, flip mode=production.

## Next step
Wait for kit PR #2 to merge (other session). Then: human runs step 2 above; agent runs steps 3–5.
