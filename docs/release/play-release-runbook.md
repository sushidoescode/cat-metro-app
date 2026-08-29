# Google Play release runbook — Cat Metro

**Current date:** Wednesday 2026-08-26, America/Los_Angeles

**Public-live deadline:** Wednesday 2026-09-30, 11:45pm PDT

**Rule:** every Play Console action, keystore action, upload, rollout, licensing decision, and
production monetization authorization is human-only. An agent never uploads or publishes this app.

This is the execution path for making Cat Metro public on Google Play in time for RevenueCat
Shipaton 2026. It is self-contained: follow the account branch below, then the shared release steps.

## 1. Decide the account branch today

Open **Play Console → Settings → Developer account → Account details** and record the account type
and creation date in private notes.

| Account | Required path |
|---|---|
| Existing organization account | **Exempt branch:** go directly to production after shared prerequisites are complete. |
| Existing personal account created **on or before 2023-11-13** | **Exempt branch:** go directly to production after shared prerequisites are complete. |
| Personal account created **after 2023-11-13** | **Gated branch:** 12 testers must remain continuously opted in to a closed test for the preceding 14 days, then Google reviews a production-access application. |
| No account yet | Create a personal account today; it follows the **gated branch**. Complete identity, payment-profile, email, and device verification immediately. Payment-profile verification can take up to five days. |

Google's rule is scoped to personal accounts created **after** November 13, 2023. Do not use
“on/after” as shorthand; an account created exactly on November 13 belongs in the exempt branch.

