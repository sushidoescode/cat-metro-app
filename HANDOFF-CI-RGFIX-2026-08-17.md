# CI triage for `task/GLB-DECIMATION` (PR #94) — verified fix spec

From the orchestrator session, 2026-08-17. For the chat that owns `task/GLB-DECIMATION`.
Evidence: CI runs `32011890979` (head `16e20e3`, 25/27) and `32022836127` (head `c62d6b8`,
24/27). Both FAILED. Neither failure is in the decimation work itself.

## Failure 1 — the runner has no `rg` (ripgrep). 2 wrappers die.

```
tests/assets/glb-decimation-pipeline.test.sh: line 2053: rg: command not found  → FAIL
tests/assets/glb-silhouette.test.sh:          line 161:  rg: command not found  → FAIL
```

Census at `c62d6b8` (`git grep` across the whole branch): **exactly 9 call sites, all in
those two test files. Production `scripts/` is clean** — this is a test-portability bug
only.

| file | lines |
|---|---|
| `tests/assets/glb-decimation-pipeline.test.sh` | 2053, 2106, 2195, 2198, 13265, 13485, 13499 |
| `tests/assets/glb-silhouette.test.sh` | 161, 205 |

### Verified one-liner

```bash
sed -i -E 's/(^|[|;&`( ])rg -q /\1grep -qE /g; s/(^|[|;&`( ])rg -Fq /\1grep -Fq /g; s/(^|[|;&`( ])rg -n /\1grep -nE /g' \
  tests/assets/glb-decimation-pipeline.test.sh tests/assets/glb-silhouette.test.sh
```

Applied to copies and checked by the orchestrator: **9 lines changed (7 + 2), 0 residual
`rg`, `bash -n` clean on both.** No pattern anywhere uses a Rust-regex-only construct
(`\d`, `\w`, `\s`, `\b`, lookaround, lazy quantifiers, `{n,m}`), so ERE is a faithful
translation of every one.

### ⚠️ `-E` is MANDATORY, not stylistic — a naive `rg`→`grep` breaks the suite silently

Line 2053 interpolates `$diagnostic`, and two callers (lines 2072, 2079) pass an
**alternation**: `glb-decimation: requires Blender (5\.1\.2|build ec6e62d40fa9)`.
Under BRE (plain `grep`), `(` `|` `)` are literals, so the test would stop matching and
fail for a bogus reason. Demonstrated:

```
input: 'glb-decimation: requires Blender 5.1.2'
grep -qE "^<pattern>$"  → MATCH      (correct; same as rg)
grep -q   "^<pattern>$"  → NO MATCH  (false failure)
```

Same for `glb-silhouette.test.sh:161`, whose pattern uses `+`, absent from BRE.

### Local verification before you push

`rg` is a shell function in Claude Code, not a binary, so `which rg` lies. Verify the way
CI sees it — with rg genuinely absent from the child environment:

```bash
env -i PATH=/usr/bin:/bin HOME="$HOME" bash tests/assets/glb-silhouette.test.sh
```

(Note the standing repo trap: `mktemp` returns EMPTY under the sandbox — run these
unsandboxed or they fail spuriously.)

## Failure 2 — your own docs gate, new at `c62d6b8`. Only you can fix this one.

```
glb-decimation-docs.test.sh: FAIL — project state does not retire the unselected-mesh issue
```

`tests/assets/glb-decimation-docs.test.sh:415` requires the branch's
`state/PROJECT_STATE.md` to contain the literal string:

```
former deferred unselected-mesh issue is resolved
```

and line 420 forbids the old deferral wording
(`selected-scene silhouette cap does not yet bound expensive unselected meshes`).
Your final state commit `c62d6b8` satisfies neither.

**The orchestrator deliberately did not write that sentence for you.** It asserts the
POSITION-count cap fix is verifiably in the tree (the 8,000,000 ceiling counting BOTH
selected index references AND selected POSITION values, per
`HANDOFF-GLB-DECIMATION-2026-08-16.md` §3C). Retire the issue only if that is true at
your head; if it is not, the honest fix is to land the cap change, not to edit the phrase.

## Push discipline

Fold **both** fixes into **one** commit and push **once**. Each push starts a ~3 h CI run.
Your 10:59/11:00 double-push started two concurrent 3 h runs; the orchestrator cancelled
the superseded one (`32022780750`, head `d687385`) to stop them throttling each other.

Everything else is green — the solver, staging, validator, taxonomy and Unity wrappers all
pass. After these two fixes the suite should read 27/27.

## Lane boundaries (unchanged)

`task/GLB-DECIMATION` is yours alone. Live lanes elsewhere: PR #96
`task/GEN-ASSET-LICENSE-ADR` (licence ADR), PR #95 `task/CM-CATS-WIRE` (Board/Home wiring
contract + RED), and a pending curation lane that will branch **off** your head — it has
been told never to push to your branch. PR #94 stays HUMAN-MERGE.
