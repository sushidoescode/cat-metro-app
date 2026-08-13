# Per-district production checklist — PROPOSED / UNSIGNED / NON-EXECUTABLE

How one paid district would actually get built in **this** repo, using the pipeline that exists today.
Docs-only: running this checklist is not authorized by writing it. Stages 4–6 cannot start at all before
the human-authored `state/mode` → `production` commit (verbatim tripwire in `sku-price-proposal.md`).

**Legend:** **[HUMAN]** = only a human may perform or approve it · **[AGENT]** = an agent may execute it
under an ordinary task contract · **[BLOCKED]** = cannot begin before the `state/mode` flip.

## Questions a human must answer before stage 0 opens

| # | Question |
|---|---|
| Q-P1 | Greenlight a slug (Q-D1/Q-S1) and rule the five conflicts in `districts.md` §5. |
| Q-P2 | ADR-0011 does not exist in `docs/adr/` on this ref (`districts.md` SC-5). Does district art production wait for it to be **written and signed**, or does the human authorize an interim custody rule in writing? |
| Q-P3 | Is district content authored **before** the mode flip (levels + art are not monetization code) or held until after? Sequencing choice with schedule consequences. |
| Q-P4 | Who plays the district on device for the CM-R21 five-rater and playtest gates, and when (`product_spec.md:647` requires every shipped level played by a human; capstones by three testers)? |

---

## Stage 0 — Greenlight **[HUMAN]**

- [ ] **[HUMAN]** Slug + display name signed (`monetization_spec.md:622` requires a per-slug signature).
- [ ] **[HUMAN]** Price row signed (`sku-price-proposal.md` §1) — or explicitly deferred.
- [ ] **[HUMAN]** Id block assigned (Q-D2); ids are permanent and never recycled.
- [ ] **[AGENT]** A frozen task contract exists for the district's content, one contract per branch
      (`AGENTS.md` hard rule 4).

## Stage 1 — Art production and asset custody

