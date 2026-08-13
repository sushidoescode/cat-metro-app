# Release checklist — versioning, signing, provenance, gates

Written 2026-08-13 (Lane 10 RELEASE-PREP, contract `state/handoffs/RELEASE-PREP-frozen-contract.md`).
Companion to `docs/runbooks/play-closed-test.md` (the step-by-step closed-test sitting).

**Truthfulness rule for this file:** nothing here describes automation that does not exist. Where a
step is manual, it says manual. Where the answer is not in the repo or in a cited source, it says
**UNKNOWN**. Google-policy claims carry source + retrieval date **2026-08-13**; repo claims carry
`path:line`.

**Actor labels used in this file and in `docs/runbooks/play-closed-test.md`:**

- **[HUMAN]** — a Play Console action, an upload, a tag push, or spend. Agents never perform these
  (`AGENTS.md` §Commands; `docs/constitution.md:43-44`).
- **[LOCAL]** — a human act on the human's own machine (Unity Editor build, keystore creation,
  running the gate scripts). **Never an agent's**: an agent has no Unity, and the keystore steps in
  §2 are explicitly outside anything an agent may reach.
- **[MANUAL]** — a hand-written record with no tooling behind it.

---

## 0. Questions a human must answer before any release act

| # | Question | Blocking what |
|---|---|---|
| **QR-1** | Play developer-account creation date (before / on-or-after 2023-11-13)? | Whether the 12-tester/14-day gate applies at all (`docs/runbooks/play-closed-test.md` §1) |
| **QR-2** | Does an upload keystore exist, and where is it custodied? | Every upload. Tree has none: `unity/ProjectSettings/ProjectSettings.asset:273` empty `AndroidKeystoreName`, `:286` `androidUseCustomKeystore: 0` |
| **QR-3** | Which commit is the release candidate, and has it been played on hardware post-band? | `state/PROJECT_STATE.md:8` records the current dev APK as STALE (b591f46-era, pre-band) |
| **QR-4** | Privacy-policy URL: live, and whose content? | Data safety form + listing (`docs/runbooks/play-closed-test.md` §3.2); see F-2 below |
| **QR-5** | Ads / IAP / data-collection declarations for the exact build? | Review risk; see F-1 and F-3 below |
| **QR-6** | Do we accept shipping with **no** CI-side Unity gate on the release artifact? | Gate map §3 — CI runs no Unity and builds no app |
| **QR-7** | Commit signing: adopt SSH signing now, or keep accepting unsigned agent commits in the release lineage? | Provenance §4; `state/PROJECT_STATE.md:84` records SSH signing "still on the debt list" |
| **QR-8** | Who bumps `AndroidBundleVersionCode`, and is the bump commit part of the candidate? | Versioning §1 |
| **QR-9** | Is `com.catmetro.game` already claimed by an existing Play app record? | Permanent on first upload — **UNKNOWN** from this repo |

---

## 1. Versioning

Current tree values:

| Field | Value | File |
|---|---|---|
| `bundleVersion` (versionName) | `0.1.0` | `unity/ProjectSettings/ProjectSettings.asset:147` |
| `AndroidBundleVersionCode` | `1` | `unity/ProjectSettings/ProjectSettings.asset:177` |
| `applicationIdentifier.Android` | `com.catmetro.game` | `unity/ProjectSettings/ProjectSettings.asset:170` |
| Unity editor | `6000.3.16f1` | `unity/ProjectSettings/ProjectVersion.txt:1` |

