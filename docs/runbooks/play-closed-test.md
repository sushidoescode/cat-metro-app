# Runbook — Google Play closed test, from zero to opted-in testers

Written 2026-08-13 (Lane 10 RELEASE-PREP, contract `state/handoffs/RELEASE-PREP-frozen-contract.md`).
Every Google-policy statement below carries a source URL and the retrieval date **2026-08-13**
(§12). Every repo statement cites the file it came from. Where the answer is not in a source or in
the repo, the line says **UNKNOWN** — it is never guessed.

**Who acts.** Steps marked **[HUMAN]** are Play Console actions, uploads, tag pushes, or spend.
Agents never perform them: `AGENTS.md` §Commands — "Never run: `fastlane supply` or any other
Google Play upload/publish (humans only, via CI from tags)"; `docs/constitution.md:43-44` — "Still
human-only regardless: tag pushes, releases, deploys, spend, `state/mode`, ADR approval". Steps
marked **[LOCAL]** are things the human does on their own machine (Unity build). Nothing in this
runbook is automated today; see `docs/release/release-checklist.md` §Gate map.

---

## 0. READ FIRST — questions only the human can answer

These are blockers, not nice-to-haves. Q0-1 can delete two weeks of critical path; Q0-3 and Q0-5
can stop today's upload dead.

| # | Question | Why it blocks | Where to check |
|---|---|---|---|
| **Q0-1** | **When was the Play Console developer account created — before or on/after 2023-11-13?** | The 12-tester/14-day rule applies only to personal accounts created **after November 13, 2023** (§1). Before that date: no tester minimum at all, and the closed test becomes optional QA rather than a gate. | **[HUMAN]** Play Console → Settings → Developer account |
| **Q0-2** | Has **device verification** been completed for this account? | Required for new personal accounts *before you can make an app available* (§2.2). Unverified = the release will not publish. | **[HUMAN]** Play Console mobile app |
| **Q0-3** | Is there an **upload keystore** anywhere outside the repo? | The tree has none configured: `unity/ProjectSettings/ProjectSettings.asset:273` `AndroidKeystoreName:` is empty and `:286` `androidUseCustomKeystore: 0`. Without one Unity signs with the debug certificate, which Play does not accept (§4.3). | **[HUMAN]** the human's own key custody |
| **Q0-4** | Is the Play app already created and is `com.catmetro.game` already claimed by an upload? | The package id is frozen by the first upload forever. Repo value: `ProjectSettings.asset:170` `Android: com.catmetro.game`. Whether a Console app exists is **UNKNOWN** from this repo. | **[HUMAN]** Play Console → All apps |
| **Q0-5** | What **privacy policy URL** will be pasted into the listing, and is it live right now? | A privacy policy link is mandatory for the Data safety form, which closed-track apps must complete (§3.2). `docs/plan/DAY1_RUNBOOK.md:112-113` plans `catmetro.com/privacy`; whether the domain is registered/served is **UNKNOWN** from this repo, and the drafted page has content problems — see §Flagged discrepancies in `docs/release/release-checklist.md`. | **[HUMAN]** browser |
| **Q0-6** | Does the closed-test build contain **ads or IAP**? | Answer today from the committed tree: **no**. `unity/Packages/manifest.json` lists only Unity first-party packages plus `com.unity.nuget.newtonsoft-json`; in `unity/ProjectSettings/UnityConnectSettings.asset` the service flags read `UnityAdsSettings.m_Enabled: 0`, `UnityPurchasingSettings.m_Enabled: 0`, `UnityAnalyticsSettings.m_Enabled: 0`, `PerformanceReportingSettings.m_Enabled: 0`, `CrashReportingSettings.m_EnableCloudDiagnosticsReporting: 0`, and top-level `m_Enabled: 0` (the one flag that is **on** is `InsightsSettings.m_EngineDiagnosticsEnabled: 1` — what, if anything, that sends from a player build is **UNKNOWN** here; confirm before answering the Data safety form). That file also had uncommitted local modifications at session start — see §4.3 step 7. `docs/plan/DAY1_RUNBOOK.md:29` instructs declaring ads = yes and IAP = yes; declaring capabilities the build does not have contradicts `docs/prd/PRD.md:715` ("answers must match actual app behavior — a mismatch is a policy violation"). The human decides the declarations. | this file + Console |
| **Q0-7** | **Which commit is the closed-test candidate**, and who has played it on hardware? | `state/PROJECT_STATE.md:8` records the current dev APK as **STALE** (b591f46-era, pre-band) while main carries 17 wired levels. A closed test ships whatever is uploaded. | **[HUMAN]** |
| **Q0-8** | Who are the **12+ testers** (target 16–20 invited), and email list or Google Group? | The roster mechanic is a one-way-ish choice made when the track is configured (§6). | **[HUMAN]** |
| **Q0-9** | Does the human accept the store copy as-is for this test? | `docs/store/play-store-listing.md:52-53` says "FIVE HANDCRAFTED LEVELS"; main now wires 17 (`state/PROJECT_STATE.md:8`). The pack's own rule 3 (`docs/store/play-store-listing.md:101`) says re-run every claim against the exact release candidate. Lane 7 owns that file — **do not edit it here**; the human either re-counts or ships the conservative copy. | `docs/store/` |