- [ ] **[HUMAN]** **ADR-0011 (Polyfork custody) written and signed** — recorded as **PENDING and, on this
      ref, not yet written** (`state/PROJECT_STATE.md:8`; `state/handoffs/PARALLEL-PUSH-2026-08-09.md:79-83`
      require it to record Polyfork's asset-license terms for GLBs shipped in a Play-Store binary). No
      district asset merges before it.
- [ ] **[AGENT]** Generation via the Polyfork MCP; **derivatives stay local-only** until custody is
      signed. Precedent for why: nine FBXs previously reached the public repo and had to be purged from
      tip and history, with a residual that pre-rewrite objects may remain fetchable by SHA
      (`state/PROJECT_STATE.md:55`). No source FBX/GLB enters the repo on an agent's judgment.
- [ ] **[AGENT]** Assets conform to the LOCKED art direction: 12-hex palette, rounded shape language,
      ortho 30° camera, one toon shader family, dressing ≤6% of screen outside the board safe rect
      (`docs/plan/specs/product_spec.md:130-181`). A district picks **Day or Night** — a third lighting
      preset needs an art-direction amendment **[HUMAN]** (`product_spec.md:175`).
- [ ] **[AGENT]** Readability evidence produced: color always paired with symbol **and** silhouette;
      silhouette-at-64px and deutan/protan/tritan captures (CM-R21, `docs/prd/PRD.md:381-386`).
- [ ] **[HUMAN]** **CM-R21 five-rater protocol run** (A-26: 5 raters, unprompted, randomized, 25 pooled
      trials, ≥90% correct; failing assets re-topo'd or cut, decision recorded) — `PRD.md:966`.
- [ ] **[HUMAN]** **TG-1** ruling honored for any new line-colored element (two of five line colors fail
      non-text contrast on the Day board until the outline rule lands — `docs/prd/ux-flows.md:17`).
- [ ] **[HUMAN]** **Standing visual-verification rule:** rendered frames of the real scene are required
      evidence for anything visual; code-green alone is insufficient (`state/PROJECT_STATE.md:79`,
      human directive 2026-08-06).
- [ ] **[HUMAN]** **Law-5 distinctness taste gate:** the district's included livery must not imitate the
      Cup participation/gold-trim or Founder liveries (`monetization_spec.md:590-592`).

## Stage 2 — Level authoring through the EXISTING validator pipeline **[AGENT]**

- [ ] Author `content/levels/L9xx.json` at schema v2 — id `^L[0-9]{3}$`, `band` from the closed enum,
      `mechanics` from the closed enum, `newMechanic: null` for a remix district
      (`docs/plan/data/level_schema.json:10,18,20,21`).
- [ ] `bash scripts/validate-content.sh` — the credential-free 11-stage validator
      (`scripts/validate-content.sh:2-8`; it runs `dotnet/CatMetro.Validator`). Exits 0 only if every
      BLOCKING stage passes on every corpus level, so a bad district level fails the **whole** corpus gate.
- [ ] Confirm the **dead-`newMechanic` liveness limb** is satisfied — CM-C5.1 made it BLOCKING on
      `CampaignVerdicts` (`state/PROJECT_STATE.md:35`).
- [ ] **Prove declared mechanics are actually exercised, not decorative.** The L011–L017 review found
      declared queue mechanics decorative on most boards and forced an honest correction rather than a
      claim (`state/PROJECT_STATE.md:52`). Carry a per-board probe as evidence, not a claim.
- [ ] Add a band wrapper under `tests/corpus/` following the shape of
      `tests/corpus/queue-reading-band.test.sh` (it re-runs `scripts/validate-content.sh:44`, asserts the
      prior corpus is byte-unchanged at `:161`, and re-runs `scripts/stage-content.sh` in check mode at
      `:170`).
- [ ] Brittleness caveat to design against: the solver's earliest-tick tie-break historically left zero
      early-side jitter margin for non-tick-0 toggles (`state/PROJECT_STATE.md:101`), later re-measured at
      100% after the centering fix (`:109`). **Verify the current solver state before relying on either
      number** — the census records PR #66 as merged while the Active-tasks row still reads
      "READY FOR EXACT-HEAD REVIEW" (`state/PROJECT_STATE.md:49,55`).
- [ ] **[HUMAN]** Playtest gate: every shipped level played by a human on device; capstones by three
      testers (`product_spec.md:647`, validator stage 11).

## Stage 3 — Staging and build **[AGENT]**

- [ ] `bash scripts/stage-content.sh --apply` — the **single** automated author of
      `unity/Assets/StreamingAssets/**` (`scripts/stage-content.sh:2-3`); byte-verbatim copy, no
      re-serialization (`:19`).
- [ ] `bash scripts/test.sh` — includes the byte-identity gate, which enforces **set-equality in both
      directions** between `content/levels/*.json` and the staged tree
      (`tests/unity/editmode.test.sh:18-24`). Consequence to design around, not around: **the district's
      JSON ships in the free APK**; access is an entitlement check, never asset absence.
- [ ] `bash scripts/check.sh` and `bash scripts/build.sh` green.
- [ ] Inherited traps, verified as still-open debt: the stager excludes all `*.meta`, so a **new**
      StreamingAssets folder is unverified (`state/PROJECT_STATE.md:100`) — district levels land in the
      existing `content/levels/` folder, so no new folder is created; the CLI build shim
      `unity/Assets/Editor/CatMetroCliBuild.cs` is **untracked on every ref** and a clean clone cannot
      build an APK (`state/PROJECT_STATE.md:113`) — **[HUMAN]** call to commit or discard; the
      `unity-editmode` remote CI job the harness names **does not exist** (`:105`).
- [ ] Player-reachability reality: `GameRoot.LevelBand` is a flat free-only array that wraps to L001 and
      there is no level select or gating (`unity/Assets/Scripts/Bootstrap/GameRoot.cs:291-301`). A
      district needs a map/level-select surface — **UNBUILT** — and a rule that `LevelBand` never
      contains an `L9xx` id.

## Stage 4 — Entitlement and commerce design reference **[BLOCKED]**

- [ ] **[BLOCKED]** Any entitlement check, purchase UI, catalog adapter or reward grant is monetization
      code: it cannot merge before the human-authored `state/mode` → `production` commit
      (`monetization_spec.md:570-573`; `AGENTS.md` risky-path tripwires; `state/PROJECT_STATE.md:10`).
- [ ] **[HUMAN]** `state/mode` flip — a human-authored commit, never an agent's.
- [ ] Design reference (docs only, already written): `sku-price-proposal.md` §2 (attachment table, one
      check per feature), §3 (placement/copy), and `fair-core-matrix.md` (conformance).
- [ ] **[HUMAN]** §8.7's **single coordinated supersession commit** — partial supersession is invalid
      (`monetization_spec.md:909-911`), and `districts.md` SC-1/SC-2 name two authority rows that the
      table does not currently cover.

## Stage 5 — QA and device pass

- [ ] **[BLOCKED/AGENT]** Durable-item test set per §8.7 (`monetization_spec.md:944-947`): purchase,
      pending, cancel, offline cache, restore, overlap-refund, duplicate callback, account mismatch,
      reinstall.
- [ ] **[BLOCKED/AGENT]** Guest-route rewarded branches per §8.9 step 5: no-fill, decline, cap,
      task-kill, offline, expired trial — and the law that the route writes **zero** progress, stars,
      tickets, medals or leaderboard evidence (`:800-804`).
- [ ] **[AGENT]** Accessibility and performance evidence at parity with a free district (§8.7's closing
      paragraph, `:945-947`).
- [ ] **[HUMAN]** Pixel device QA on a fresh APK; device findings recorded (the open one that touches
      save/dev seams: `persistentDataPath` resolves to **external** app-scoped storage on the Pixel —
      `state/PROJECT_STATE.md:112`).
- [ ] **[HUMAN]** Taste gates that this content can trigger: **TG-3** (how a paid district tile reads on
      the Home map — labelled tile vs depot silhouette; the conflict is explicit and unresolved,
      `docs/prd/ux-flows.md:19`), **TG-4** (results-footer stacking if a district CTA appears, `:20`),
      **TG-5** (copy voice, `:21`), **TG-8** (the rewarded-trial precedent question the guest route
      inherits, `:24`). Not triggered by district content as designed: **TG-2** (planning pause, `:18`),
      **TG-6** (audio mute, `:22`); **TG-7** (consent screen, `:23`) triggers only when ads/commerce
      turn on.

## Stage 6 — Store, release and activation **[HUMAN]**

- [ ] **[HUMAN]** Play Console product creation, promo codes, listing and Data Safety updates. Agents
      never run `fastlane supply` or any Play upload/publish (`AGENTS.md`). Codes and redemption URLs
      never enter this repository (`monetization_spec.md:744-746`) — see `districts.md` SC-4, which
      records that CM-R31.5 currently says the opposite.
- [ ] **[HUMAN]** Listing/store artifacts are **Lane 10's** ownership (`docs/store/**`, `docs/release/**`,
      `docs/runbooks/**`); this lane writes none of them and only points at them.
- [ ] **[HUMAN]** Activation order per §8.9: manifest ships products inactive → license/restore/refund
      matrix → activate one at a time with signed dashboard evidence; district content activates **only
      with its validated routes** (`monetization_spec.md:1007-1011`).

---

## Per-district status board (as of this document)

| District | Slug signed | Levels authored | Art | SKU | Stage reached |
|---|---|---|---|---|---|
| Night Harbor (D-0) | **yes** (`monetization_spec.md:621`) | **no** — no `content/levels/L9*.json` exists | none produced | signed, non-executable | Stage 0 complete, Stage 1 not started |
| Sardine Sidings (D-1) | no — proposal only | no | none | **PROPOSED / UNSIGNED** | awaiting Q-P1 |
| Lantern Hill (D-2) | no — proposal only | no | none | **PROPOSED / UNSIGNED** | awaiting Q-P1 |
| Signal Works (D-3) | no | no | none | none proposed | blocked: needs `cooldown`/`gate` free first |

Nothing in this table is shipped. Nothing in this table is scheduled.
</content>