Rules (verbatim, [Version your app](https://developer.android.com/studio/publish/versioning),
retrieved 2026-08-13): `versionCode` is "a positive integer"; each successive release "must use a
greater value than the previous release"; "Google Play allows a maximum versionCode of 2,100,000,000";
"You cannot upload an APK with a versionCode you have already used".

Checklist per candidate:

- [ ] `AndroidBundleVersionCode` incremented **[LOCAL]** — including for a rebuild of the same commit.
- [ ] `bundleVersion` updated if the change is player-visible **[LOCAL]**.
- [ ] The bump is a commit whose subject names the version code **[LOCAL]** — this is the only
      version↔commit binding that exists; nothing generates it.
- [ ] Ledger row appended (§5) **[MANUAL]**.

## 2. Signing

- **Today the project is unsigned for release.** `androidUseCustomKeystore: 0` and an empty
  `AndroidKeystoreName` (`unity/ProjectSettings/ProjectSettings.asset:273,286`) mean Unity falls back
  to the debug certificate. "Because the debug certificate is created by the build tools and is
  insecure by design, most app stores (including the Google Play Store) do not accept apps signed
  with a debug certificate for publishing"
  ([Sign your app](https://developer.android.com/studio/publish/app-signing), retrieved 2026-08-13).
- **Play App Signing** is the default for new apps: "By default, when you upload your app bundle,
  your app is automatically enrolled in quantum-ready, hybrid signing with Google-generated keys";
  the upload key is "the key you use to sign your app bundle before uploading it to the Play Console"
  ([Play App Signing](https://support.google.com/googleplay/android-developer/answer/9842756?hl=en), retrieved 2026-08-13).
- **Custody:** `docs/security/threat-model.md:235` scores the upload keystore + password
  **"In the repo? Never"** and **"Agent-reachable? Never"**, and answers "Where it must live" with
  "Encrypted CI secret + Play App Signing, so the repo can only ever hold an *upload* key (RK-33)".
  Both prohibitions bind independently: it is never committed, **and** it is never placed where an
  agent session can read it (no repo-relative path, no exported variable in a shell an agent runs in,
  no `.env` an agent could open). There is **no secret-scanning gate in CI today** (§3), so this rule
  is enforced by the human, not by the machine.
- **Recorded follow-up (OWED, outside this lane's charter — flagged for a follow-up contract):** the
  repo has no `.gitignore` patterns for keystore-shaped files (`*.keystore`, `*.jks`, `*.p12`,
  `*.pem`). Until some contract adds them, an accidental `git add` of a key is not caught by anything.
  If one ever does touch git history, the recovery is Play's upload-key reset (see Loss recovery
  below) plus rotating the key — not a history rewrite alone.
- **Loss recovery:** with Play App Signing you "can create a new one and request an upload key reset
  in the Play console"; "Resetting your upload key will not affect the app signing key that Google
  Play uses" (same Android source, retrieved 2026-08-13).
- Checklist: [ ] keystore created outside the repo **[HUMAN]** · [ ] backed up in two places
  **[HUMAN]** · [ ] `androidUseCustomKeystore` on and keystore/alias set in Player Settings
  **[LOCAL]** · [ ] "Development Build" **off** **[LOCAL]** · [ ] built artifact is an `.aab`
  **[LOCAL]**.

## 3. Gate map — what actually runs today

| Gate | Command / trigger | What it really enforces | What it does NOT do |
|---|---|---|---|
| `scripts/check.sh` | local + CI: PR + push to main (`.github/workflows/ci.yml:13`) | `bash -n` syntax over `scripts/**` and `tests/**` shell files (`:21-27`); zero unresolved double-brace init tokens anywhere in the tree, binary files skipped (`:29-38`) — the scan is repo-wide, so writing such a token into *any* file, including a doc, fails the gate (`check.sh:29` assembles its own pattern by concatenation precisely to avoid self-matching); banned-symbol scans over `unity/Assets/Scripts/Domain`, `.../Content`, `.../Domain/Solver`, `.../Content/Daily` and linked pure tests, fail-closed on a missing root (`:40-141`) | It is **not** a compiler and **not** a typechecker — its own line 2 says "stack-agnostic stand-in until the engine lands (TODO(stack): wire real lint+typecheck)"; line 143 prints "interim harness". **No C# is compiled by check.sh.** |
| `scripts/test.sh` | local + CI (`.github/workflows/ci.yml:14`) | runs every `tests/**/*.test.sh`, pass iff exit 0 (`:10-18`) | it does **not** run Unity EditMode/PlayMode suites; those run only where a Unity install exists. "No tests found = green" (`:5,20-23`) |
| `scripts/build.sh` | local + not in CI | content-staging check only, refuses `--apply` (`:12-18`) | **produces no artifact**: `:20` prints "build: nothing to build yet — engine scaffold lands with the specs (TODO(stack))"; `:9-11` disclaims the real device build path |
| `.github/workflows/ci.yml` | PR + push to main | `ubuntu-latest`, checkout, `check.sh`, `test.sh` | no toolchain/dependency install (`:11-12` TODOs), **no Unity, no compile, no APK/AAB**, no e2e (`:15`), no dependency audit (`:16`), **no secret scan** (`:17` `TODO(secret-scan)`) |
| `.github/workflows/forge-policy.yml` | PR events | protected-path provenance: a branch cannot modify `ci.yml`/`forge-policy.yml` (`:57-60,223-234`); commits touching immutable paths must be server-resolved human GitHub users with a verified signature (`:82-109,172-209`); fails closed (`:286-288`) | its own header (`:5-10`) says a green result means "no bot-authored change to a protected path was detected", **not** "a human definitely wrote this" |
| `.github/workflows/deploy.yml` | push of tag `v*` | checkout only; `environment: production` | **empty** — `:14` is `TODO(deploy): no production yet — fill Google Play rollout …`. **Nothing uploads to Play.** `:11` notes reviewers/protection rules on the `production` environment still have to be added in repo settings (state **UNKNOWN** from the repo) |
| `.github/workflows/claude-review.yml.disabled` | — | nothing; disabled | per-push agent review is off (`state/PROJECT_STATE.md:93`, `TODO(review-auth)`) |
| Unity EditMode/PlayMode suites | manual, one machine | the real C# coverage (counts recorded per-PR in `state/PROJECT_STATE.md`) | **the `unity-editmode` remote job the harness names does not exist** — `state/PROJECT_STATE.md:105`: "the `unity-editmode` remote job the test harness names does NOT exist — Unity suites run on exactly one machine" |

**Read this map before writing any release claim.** The honest summary: *the machine gate proves
shell syntax, token hygiene, banned-symbol purity and shell-test exit codes. It does not compile the
game, does not run the Unity suites, does not build the artifact, does not scan for secrets, and does
not upload anything.*

Related recorded CI gaps (`state/PROJECT_STATE.md:105`): (b) once the release build path is tracked,
CI must assert the release job never sets `BuildOptions.Development` — **not built**; (c) the
`check.sh` init-token block has zero behavioral test coverage.

## 4. Provenance and attestation — what is and is not attested

- **Build shim untracked.** `unity/Assets/Editor/CatMetroCliBuild.cs` "is untracked on EVERY ref; a
  clean clone cannot build an APK until it is committed or copied in from the main checkout"
  (`state/PROJECT_STATE.md:113`; same warning `state/handoffs/SESSION-HANDOFF-2026-08-08.md:54`).
  Verified in this worktree: `unity/Assets/Editor/` holds only `SceneBootstrapper.cs`. Its recorded
  text (`evals/results/device/c2b-crit8/ARTIFACT.md:114-151`) builds an **APK** via
  `BuildPipeline.BuildPlayer` and never sets `EditorUserBuildSettings.buildAppBundle` — **so no
  scripted/CLI AAB path exists; the manual Unity Editor route (`docs/runbooks/play-closed-test.md`
  §4.3) is the only path today.** Human call still open (`state/PROJECT_STATE.md:113`): commit it, record it
  as an artifact, or discard it (two sibling shims, `DevfixUrpSetup.cs` and `SpikeUrpSetup.cs`, are in
  the same class).
- **No signed commits.** `state/PROJECT_STATE.md:84` — Amendment 1's bootstrap route relied on the
  web-flow squash, and "SSH commit signing still on the debt list". `forge-policy.yml` requires a
  verified signature only for commits touching immutable paths (`:196-205`); ordinary product commits
  in a release lineage are unsigned.
- **No secret scanning.** `state/PROJECT_STATE.md:92` + `.github/workflows/ci.yml:17` — gitleaks or
  equivalent is a TODO, "required before graduation to production".
- **Binary provenance unattested.** The last device APK is recorded with a SHA-256 in
  `state/PROJECT_STATE.md:8` but that record explicitly ends "binary provenance unattested".
- **Merge-authority census open.** `state/PROJECT_STATE.md:73` carries the HC-25 deviation register
  (appends one–five) — ratification is a human-authored-commit act and is still open for #35–#76.
  Any "this release was reviewed and merged under policy X" claim must point at that register rather
  than assert clean provenance.
- **Attested evidence path:** `evals/results/attested/` is human/CI-only (`AGENTS.md` hard rule 1);
  agents write claims to `evals/results/`. No release artifact has been written to either as an
  attested build record.

**Net:** the strongest provenance available for the first closed-test AAB is *manual*: a version-code
bump commit, a recorded `git rev-parse HEAD`, a recorded local `git status`, and a SHA-256 the human
computes. Record it in §5. Do not describe it as an attested supply chain.

## 5. Build provenance ledger (manual — append one row per uploaded artifact)

| versionCode | versionName | git SHA built from | tree clean? | AAB SHA-256 | uploaded to | date | by |
|---|---|---|---|---|---|---|---|
| _(example — not a record)_ 2 | 0.1.1 | `abc1234` | yes | `…` | closed track | 2026-08-13 | HUMAN |

Rules: one row per **upload attempt**, including rejected ones (the version code is consumed either
way); "tree clean?" is the `git status` observation at build time; the SHA-256 is computed by the
human (`shasum -a 256 <file>`), not by any gate in this repo.

## 6. Human-only floor (agents must never do these)

Sourced, not invented:

1. **Any Google Play upload or publish** — `AGENTS.md` §Commands: "Never run: `fastlane supply` or
   any other Google Play upload/publish (humans only, via CI from tags)".
2. **Tag pushes, releases, deploys, spend, `state/mode`, ADR approval, and anything a review flags
   for human judgment** — `docs/constitution.md:43-44` (Amendment 1 condition 4); reinforced by
   `docs/constitution.md:12` ("Irreversible actions get human gates: deploy, spend, data migration,
   disclosure, tag/release").
3. **Flipping `state/mode` to production before any billing/IAP/ads code merges** — `AGENTS.md`
   §Risky paths + `state/PROJECT_STATE.md:10`; `state/mode` is an immutable path (`AGENTS.md` hard
   rule 1) and human-authored-commit only.
4. **Play Console actions of every kind** — account settings, declarations, track config, tester
   roster, production-access application.
5. **Editing immutable paths** — `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
   `scripts/git-hooks/`, `state/mode`, `evals/` except `evals/results/`
   (`AGENTS.md` hard rule 1; enforced by `forge-policy.yml:82-109`).
6. **Rotating/ratifying the HC-25 census rows** in `state/PROJECT_STATE.md:73` — the row says so
   itself.

## 7. Pre-upload checklist (one pass, in order)

Repo side **[LOCAL]**:

- [ ] `bash scripts/check.sh` green
- [ ] `bash scripts/test.sh` green (record `N/N`)
- [ ] `bash scripts/build.sh` green — *content staging check only; it builds nothing*
- [ ] Unity EditMode + PlayMode suites run on the build machine; counts recorded (no CI equivalent — §3)
- [ ] `git status` clean, or the deviation recorded in the ledger row
- [ ] version code bumped (§1)
- [ ] release build: App Bundle **on**, Development Build **off**, custom keystore configured (§2)
- [ ] artifact SHA-256 recorded (§5)
- [ ] the device play-through: candidate installed and played on hardware (`state/PROJECT_STATE.md:8`
      standing visual-verification rule — code-green alone is not evidence)

Console side **[HUMAN]** — detail in `docs/runbooks/play-closed-test.md`:

- [ ] device verification complete (§2 of the runbook)
- [ ] app record exists; package id correct and intentional
- [ ] store listing fields pasted from `docs/store/play-store-listing.md` (pointer only — that file
      is Lane 7's and is not edited by this lane)
- [ ] graphics meet Play specs (icon 512×512, ≥2 screenshots — runbook §3.2)
- [ ] content rating, target audience, ads declaration, data safety, privacy policy URL — all
      answered **for this build**
- [ ] closed track created, testers configured, release rolled out
- [ ] opt-in URL copied and sent (`docs/release/tester-comms-template.md`)

---

## 8. Flagged discrepancies — `docs/plan/**` (FLAG, never fix)

`docs/plan/**` is read-only for this lane (contract §Charter). Each row states what the plan says,
what the repo/current sources say, and who decides. **No plan file was edited.**

| # | Plan claim | Reality as of 2026-08-13 | Impact | Disposition |
|---|---|---|---|---|
| **F-1** | `docs/plan/DAY1_RUNBOOK.md:29`: create the app declaring "App will contain **in-app purchases** and **ads** (rewarded only — declare ads = yes)" | No monetization or ads code exists: `unity/Packages/manifest.json` lists only Unity first-party packages + `com.unity.nuget.newtonsoft-json`; `unity/ProjectSettings/UnityConnectSettings.asset` has `UnityAdsSettings.m_Enabled: 0`, `UnityPurchasingSettings.m_Enabled: 0`. `AGENTS.md` §Risky paths requires a human `state/mode`→production flip *before* any billing/IAP/ads code merges | Declaring capabilities the build lacks contradicts `docs/prd/PRD.md:715` ("answers must match actual app behavior — a mismatch is a policy violation") | **HUMAN decides the declarations.** Flagged only |
| **F-2** | `docs/plan/DAY1_RUNBOOK.md:112-113` + `docs/plan/EXECUTION_PLAN.md:46,425`: privacy policy live at `catmetro.com/privacy` before the first listing save | The drafted page `docs/plan/web/privacy/index.html:112-114` names Google (Play, Crashlytics, AdMob), **RevenueCat** and **OneSignal** as data recipients. None of those SDKs is a dependency — verified against `unity/Packages/manifest.json` (Unity first-party packages + `com.unity.nuget.newtonsoft-json` only). All in-repo occurrences of those four names are inert text, not code paths: in `unity/**` `.cs` they are 3 matching lines across 2 files — a comment (`unity/Assets/Scripts/Services/Analytics/IAnalytics.cs:21`) and two QA-procedure sink strings in the declared-dark event taxonomy (`unity/Assets/Scripts/Application/EventTaxonomy/Taxonomy.cs:51,100`) — plus prose and CSV rows under `docs/`. Domain registration status: **UNKNOWN** from this repo | Publishing that page as-is over-declares data collection for the closed-test build and will not match a truthful Data safety form | **HUMAN** authors/points the policy at the real build. Flagged only |
| **F-3** | `docs/plan/specs/revenuecat_implementation.md:54`: upload "the Day-1 skeleton AAB (with Billing permission, targetSdk 36)" to the closed track and recruit 12 testers | No Billing library, no RevenueCat, no service-account wiring in the tree; the production-mode gate (F-1) is unflipped (`state/mode` = sprint per `state/PROJECT_STATE.md:8`) | Following it literally would add a Billing permission to a build with no billing code | **HUMAN**; the closed test does not need it. Flagged only |
| **F-4** | `docs/plan/data/github_issue_backlog.md:23-24`: M1/M2 gates dated **Aug 1–7 / Aug 8–14** requiring 20 solver-validated levels plus "RC sandbox purchase + rewarded ad + push each pass on the current device build" by D14 | Today is 2026-08-13 with 17 wired levels (`state/PROJECT_STATE.md:8`) and none of the RC/ads/push systems built; the closed test has not started | The plan's milestone dates are stale by construction; treating them as live gates mis-sequences today's work | **HUMAN** re-dates or retires. Flagged only |
| **F-5** | `docs/plan/DAY1_RUNBOOK.md:16-19` and `docs/prd/PRD.md:958` (A-18) both treat an account created before 2023-11-13 as exempt from the tester gate entirely | **Consistent with current Google documentation** — the requirement binds "personal accounts created after November 13, 2023" ([source](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en), retrieved 2026-08-13). Not a discrepancy; recorded because the *answer for this account* is still QR-1/**UNKNOWN** | none if answered | **HUMAN** answers QR-1 |
| **F-6** | `state/handoffs/CM-C10-frozen-contract.md:347` claims the build shim "is committed on main" | False — the shim is untracked on every ref (`state/PROJECT_STATE.md:113`, `state/handoffs/SESSION-HANDOFF-2026-08-08.md:54`, and this worktree's `unity/Assets/Editor/` listing). This is a `state/handoffs` file, not `docs/plan`, and is recorded here because it misleads a release-time reader | A release procedure trusting that line would fail at build time | Already corrected in the newer handoff; **HUMAN** may prune. Flagged only |

### Cross-pack notes on `docs/store/**` (Lane 7's shipped pack — pointer only, never edited)

| # | Observation | Source of truth |
|---|---|---|
| **X-1** | `docs/store/creative-shot-list.md:48-51` specifies **1179 × 2556** screenshot masters. That is a 1:2.168 ratio; Play's asset page states phone screenshots use "a 9:16 aspect ratio for portrait" (1:1.778) and that "The maximum dimension of your screenshot can't be more than twice as long as the minimum dimension", with 1080×1920 named as the minimum for large-format promotion eligibility ([source](https://support.google.com/googleplay/android-developer/answer/9866151?hl=en), retrieved 2026-08-13). Whether Console rejects 1179×2556 in practice is **UNVERIFIED** | Lane 7 owns the file; **HUMAN** decides (re-spec, or test one upload) |
| **X-2** | `docs/store/play-store-listing.md:52-53` ships "FIVE HANDCRAFTED LEVELS"; main now wires 17 (`state/PROJECT_STATE.md:8`). The pack's own release-editor rule 3 (`:101`) already requires re-running every claim against the exact release candidate | Not a defect — the copy is conservative and self-governed. **HUMAN** may re-count for the candidate |
| **X-3** | `docs/store/creative-shot-list.md:19` briefs a **1024 × 1024** icon master; Play's upload spec is "512px by 512px", "32-bit PNG (with alpha)" ([same source](https://support.google.com/googleplay/android-developer/answer/9866151?hl=en), retrieved 2026-08-13). The brief already prescribes inspecting native exports at 512 (`:39`) | Downscale at upload time; no conflict |

---

## 9. Sources

Google-policy sources with retrieval dates are tabulated in `docs/runbooks/play-closed-test.md` §12
(all retrieved 2026-08-13). The two used directly above:

- Version codes — https://developer.android.com/studio/publish/versioning (retrieved 2026-08-13)
- Signing / debug certificates / upload key reset — https://developer.android.com/studio/publish/app-signing (retrieved 2026-08-13)
- Play App Signing — https://support.google.com/googleplay/android-developer/answer/9842756?hl=en (retrieved 2026-08-13)
- Store graphics specs — https://support.google.com/googleplay/android-developer/answer/9866151?hl=en (retrieved 2026-08-13)
- Closed-test tester requirement — https://support.google.com/googleplay/android-developer/answer/14151465?hl=en (retrieved 2026-08-13)