---

## 1. The rule that sets the clock (verified today)

- **Who it binds:** "Developers with personal accounts created after November 13, 2023, will need to
  test their apps before those apps are eligible to be published."
  ([Play Console Help, App testing requirements for new personal developer accounts](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en), retrieved 2026-08-13)
- **The bar:** "You must run a closed test with a minimum of 12 testers who have been opted-in for
  at least the last 14 days continuously." (same page, retrieved 2026-08-13)
- **Continuity is strict:** "we won't count testers who opted in, tested for less than 14 days, and
  then opted out. Even if they opt back in so that they are opted in for a total of 14 days, these
  14 days must be consecutive to count." (same page, retrieved 2026-08-13)
- **At application time:** "When you apply for production access, at least 12 testers must be
  opted-in to your closed test. They must have been opted-in for the last 14 days continuously."
  (same page, retrieved 2026-08-13)
- **History note:** the bar was 20 testers before December 11, 2024 and is 12 now — secondary
  reporting ([PrimeTestLab](https://primetestlab.com/blog/google-play-changed-20-to-12-testers), retrieved 2026-08-13); the 12 figure itself is primary-sourced above. The repo's plan documents already say 12/14 (`docs/plan/_working_claim_inventory.md:29`), which matches today's primary source.

### 1.1 What the 14 days actually gate — and what they do not

The requirement text conditions on **tester opt-in continuity**, not on build freshness: nothing on
the cited page ties the 14 days to a release, an upload, or a version code. So a better build
uploaded on Aug 20 does **not** restart the tester clock. *No source states this explicitly in the
negative* — treat "a new closed release does not reset the 14 days" as read-from-silence, i.e.
**UNVERIFIED** as an affirmative Google statement, while the affirmative requirement (12 testers,
14 continuous days) is quoted verbatim above.

What *does* sit on the critical path is the first publish: **the opt-in link "only shows when an app
is Published"** ([Play Console Help, Set up an open, closed, or internal test](https://support.google.com/googleplay/android-developer/answer/9845334?hl=en), retrieved 2026-08-13). Testers cannot opt in before the first closed release clears review.

### 1.2 Date math for this project (assumes Q0-1 = "on/after 2023-11-13")

Review duration, quoted: "For certain developer accounts, we'll take more time to thoroughly review
your app to help better protect users. This may result in review times of up to seven days or longer
in exceptional cases."
([Play Console Help, Publish your app](https://support.google.com/googleplay/android-developer/answer/9859751?hl=en), retrieved 2026-08-13.) There is **no published numeric SLA** for a first release; treat 7 days as the planning worst case.

| Scenario | First closed release published | 12th tester opted in | 14 continuous days complete | Earliest production-access application **[HUMAN]** |
|---|---|---|---|---|
| Best case (review clears same day, roster ready) | 2026-08-13 | 2026-08-13 | 2026-08-27 | 2026-08-27 |
| Planning case (review ~3 days, roster fills over 2) | 2026-08-16 | 2026-08-18 | 2026-09-01 | 2026-09-01 |
| Worst case (7-day review, slow roster) | 2026-08-20 | 2026-08-23 | 2026-09-06 | 2026-09-06 |

Boundary caveat: Google's page does not define whether the 14th day counts inclusively or whether a
timezone/midnight boundary applies — **UNKNOWN**. Add one buffer day before applying.

**Consequence for the ~Aug-15 clock in `state/PROJECT_STATE.md:8` and the Aug-20 feature-complete
target:** the two are not in tension — upload *something shippable today*, keep uploading better
builds into the same track, and let the Aug-20 build land on a roster whose clock has already been
running for a week. Waiting for feature-complete before the first upload converts a 7-day review
risk into a 7-day slip of the entire production-access date.

---

## 2. Account preflight **[HUMAN]** (~15 minutes, do before anything else)

1. **Account creation date** — Settings → Developer account. Record the answer to Q0-1 in the
   session record; it decides whether §1 binds at all.
2. **Device verification** — required for new personal accounts: "Developers with new personal
   accounts" must verify device access "before they can make their app available on Google Play";
   the device must be "any non-rooted physical Android mobile device that runs at least the Android
   10 operating system"; the flow is Console → QR code → Play Console mobile app → Verify.
   ([Play Console Help, Device verification requirements](https://support.google.com/googleplay/android-developer/answer/14316361?hl=en), retrieved 2026-08-13.) The Pixel 9 Pro already used for device testing (`state/PROJECT_STATE.md:8`) satisfies the device condition.
3. **Payment/registration fee** — a one-time developer registration fee applies to new accounts
   (`docs/plan/DAY1_RUNBOOK.md:23` records **$25**; the *current* fee is **UNVERIFIED** here — read it
   in the signup flow). Spend is **[HUMAN]** by `docs/constitution.md:43-44`.
4. Turn on **Managed publishing** if you want review results to queue instead of going live the
   moment they pass (`docs/plan/DAY1_RUNBOOK.md:36` recommends it). Optional; it adds one click per
   release and removes accidental publishes.

---

## 3. Create the app and clear the content gates **[HUMAN]**

### 3.1 App record

Play Console → **Create app**: name from `docs/store/play-store-listing.md:22` (`Cat Metro: Train
Puzzle`, 23/30 characters per that file's count), default language en-US, **Game**, **Free**.
The package id is *not* typed here — it is frozen by the first uploaded artifact
(`docs/plan/DAY1_RUNBOOK.md:31-32`). Repo value: `com.catmetro.game`
(`unity/ProjectSettings/ProjectSettings.asset:170`). A wrong-package first upload is unrecoverable
on that app record.

### 3.2 App content declarations — the closed test will not publish without these

- **Data safety applies to closed tracks:** "All developers that have an app published on Google
  Play must complete the Data safety form, including apps on closed, open, or production testing
  tracks", and "Even developers with apps that do not collect any user data must complete this form
  and provide a link to their privacy policy." Internal-testing-only apps are exempt: "Apps that are
  active on internal testing tracks are exempt from inclusion in the data safety section."
  ([Play Console Help, Data safety](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en), retrieved 2026-08-13.)
- **Also required before review:** privacy policy link, content rating questionnaire, ads
  declaration, target audience/age group, and app-access details if any part of the app needs
  sign-in ([Play Console Help, Prepare your app for review](https://support.google.com/googleplay/android-developer/answer/9859455?hl=en), retrieved 2026-08-13).
- **Store listing graphics** (needed to publish the listing): app icon "512px by 512px", "32-bit PNG
  (with alpha)", max 1024KB; feature graphic "1024px by 500px"; "a minimum of two screenshots across
  different device types"; screenshot "Minimum dimension: 320px", "Maximum dimension: 3840px", and
  "The maximum dimension of your screenshot can't be more than twice as long as the minimum
  dimension"; phone aspect ratio "16:9 for landscape and a 9:16 aspect ratio for portrait".
  ([Play Console Help, Add preview assets](https://support.google.com/googleplay/android-developer/answer/9866151?hl=en), retrieved 2026-08-13.)
  Cross-reference only (Lane 7 owns these, never edit them here): the creative brief in
  `docs/store/creative-shot-list.md:19-46` (1024×1024 icon master → downscale to Play's 512) and
  `:48-51` (1179×2556 screenshots — that is 1:2.168, which is **outside** both the 9:16 shape and the
  "no more than twice the minimum dimension" rule as written; see the flag in
  `docs/release/release-checklist.md`).
- Answer every declaration **for the exact build you are uploading**, not for the roadmap. See Q0-6.

### 3.3 Create the closed track

Play Console → **Testing → Closed testing → Manage track**
([Play Console Help, Set up an open, closed, or internal test](https://support.google.com/googleplay/android-developer/answer/9845334?hl=en), retrieved 2026-08-13). Play provides a default closed track; `docs/plan/DAY1_RUNBOOK.md:33` names the intended track `closed-alpha`. Console labels drift between releases — the intent is "a closed testing track whose testers you control".

---

## 4. Build the upload artifact from THIS repo — the honest state

### 4.1 What the repo's build gate does today (nothing shippable)

`scripts/build.sh` is a stand-in. Verbatim from the file:

- `scripts/build.sh:2` — "Build gate — TODO(stack): no build target until the engine lands."
- `scripts/build.sh:18` — it runs `stage-content.sh` in check-only mode (it refuses `--apply`,
  `:12-17`).
- `scripts/build.sh:20` — `echo "build: nothing to build yet — engine scaffold lands with the specs (TODO(stack))"`
- `scripts/build.sh:9-11` explicitly disclaims the real device path.

**`bash scripts/build.sh` does not produce an APK or an AAB.** Do not put it in a release procedure
as if it did.

### 4.2 The build shim is untracked, and as recorded it makes an APK, not an AAB

- `state/PROJECT_STATE.md:113` — "`unity/Assets/Editor/CatMetroCliBuild.cs` — the `-executeMethod`
  build shim — is untracked on EVERY ref; a clean clone cannot build an APK until it is committed or
  copied in from the main checkout". Confirmed in this worktree: `unity/Assets/Editor/` contains only
  `SceneBootstrapper.cs` (+ `.meta`).
- `state/handoffs/SESSION-HANDOFF-2026-08-08.md:54` — same warning, plus the failure mode: Unity exits
  with "executeMethod class 'CatMetroCliBuild' could not be found", easy to misdiagnose under
  licensing noise.
- The shim's recorded text (`evals/results/device/c2b-crit8/ARTIFACT.md:114-151`) calls
  `BuildPipeline.BuildPlayer` with `locationPathName = $CM_APK_OUT` and sets
  `options = dev ? BuildOptions.Development : BuildOptions.None`. It never sets
  `EditorUserBuildSettings.buildAppBundle`. **So even with the shim in place, the recorded CLI path
  produces an APK.**
- Google Play requires the **Android App Bundle** for new apps (announced 2020-11 for August 2021,
  [Android Developers Blog](https://android-developers.googleblog.com/2020/11/new-android-app-bundle-and-target-api.html), retrieved 2026-08-13; [About Android App Bundles](https://developer.android.com/guide/app-bundle), retrieved 2026-08-13).

**Therefore: there is no AAB build path in this repository today.** Adding one is a code change
(`unity/**`) and is out of scope for this lane — it needs its own contract. Today's route is the
Unity Editor GUI, done by the human.

### 4.3 Today's route — Unity Editor, by hand **[LOCAL]**

Unity `6000.3.16f1` (`unity/ProjectSettings/ProjectVersion.txt:1`). Menu labels drift between Unity
versions; the intent line matters more than the exact label.

1. Open `unity/` in Unity 6000.3.16f1. Confirm the Android module is installed.
2. **Content staging first.** Run `bash scripts/build.sh` (check-only stager) and `bash scripts/test.sh`
   from the repo root before building — that is the only automated verification that the staged
   content tree matches `content/` (`scripts/build.sh:5-18`).
3. Build Settings / Build Profiles → platform **Android** →
   - **enable** "Build App Bundle (Google Play)" — this is what makes the artifact an `.aab`;
   - **disable** "Development Build". A development build is debuggable and carries the dev-fenced
     capture seams (the `DEVCAP_*` fences recorded in `state/PROJECT_STATE.md:8`); it must never be
     the Play artifact. Play's own tooling docs: "Because the debug certificate is created by the
     build tools and is insecure by design, most app stores (including the Google Play Store) do not
     accept apps signed with a debug certificate for publishing."
     ([Sign your app](https://developer.android.com/studio/publish/app-signing), retrieved 2026-08-13.)
4. **Create the upload key** — Player Settings → Publishing Settings → Keystore Manager → create a
   new keystore + key. Today `androidUseCustomKeystore: 0` and `AndroidKeystoreName:` is empty
   (`unity/ProjectSettings/ProjectSettings.asset:273,286`), so nothing is configured yet.
   - Store the `.keystore` **outside the repository**. The repo's own rule:
     `docs/security/threat-model.md:235` — upload keystore + password "Never" in repo, "Never" in
     logs; the repo "can only ever hold an *upload* key (RK-33)". Treat that as never-in-repo.
   - Back it up (password manager + one offline copy). If it is lost: with Play App Signing you "can
     create a new one and request an upload key reset in the Play console", and "Resetting your
     upload key will not affect the app signing key that Google Play uses"
     ([Sign your app](https://developer.android.com/studio/publish/app-signing), retrieved 2026-08-13).
   - Play App Signing is the default for new apps: "By default, when you upload your app bundle, your
     app is automatically enrolled in quantum-ready, hybrid signing with Google-generated keys"; the
     upload key is "the key you use to sign your app bundle before uploading it to the Play Console"
     ([Play Console Help, Play App Signing](https://support.google.com/googleplay/android-developer/answer/9842756?hl=en), retrieved 2026-08-13).
5. Set the version code and name — see §5.
6. Build. Record the output path, the `git rev-parse HEAD` of the tree you built from, and the AAB's
   SHA-256 into the provenance ledger in `docs/release/release-checklist.md`. This is manual; nothing
   computes it for you.
7. **Warning about the local tree:** the main checkout was reported at 2026-08-13 session start with
   *uncommitted* modifications to `unity/ProjectSettings/ProjectSettings.asset` and
   `unity/ProjectSettings/UnityConnectSettings.asset` (coordinator-reported `git status`; not
   verifiable from any ref). A build made there is not necessarily a build of any commit. Check
   `git status` before building, and record what you saw.

### 4.4 Platform settings the tree already satisfies (verify, do not assume)

| Setting | Repo value | Play requirement (retrieved 2026-08-13) |
|---|---|---|
| Target API | `AndroidTargetSdkVersion: 36` (`ProjectSettings.asset:179`) | "New apps and app updates must target Android 16 (API level 36) or higher to be submitted to Google Play" from August 31, 2026 ([target API requirements](https://support.google.com/googleplay/android-developer/answer/11926878?hl=en)) — satisfied |
| Min SDK | `AndroidMinSdkVersion: 25` (`:178`) | no Play minimum found in the retrieved sources — narrows the tester device pool only |
| Architectures | `AndroidTargetArchitectures: 2` (`:269`) — ARM64 in Unity's architecture flags | 64-bit is required; **ARM64-only means 32-bit-only phones cannot install** — relevant when recruiting 12 testers with old hardware. Confirm the checkbox state in Build Settings rather than trusting this reading of the enum |
| Scripting backend | `scriptingBackend: Android: 1` (`:837-838`) = IL2CPP | required for 64-bit |
| Code stripping | `stripEngineCode: 1` (`:183`) | not a Play rule; it is the cause of the device log spam recorded at `state/PROJECT_STATE.md:111` (missing collider classes; gameplay unaffected). Testers may see console noise only on dev builds |

---

## 5. Version-code discipline **[LOCAL + HUMAN]**

Repo state: `bundleVersion: 0.1.0` (`unity/ProjectSettings/ProjectSettings.asset:147`) and
`AndroidBundleVersionCode: 1` (`:177`).

Rules, verbatim from [Version your app](https://developer.android.com/studio/publish/versioning)
(retrieved 2026-08-13): `versionCode` is "a positive integer", each successive release "must use a
greater value than the previous release", "Google Play allows a maximum versionCode of 2,100,000,000",
and "You cannot upload an APK with a versionCode you have already used".

Working discipline for this project:

1. **Increment `AndroidBundleVersionCode` before every build you intend to upload — including
   re-builds of the same commit.** A rejected upload burns its version code; reusing it is refused
   by Console.
2. Keep `bundleVersion` human-meaningful (`0.1.0` → `0.1.1` …). It is the string players see; it has
   no ordering power ([same source](https://developer.android.com/studio/publish/versioning)).
3. `AndroidBundleVersionCode` lives in a tracked file, so a bump is a commit. That commit is the
   cheapest provenance record you will get today — write the version code into the commit subject.
4. Record every uploaded artifact in the ledger table in `docs/release/release-checklist.md`
   (versionCode ↔ git SHA ↔ AAB SHA-256). Manual, by hand, every time.

---

## 6. Tester roster mechanics **[HUMAN]**

Play Console → Testing → Closed testing → **Manage track → Testers tab**, then choose the mechanic
([Set up an open, closed, or internal test](https://support.google.com/googleplay/android-developer/answer/9845334?hl=en), retrieved 2026-08-13).

| | **Email list** | **Google Group** |
|---|---|---|
| How it is configured | "Create email list" → name it → "Add emails separated by commas or upload a CSV file" | enter group addresses in the form `yourgroupname@googlegroups.com` |
| Who can join | exactly the addresses on the list | "Only users who are members of the Google Groups you enter will be able to join your test" |
| Adding a tester later | edit the list in Console (a Console action every time) | the person joins the group; no Console action |
| Caution | "If you upload a .CSV file, it will overwrite any email addresses you've added" | you must manage group membership; a removed member loses access |
| Scale limits | "a total of 200 lists, and each list can have up to 2,000 users" | not stated in the retrieved source — **UNKNOWN** |

**Recommendation for a solo dev filling 12 seats this week:** email list. The roster is small, you
know every address, and it needs no second product. Use a Google Group instead only if you expect
churn/backfill traffic you do not want to hand-edit. `docs/plan/DAY1_RUNBOOK.md:33-34` already plans
the email-list route with a list named `catmetro-testers`.

Roster sizing: recruit **16–20** to land **12** opted-in. The 12 is a floor that must hold
*continuously* (§1), so every drop-out costs a fresh 14 days for that seat.

---

## 7. The opt-in flow **[HUMAN → testers]**

1. Publish the first closed release (upload the AAB to the closed track → review → published).
2. The opt-in link "only shows when an app is Published" — copy it from the Testers tab
   ([source](https://support.google.com/googleplay/android-developer/answer/9845334?hl=en), retrieved 2026-08-13).
3. Send the link. Paste text: `docs/release/tester-comms-template.md`.
4. **Each tester must open the link, sign in with the Google account that is on your list (or in the
   group), and accept ("Become a tester").** Invited ≠ opted-in; only opted-in counts toward the 12.
   The requirement page is explicit that the 12 must be "opted-in", and the setup page says "Each
   tester needs to opt-in using the link"; for Google Groups, "users need to join the group before
   opting into your test" (both retrieved 2026-08-13).
5. Testers then install from Play on that same Google account. Sideloaded APKs do **not** count.
6. Track it yourself: name → invited → opted-in date → last confirmed still opted-in. Console shows
   the tester count; your sheet shows who is about to break the streak.

**Parallel path worth using today:** the **internal testing** track publishes fast — "When you
publish a new Android App Bundle to the internal test track, it will be available to testers within
minutes", and "An internal test can have up to 100 testers per app"
([source](https://support.google.com/googleplay/android-developer/answer/9845334?hl=en), retrieved 2026-08-13). Use it to smoke-test the exact AAB on your own device while the closed release is in review. **Internal testers do not satisfy §1** — that requirement names a *closed* test.

---

## 8. Feedback loop (cheap, and it is also evidence)

The track configuration asks for a **feedback URL or email address** shown to testers
([source](https://support.google.com/googleplay/android-developer/answer/9845334?hl=en), retrieved 2026-08-13). Set it to an address the human actually reads.

Weekly rhythm that costs ~20 minutes:

- **Day 0** — send the invite + expectations message (`docs/release/tester-comms-template.md`),
  including the three questions you want answered.
- **Day 2 and Day 7** — one short nudge each; ask for one sentence, not a report.
- **Every upload** — post a two-line "what changed / what to look at" note to the same channel and
  put the same text in the track's release notes.
- **Collection** — keep raw tester replies verbatim in one place. Do not paraphrase into the state
  file; `docs/prd/venture-critique.md` V-1 is an evidence request, and paraphrased feedback is not
  evidence. Real quotes only; if there are none yet, the honest record is "none yet".
- **The D7 fun gate** (`docs/prd/PRD.md:69`, `docs/prd/hypothesis.md:31`) is a *product* gate the
  human already defined; this runbook does not restate or change it.

---

## 9. Promotion to production **[HUMAN]**

1. Confirm the roster: at application time, "at least 12 testers must be opted-in to your closed
   test. They must have been opted-in for the last 14 days continuously"
   ([source](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en), retrieved 2026-08-13).
2. Apply on the Play Console **Dashboard**: "When you meet these criteria, you can apply for
   production access on the Dashboard in Play Console"; the application asks "some questions to help
   us understand your app, its testing process, and its production readiness" (same page, retrieved
   2026-08-13). Answer them about the build the testers actually played — the repo already flags this
   as an open question (`docs/prd/PRD.md:1079`, NEW-Q40).
3. Repo-side gates that must be true before a *production* release, independent of Google:
   - `state/mode` flipped to `production` by a **human-authored commit** before any billing/IAP/ads
     code merges (`AGENTS.md` §Risky paths; `state/PROJECT_STATE.md:10`).
   - Perf budget rows filled — `state/PROJECT_STATE.md:94`: "Perf budgets: docs/perf/budgets.md rows
     are TBD (human) — required before /forge-release."
   - Secret scanning wired — `state/PROJECT_STATE.md:92` and `.github/workflows/ci.yml:17`
     (`TODO(secret-scan)`), "required before graduation to production".
   - The full human-only floor and gate map: `docs/release/release-checklist.md`.
4. Production rollout itself stays human, via tag → CI, and `deploy.yml` is empty today
   (`.github/workflows/deploy.yml:14`, `TODO(deploy)`). Nothing ships itself.

---

## 10. Known ways this derails

| Failure | Early signal | Prevention |
|---|---|---|
| Waiting for feature-complete before the first upload | nothing published by Aug 15 | upload today; review time is serial and unbounded in the worst case (§1.2) |
| Debug-signed / development build uploaded | Console rejects the artifact | §4.3 steps 3–4 |
| Version code reused | Console refuses the upload | §5 |
| Testers invited but never opted in | Console tester count < 12 while your sheet says 12 | §7 step 4; chase the "Become a tester" tap specifically |
| A tester opts out mid-window | count dips | over-recruit (16–20); a broken streak restarts *that seat's* 14 days (§1) |
| Declarations describe the roadmap, not the build | policy mismatch risk at review | Q0-6; `docs/prd/PRD.md:715` |
| Store copy claims features not in the build | listing rejection / claim-ledger violation | `docs/store/play-store-listing.md:80-96` (blocked-claims table) — Lane 7 owns it; do not edit |
| Wrong package id on the first upload | permanent | §3.1 |
| The build shim is missing in a fresh clone/worktree | Unity: "executeMethod class 'CatMetroCliBuild' could not be found" | §4.2; it is untracked (`state/PROJECT_STATE.md:113`) |

---

## 11. Cross-references (pointers only — never edited by this lane)

- Listing copy, ASO, creative specs: `docs/store/play-store-listing.md`, `docs/store/aso-keywords.md`,
  `docs/store/creative-shot-list.md` (Lane 7's shipped pack).
- Release/gate/provenance detail + flagged plan discrepancies: `docs/release/release-checklist.md`.
- Tester messages: `docs/release/tester-comms-template.md`.
- Device build/capture mechanics: `docs/runbooks/device-capture.md`,
  `state/handoffs/SESSION-HANDOFF-2026-08-08.md:52-66`.
- Plan-of-record console runbook (read-only, older, partly stale): `docs/plan/DAY1_RUNBOOK.md`.

---

## 12. Sources (all retrieved 2026-08-13)

| # | Claim used here | Source |
|---|---|---|
| S1 | 12 testers / 14 continuous days; personal accounts created after 2023-11-13; production-access application | https://support.google.com/googleplay/android-developer/answer/14151465?hl=en |
| S2 | Closed-track setup, email list vs Google Groups, 200 lists × 2,000 users, opt-in link only when Published, internal test = 100 testers / minutes | https://support.google.com/googleplay/android-developer/answer/9845334?hl=en |
| S3 | Device verification for new personal accounts; non-rooted device, Android 10+ | https://support.google.com/googleplay/android-developer/answer/14316361?hl=en |
| S4 | Play App Signing default; upload key vs app signing key | https://support.google.com/googleplay/android-developer/answer/9842756?hl=en |
| S5 | Data safety form applies to closed tracks; privacy policy link required; internal-only exempt | https://support.google.com/googleplay/android-developer/answer/10787469?hl=en |
| S6 | Pre-review requirements: privacy policy, content rating, ads declaration, target audience, app access | https://support.google.com/googleplay/android-developer/answer/9859455?hl=en |
| S7 | Review "up to seven days or longer in exceptional cases" | https://support.google.com/googleplay/android-developer/answer/9859751?hl=en |
| S8 | Target API 36 from 2026-08-31; extension to 2026-11-01 | https://support.google.com/googleplay/android-developer/answer/11926878?hl=en |
| S9 | Icon 512×512, feature graphic 1024×500, ≥2 screenshots, 320–3840px, ≤2× rule, 9:16 portrait | https://support.google.com/googleplay/android-developer/answer/9866151?hl=en |
| S10 | versionCode: positive integer, must increase, max 2,100,000,000, no reuse | https://developer.android.com/studio/publish/versioning |
| S11 | Debug certificates not accepted for publishing; upload key reset process | https://developer.android.com/studio/publish/app-signing |
| S12 | AAB required for new apps (announced 2020-11, effective Aug 2021) | https://android-developers.googleblog.com/2020/11/new-android-app-bundle-and-target-api.html · https://developer.android.com/guide/app-bundle |
| S13 | Closed testing overview (marketing page) | https://play.google.com/console/about/closed-testing/ |
| S14 | 20 → 12 tester change dated 2024-12-11 (secondary, corroborating only) | https://primetestlab.com/blog/google-play-changed-20-to-12-testers |

## 13. Explicitly not covered / UNKNOWN

- Whether the developer account predates 2023-11-13 (Q0-1) — decides whether §1 binds.
- Current developer registration fee amount — read it in the signup flow.
- Whether `catmetro.com` is registered and serving a privacy policy (Q0-5).
- Whether a Play app record and the `com.catmetro.game` package are already claimed (Q0-4).
- Google Group tester-count limits (not stated in S2).
- Exact day-boundary semantics of the 14-day window.
- Any automated build/upload path: **none exists in this repo** (§4.1, §4.2). There is no
  `fastlane/` directory and no `Fastfile` anywhere in the tree (the name appears only in prose, e.g.
  `AGENTS.md`'s never-run rule), and per `AGENTS.md` an agent must never run a Play upload.
