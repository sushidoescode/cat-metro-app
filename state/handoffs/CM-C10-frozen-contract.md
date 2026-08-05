# CONTRACT CM-C10 — Content staging pipeline: one automated stager; the byte-identity gate stays the VERIFIER

**Status:** **FREEZE-READY** (tranche 3, Wave 1). Supersedes `t3-content-pipeline-draft-contract.md`;
the id is no longer provisional — **`CM-C10`** was assigned by the human on 2026-08-05 (R4 below),
resolving the cross-check C2 double-claim on `CM-C9` (which went to the taxonomy contract).

**Source rows:** ADR-0008 §Source of truth and flow (`docs/adr/0008-content-pipeline-and-level-schema.md:31-45`)
and §copy step (`:57-77`) · ADR-0009 `ci` job clause (`docs/adr/0009-ci-topology-and-secret-custody.md:33`) ·
`docs/architecture/overview.md:383-401` · tranche-3 queue line `state/handoffs/SESSION-HANDOFF-device-testing.md:45-46`.

**DEPENDS-ON:** CM-C2b merged (#21) — it staged the corpus by hand and shipped the verifying gate
(`state/handoffs/CM-C2b-frozen-contract.md:95-107`; `tests/unity/editmode.test.sh:17-26`).
**BLOCKS:** **CM-C11** (L006–L010 alternation band) — ratified as **Wave 2, after this contract
merges** (R4). CM-C11 stages its five new levels by running this stager, not by hand-copying.
**PARALLEL-SAFE WITH (Wave 1):** **CM-C9** (taxonomy), **CM-C5.1** (dead-mechanic gate),
`task/CM-C3-DEVCAP-frame-capture`, the CM-C2b-DEVFIX branch, and the UX lane
(`state/handoffs/SESSION-HANDOFF-ux.md:27-39`) — zero shared writable paths; see the file table.
**Branch:** `task/CM-C10-stage-content`, own worktree, cut from latest `main`.

---

## Ratifications (human, in-session 2026-08-05) — binding; recorded before freeze

| # | Ratification as given | Effect on this contract |
|---|---|---|
| **R1 (HC-1)** | **`scripts/stage-content.sh` — the PIPE draft — is THE single staging author.** | Open question **H1 is CLOSED as (a)**: the ADR-0008:64-66 / `overview.md:396-401` "copy step = `CatMetro.Editor`'s ContentSync" line is **errata**; the author is this shell stager. Option (c) — two authors of one artifact — stays explicitly forbidden (notes finding 9). The PR must carry the ADR-deviation note naming ADR-0008:64-66 and `overview.md:396-401` as superseded-by-errata, and the **ADR/overview text edit itself is a human, ADR-gated follow-up** — this contract writes no `docs/adr/**` and no `docs/architecture/**`. |
| **R1 (HC-2)** | **Deterministic path-derived guids for staged `.meta` files.** | Open question **H3(i) is CLOSED: yes, a script may create a `.meta`**, and the derivation is path-derived and deterministic. Criterion 6 ships **as drafted, un-weakened** (preserve existing guids always; generate only when absent; deterministic; cross-implementation reproducible; unique). The commented-out house rule at `.claude/hooks/protect-files.sh:71` is inactive on this track and is now explicitly overruled **for stager-generated staged metas only** — hand-editing a `.meta` remains out of bounds. **Residual, RIDES-WITH-PR:** H3(ii) does the derivation need a `config/pins.json` row + a cross-tool vector (the seed/ledger/queue-id precedent), and H3(iii) does an expected-guid vector count as a golden needing human custody (Q-I, `state/backlog.md:50`). Criterion 6 pins **no literal guid**, so both answers land without a re-cut. |
| **R1 (ContentSync disposition)** | **ContentSync is NOT frozen now; it will be re-cut later as ASSERT-ONLY.** | The `t3-contentsync-editor-draft-contract.md` (CM-T3-SYNC) draft is **shelved, not dead**: its criteria 8/9 (Copy/Remove — the *authoring* half) are **superseded by this contract** and must be deleted at re-cut; its remaining value (the `CatMetro.Editor` asmdef as ADR-0003 row 11, zero-player-footprint proofs, importer-depth validation, an `IPreprocessBuildWithReport` hook that **fails the build on drift and never writes a byte**) is the re-cut scope. Placement option **B in this contract's §Placement decision is exactly that re-cut**; this contract's criterion 9 buys its cheap, licence-free half today. The `Tests/EditMode/Engine/ContentSync/**` carve-out and the asmdef-`references`-append exception are **pre-provisioned** in the delegated backlog batch (`t3-backlog-amendment.md` EDIT-3/EDIT-4) so the re-cut does not need a second human edit. |
| **R3 (HC-5)** | **The five-item backlog ownership amendment batch is delegated to the agent.** | Drafted at `t3-backlog-amendment.md`. Two of its items pre-provision the shelved ContentSync re-cut (above). **The `unity/Assets/StreamingAssets/**` writer-grant row implied by R1 is NOT one of the five delegated items** — it is drafted as a clearly-marked **non-delegated appendix** there, for the queue owner to accept or refuse. See H5 below: this contract is executable either way, because `--apply` on the real root produces a **zero diff** today. |
| **R4 (ids + sequencing)** | **ids: TAX = CM-C9, stager = CM-C10, LVL = CM-C11, DM = CM-C5.1. Wave 1 = TAX + DM + stager in parallel worktrees; LVL is Wave 2, after the stager merges. DEVFIX's 7 Presentation lines precede the UX lane.** | Id and branch fixed. The draft's "BLOCKS (recommended order): the L006–L010 authoring contract" is now a **ratified ordering**, not a recommendation. Cross-check C4 (LVL's hand-copy default vs both staging drafts' ordering advice) is closed in this contract's favour. |
| **Earlier same-session, tranche-3 relevant** | CM-C3's device legs use a **dev-only failable-level override**; **URP is being restored by CM-C2b-DEVFIX**. | Recorded because criterion 9's note names the **real device build path** (Unity GUI *Build And Run* / `unity/Assets/Editor/CatMetroCliBuild.cs:10`), which is the path DEVFIX's URP/shader-stripping restoration also runs through. This contract renders nothing, touches no `ProjectSettings/**`, no shader, no quality setting, and no `Bootstrap/**`; it must not be read as covering, blocking or being blocked by the URP restoration. If DEVFIX changes the build path itself, criterion 9's *note* is re-worded at rebase; the criterion does not change meaning. |

**Cross-check dispositions applied to this file:** C1 (PIPE vs SYNC, two authors) closed by R1;
C2 (id clash) closed by R4; C3 (three `.meta` provenance policies) closed by R1 — the derived-guid
policy wins and CM-C11's hand-authored-meta plan is deleted; C4 closed by R4; C6 applied as the rule
below; PIPE **H6** sequencing note added to criterion 8.

### Rule (cross-check C6) — wrapper counts are relative, never literal

**No criterion in this contract may state a literal `scripts/test.sh` wrapper count.** Where a count is
asserted it reads: *"`bash scripts/test.sh` is green at **N+1**, where **N** is the wrapper count
printed by `bash scripts/test.sh` on this branch's **rebase baseline** (`git merge-base HEAD main`),
captured and pasted in the handoff note **before** the first new wrapper is added."* A contract that
adds no wrapper asserts **N unchanged**. Rationale: CM-C9, CM-C10, CM-C11, DEVCAP and DEVFIX are each
adding a wrapper in parallel, so a literal is false for whichever lands second —
`state/PROJECT_STATE.md:8` already carries two different literals (8/8, 10/10) from different days.
This contract adds **exactly one** wrapper (`tests/staging/stage-content.test.sh`) → target **N+1**.

### Revision log vs `t3-content-pipeline-draft-contract.md`

1. Title/id: provisional `CM-C9` → assigned **`CM-C10`** (R4).
2. Ratifications block added (above). H1 → closed (a). H3(i) → closed (yes, derived guids). H5 → settled in substance; ownership-row residual flagged as non-delegated.
3. §Placement decision rewritten as **decided**, not recommended; option B relabelled "the shelved ContentSync re-cut (assert-only)".
4. Criterion 7 gains the explicit **N+1** clause (C6 rule).
5. Criterion 8 gains the **H6 sequencing note** — the merge-base byte-identity assertion **rebases** when the UX gate-evolution contract lands.
6. Criterion 9's note gains the rendering/device-build cross-reference (URP restoration rides DEVFIX, not this contract).
7. Criterion 6 gains one explicit sentence that CM-C11's five new levels are its first real "payload with no `.meta`" case.
8. Open-questions table replaced by a **RIDES-WITH-PR** table (H2, H3(ii)/(iii), H4, H5-residual, H6, merge delegation).
9. **No criterion was weakened, renumbered or removed.** 9 criteria in, 9 criteria out.

---

### Goal

One script stages the two declared source→destination rules (`content/levels/*.json` and
`config/runtime_bounds.json`) into `unity/Assets/StreamingAssets/`, byte-for-byte, idempotently,
read-only by default. The existing set-equality + byte-identity block at
`tests/unity/editmode.test.sh:17-26` is left **byte-unchanged** and keeps its job: it **verifies**
the staged tree and **never authors** it. A new wrapper proves the stager against fixtures, and
proves — without invoking that gate — that the committed staged tree is exactly what the stager
produces.

Today staging is a human `cp` (`state/handoffs/CM-C2b.md:37-38`: "corpus (L001-L005) + runtime_bounds
staged under StreamingAssets"). This contract replaces the hand step, not the gate.

### Spec reference

- `docs/adr/0008-content-pipeline-and-level-schema.md:31-45` — the flow diagram: `content/levels/*.json`
  and `config/runtime_bounds.json` are the sources; StreamingAssets is the destination.
- `docs/adr/0008-...:57-77` — the destination is a **sibling of** `content/`, the copy "is a byte copy:
  no re-serialization, no key reordering, no formatting pass, because the `ci` assertion is
  byte-identity and any prettifier would break it" (`:64-66`); the shipped copy "is a build artifact
  that must never be hand-edited" (`:74-75`); bounds are **not** indexed in `catalog.json` (`:68-73`).
  **The `:63-66` naming of `CatMetro.Editor`'s `ContentSync` as *the* copy step is errata under R1**;
  the ADR/overview text edit is a human ADR-gated follow-up, not this contract's diff.
- `docs/adr/0008-...:194-196` — **`content.sha256` / `catalog.json` format and the `content/`
  StreamingAssets path layout are declared IRREVERSIBLE, human ADR gate.** This contract touches
  neither format (see non-goals + H2).
- `docs/adr/0009-ci-topology-and-secret-custody.md:33` — the required credential-free `ci` job asserts
  "`config/runtime_bounds.json` ↔ `unity/Assets/StreamingAssets/config/runtime_bounds.json`
  byte-identity (ADR-0008 names the copy step)". The job has **no credentials and no Unity licence**.
- `docs/architecture/overview.md:396-401` — the staged bounds file "is a build artifact, copied
  verbatim by `CatMetro.Editor`'s `ContentSync` step". **Superseded by R1 as errata**; the deviation
  note is a PR deliverable.
- `docs/architecture/overview.md:388` — `content/` holds "authored level JSON (source of truth) **+
  generator inputs**": generator inputs must never ship, which is why the rule set is an allowlist and
  not `content/**`.
- `tests/unity/editmode.test.sh:17-26` — the verifier as shipped (set-equality both directions, then
  per-file `cmp`, then the `runtime_bounds` limb labelled "Q-Y gate").
- `state/backlog.md:66` (Q-Y) — the ownership history of the StreamingAssets config path.
- `scripts/test.sh:18` — wrapper discovery (`find tests -name '*.test.sh'`).
- `scripts/check.sh:7-8,14-19` — the `--root <dir>` override convention this contract copies.

### Placement decision — DECIDED by R1 (both options were priced; A is ratified)

**A — RATIFIED: a script invoked explicitly, asserted by gates (`scripts/stage-content.sh`).**
Buys: runs with no Unity licence and no editor, so it works in the credential-free `ci` job
(ADR-0009:33) and on any contributor machine; testable in the existing shell harness
(`scripts/test.sh`); creates **zero assemblies** (`CatMetro.Editor` still has no asmdef — the only
`unity/Assets/Editor/` files are two loose scripts, `unity/Assets/Editor/SceneBootstrapper.cs`,
`unity/Assets/Editor/CatMetroCliBuild.cs`, compiled into Unity's default editor assembly); boring
tech — `cp`, `cmp`, `find`, `shasum`.
Costs, stated plainly and **accepted by the ratification**: it does **not** run inside the real device
build path (Unity GUI *Build And Run*, `state/handoffs/SESSION-HANDOFF-device-testing.md:26-29`, or
`-executeMethod CatMetroCliBuild.BuildAndroid`, `unity/Assets/Editor/CatMetroCliBuild.cs:10`). Between
merges a human can therefore build an APK from a drifted tree. The residual is bounded by the PR-time
gate and by criterion 9, not eliminated.

**B — the shelved ContentSync re-cut, ASSERT-ONLY (a Unity build hook,
`IPreprocessBuildWithReport` under `unity/Assets/Editor/**`).**
Covers the exact path A misses — both the GUI build and the CLI entry point above. It only runs when a
licensed editor runs, so the credential-free `ci` job still needs A. Under R1 it **fails the build on
drift and never writes a byte**: author (A) and verifier (B) stay separate, which is the same
separation this contract enforces between the stager and `tests/unity/editmode.test.sh`. A **writing**
hook was rejected: it mutates a git-tracked tree during a build, so the artifact a reviewer approved
and the artifact that ships can differ silently — the opposite of what the byte-identity gate exists
to prove.

**This contract ships A and buys the cheap half of B today** by wiring the read-only check into
`scripts/build.sh` (criterion 9).

### Acceptance criteria (9)

Each is met only when the named check exits 0 (or fails exactly as specified). Every check below can
fail: each has a named negative fixture or a named drift condition.

1. **The stager exists and is read-only by default.** `scripts/stage-content.sh` runs in **check mode
   with no arguments** (asserts, writes nothing, exits non-zero on drift naming every drifted path)
   and writes only under `--apply`. It accepts `--root <dir>` (treat `<dir>` as the repo root — the
   `scripts/check.sh:7-8,14-19` convention) and prints the effective root plus one line per rule.
   *Check:* (a) `bash scripts/stage-content.sh` exits 0 on the clean tree and
   `git status --porcelain` is empty afterwards; (b) on fixture
   `tests/fixtures/staging/drift-bytes/` (a staged file with one byte changed) check mode exits
   non-zero, names that file, and the fixture copy is **still drifted** afterwards (proving check mode
   did not repair it); (c) an unknown flag exits non-zero with usage.
2. **The rule set is an explicit allowlist, never a directory sweep.** Exactly two rules ship:
   `content/levels/*.json` → `unity/Assets/StreamingAssets/content/levels/`, and
   `config/runtime_bounds.json` → `unity/Assets/StreamingAssets/config/`. Each rule carries its
   ADR-0008:31-45 citation in a comment. Nothing else in `content/` or `config/` is staged — the tree
   holds three non-shipping config files today (`config/pins.json`, `config/validator_thresholds.json`,
   `config/daily_pipeline.json`) and `overview.md:388` reserves `content/` for generator inputs too.
   *Check:* fixture `tests/fixtures/staging/extra-sources/` carries `config/validator_thresholds.json`,
   `config/pins.json` and `content/generator_inputs.json`; after `--apply` none of the three exists
   anywhere under the fixture's StreamingAssets, and a test asserts the staged config file **set** is
   exactly `{runtime_bounds.json}`.
3. **The committed staged tree IS the stager's output (anti-tautology; does not touch the criterion-10
   gate).** Copy the real `content/levels/` + `config/runtime_bounds.json` into a temp root with an
   **empty** destination, `--apply` there, then compare payload files against the real
   `unity/Assets/StreamingAssets/`: filename set equality **in both directions** and per-file SHA-256
   equality. Zero difference. Separately assert `.meta` coverage on the real tree: every staged payload
   has a `.meta` sibling and no `.meta` in a rule directory lacks its payload (orphan check).
   *Check:* one wrapper case doing exactly the above; it fails if any payload differs, is missing, is
   extra, or if a `.meta` is orphaned/absent. **This case never runs `tests/unity/editmode.test.sh` and
   never runs `--apply` against the default root.**
4. **Byte copy, never a re-serialization (ADR-0008:64-66).** Fixture
   `tests/fixtures/staging/odd-bytes/` holds a source level with irregular indentation, CRLF line
   endings, a non-ASCII UTF-8 string and **no trailing newline**; after `--apply` the staged file's
   SHA-256 equals the source's.
   *Check:* that case, plus a grep asserting `scripts/stage-content.sh` contains no JSON parser or
   formatter invocation (no `jq`, no `python3 -m json`, no `json.dump`) — a prettifier would pass a
   naive test and break the `ci` byte-identity clause.
5. **Prune is scoped, both-directional, and never destructive outside its own rule pattern.**
   `--apply` removes a destination file that matches a rule's pattern and has no source counterpart
   (with its `.meta`); anything else inside a rule directory is **reported as drift, not deleted**;
   anything outside the two rule directories is untouched and unreported.
   *Check:* fixture `tests/fixtures/staging/stale-dest/` contains (a) `.../content/levels/F999.json`
   + `.meta` with no source → pruned by `--apply`, reported by check mode; (b)
   `.../content/levels/notes.txt` → **still present** after `--apply` and reported as drift; (c)
   `.../content/catalog.json` and `.../content/daily_backup_pool.json` sentinels (the future
   ADR-0008:38-41 artifacts) → byte-unchanged after `--apply` and **not** reported.
6. **`.meta` policy: preserve always, generate only when absent, deterministically.** An existing
   `.meta` is never rewritten (byte-identical after `--apply`, including its guid). A payload with no
   `.meta` in the destination gets one generated in the shipped shape
   (`fileFormatVersion: 2` / `guid:` / `DefaultImporter:` block — see
   `unity/Assets/StreamingAssets/content/levels/L001.json.meta:1-7`) with a **deterministic** 32-hex
   guid derived from the destination-relative path, identical on every machine and every run.
   **This is the ratified policy (R1/HC-2) and it is the single policy for the staged tree**; the
   first real production case is **CM-C11's five new levels** (Wave 2), which arrive with no `.meta`
   and receive derived guids from this stager — no hand-authored guid, no editor-generated guid.
   *Check:* (a) fixture with a sentinel guid → unchanged after `--apply`; (b) fixture with a new
   payload and no `.meta` → two consecutive `--apply` runs produce byte-identical `.meta`;
   (c) a second, independent implementation of the derivation (python3 `hashlib` in the wrapper vs the
   script's `shasum`) agrees on the guid for three fixture paths; (d) guid uniqueness across
   `unity/Assets` — scanned with that **explicit root**, never `.`, because
   `.claude/worktrees/ux-lane/` is a second full checkout that duplicates every guid.
   *Recorded asymmetry:* the six `.meta` files committed today were editor-generated with random
   guids and are **not** derivable. Guid stability outranks uniformity: the stager preserves them and
   only new files get derived guids. **RIDES-WITH-PR:** whether the derivation needs a
   `config/pins.json` row + cross-tool vector, and whether an expected-guid vector is a golden needing
   human custody (H3(ii)/(iii)). This criterion pins **no literal guid**, so either answer lands
   without a re-cut.
7. **A discovered wrapper proves all of the above and leaves the tree clean.**
   `tests/staging/stage-content.test.sh` is found by `scripts/test.sh:18`, copies each fixture into a
   temp dir before any `--apply` (fixtures stay pristine), asserts the expected **non-zero** exits for
   every negative fixture (not only the green paths), and ends with `git status --porcelain` empty.
   *Check:* `bash scripts/test.sh` prints `PASS tests/staging/stage-content.test.sh` and a summary line
   matching `^test: [0-9]+/[0-9]+ passed` whose two numbers the wrapper's PR evidence compares equal
   (the backreference form `\1` is not POSIX ERE — CM-C2a criterion 13, `tests/content/importer.test.sh:7-8`),
   and the wrapper total is **N+1** where **N** is the rebase-baseline count captured before the
   wrapper was written and pasted in the handoff note — **never a literal** (C6 rule above);
   plus a case asserting the wrapper itself exits non-zero when a fixture's expected failure is turned
   into a success.
8. **The verifier is untouched and never calls the author.** `tests/unity/editmode.test.sh` is
   byte-identical to its merge-base version, and neither it nor `scripts/check.sh` references
   `stage-content`. The wrapper never invokes `--apply` without `--root`.
   *Check:* `git diff --exit-code "$(git merge-base HEAD main)" -- tests/unity/editmode.test.sh` exits 0;
   `grep -c stage-content tests/unity/editmode.test.sh scripts/check.sh` is 0 for both; a grep
   asserting every `--apply` occurrence in `tests/staging/*.test.sh` is on a line that also carries
   `--root`.
   **Sequencing note (H6 — required, the criterion is unmet without it).** The merge-base assertion is
   **relative to this branch's own merge-base, computed at run time — never a pinned commit** — and it
   **rebases** if the **UX lane's gate-evolution contract** lands first. That contract is the single
   writer of `tests/unity/editmode.test.sh` after CM-C3-DEVCAP merges
   (`state/handoffs/SESSION-HANDOFF-ux.md:36-39`). Both orders are legal:
   *(i)* if this contract lands first, the UX contract inherits an unmodified gate and must be told, in
   its own contract text, that **the criterion-10 block stays author-free** — it may evolve the input/
   chrome limbs, but must not make the block invoke `stage-content.sh` (that is this contract's stop
   condition 2, and it would make the byte-identity evidence self-fulfilling);
   *(ii)* if the UX contract lands first, this branch rebases onto it and the assertion compares
   against the **new** merge-base, which then legitimately contains the UX edits. The PR states which
   order actually happened. Nothing here licenses this contract to edit that wrapper in either order.
9. **`scripts/build.sh` fails closed on staged drift.** It forwards `"$@"` and calls the stager in
   **check mode** (never `--apply`) before anything else, per the placement decision above.
   *Check:* `bash scripts/build.sh` exits 0 on the clean tree;
   `bash scripts/build.sh --root tests/fixtures/staging/drift-bytes` exits non-zero naming the drifted
   file. The PR states plainly that this does **not** cover the real device build path (Unity *Build
   And Run* / `unity/Assets/Editor/CatMetroCliBuild.cs:10`) and names **the shelved assert-only
   ContentSync re-cut** (R1) as the follow-up that will. **The criterion fails if that note is
   absent**, not if the hook is unbuilt.
   *Rendering/device note (recorded, no criterion depends on it):* the same device build path is where
   **CM-C2b-DEVFIX restores URP** (2026-08-05 ratification). This contract changes no
   `ProjectSettings/**`, no shader, no quality setting and no `Bootstrap/**`; it neither covers nor
   blocks that restoration, and if DEVFIX changes the build entry point the note above is re-worded at
   rebase without changing the criterion's meaning.

### Scope boundary

**Complete file table.** Any path not listed is out of scope; touching one is a stop condition.

| Path | Mode | Why / owner |
|---|---|---|
| `scripts/stage-content.sh` | **CREATE** (this contract owns) | new; unowned by `state/backlog.md:113-123` |
| `tests/staging/stage-content.test.sh` | **CREATE** (owns) | new dir — `tests/content/**` is CM-C2a's (`backlog.md:116`), `tests/unity/**` is CM-C2b/C3's (`:117-118`) |
| `tests/fixtures/staging/**` | **CREATE** (owns) | disjoint from `tests/fixtures/purity-bad/**` (CM-C1), `tests/fixtures/content-bad/**` (CM-C2a) and `tests/fixtures/taxonomy-bad/**` (CM-C9) |
| `scripts/build.sh` | **APPEND** (forward `"$@"` + one check-mode call) | unowned by the ownership table (CM-C1 owns `scripts/check.sh` only, `backlog.md:115`) — see H4 |
| `state/handoffs/CM-C10.md`, `state/handoffs/CM-C10-frozen-contract.md` | **CREATE** | this contract's frozen text + status log |
| `state/PROJECT_STATE.md` | **APPEND one line, on merge only** | four lanes append in parallel (`SESSION-HANDOFF-ux.md:45-49`); ~150-line cap |
| `unity/Assets/StreamingAssets/**` | **WRITE ONLY as mechanical stager output; expected diff = ZERO** | owned by CM-C2b (`backlog.md:117`); **R1 makes this contract the single author of that tree** — see H5 and stop condition 3 |
| `tests/unity/editmode.test.sh` | **READ-ONLY — forbidden** | it is the verifier (criterion 8); the UX lane is its single writer after DEVCAP merges (`SESSION-HANDOFF-ux.md:36-39`) |
| `scripts/check.sh` | **forbidden** | CM-C1's; this contract needs no new block |
| `content/**`, `config/**` | **READ-ONLY** | authored sources owned by CM-C2a/C5/C6/C7 (and, for L006–L010, CM-C11 in Wave 2) |
| `unity/Assets/Scripts/**`, `unity/Assets/Editor/**`, `unity/ProjectSettings/**`, `unity/Assets/Scenes/**` | **forbidden** | Bootstrap is a flat deny while CM-C3-DEVCAP + CM-C2b-DEVFIX are in flight; `Editor/**` is the shelved ContentSync re-cut |
| `docs/adr/**`, `docs/architecture/**` | **forbidden** | the ADR-0008/overview errata under R1 is a human, ADR-gated edit; this contract records the deviation in the PR only |
| `unity/Assets/Resources/Strings/ui.csv` | **forbidden** | no UI strings here; rows are append-only and belong to the UX lane |
| `.github/**` | **forbidden** | Q-V (`backlog.md:63`), human + security review |
| `state/backlog.md` | **forbidden** | the ownership amendment is the human's to apply from `t3-backlog-amendment.md` |
| `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`, `scripts/git-hooks/`, `state/mode`, `state/trust.json`, `evals/` except `evals/results/` (never `attested/`) | **immutable — forbidden** | AGENTS.md hard rule 1; hooks enforce |

**Explicit non-goals:**
- **No `catalog.json`, no `content.sha256`, no `daily_overrides.json`, no `daily_backup_pool.json`.**
  Their format is declared irreversible with a human ADR gate (ADR-0008:194) — separate contract, H2.
- **No `CatMetro.Editor` asmdef, no `ContentSync`, no build hook, no editor C#.** That is the shelved
  assert-only re-cut (R1), and its ownership carve-outs are already pre-provisioned in the delegated
  backlog batch.
- **No ADR or architecture text edit.** R1 makes ADR-0008:63-66 / `overview.md:396-401` errata; writing
  the errata is human and ADR-gated.
- **No new authored levels** (L006–L010 is CM-C11, Wave 2) and no edit to any existing level or
  config file. The stager copies; it never authors content.
- **No `.github/**` wiring of ADR-0009:33** — still Q-V.
- **No new dependency.** Only tools the harness already uses: `cmp`/`find`/`sort`/`cp`/`rm`
  (`tests/unity/editmode.test.sh:18-26`), `python3` (`:88-91`), `shasum`. Adding `jq` or `rsync` would
  need an ADR named in the PR (there is none) — stop condition 6.

### Assumptions

- **A-1** Check mode is the default; `--apply` is the only writing mode. Accidental invocation is
  therefore harmless, which is what makes it safe to call from `scripts/build.sh`.
- **A-2** `--root <dir>` is the single override, mirroring `scripts/check.sh:14-19`. With no `--root`
  the effective destination is exactly `unity/Assets/StreamingAssets` under
  `git rev-parse --show-toplevel`; a wrapper case asserts that printed default.
- **A-3** All scan roots are explicit and repo-root-relative. Never `find .` / `grep -r .`:
  `.claude/worktrees/ux-lane/` is a second full checkout of this repo and would duplicate every
  filename and every `.meta` guid.
- **A-4** The committed staged tree is currently correct, so criterion 3 is expected to produce a zero
  diff. A non-zero diff is a **finding**, not a fix (stop condition 3).
- **A-5** The `.meta` guid derivation is implementer-shaped **within the ratified policy** (R1):
  properties are the criteria — deterministic, path-derived, unique, cross-implementation
  reproducible. Recorded in the script header. The pins-row / golden-custody sub-questions ride the PR.
- **A-6** The wrapper runs on a committed tree (`SESSION-HANDOFF-ux.md:65`) and mutates only temp dirs.
- **A-7** No collision with `task/CM-C3-DEVCAP-frame-capture` (Bootstrap + `tests/unity/devcap.test.sh`),
  CM-C2b-DEVFIX (ProjectSettings/quality/shader config, URP restoration), the UX lane (Presentation +
  `ui.csv` + `tests/unity/editmode.test.sh`), CM-C9 (`Application/EventTaxonomy/**` + `tests/taxonomy/**`)
  or CM-C5.1 (`Content/Validation/**` + `tests/validation/**`): the file table shares no writable path
  with any of them.
- **A-8** Sprint mode (`state/mode`) prices ceremony only; TDD per criterion, immutable paths and the
  independent fresh-context review round stand at full strength (AGENTS.md hard rules 1, 5, 7).
- **A-9 (hook awareness).** `.claude/hooks/protect-bash.sh:31-36` denies a Bash command that pairs a
  write-ish op (`cp`, `rm`, `mv`, `>`, `sed -i`, `python3 -c`) with a protected path **substring** —
  including inside a comment in a `bash -c` string. Fixture paths under `tests/fixtures/staging/` are
  safe; never name a `tests/contract…` path in a staging command line.

### Stop conditions

Defaults apply. Plus:
1. Any criterion appears to need an edit to `tests/unity/editmode.test.sh` → **STOP**. The gate is the
   verifier and the UX lane is its single writer.
2. Any design where the identity gate invokes the stager, or the stager runs before the gate inside the
   same check → **STOP**. That makes the byte-identity evidence self-fulfilling (the "tautological hash
   law" class caught in the CM-C3 review, `state/PROJECT_STATE.md:25`).
3. `--apply` on the real root produces a non-zero diff under `unity/Assets/StreamingAssets/` → **STOP**
   and report which side moved. Do not commit the staged result: either an authored source or a shipped
   copy is wrong, and which one is a human call.
4. Any need to create `CatMetro.Editor`, an asmdef, a build hook, or any file under
   `unity/Assets/Editor/**` or `unity/Assets/Scripts/**` → **STOP** (the shelved assert-only re-cut;
   Bootstrap branches in flight).
5. Any need to write `catalog.json` / `content.sha256` / a `daily_*` artifact → **STOP**; ADR-0008:194
   declares the format irreversible behind the human ADR gate.
6. Any new tool/dependency (`jq`, `rsync`, node, a JSON formatter) → **STOP** and name the ADR it would
   need (AGENTS.md hard rule 2).
7. The `.meta` derivation looks like it needs a committed expected-guid vector → **STOP** and ask: every
   golden in this repo is human-committed (Q-I, `state/backlog.md:50`). R1 ratified the *policy*, not a
   golden.
8. A prune would remove a file the two rules do not own → **STOP**; report it as drift instead.
9. Any need to edit `docs/adr/**`, `docs/architecture/**` or `state/backlog.md` → **STOP**; the R1
   errata and the ownership amendment are human edits.
10. Anything requires `state/mode=production` or touches `**/billing/**`, `**/iap/**`, `**/ads/**` →
    **STOP** (AGENTS.md §Risky paths).

---

### RIDES-WITH-PR human calls (default recorded; ratify at review/merge)

| # | Call | Default this contract ships | ADR gate? |
|---|---|---|---|
| **H2 / HC-19** | **Confirm `catalog.json` + `content.sha256` stay excluded** from staging and deferred to their own contract (`content.sha256` lands in the save as `contentHash`, ADR-0006 §2). | **Excluded** (non-goal 1); ADR-0008:36-41's StreamingAssets picture stays partially unrealised — recorded as **deferred, not met**. | **YES — the format itself is IRREVERSIBLE** (ADR-0008:191-196). |
| **H3(ii)/(iii)** | **Guid-derivation custody** — does the path→guid derivation need a `config/pins.json` row plus a cross-tool vector (the seed / ledger-key / queue-id precedent)? Does an expected-guid vector count as a **golden** requiring human custody (Q-I, `backlog.md:50`)? | Derivation shipped with **properties asserted, no literal pinned**; no `pins.json` row added, no golden committed. Either answer lands as an additive follow-up. | A pins row is a cross-tool contract; a golden needs human custody. |
| **H4 / HC-20** | **Should `scripts/build.sh` be drift-gated now** (it is a stub, `scripts/build.sh:5`, and not the real build path)? And keep / delete / promote `unity/Assets/Editor/CatMetroCliBuild.cs`, which self-describes as an untracked delete-after-session shim yet is committed on main — the answer decides where the eventual assert-only hook attaches. | **Gate it now** (criterion 9); shim left untouched. If the human deletes the shim, criterion 9's note loses one of its two named build paths and is re-worded — the criterion does not change meaning. | No. |
| **H5 (residual)** | **Ownership-table row for `unity/Assets/StreamingAssets/**`.** R1 settled *who authors* (this stager). What is not settled is whether `state/backlog.md:117` gains an explicit row/annotation saying so, and whether the tree is relabelled "derived, tool-only". Drafted as the **non-delegated appendix** of `t3-backlog-amendment.md`. | Criteria 1–9 all pass in check mode; only the "run `--apply` for real" step depends on it, and it produces a **zero diff** today. CM-C11 (Wave 2) is the first PR where a real staged write lands. | No — but CM-C11 needs the answer. |
| **H6 / HC-21** | **Sequencing vs the UX lane's gate-evolution contract** (single writer of `tests/unity/editmode.test.sh`). | Criterion 8's note covers both orders; the PR states which happened. The UX contract must be told the criterion-10 block stays author-free. | No. |
| **HC-25** | **Merge-delegation re-confirmation for this lane this session** (`state/handoffs/SESSION-HANDOFF-device-testing.md:9-10`; Constitution Amendment 1). | Assume **not** delegated until the human re-confirms in-session. | Blocks **merge**, not work. |

**Closed since the draft:** H1 (author identity) by **R1** = option (a); H3(i) (may a script create a
`.meta`) by **R1** = yes, deterministic path-derived; H5 (writer model) in substance by **R1**;
the id/ordering questions by **R4**.
