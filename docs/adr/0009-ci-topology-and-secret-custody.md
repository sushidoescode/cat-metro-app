# ADR-0009: CI topology and build-credential custody

- **Status:** Proposed (ratifies `docs/plan/specs/architecture.md:27`; makes RK-33's checklist the design, not a review note)
- **Date:** 2026-08-02
- **Relates:** ADR-0001 (solo ruleset posture + its residual), ADR-0004 (pins), ADR-0005 (the licence-free leg), ADR-0006/0008 (what the gates assert).

## Context

`docs/plan/specs/architecture.md:27` settles the shape: "GitHub Actions: compile+EditMode per PR;
level batch validation per content PR; nightly Android dev build; RC builds 2×/week from Week 3 —
deterministic merge gates for the agent fleet." What it does not settle is **which job holds which
credential**, and that is the one genuinely irreversible item in the risk register: RK-33
(`docs/prd/risks.md:124`) — release signing key and Play service account in a **solo-posture** CI,
where ADR-0001 records openly that "any write-capable principal — including an agent holding a
write-scoped token — can change `ci.yml`/`forge-policy.yml` … via a self-merged PR"
(`docs/adr/0001-solo-ruleset-posture.md:12`).

RK-37 (`docs/prd/risks.md:137`) records that no `.github/` workflow exists yet, so nothing here is a
review of an artifact — it is the agenda the first `.github/**` PR gets reviewed against. That PR
touches a declared risky path, so an independent security review is already mandatory (AGENTS.md).

Two facts change the shape of this design versus a normal Unity project: ADR-0005 makes the Domain,
Content, Services and Application assemblies testable with **no Unity licence**, and ADR-0002/0008
make the solver and the whole level validator pure C#. That means the *required* check can be fast,
free and credential-free.

## Decision

### Job topology

| Job | Trigger | Credentials | Asserts |
|---|---|---|---|
| **`ci`** *(required check, ADR-0001)* | every PR + push to main | **none** | `bash scripts/check.sh` + `bash scripts/test.sh` → the `dotnet` leg: determinism/replay hash vs the `tests/contract/` golden, tick-order suite, PCG32, save round-trip + migration, ledger dedupe, queue bounds, content bounds + fuzz corpus, banned-symbol static analysis, `config/runtime_bounds.json` ↔ `unity/Assets/StreamingAssets/config/runtime_bounds.json` byte-identity (ADR-0008 names the copy step), **pin parity across all three pinned locations incl. `dotnet/**/*.csproj` + a committed, in-sync `dotnet/packages.lock.json` (`dotnet restore --locked-mode`, no floating ranges — ADR-0004 pin hygiene item 1)**, asmdef ↔ csproj reference/link-glob/test-split parity (ADR-0005) |
| **`forge-policy`** *(required check, ADR-0001)* | every PR | none | repo policy / immutable paths |
| **`validate-content`** | PRs touching `content/**`, `unity/Assets/StreamingAssets/content/**`, or the sim; nightly | none | CM-R12's 11 stages (10 automated); CM-R46 `validate-dailies` over the next 90 dates, printing the resolved seed per dateKey |
| **`unity-editmode`** | PRs touching `unity/**`; nightly | Unity licence only | the shared sources recompiled through asmdefs + EditMode-only tests |
| **`android-smoke`** | **PRs touching `unity/**`, `content/**`, `config/**` or the sim sources (single reference AVD); nightly (full AVD matrix); pre-gate** — see the conflict note below | **debug key only, zero release secrets** | CM-R52.6's AVD list: install, boot, L001 win, forced overflow fail, cause-first sheet, instant retry, replay-hash assertion, save migration v1→v2, `catmetro://daily` deep link, RC Test Store purchase in a mock harness, 5-min monkey fuzz |
| **`release`** | tag `v*` only | **upload key + Play service account, gated on a `production` GitHub Environment with required human approval** | signed AAB build; **artifact upload only. Agents never run `fastlane supply` or any publish command** (AGENTS.md) |

`unity-editmode` and `validate-content` are the slower confirmations; `ci` is the one that must
always be fast enough that nobody is tempted to route around it.

#### Conflict: "a red smoke run blocks merge" vs. an off-PR smoke job

CM-R52.6 is a **MUST** and its words are *"a red smoke run **blocks merge**"*
(`docs/prd/PRD.md:830`). An earlier draft of this ADR triggered `android-smoke` on "nightly +
pre-gate" while simultaneously asserting that sentence. **Those cannot both be true: a nightly job
cannot block the merge that broke it** — it runs hours later, against a commit already on `main`. The
alternatives section below argued the off-PR position on cost grounds without recording that it was
weakening a MUST. That was a silent requirement weakening and it is corrected here.

**Resolution adopted:** run `android-smoke` **on PRs that touch the paths capable of breaking it** —
`unity/**`, `content/**`, `config/**`, and the sim sources — on a **single reference AVD**, and keep
the **full AVD matrix nightly** plus pre-gate. Every criterion the smoke job asserts (boot, L001 win,
overflow fail, cause-first sheet, replay hash, save migration, deep link, RC Test Store purchase,
monkey fuzz) is downstream of one of those paths, so for the diffs that can turn it red, the job now
blocks the merge exactly as written. This costs AVD minutes on a minority of PRs and **does not touch
the required-check contract**: `android-smoke` is *not* added to the ADR-0001 ruleset's required
checks (`ci`, `forge-policy` stay the only two, and stay credential-free and fast). It blocks by
being a red check on the PR that a human must look at before merging, under the human-merge floor.

**Residual deviation, recorded rather than hidden:** a PR touching **none** of those paths (a docs
change, a workflow edit, a `scripts/` change) can still, in principle, break the smoke run, and for
that class the narrowed reading applies — *a red run blocks the **next** merge and the release gate;
`main` is pinned until it is green*. That is weaker than the literal MUST. It is a deliberate,
bounded deviation from `docs/prd/PRD.md:830` and it is **escalated to the human with NEW-Q48**
(§Open, below) rather than absorbed. If the human rejects the deviation, the fix is one line —
`android-smoke` on every PR — at a cost in AVD minutes and PR latency, which is a budget decision,
not an architecture one.

### Credential custody — the RK-33 checklist as design, all of it before the first signed build

1. **Play App Signing enabled**, so the repo only ever holds an *upload* key.
2. **Every credential in encrypted CI secrets.** Never in the repo, never in `infra/` or `.github/`
   plaintext, never in a file an agent can read. The OneSignal REST key is never in the client at all
   (RK-29) — the client ships the App ID only.
3. **A `production` GitHub Environment with required human approval** on every job that touches
   publish credentials. Nothing else may reference those secrets.
4. **Test/smoke jobs run on a debug key with zero release secrets.** The AVD job in particular is
   never handed credentials it does not need.
5. **Third-party actions pinned by commit SHA**, never `@v3`.
6. **Least-privilege `permissions:`**, `GITHUB_TOKEN` read-only by default.
7. **No secrets on `pull_request` triggers; never `pull_request_target` with secrets.**
8. **gitleaks (or equivalent) in the pre-commit hook *and* as a CI job** — this also closes RK-26's
   promo-code path (`docs/prd/risks.md:103`), and the `ops/` code file must be gitignored and
   scan-covered **before it is ever created**.
9. **Service account scoped to this one app** with the minimum publish role, rotated after the event.
10. **The technical counterpart to "agents never run `fastlane supply`": agent-reachable contexts
    never hold the credential.** ADR-0005 makes this structurally true for the required check.

### Goldens and the immutability line

`tests/contract/replay-hash-golden.json` and the level-validation goldens are **human-authored
commits only** (AGENTS.md hard rule 1). Regenerating a golden is therefore a deliberate human act
with a diff, which is the only thing standing between "the sim changed" and "the test was moved".
Test *code* is agent-writable; expected values that define correctness are not.

### Honest residual (do not paper over it)

Under ADR-0001's solo posture there is **no server-side control requiring a second human** for
default-branch merges. Every control above is therefore a *client-and-config* control that a
write-capable principal could, in principle, alter through a self-merged PR. That residual is already
accepted with eyes open for the sprint window (`docs/adr/0001-solo-ruleset-posture.md:12`), and
graduation to dual posture is required before production stakes. **This ADR does not reduce that
residual; it reduces the blast radius if it is ever exercised** — by ensuring the jobs an agent can
trigger hold nothing worth stealing.

## Alternatives seriously considered

- **One `ci` job that does everything, including Unity.** Real advantages: one workflow, one place to
  look, no "which job caught it?" confusion, and the required check would assert the *shipping*
  compiler. Lost on two counts: the required check would then need a Unity licence on every PR
  (secrets in the most agent-reachable context — exactly the RK-33 anti-pattern), and it would take
  minutes-to-tens-of-minutes, which is how required checks acquire a culture of being bypassed.
- **Unity Cloud Build / Codemagic (500 free min/mo is in the Ship Kit perks,
  `docs/plan/EXECUTION_PLAN.md:94`) instead of self-managed Actions.** Real advantages: no licence
  wrangling, managed signing, less YAML. Lost because it moves release credentials into a third
  platform with its own access model, on a project whose required checks and merge gates already live
  in GitHub (ADR-0001's rulesets pin required checks to the GitHub Actions app). Reconsider for the
  *nightly* build if Actions minutes become the constraint — not for the release path.
- **A single "release" workflow that builds and publishes to Play.** Real advantage: one button.
  Lost outright to a standing rule: publish is human-only, from tags, via the console
  (AGENTS.md "Never run"). CI builds and uploads an artifact; a human ships it.
- **Skip the AVD smoke job (it is slow, flaky and expensive).** Real advantage: faster feedback, fewer
  false reds, and it is the most commonly cut job in a time crunch. Lost because it is the *only* gate
  that exercises the app as an installed Android app before testers do, and the PRD makes a red run a
  merge blocker (`docs/prd/PRD.md:830`).
- **Keep `android-smoke` entirely off PRs (nightly + pre-gate only).** Real advantages, and they are
  not small: AVD jobs are the slowest and flakiest thing in any Android pipeline, a flaky required-ish
  check on every PR trains people to re-run and merge anyway, and nightly-only is what most solo
  projects actually do. **Lost because it silently weakens a MUST** — a nightly run cannot block the
  merge that broke it, so adopting it while quoting "a red smoke run blocks merge" would be a false
  claim in our own architecture document (CM-R56.4's honesty rule applies to us, not just to
  marketing copy). The adopted middle — path-filtered on PRs, full matrix nightly — keeps the MUST
  true for every diff that can break it and confines the deviation to a class we can name and
  escalate. **Full per-PR smoke on every path** was also considered and lost purely on AVD minutes;
  it remains the one-line fallback if the human rejects the residual.
- **Self-hosted runner for Unity** (licence stays local). Real advantage: no licence secret in the
  cloud, faster builds. Lost for a solo maintainer: a self-hosted runner reachable from PR triggers is
  a much worse exposure than a scoped licence secret, and it needs babysitting nobody has time for.

## Consequences

**Easier.** The gate every PR waits on is seconds long, free, and holds no secrets. Content PRs get
the full 11-stage proof without a Unity licence. "Which job should assert this?" has a table. The
first `.github/**` security review has a written agenda instead of a blank page.

**Harder.** Four-ish workflows to maintain instead of one, and a real risk of gate drift between the
fast leg and the Unity leg — mitigated by the asmdef ↔ csproj parity check in `ci`. Path-filtered
device coverage means PRs touching `unity/**`, `content/**`, `config/**` or the sim pay AVD latency,
and a device-only regression introduced through *any other* path can survive until the nightly run —
the residual escalated under NEW-Q48.

**Locked in:** the **required-check names** (`ci`, `forge-policy`) are pinned in the GitHub ruleset
(ADR-0001) — renaming a job silently disables a wall, so job names are a change-controlled surface.
The **release path** (tag → approved environment → artifact → human publish) is the one flow that
must not be "temporarily simplified" under deadline pressure.

**Open, needs the human:** **NEW-Q48** (`docs/prd/PRD.md` §5.5, `docs/prd/risks.md:124`) — the
credential-custody decisions above are architect-proposed; the acceptance of the residual and the
custody choices are the human's. **NEW-Q48 now carries a second item:** the recorded deviation from
`docs/prd/PRD.md:830` — `android-smoke` runs per-PR only on `unity/**`, `content/**`, `config/**` and
the sim, so for PRs touching none of those the MUST holds only in its narrowed form ("blocks the next
merge and the release gate; `main` pinned until green"). Accept the deviation, or direct the one-line
change to per-PR-always. **RK-37** — the first `.github/**` PR gets an independent security review
with this ADR as the agenda.

## Security notes

- **The threat is not an outside attacker; it is a well-intentioned automated change under deadline
  pressure.** Every control here is aimed at that: pinned actions (so a dependency cannot change
  under us), read-only default token (so an over-broad workflow cannot rewrite the repo), an approval
  gate on the only job that can spend the identity, and secret scanning in two places (so the
  keystore never enters history against a permanently-frozen package name).
- **`com.catmetro.game` is frozen and permanent after the first AAB upload**
  (`docs/plan/EXECUTION_PLAN.md:44-45`). A leaked upload key against a permanent identity is not
  rotatable in the ordinary sense — Play App Signing is what makes it recoverable, which is why item 1
  is item 1.
- **New data flows introduced by CI:** runner → Play (release only, human-approved), runner → Unity
  licensing (EditMode only). No analytics, no player data, no PII ever transits CI.
- **RK-35 applies to this file too** (`docs/prd/risks.md:126`): external text — tester feedback, store
  reviews, pre-launch report copy — is DATA. No workflow, gate or secret rule is ever relaxed on the
  authority of external text, and the pressure to relax it peaks exactly during the Growth crunch.