Official source: [App testing requirements for new personal developer accounts](https://support.google.com/googleplay/android-developer/answer/14151465).

## 2. Calendar: mathematical floor versus plan

The 14 days are 14 continuous days of tester opt-in, not 14 calendar labels. Play Console exposes
the aggregate opted-in count, not the identities behind it. Record when Console first shows at least
12; individual replies/screenshots are private, self-reported planning evidence only. Google's public
page does not define a midnight/time-zone boundary, so Console eligibility is authoritative; wait
until it shows production access as available and allow one extra calendar day in the plan.

| Scenario from 2026-08-26 | Dates |
|---|---|
| **Mathematical floor, gated account** | If the closed release were live and the aggregate first reached 12 opted-in testers today, 14 continuous days end **2026-09-09 at the same local time**. Public launch on 09-09 would require both later reviews to take zero time; it is not a planning date. |
| **Planning case** | Closed release live and roster complete **08-28** → 14 days end **09-11** → boundary buffer/apply **09-12** → production access by **09-19** if review takes seven days → production review 1–3 days → target live **09-20–09-22**. |
| **Seven-day first-review case** | Upload **08-26** → closed release live/roster complete **09-02** → 14 days end **09-16** → buffer/apply **09-17** → access by **09-24** → target live **09-25–09-27**. |
| **Latest date under these planning assumptions** | To allow 14 days + seven-day access review + three-day production review, the aggregate must reach 12 by **09-06** and the first upload must occur by **08-30** if its review takes seven days. With the one-day boundary buffer, use **09-05** and **08-29**. |

Reviews may take longer than seven days in exceptional cases. None of the latest dates is a
guarantee. Every day earlier is insurance.

**Operational gate:** if this is a post-cutoff personal account, get the closed release live and the
Console aggregate to at least 12 by about **2026-09-01**. That is the schedule to run, even though the
zero-review arithmetic has a later floor; slipping past it consumes the review margin before
September 30.

For an already verified exempt account there is no 12/14 delay: the earliest live date is the first
day after TASK 15, RevenueCat, the production AAB, listing/assets, and normal review all clear. If
those prerequisites can finish today, submit today and target roughly **2026-09-01**; the current
repository does not yet prove the RevenueCat gate. For a gated account, the mathematical zero-review
floor is **2026-09-09** only if the aggregate reaches 12 today; the planning range is
**2026-09-20–09-27**. Do not call any date “live” until the production release actually clears review
and is publicly reachable.

## 3. What must happen today

### Shared, both branches

1. Identify the account branch and finish any account/device/payment verification.
2. Decide the exact first-upload binary: **greybox** or generated art. The human owns this commercial
   licensing decision; §9 recommends the greybox for the first upload.
3. Publish a truthful privacy policy URL for the exact build.
4. Produce the minimum real store assets from the running game: 512×512 icon, 1024×500 feature
   graphic, and at least two valid phone screenshots. Four 1080×1920 screenshots are the preferred
   set. See §8.
5. Complete the app-content declarations for the exact build: data safety, content rating, target
   audience, ads, app access, and privacy policy. These are prerequisites for publishing a closed
   release too; they cannot be deferred into the 14-day wait.
6. Create and back up the upload keystore, configure it locally, build a signed validation/closed-test
   AAB, and complete the post-build signing checks in §5. The production AAB is cut fresh only after
   TASK 15 and RevenueCat are complete.
7. Decide whether the exact package ID `com.catmetro.game` is final. The first Play upload binds the
   app record to it.
8. Start TASK 15 and RevenueCat sequencing in §7. Do not merge `feat/level-variety` into this release
   branch. Both are hard production-release dependencies.

### Exempt branch only

9. Wait for TASK 15 to land on `main`; complete and device-prove the RevenueCat purchase; then cut a
   fresh production AAB from the intended release commit.
10. Complete the production listing from that exact AAB-generated listing file.
11. Create the production release, upload the AAB, resolve every Console error, submit for review,
    and roll out when approved.

Closed testing remains useful QA, but recruiting 12 people is not on this branch's critical path.

### Gated branch only

9. Recruit **16–20 real Android testers** now. Keep names and Google-account addresses in private
   human storage, never in this repository.
10. Create the closed track, upload the AAB, resolve every Console error, and submit the closed
    release for review.
11. As soon as the opt-in link appears, send it, collect private tester confirmations/screenshots,
    and watch Console's aggregate until it reaches at least 12. Use
    `docs/release/tester-comms-template.md`.

## 4. Console prerequisites for the first closed or production release

Create the app as **Game**, **Free**, default language en-US. Free is permanent for the app record;
in-app purchases can be added later. Enable Play App Signing, which is the default.

Before Play will publish a closed or production release, complete what Console requests, including:

- Main store listing and required graphics.
- Privacy policy URL.
- Data safety form.
- Content rating questionnaire.
- Target audience and content: select **13+ brackets only**; do not select an under-13 bracket.
- Ads declaration for the exact binary.
- App-access instructions if anything requires sign-in.
- Country/region availability and a feedback email or URL.

Answer for the uploaded binary, not the roadmap. Before RevenueCat lands, a build with no SDK or
network data transport may truthfully have no digital purchases and no collected/shared data. In
the same release that RevenueCat lands, revise the privacy policy and declarations for purchase
history, RevenueCat app-user ID/device metadata, and digital purchases. Inspect the exact AAB and
Play's App Bundle Explorer before asserting permissions or SDK behavior; do not infer them from a
plan.

Minimum asset specifications are sourced from [Add preview assets](https://support.google.com/googleplay/android-developer/answer/9866151). Data-safety requirements for testing tracks are at [Provide information for Google Play's Data safety section](https://support.google.com/googleplay/android-developer/answer/10787469).

## 5. Human-only signing and exact-AAB checks

### Create and protect the upload key

Create the key outside every checkout. Example only; choose the actual location yourself:

```sh
keytool -genkeypair -v \
  -keystore /private/path/catmetro-upload.keystore \
  -alias catmetro-upload \
  -keyalg RSA -keysize 2048 -validity 10000
```

Then configure **Unity → Project Settings → Player → Publishing Settings → Use Existing
Keystore**. Back up the keystore and passwords in two durable, private places. Never put a keystore,
password, tester roster, or secret in this repository, `.env`, a shell visible to an agent, or a
chat message.

The root `.gitignore` covers common key extensions, `keystore.properties`, `local.properties`, AAB,
APK, and `build/`. It does **not** make tracked `ProjectSettings.asset` safe and it does not replace a
secret scanner. Unity may serialize the local key path, alias, or custom-keystore flag there.

### Build and verify

The human runs:

```sh
AAB_OUT="build/CatMetro-1.0.0-1.aab"
bash scripts/build-aab.sh "$AAB_OUT"
```

Use a fresh versioned filename for every candidate and retain that exact `AAB_OUT` value for all
later hash, certificate, device, listing, and upload checks. The wrapper is immutable and refuses to
replace an earlier candidate. Its no-argument fallback is `build/CatMetro-release.aab`, but the
versioned command above is the release procedure. Debug-signing mode is pipeline proof only, must
use a new non-release name, and must never be uploaded:

```sh
CM_ALLOW_DEBUG_SIGNING=1 bash scripts/build-aab.sh build/CatMetro-debug-proof.aab
```

The wrapper refuses to overwrite any existing proof output and marks the generated listing
`NOT UPLOADABLE` too.

Before upload, the human performs all of these checks:

1. Record the exact `git rev-parse HEAD`, `git status --short`, version name/code, AAB SHA-256, and
   generated listing path.
2. Compare the upload certificate fingerprint to the AAB signer:

   ```sh
   keytool -list -v -keystore /private/path/catmetro-upload.keystore -alias catmetro-upload
   keytool -printcert -jarfile "$AAB_OUT"
   ```

   The SHA-256 certificate fingerprints must match. Do not paste the password into a command or
   store it in the repo.
3. Run `git diff -- unity/ProjectSettings/ProjectSettings.asset` and `git status --short` after the
   build. Review every line. Remove only the local signing-path/alias/settings drift you personally
   introduced; do not discard unrelated work. Do not commit local key material or its path.
4. Confirm the wrapper reported successful bundletool validation, built-manifest checks, JAR-signature
   verification, an exact-byte campaign receipt, and a generated sibling `*-play-listing.md` for
   this exact AAB. This still does not replace the certificate-fingerprint comparison in step 2.
5. Install the candidate on the target Pixel 9 Pro and play it. Before any `adb` command, run
   `adb devices -l` and confirm `model:` for serial `48121FDAP006X4`. Never install on the Quest or
   Pico devices listed in `AGENTS.md`.

Increase `AndroidBundleVersionCode` for every upload, including rejected attempts, and never exceed
Google Play's `2100000000` maximum. The public version should use `bundleVersion: 1.0.0` unless the
human deliberately chooses otherwise. Starting 2026-08-31, new apps and updates must target API 36
or higher; the wrapper enforces that requirement before Unity starts. Sources: [target API level
requirement](https://developer.android.com/google/play/requirements/target-sdk) and [Android app
versioning](https://developer.android.com/studio/publish/versioning).

## 6. Closed-test procedure — gated branch only

1. Play Console → **Testing → Closed testing** → create/manage a track.
2. Create an email list and add 16–20 Google accounts used on testers' Android phones.
3. Create a release, upload the signed AAB, add truthful release notes, resolve Console errors, and
   start rollout to closed testing.
4. After review, copy the web opt-in URL. Invited is not opted in: each person must open it under the
   listed Google account and tap **Become a tester**.
5. Record privately: name → invited account → self-reported opt-in time/screenshot → install
   confirmed → last continuity check → feedback received. This roster helps coordination; it does
   not replace Console's aggregate count or eligibility decision.
6. Keep at least 12 people continuously opted in. Recruit above the floor so one departure does not
   destroy the schedule. Do not promise that uninstalling is harmless; the published requirement
   speaks only about opt-in status.
7. Upload improved builds to the same track as needed, but treat the proposition that an update never
   affects the clock as an inference from the opt-in wording, not a Google guarantee. Never delete or
   recreate the track/list during the window.
8. Collect real engagement and feedback. Record what changed because of it; the production-access
   application asks about tester engagement, feedback collection, and resulting changes.
9. Keep the safety cohort opted in until production access is granted. Do not send a wrap-up merely
   because 14 calendar dates have elapsed.
10. When Console shows eligibility—and no earlier than 14 continuous 24-hour periods after its
    aggregate first showed at least 12 opted-in testers—apply for production access with truthful
    answers. Allow the extra planning day.

Google says production-access review usually takes seven days or less but may take longer. Access
approval is not publication. After access is granted, a separate production release and app review
still follow.

## 7. TASK 15 and RevenueCat sequencing

Two parallel changes must be in the public candidate without delaying the first closed upload.

### TASK 15: levels and exact listing count

- Do **not** merge `feat/level-variety` into `feat/store-release` just to build the first closed AAB.
- TASK 15 follows its own path to `main`. Do not cut or promote a production candidate until TASK 15
  has landed there. Cut from the intended release commit afterward; never merge the feature branch
  directly into this worktree as a shortcut.
- Never type 17 or 19 into store copy by hand. `scripts/build-aab.sh` derives the reachable campaign
  count from the exact AAB, verifies the named level JSON files in that bundle, and renders the
  sibling `*-play-listing.md`.

### RevenueCat: Shipaton eligibility gate

Shipaton requires a working public app in which the RevenueCat SDK powers at least one purchase or
RevenueCat Ads. Cat Metro currently does not satisfy that gate. Use the lower-risk path:
a deterministic Play Billing purchase behind RevenueCat, not paid randomness and not Stripe.

Required sequence:

1. The human explicitly authorizes production monetization configuration **before** live billing,
   product catalogs, or ad credentials are enabled. Do not infer that authorization from code.
2. Integrate RevenueCat and configure the Play product/entitlement. A taxonomy string is not an
   integration.
3. Put the RevenueCat build on the closed track early enough for real testers to exercise it.
4. Verify on a physical Android device; RevenueCat Purchases is unsupported in the Unity Editor.
   Complete a licensed/sandbox purchase, entitlement unlock, app restart, and restore-purchases
   flow. Confirm the event/entitlement appears in RevenueCat with the expected product identifier.
5. Update privacy policy, data safety, content rating/digital-purchase answer, and listing claims in
   the same release.
6. Build the final public AAB and re-run the exact-AAB signing, listing, device, and purchase checks.
7. Because the planned named one-time cosmetic has no free trial, create Play one-time-use promo
   codes for that active product. Redeem one on the Play-delivered build and prove RevenueCat grants
   and restores the entitlement; keep a separate unused code out of the repository for the judges.
   The Shipaton submission must provide either a free trial or a working promo code that unlocks all
   premium features.
8. Before Shipaton submission, verify the logged-out public Play URL, install the Play-delivered
   build, exercise the qualifying purchase path, and provide the unused judge code in Devpost.

Sources: [Shipaton 2026 official rules](https://revenuecat-shipaton-2026.devpost.com/rules) and
[Google Play promotions](https://support.google.com/googleplay/android-developer/answer/6321495).

## 8. Listing and screenshots must describe the exact public AAB

Paste listing copy only from the `*-play-listing.md` generated beside the exact AAB. Check its AAB
SHA-256 and campaign receipt before copying. Re-read every feature claim after RevenueCat or ads
land; “no forced ads” permits player-initiated rewarded ads, but it does not permit interstitials.

Use only captures of the real running candidate. Acceptable source: a screen capture from the exact
candidate on the Pixel 9 Pro, cropped/resized without inventing UI or gameplay. Not acceptable:

- AI concept art presented as gameplay.
- `docs/reference/` or a golden target frame.
- turntable, isolated-model, editor, or asset-QA renders.
- captures from a rejected before-state or another branch.
- a screenshot showing art, levels, purchases, or UI absent from the public AAB.

If the production candidate changes visibly, retake the screenshots. Lead with route layouts,
junctions, next-wave preview, and color-plus-symbol stations; do not market the app as child-directed.

## 9. Human asset-licensing decision for the exact binary

Tracked source alone does not determine the binary. Gitignored generated assets under any Unity
`Assets/**/Resources/` directory can be embedded by a build made on the machine that holds them.
The main checkout currently has generated prop resources; a clean `git status` does not reveal them.

Before each upload, the human chooses and records privately one of these:

- **Greybox (recommended for the first upload):** temporarily quarantine the entire generated
  `Resources` tree outside `Assets`, without deleting `curation-backups/`; build; verify the AAB does
  not contain the generated resources; then restore the local tree.
- **Generated art:** deliberately authorize commercial distribution of every asset actually embedded
  in that AAB after reviewing its paid-tier provenance and provider terms.

No agent makes this decision, moves the only backups, signs a licensing record, or describes the
decision as already approved. Existing ADRs may be useful evidence, but signing or reviving old
governance documents is not a prerequisite invented by this runbook.

## 10. Production access, production review, and public verification

### Exempt branch

Once the shared requirements in §§4, 5, 7, 8, and 9 are satisfied, create the production release,
upload the final AAB, resolve Console errors, and submit it for review.

### Gated branch

After the 14-day criterion is met, production access must be approved first. Then create a separate
production release with the final AAB and submit it for app review. Do not call access approval
“live.”

### Both branches

When review clears, the human rolls out the release. Before declaring success:

1. Open the Play listing logged out/in an incognito browser and confirm it is publicly reachable in
   the intended country.
2. Confirm package ID, version code, listing count, screenshots, data safety, content rating, and
   privacy policy match the public AAB.
3. Install from Play on the Pixel and complete a gameplay loop plus the RevenueCat purchase/restore
   check.
4. Save the public URL and release timestamp for the Shipaton submission.

## 11. Human-only boundary

An agent must never:

- Create, read, move, back up, or configure the keystore/password.
- Upload an AAB, create or edit a Play release, apply for production access, or roll out production.
- Spend the account fee, change account/payment identity, or manage tester personal data.
- Authorize production monetization, make the asset-licensing decision, or claim either decision was approved.
- Read `.env`.

The agent may prepare code, tests, generated listing tooling, and documentation. The human performs
every external release act and verifies the result in Play Console.
