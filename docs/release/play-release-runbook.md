# Google Play release runbook — Cat Metro

**Written 2026-08-25 (Tuesday) by the STORE-RELEASE lane.** Purpose: make the human's part of
shipping as small and as fast as possible, against the RevenueCat Shipaton deadline of
**Wednesday 2026-09-30, 11:45pm PDT** — 36 days away.

Sources this runbook depends on, both fully cited: `docs/research/shipton-hackathon.md` (the
event and its two eligibility gates) and `docs/research/store-compliance-2026.md` (store policy).
Where I verified something myself against a Google page on 2026-08-25, I say so inline.

> **The hackathon gate this serves:** the app must be **publicly live on a store** before the
> deadline. A closed test is not public. An open test is not reachable either — see §2. For a
> post-2023 personal account, "publicly live on Play" means *production*, and there is exactly
> one road to it.

---

## 1. The answer, in one page

### 1.1 Earliest possible live date

**It depends entirely on one fact nobody has written down yet: the Play developer account's
type and creation date.** Everything else is arithmetic.

| Account type | Closed test required? | Earliest live | Expected live | Slack vs 09-30 |
|---|---|---|---|---|
| **Organization**, or **personal created before 2023-11-13** | **No** | **2026-08-27** | **2026-09-01** | ~4 weeks. Comfortable. |
| **Personal created after 2023-11-13** | **Yes — 12 testers × 14 continuous days** | **2026-09-17** | **2026-09-22 – 09-24** | 6–13 days. Tight but real. |
| Personal post-2023, and the first build slips past **2026-08-31** | Yes | — | **misses 09-30** | None. |

**Assumptions behind the post-2023 personal numbers** (each one is a place the estimate can move):

1. A signed, uploadable AAB exists and goes to a closed track **today or tomorrow**.
2. The first review of a brand-new app clears in ~2 days (Google says "usually seven days or
   less"; 7 days is the worst case I have budgeted, and it is what produces the 09-24 figure).
3. **12 real testers are ready to opt in on the day the track goes live.** This is the step most
   likely to slip, and it is pure logistics — no amount of engineering compresses it.
4. The production-access application review takes ≤7 days. Google publishes no SLA for this
   beyond the same "seven days or less" line.
5. Promoting to production triggers one more review; budgeted at 1–3 days because the binary has
   already been reviewed once.

Arithmetic for the expected case: upload 08-25 → track live 08-27 → testers opted in 08-27 →
14 continuous days end **09-10** → apply for production access → granted by **09-17** → production
release reviewed and live **09-19 – 09-24**.

### 1.2 The drop-dead dates

Working backwards from 2026-09-30 for a post-2023 personal account:

| Must happen by | What |
|---|---|
| **2026-08-31** | First build uploaded to the closed track (it needs up to 7 days of review before testers can opt in). |
| **2026-09-07** | 12 testers **opted in**. This is the day the 14-day clock must start. |
| **2026-09-21** | Apply for production access. |
| **2026-09-30** | Live. |

Every day earlier than these is a day of insurance. **Aim for 12 testers opted in by 2026-08-29.**

### 1.3 What must happen today

Ordered. The first item gates everything and takes five minutes.

1. **Answer: what type is the Play developer account and when was it created?** Play Console →
   Settings → Developer account → Account details. If it does not exist yet, that is also an
   answer — create it now ($25, personal, email verification only).
2. **Create the upload keystore** (§3.3) — ~10 minutes, and nothing can be uploaded without it.
3. **Run `bash scripts/build-aab.sh`** and get a signed AAB (~25–45 min, mostly unattended).
4. **Start recruiting 12 testers** in parallel with the build. This has the longest human tail.
5. **Upload to a closed track** and complete the store listing, content rating and data safety
   forms (§5, §6, §7 have every answer pre-written).

> **An agent must never run the upload.** Steps 1, 3 and 5 are human-only in this repo.

### 1.4 The honest risk statement

If the account is a post-2023 personal account **and** no closed test is running, this is
recoverable but has no slack. If the first upload slips past **2026-08-31**, Play cannot deliver
the hackathon's Gate 1 and the answer is not to work harder on Play — it is to open a second
front. Note two escape hatches that are **not** available:

- **Switching to an organization account does not help.** It exempts you from the 12/14 gate, but
  requires a D-U-N-S number and Google warns "This process can take up to 30 days" (verified
  2026-08-25). That is longer than the runway.
- **An open test is not a shortcut.** I checked this specifically because it would have been the
  single biggest schedule win available. Google's testing-requirements page states verbatim:
  **"Open testing becomes available after you gain production access."** The open track sits
  *behind* the same gate, not beside it.

The real alternative fronts are the Apple App Store (~10–14 days, no minimum-tester rule) and the
Samsung Galaxy Store — both are accepted stores for the hackathon. `store-compliance-2026.md` §3.2
and `shipton-hackathon.md` §8.3 cost those out. That is a human decision, not a build task.

---

## 2. Branch point: which account do we have?

Read this once and then follow only your branch.

### Branch A — organization account, or personal created **before 2023-11-13**

The 12/14 gate does not apply. You may go straight to a production release.

1. Complete §5 (listing), §6 (content rating), §7 (data safety), §4 (privacy policy URL).
2. Build the AAB (§3).
3. Play Console → Production → Create new release → upload the AAB → roll out.
4. Wait for review (up to 7 days for a first submission; often much less).

Skip §8 entirely. You still want testers, but they are no longer on the critical path.

### Branch B — personal account created **after 2023-11-13**

The 12/14 gate applies and it *is* the schedule. Go to §8 and start the clock today. Do §5–§7 in
parallel while the 14 days run — none of that work blocks the closed test, and the closed test
blocks everything else.

**A build uploaded to a closed track does not have to be finished.** See §8.4 for exactly what can
be swapped later without restarting the clock. This is the single most important paragraph in the
document for Branch B.

---

## 3. The build

### 3.1 What changed in this branch

Play accepts **only `.aab`** for new apps. The repo had a correct AAB *entry point*
(`unity/Assets/Editor/CatMetroCliAabBuild.cs`) with **no shell path to it** — the only runnable
script, `scripts/build-apk.sh`, is deliberately APK-only. This branch adds the missing path and
closes the flag trap:

| File | Change |
|---|---|
| `scripts/build-aab.sh` | **New.** The release AAB path. Refuses a non-`.aab` output, unsets `CM_DEV_BUILD`, never passes `-quit`, and after the build **verifies the artifact is really a bundle** by looking for `BundleConfig.pb` inside it. Prints upload instructions; never uploads. |
| `unity/Assets/Editor/CatMetroCliBuild.cs` | Now sets `EditorUserBuildSettings.buildAppBundle = false` **explicitly** in a `try`/`finally`, and refuses an output path that is not `.apk`. |
| `.gitignore` | Now ignores `build/`, `*.aab`, `*.apk`, `keystore.properties`, `local.properties`. |
| `tests/unity/cli-apk-build.test.sh` | **New.** Static gate gets the flag discipline, the wrapper's shape, and the gitignore coverage. |

**Why the flag matters.** `EditorUserBuildSettings.buildAppBundle` persists in `unity/Library`
across sessions. Once the AAB builder (or a human clicking "Build App Bundle") has set it true,
the APK path silently emitted a *bundle named `.apk`* — `.claude/rules/unity.md` already records
this as a lane that got bitten. Neither entry point now inherits the flag; each declares its own
artifact kind and restores the human's editor state afterwards.

### 3.2 Running it

```
bash scripts/build-aab.sh                       # -> build/CatMetro-release.aab
bash scripts/build-aab.sh /path/to/CatMetro.aab # explicit output
```

**The human runs this.** Unity needs the network on a cold Library and writes to `~/Library/Unity`,
both outside the agent sandbox. 25–45 minutes for a cold IL2CPP/ARM64 build.

Without a configured keystore the builder **refuses to produce a bundle** — a debug-signed `.aab`
is not uploadable to Play and must never exit 0 by default. To prove the pipeline before a
keystore exists:

```
CM_ALLOW_DEBUG_SIGNING=1 bash scripts/build-aab.sh
```

That artifact is a pipeline proof only. The script marks it as such and tells you it cannot be
uploaded.

### 3.3 The keystore — human-only, never in this repo

**No agent will create, read or commit a keystore, and none of this is automatable here.** The
upload key is the one secret whose loss is genuinely expensive.

Create it once (~10 minutes):

```
keytool -genkeypair -v \
  -keystore ~/keystores/catmetro-upload.keystore \
  -alias catmetro-upload \
  -keyalg RSA -keysize 2048 -validity 10000
```

Then in Unity: **Project Settings → Player → Publishing Settings** → *Use Existing Keystore*,
browse to the file, enter the keystore and alias passwords. Unity stores this in your local,
uncommitted editor state.

Rules, in order of how much they cost to get wrong:

1. **Store it outside the repo.** `~/keystores/` is a good home. The path above deliberately is not
   inside the working tree.
2. **Back it up somewhere you will still have in two years** — a password manager entry with the
   file attached, plus one offline copy. Losing the upload key is recoverable through Play's
   upload-key reset, but it is a support round-trip you do not want in September.
3. **Never commit it.** `.gitignore` covers `*.keystore`, `*.jks`, `*.p12`, `*.pem`, `*.pfx`,
   `*.bks`, `*.key`, and — new in this branch — `keystore.properties`, which is the file that
   conventionally carries the *password* rather than the key and was not covered before.
4. **Turn on Play App Signing** when you create the app (it is the default). Google then holds the
   real signing key and your upload key is replaceable if lost.

### 3.4 Release settings to bump before the store build

In `unity/ProjectSettings/ProjectSettings.asset`, currently:

- `bundleVersion: 0.1.0` → set to **`1.0.0`** for a public launch.
- `AndroidBundleVersionCode: 1` → must **increase on every upload**. Play rejects a re-used code.
  Bump it for each closed-test iteration too; this is the single most common trivial upload failure.

---

## 4. What would fail review today

I checked the shipping configuration against Play's current requirements. **The build
configuration is in good shape — the gaps are all paperwork, not code.**

### Passing already

| Requirement | Setting | Status |
|---|---|---|
| **Target API level** | `AndroidTargetSdkVersion: 36` | **Correct, and only just.** Verified against Google 2026-08-25: from **2026-08-31** (six days away) "New apps and app updates must target Android 16 (API level 36) or higher." We are exactly at the floor. Do not lower it. |
| **64-bit** | `AndroidTargetArchitectures: 2` (ARM64 only) | Compliant. |
| **IL2CPP** | `scriptingBackend.Android: 1` | Correct — required for ARM64. |
| Minimum API | `AndroidMinSdkVersion: 25` (Android 7.1) | Fine; no Play floor conflicts. |
| Application ID | `com.catmetro.game`, `overrideDefaultApplicationIdentifier: 1` | Real, not a `com.DefaultCompany.*` placeholder. **Permanent once published — cannot ever be changed.** |
| Permissions | No `AndroidManifest.xml` in `Assets/`; `ForceInternetPermission: 0` (Auto) | Nothing over-requested. Unity will likely add `INTERNET` because `StreamingAssetsContentSource` uses `UnityWebRequest` to read StreamingAssets on Android — that is a local `jar:file://` read, not networking. `INTERNET` needs no declaration and is unremarkable to a reviewer. |
| Debug code | All of `Bootstrap/DevCapture/*` is `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR` | Compiled out of a release AAB. |
| `android:debuggable` | Release build (no `BuildOptions.Development`) | False, as required. |

### Blocking — must be done before a production release

1. **A privacy policy URL.** Required in Play Console → App content for every app, regardless of
   whether you collect data. There is no URL in the repo. This needs a real, publicly reachable
   page. It is the cheapest blocker on the list and the one most likely to be forgotten — a GitHub
   Pages or Notion page is acceptable.
2. **Content rating questionnaire** (IARC) — answers pre-written in §6.
3. **Data safety form** — answers pre-written in §7.
4. **Target audience declaration** — see §6.3. Get this one right; it is the families-policy trap.
5. **Store listing assets** — the graphics do not exist yet. See §5.4.
6. **The licensing decision** on shipping paid Meshy/Tripo/Polyfork assets in a distributed
   binary. `AGENTS.md` reserves this for the human explicitly. See §9.

### Not a review failure, but wrong today

7. **`bundleVersion` is `0.1.0`.** Ship `1.0.0`.
8. **The listing's level count is stale.** See §5.1 — this is a factual claim in store copy and it
   is wrong in both directions depending which document you read.

---

## 5. The listing pack

Assembled from the existing drafts rather than written fresh: the copy below is
`docs/store/play-store-listing.md` with the level count corrected and a what's-new field added.
The positioning, the ASO priorities and the fairness line are all inherited unchanged — they were
well-judged and `docs/plan/marketing/claim-ledger.md` already backs each claim.

> **Ownership note.** `docs/store/play-store-listing.md` is Lane 7's file and
> `docs/runbooks/play-closed-test.md:32` says not to edit it from another lane. I have therefore
> **not** modified it. The corrected copy lives here, and §5.1 gives the exact lines that must
> change in the source file when Lane 7 next touches it.

### 5.1 The stale claim — corrected

**The listing says "Five handcrafted campaign levels." That is wrong, and so is the "19" I was
briefed with. The correct number is 17.**

| Ref | Levels | Status |
|---|---|---|
| `feat/store-release` / `integration/look-stack` / `main` | **17** (`content/levels/L001…L017.json`, all staged into `StreamingAssets`, all wired into normal progression by `GameRoot.LevelBand`) | **This is what a build today contains.** |
| `feat/level-variety` | 19 | **Unmerged.** Not on any shipping ref. |
| The listing copy | 5 | Stale — accurate when frozen on 2026-08-10, when only L001–L005 were reachable. |

**Use 17.** Use 19 only if `feat/level-variety` merges before the release build, and re-count on the
actual release candidate either way — the claim ledger's own instruction is "Recount the normal
player path on the exact release candidate," which is the right discipline.

Lines to change in `docs/store/play-store-listing.md` when Lane 7 next edits it:

| file:line | current | should be |
|---|---|---|
| `:52` | `FIVE HANDCRAFTED LEVELS` | `SEVENTEEN HANDCRAFTED LEVELS` |
| `:53` | "Play five campaign puzzles." | "Play seventeen campaign puzzles." |
| `:63` | "One thumb. Small railway. Five solvable puzzles." | "…Seventeen solvable puzzles." |
| `:70` (stated char count) | 1,132 | 1,148 |

And the governing claim rows that must move with it, or the copy edit is unbacked:
`docs/plan/marketing/claim-ledger.md:29` (C-11, "keep the listing count at five until player
reachability changes" — reachability *has* changed), `:32` (C-14, "Normal player progression
currently exposes L001–L005"), and `docs/store/play-store-listing.md:74`.

Two lanes had already caught this and correctly declined to fix it out of lane:
`docs/release/release-checklist.md:232` (X-2) and `docs/runbooks/play-closed-test.md:32` (Q0-9).

### 5.2 Paste-ready copy

**Title** — 23 / 30 chars:

```
Cat Metro: Train Puzzle
```

**Short description** — 79 / 80 chars. *One character of headroom; do not edit casually.*

```
Route cat commuters with one thumb. A tabletop train puzzle with no forced ads.
```

**Full description** — 1,148 / 4,000 chars:

```
Cat Metro is a one-thumb train puzzle about routing cat commuters through a tiny tabletop metro. Tap a junction, throw the switch, and guide each cat to the matching color-and-symbol station.

Fair by design: no forced ads, no energy, no loot boxes, every level solvable free.

HOW IT PLAYS
- Tap junctions to change each route
- Read the next-wave preview and plan ahead
- Follow color-and-symbol station signs
- Match every cat to the right station

SEVENTEEN HANDCRAFTED LEVELS
Play seventeen campaign puzzles. Every level passes the project's content validation and solver gates. Each cat puzzle grows from clear first routes into tighter switching challenges.

BUILT TO BE READ
Stations pair color with a symbol. The next-wave preview shows what is coming before the next routing decision.

A TABLETOP METRO PUZZLE
Cat Metro pairs a focused route puzzle with a small tabletop-railway premise. Switch the line, watch the next wave, follow each route, and help every cat reach the right station.

No energy timer limits play. Read the next wave, throw the switch, and guide every cat home.

One thumb. Small railway. Seventeen solvable puzzles.
```

**What's new** — 263 / 500 chars. *No draft existed anywhere in the repo; this is new.*

```
First release of Cat Metro.

Seventeen handcrafted campaign levels, a next-wave preview so you can plan ahead, and stations that pair color with a symbol so every route stays readable.

No forced ads. No energy timer. No loot boxes. Every level is solvable free.
```

For closed-test builds, use `docs/release/tester-comms-template.md` template D instead — it is
already written for exactly that purpose.

### 5.3 Claims discipline

Every sentence above maps to a `VERIFIED` row in `docs/plan/marketing/claim-ledger.md`. The
fairness line — "no forced ads, no energy, no loot boxes, every level solvable free" — is approved
verbatim (C-09, C-10, C-11) and is **true of the build today because there is no monetization code
at all**.

> **This is the one claim that will become a lie by accident.** The moment rewarded ads or IAP
> land, re-read that sentence. Rewarded, player-initiated ads keep it true — "no *forced* ads" is
> deliberately narrower than "no ads", and `docs/store/aso-keywords.md:37-47` already bans the
> absolute phrasing for this reason. Interstitials, an energy timer or gacha would each falsify it,
> and shipping a listing that contradicts the app is a policy problem, not just a brand one
> (`store-compliance-2026.md` R3).

Do not add: level counts above 17, district names, Daily Line, streaks, level select, shop/IAP,
premium themes, leaderboards, share cards, or any install/rating/revenue figure. All are `BLOCKED`
in the claim ledger because the code does not exist.

### 5.4 Image assets — none of these exist yet

**No store image file exists anywhere in the repo.** No icon, no screenshot, no feature graphic.
`docs/store/creative-shot-list.md` is a production brief whose own status line says "No raster
asset is approved by this document," and every quality gate in it is `BLOCKED`. This is the largest
remaining unit of work in the listing and it needs a human or a design pass.

**Play's requirements, verified against Google 2026-08-25:**

| Asset | Spec | Required? |
|---|---|---|
| **App icon** | **512 × 512 px**, 32-bit PNG **with alpha**, max 1024 KB | Yes |
| **Feature graphic** | **1024 × 500 px**, JPEG or 24-bit PNG, **no alpha** | Yes |
| **Phone screenshots** | **Minimum 2** to publish. Min dimension 320 px, max 3840 px, JPEG or 24-bit PNG no alpha | Yes (2 minimum) |
| Screenshots for promotion eligibility | **At least 4**, min 1080 px, 16:9 or 9:16 | Recommended |

#### The spec conflict — and it would have failed the upload

`docs/store/creative-shot-list.md` specifies six screenshots at **1179 × 2556**. Play's rule,
verbatim:

> "The maximum dimension of your screenshot can't be more than twice as long as the minimum
> dimension."

2556 ÷ 1179 = **2.168**. **That exceeds 2.0, so Play will reject those files.** The repo flagged
this as unverified (`docs/release/release-checklist.md:231`, X-1); I verified it, and the conflict
is real.

The explanation is that **1179 × 2556 is the *Devpost* screenshot spec** required by the Shipaton
submission (`shipton-hackathon.md` §6), not a Play spec. The older
`docs/plan/specs/growth_aso_plan.md` §5 called for **1080 × 1920**, which is ratio 1.778 — inside
Play's limit and above the 1080 px promotion threshold. That spec was right for Play.

**Therefore produce two sets from the same captures:**

| For | Icon | Screenshots |
|---|---|---|
| **Google Play** | 512 × 512 PNG **with alpha** | **1080 × 1920**, at least 4 (2 is the minimum to publish) |
| **Devpost / Shipaton** | **1024 × 1024** | **1179 × 2556**, frameless, no device frames |

Plus **1024 × 500** feature graphic for Play only. Master everything at high resolution once and
downscale; do not shoot twice.

**On the composition** — this is where the families-policy risk actually lives (§6.3). Google's
named trigger is "youthful animation or young characters in the **graphic assets**." So the
screenshots must lead with the *puzzle*: routes, junctions, the next-wave preview, station signs.
The shot list already does this well — S1 "Tap the switch. Send every cat home.", S2 "Color plus
symbol. Match every route.", S3 "Read the next wave. Time one tap." Keep that. The feature graphic
is the single highest-risk asset; keep it wordmark-and-track, not a cat's face filling the frame.

**Not eligible as source images:** the existing `evals/results/ux/**` captures (the shot list
explicitly calls them "a rejected before-state"), the `.catshots/` turntable renders (asset QA, not
gameplay), and the golden target frame on `art/diorama-pass` (a style reference that must never be
presented as a frame from the app).

### 5.5 Privacy policy — blocking, and the existing draft is dangerous

Play requires a privacy policy URL for every app. The planned URL is `https://catmetro.com/privacy`.
**Whether `catmetro.com` is even registered is recorded as UNKNOWN** (`docs/runbooks/play-closed-test.md:474`).

> **Do not publish `docs/plan/web/privacy/index.html` as-is.** It names Google Play, Crashlytics,
> AdMob, **RevenueCat** and **OneSignal** as data recipients. **None of those SDKs is in the build.**
> Publishing it would over-declare collection and directly contradict the truthful data-safety
> answer in §7 — the exact inconsistency reviewers look for. `docs/release/release-checklist.md:221`
> (F-2) already flags this.

Write a short policy describing **today's** behaviour: no data collected, no data transmitted, save
data stored locally on the device. Then revise it in the same release that adds any SDK. Any
publicly reachable URL is acceptable — GitHub Pages is fine and free, and does not require the
domain to exist.

---

## 6. Content rating (IARC) — answers

Play Console → App content → Content rating. The questionnaire is issued by IARC and produces
ratings for every territory at once. **Answer it honestly.** `store-compliance-2026.md` §5.5 makes
the point sharply: inflating an answer ("contains violence" on a cat game) is its own
misrepresentation risk, and mis-rating is one of the things Apple's guidelines warn "could trigger
an inquiry from government regulators."

### 6.1 Category

**Game** → sub-category **Puzzle**.

### 6.2 The questionnaire, as it applies today

Every content question is **No** for the build as it stands. Cat Metro is a route-planning puzzle:
there is no combat, no characters in peril, no depicted substance, no wagering.

| Question area | Answer | Note |
|---|---|---|
| Violence (realistic, fantasy, cartoon) | **No** | Trains and cats. Nothing is destroyed or harmed; a failed level is a halted train. |
| Blood / gore | **No** | |
| Sexuality, nudity | **No** | |
| Profanity, crude humour | **No** | |
| Controlled substances (drugs, alcohol, tobacco) | **No** | |
| Horror / fear | **No** | |
| Gambling — real money | **No** | |
| **Simulated gambling / randomised purchases (loot boxes)** | **No — today** | **This is the answer that flips if gacha ships.** See §6.4. |
| Users can interact / user-generated content | **No** | No accounts, no chat, no multiplayer, no leaderboards in the build today. |
| Shares user location | **No** | No location code exists (verified by grep, 2026-08-25). |
| Shares personal information | **No** | See §7. |
| **Digital purchases** | **No — today** | Flips to **Yes** the moment RevenueCat IAP lands. See §6.4. |
| Unrestricted internet access | **No** | No browser, no arbitrary URL loading. |

**Expected outcome:** ESRB **Everyone**, PEGI **3**, USK **0**, IARC **3+**, and equivalents.

An "Everyone / PEGI 3" rating is **not** a families designation and does not by itself pull us into
the Families programme. Content rating and target audience are separate axes in Play Console
(`store-compliance-2026.md` §5.1). Do not distort the rating in an attempt to steer the audience
declaration — use §6.3 for that.

### 6.3 Target audience — the trap, and how to stay out of it

Play Console → App content → **Target audience and content**.

**Select 13+ age brackets only. Do not tick any under-13 bracket.**

Ticking an under-13 bracket triggers the full Google Play Families policy: certified-ads-SDKs only,
no interest-based advertising, and a ban on transmitting AAID, IMEI, IMSI, MAC, SSID, BSSID, SIM
serial and build serial. `store-compliance-2026.md` §5.3 costs this out — it removes the entire
attribution and ad-monetization toolkit, and the economics do not work for a solo developer.

The declaration is **rebuttable**, which is the part people miss. Google, verbatim:

> "Regardless of what you identify in the Google Play Console, if you choose to include imagery and
> terminology in your app that could be considered targeting children, this may impact Google Play's
> assessment of your declared target audience."

and, naming the exact trigger and remedy:

> "If your app is not primarily designed for children under 13 but your listing contains marketing
> elements that suggest otherwise (such as **youthful animation or young characters in the graphic
> assets**), Google Play may reject your app."

The named evidence is **store-listing graphics and marketing copy**, not art direction. A cute cat
aesthetic does not decide this — Neko Atsume 2 ships "Everyone" with ads, IAP and a subscription.
What decides it is the listing. So:

1. **Screenshots lead with the puzzle** — route complexity, track layouts, the wave preview — not
   with cat faces. The feature graphic is the highest-risk single asset.
2. **Zero child-coded vocabulary** anywhere in title, short description, full description or
   keywords: no "kids", "children", "toddler", "preschool", "learning", "educational", "my first".
3. **Adult-coded framing**: "for your commute", "unwind after work", "logistics puzzle".
4. **No cartoon children in promo art.** Cats are fine; a child holding a cat is not.
5. **Do not apply for Teacher Approved.**

The existing positioning already does this well and should be protected — `docs/store/aso-keywords.md`
deliberately makes `train puzzle` the P0 query and treats `cat game` as visual discovery only. That
was the right instinct.

**Also answer:** "Do you have ads in your app?" → **No, today.** Flips with the ads work.

### 6.4 What re-opens this form

The content rating is a declaration about the build, so it must be **re-submitted when the build's
answers change**. Two pending changes will change them:

| Change | New answer |
|---|---|
| RevenueCat IAP ships (Shipaton Gate 2) | Digital purchases → **Yes** |
| Rewarded ads ship | "Ads in your app" → **Yes**; ad content declarations apply |
| Gacha / randomised cosmetics ship | Simulated gambling → **Yes**. This puts a floor of **9+** on the Apple rating and pulls in odds-disclosure obligations *prior to purchase*. `store-compliance-2026.md` §7 recommends not shipping it in the launch build; the FTC's $20M Cognosphere order is the precedent. |
| Leaderboards ship (ADR-0010) | "Users can interact" → likely **Yes** |

Updating a content rating does **not** restart the closed-test clock.

---

## 7. Data safety — answers

Play Console → App content → **Data safety**. This is a **legal declaration**, not marketing copy.
Google acts on it: a declaration that does not match observed app behaviour is grounds for removal.

### 7.1 The answer for the build as it stands today

I audited the shipping code on 2026-08-25 (`grep` across `unity/Assets/Scripts/`):

- **No analytics sink.** `IAnalytics` is an interface; the only implementation is
  `Application/Analytics/AnalyticsQueue.cs`, an in-memory queue. Per ADR-0003 the SDK types would
  live in `Integrations.*`, and that tree does not exist. Nothing is transmitted.
- **No network egress.** The only `UnityWebRequest` in the entire codebase is
  `Bootstrap/StreamingAssetsContentSource.cs`, reading StreamingAssets through a `jar:file://` URL
  — a local file read, not a network call. No `System.Net`, no `HttpClient`.
- **No identifiers.** No `deviceUniqueIdentifier`, no advertising ID request.
- **No sensors or permissions of interest.** No location, microphone, camera or contacts code.
- **Save data is local only.** `Bootstrap/EngineStorageRoot.cs` writes to the app's own storage.
  Data that never leaves the device is **not** "collected" for the purposes of this form.

**Therefore:**

| Field | Answer |
|---|---|
| Does your app collect or share any of the required user data types? | **No** |
| Data types collected | *(none)* |
| Data types shared | *(none)* |
| Is all data encrypted in transit? | N/A — no data is transmitted |
| Do you provide a way for users to request data deletion? | N/A — no data is collected |
| Has your data collection been independently validated? | **No** (this is optional and we have not) |

That is the simplest possible declaration and it is **true today**. It is worth appreciating how
rare and how temporary that is.

### 7.2 The dependency — this answer expires

**Every monetization item on the roadmap changes this form.** Do not ship a build with any of the
following while the "No data collected" declaration stands:

| If we add | Data safety must then declare |
|---|---|
| **RevenueCat SDK / Play Billing** (Shipaton Gate 2 — mandatory to enter) | **Purchase history** — collected, and shared with RevenueCat as a third party. RevenueCat also collects an app-user ID and device metadata. Purpose: app functionality + analytics. |
| **Any ad network** (AdMob, Unity Ads, ironSource, AppLovin) | **Device or other IDs** (AAID) — collected and shared; purpose advertising/marketing. Plus approximate location in most SDKs' default configuration, and "App activity". This is the biggest single change. |
| **OneSignal** (the retention track) | **Device or other IDs**, push tokens; "App activity". |
| **Play Games Services leaderboards** (ADR-0010) | Player ID and in-game scores; "App info and performance". |
| **Any crash reporter** | "Crash logs", "Diagnostics". |

Two consequences worth stating plainly:

1. **The declaration must be updated in the same release that ships the SDK**, not afterwards.
   Play evaluates the form against the binary you uploaded.
2. **The privacy policy must match the form.** A policy saying "we collect nothing" alongside a form
   declaring AAID sharing is exactly the inconsistency reviewers look for. Write the privacy policy
   *last*, after the monetization decisions are final — or write it now to describe only today's
   behaviour and commit to revising it with the SDK.

**Recommendation:** ship the first closed-test build with the honest "no data collected" answer, and
treat the data-safety update as a required line item on the RevenueCat integration ticket rather
than a separate task that can be forgotten.

---

## 8. The closed-test runbook (Branch B)

Only for a **personal account created after 2023-11-13**. Branch A skips this entirely.

The rule, verbatim from Google (verified 2026-08-25):

> "Google Play requires personal developer accounts created after November 13, 2023, to test their
> apps before those apps are eligible for distribution on Google Play."
>
> "At least 12 testers must be opted in to your closed test when you apply for production access,
> and they must have been opted in continuously for the preceding 14 days."

### 8.1 Create the track

1. Play Console → **Create app**. Set the app name, default language, **App** vs **Game** → *Game*,
   Free vs Paid → *Free*. (**Free vs Paid is permanent.** A free app can add IAP later; a free app
   can never become paid.)
2. Confirm **Play App Signing** is enabled (default). Google holds the real signing key; your
   upload key becomes replaceable.
3. **Testing → Closed testing → Create track.** The default `alpha` track is fine — the track name
   is not user-visible.
4. **Testers tab → Create email list.** Add the 12+ Google account addresses. They must be the
   Google accounts the testers actually use on their phones.
5. **Create new release → upload the AAB** (§3). Add release notes — anything truthful.
6. **Start rollout to Closed testing.**

The first release of a new app goes to review. Google: "Review usually takes seven days or less."
Budget 7, hope for 2.

### 8.2 Get testers opted in — the real bottleneck

**The opt-in link only exists once the release has been reviewed and is live on the track.** Until
then there is nothing for testers to click, which is why §1.2 puts the upload deadline a full week
before the opt-in deadline.

When it is live, Play Console shows a **"Copy link"** web opt-in URL on the closed track. Send it to
your testers with `docs/release/tester-comms-template.md`. Each tester must:

1. Open the link **while signed into the Google account you added to the list**.
2. Tap **"Become a tester"** → accept.
3. Install the app from the Play link that then appears.

Then, and this is the part that quietly kills schedules:

> "Testers who opt in, test for fewer than 14 days, and then opt out do not count toward the
> requirement. If a tester opts out and opts back in later, the 14 days must be **consecutive**."

**Practical rules:**

- **Recruit 15–16, not 12.** One will use the wrong Google account, one will uninstall, one will
  never click. 12 is a floor with no tolerance, and losing one on day 12 costs you 14 days.
- **Tell them explicitly: do not opt out, and do not uninstall, until told.** Uninstalling the app
  is fine for the count (opt-in status is what matters), but people who uninstall often opt out too.
  Simplest instruction: leave it installed and leave it alone.
- **Keep a list** of who confirmed opt-in and on what date. Play Console shows the opted-in count
  but reconciling it against 12 named humans is on you.
- **They must actually be able to install it.** An ARM64-only build (which ours is) excludes 32-bit
  devices and emulators. Ask testers to confirm the install succeeded, not just the opt-in.
- Testers do **not** need to play the game for the requirement. Opt-in continuity is the metric.
  Google does ask you to describe your testing in the production-access questionnaire, so real
  feedback is genuinely useful — but it is not what the 14 days measure.

### 8.3 Apply for production access

On day 14 of continuous opt-in: **Play Console → Production → Apply for production access**.

You answer a short questionnaire about how you tested and what you changed. Write real answers —
what testers reported, what you fixed. Then Google reviews it; the same "seven days or less" line
applies, and there is **no published SLA** beyond that.

While this review runs, complete everything in §5, §6 and §7. None of it blocks the application and
all of it blocks the production release that follows.

### 8.4 What the placeholder build must contain — and what can change later

**This is the section that buys the schedule.** The build that starts the 14-day clock does not
have to be the build that launches.

**It must:**

- Be a **signed release AAB** with your upload key (debug-signed is rejected).
- Have the **final `applicationIdentifier`** — `com.catmetro.game`. **This can never change.** It is
  the one field that is genuinely permanent.
- **Install and run** on a real device without crashing on launch. A tester who cannot open it may
  well opt out.
- Target **API 36** (§4).
- Have a **unique, increasing `AndroidBundleVersionCode`** for every upload.

**It does not have to:**

- Be feature-complete, contain final art, or contain any monetization at all.
- Have the final name, icon, screenshots or description.
- Have the final level set.

**Freely swappable during the 14 days, with no effect on the clock:**

| Swappable | Notes |
|---|---|
| **New builds on the track** | Upload as many as you like. Uploading a new build does **not** reset tester opt-in continuity. This is what makes the parallel-development plan work. |
| Store listing text, screenshots, icon, feature graphic | Edit freely. |
| Content rating answers | Re-submit the questionnaire whenever the build changes. |
| Data safety declaration | Update alongside the SDKs that change it (§7.2). |
| App name | Changeable. |
| Adding testers | Adding more testers later is fine; their own 14 days start at their opt-in, so top up early. |

**Not swappable, ever:**

| Locked | |
|---|---|
| `applicationIdentifier` (`com.catmetro.game`) | Permanent from first publish. |
| Free vs Paid | A free app cannot become paid. |
| The signing key | Replaceable only via Play's upload-key reset support flow. |

**Resets or endangers the 14-day clock:**

- A tester opting out, or dropping below 12 opted-in testers at any point.
- Deleting and recreating the closed track or the tester list.
- Removing testers from the email list.

> **The consequence for the plan:** upload whatever installs and runs, today. Then build the real
> game while the clock runs. Nothing about the placeholder constrains the launch build except the
> package name and the signing key — and both of those are decisions you want to make once anyway.

---

## 9. Licensing position for a shipped binary

`AGENTS.md` reserves this decision for the human: *"Putting them in a Play Store binary is a real
commercial question the human decides deliberately."* Entering the hackathon forces the question,
because Gate 1 *is* store distribution. Here is what the repo actually records.

### 9.1 The headline — and it is much better news than expected

**A build from a clean checkout of this branch contains no Meshy, Tripo or Polyfork geometry at
all. It is greybox. The licensing exposure of that binary is nil.**

Evidence:

- **Zero tracked 3D assets on any shipping ref.** No `.fbx`, `.glb`, `.obj` or `.prefab` is tracked
  on `feat/store-release`, `integration/look-stack` or `main`. `unity/Assets/Art/` does not exist in
  a fresh checkout — only `Art.meta`.
- `unity/Assets/Resources/` contains two materials and a strings CSV. No models.
- `CatModelCatalog` — the thing that would put generated cats on screen — **is not on any shipping
  branch**. It exists only at the tip of `task/CM-CATS-WIRE`. Cats are `GameObject.CreatePrimitive`
  spheres and cubes today.
- `PropModelCatalog` is present but `Resources.Load` returns `null` for all five prop ids on a clean
  checkout, `CanAdmit` rejects on a null prefab, and `AdmittedEntryCount` is 0 — silently, exactly
  as the `AGENTS.md` gotcha describes.

**This decouples the two problems.** The licensing decision does *not* block getting live on Play.
A greybox build can start the 14-day clock today, and the art can land later in the same track
without restarting anything (§8.4). Given that Gate 1 is the project's largest risk and the asset
paperwork is its most tangled, that separation is worth a great deal.

### 9.2 The trap — a build from the human's own machine is NOT the same binary

**`/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/Resources/CatMetroProps/`
exists on this machine and contains five prop prefabs with baked textures.**

Unity treats **any** folder named `Resources` under `Assets/` as a resource root. That path is
gitignored, so it is invisible to CI and to any fresh clone — but a build run **on this machine**
would embed those props into the AAB. And `scripts/build-aab.sh` is run by the human, on this
machine.

The five are `prop-toy-engine` (**Meshy**) and `prop-depot-shed`, `prop-desk-clutter`,
`prop-station-kiosk`, `prop-trees` (**Tripo**).

> **Before the first upload, confirm which binary you are shipping.** If those props are in it, the
> §9.4 evidence gaps apply and ADR-0013 — the governing document — says in its own status line that
> "generated assets are blocked from every Play-bound binary." Temporarily moving
> `unity/Assets/Art/Generated/incoming/Resources/` aside before a store build is a one-command way
> to guarantee the greybox binary. Do not delete it; `curation-backups/` inside that tree holds the
> only surviving provider-delivered copies of two paid assets (§9.4, item 4).

### 9.3 What evidence exists — and it is more than I expected

The provider *licences* are settled in our favour on paid tiers (`shipton-hackathon.md` §8.1: all
three permit commercial distribution in a shipped binary, no attribution). The residual is
**provenance** — proving each asset came from a paid account. On that, the repo records a lot:

| Provider | Record | Where | Fields |
|---|---|---|---|
| **Polyfork** | `unity/Assets/Art/Polyfork/PROVENANCE.md` — 9 rows | **`rescue/diorama-tip-adr0011`, `art/diorama-pass` only** | Asset id + URL, triangle count, source GLB SHA-256, derivative FBX name + SHA-256, Unity GUID, `.meta` SHA-256, acquisition date, and the authenticated **Founders entitlement** used |
| **Polyfork** | **ADR-0011** — 504 lines, licence pinned to a retrieved-HTML SHA-256 | same branches | **Signed by the human 2026-08-10** against an exact reviewed head |
| **Meshy / Tripo** | Per-asset `.glb.json` sidecars | gitignored `incoming/` | `service`, `task_id`, `timestamp_utc`, `sha256`, **`plan_tier: "paid"`**, prompt |
| **Meshy / Tripo** | `docs/design/assets/PIPELINE.md` — licence terms, both tiers | **on this branch** | Paid vs free terms for both providers, retrieval dates |
| **Meshy / Tripo** | **ADR-0013** — 15-row manifest, 4 SHA-256s per asset | **`task/GEN-ASSET-LICENSE-ADR` only** | **Unsigned** |

That is a genuinely good custody trail — better than most shipped indie games have. The problem is
not that the work was not done. It is that **none of it is on a shipping branch, and the two
documents that authorise shipping are not signed.**

### 9.4 What is missing — stated plainly

1. **ADR-0011's Amendment A is unsigned.** The original ADR is signed, but the amendment covering
   the *permanently public repository* is "pending human signature" on all nine boxes. The ADR
   itself says Lane 1A may not merge until it is signed. It records a containment residual too: a
   history rewrite "did not recall bytes already disclosed", and former object IDs, caches, forks
   and clones may retain the nine FBXs. Erasure is explicitly not claimed.
2. **ADR-0013 is unsigned, and its own status line blocks the assets.** All 14 propositions
   unchecked, signature fields blank.
3. **Neither ADR is on a shipping branch.** `docs/adr/` here has holes at 0011 and 0013.
4. **Two Tripo assets no longer carry provider-delivered bytes, and the originals have no backup.**
   Under a no-plinth ruling relayed by an orchestrator session — *with no human-authored commit
   recording it* — `cat-blue-siamese-loaf` lost **45.9% of its provider-delivered geometry** and
   `cat-yellow-longhair-wave` 7.4%. The curation lane then **overwrote the `sha256` field in those
   two source sidecars** with the post-edit hash, so the bytes Tripo actually delivered are recorded
   in no sidecar and survive only in gitignored, machine-local `curation-backups/`. ADR-0013 leaves
   open whether geometry-editing a source is permitted at all — §1's allowed-modification list
   covers decimation, scaling, recolouring, materials, animation and composition, and does **not**
   authorise deleting source geometry.
5. **Meshy's operative Terms contain no express paid-output assignment.** The ownership promise
   lives only on undated pricing/help pages. `shipton-hackathon.md` §8.1 hit the same wall
   independently. Also: Meshy §2.5 deletes non-Enterprise API output **3 days after generation** —
   provider-side re-acquisition is already impossible.
6. **Tripo paid-credit evidence is unretained.** §5.2.2 grants paid users full rights, but the terms
   do not define which credit sources make a task paid, and no task-linked receipt is kept.
   `plan_tier: "paid"` is set by a human-supplied environment variable — ADR-0013 is admirably
   honest that it is "an attestation to verify, not a vendor receipt."
7. **Task IDs are embedded in shipped bytes.** Each of the eight Tripo derivatives carries its
   provider task id in six internal GLB JSON name fields.

### 9.5 Verdict

**On licence:** all three providers permit commercial distribution inside a shipped game binary on
paid tiers, with no attribution. There is no licensing *prohibition* to overcome.

**On evidence:** Polyfork is in good shape — signed ADR, byte-level provenance, a hashed capture of
the licence page. Meshy and Tripo are not: the paid-tier *entitlement* is a self-attestation with no
vendor receipt, Meshy's own terms have a drafting gap on the ownership grant, and two assets have
irreproducible provenance.

**The practical recommendation, and it is genuinely convenient:** **ship the greybox binary first.**
It carries zero asset exposure, satisfies Gate 1, starts the 14-day clock, and buys the entire
closed-test window to resolve the paperwork properly — sign ADR-0011 Amendment A, sign or amend
ADR-0013, retrieve Tripo's billing history, capture Meshy's terms with timestamps, and get both
ADRs onto a shipping branch. Art can be swapped into the same track later without restarting the
clock.

The alternative — shipping the props now — means uploading a binary whose governing ADR says it may
not be uploaded, on evidence that is a self-attestation, three weeks before a deadline. The upside
is prettier screenshots. That is not a good trade, and it is a decision only the human can make in
any case.


---

## 10. The human-only list

Everything an agent cannot do, ordered. Times are hands-on effort, not elapsed time — items 5, 8
and 10 have long waits attached that other work runs inside.

| # | Action | Time | Blocks |
|---|---|---|---|
| 1 | **Answer: Play account type + creation date.** Console → Settings → Developer account. If none exists, create it ($25, personal, email verification only). | **5 min** (or 30 min to create) | Everything. Decides Branch A vs B. |
| 2 | **Create the upload keystore** and configure it in Player Settings (§3.3). Back it up. | **15 min** | Any upload at all. |
| 3 | **Decide the asset question** (§9): greybox binary, or props included. Recommended: greybox. | **10 min** | The build's contents. |
| 4 | **Run `bash scripts/build-aab.sh`.** Unity cannot run sandboxed. | **5 min + 25–45 min wait** | The upload. |
| 5 | **Create the Play app + closed track, upload the AAB.** Set app name, Game, Free (permanent). Confirm Play App Signing on. | **45 min** | Starts the review clock. |
| 6 | **Recruit 12+ testers** — aim for 15–16. Collect the Google addresses they use on their phones. | **1–3 h, spread** | The 14-day clock. Longest human tail. |
| 7 | **Publish a privacy policy URL.** Write it fresh for today's behaviour; do **not** publish the existing draft (§5.5). GitHub Pages is fine. | **30 min** | Production release. |
| 8 | **Send the opt-in link** once the track clears review, and confirm each tester actually opted in. Use `docs/release/tester-comms-template.md`. | **30 min + 14-day wait** | Production access. |
| 9 | **Produce the store images** (§5.4) — 512×512 icon, 1024×500 feature graphic, 4× 1080×1920 screenshots. Plus the Devpost set at 1024×1024 and 1179×2556. | **3–6 h** | Production release. Largest remaining unit of work. |
| 10 | **Complete listing, content rating, data safety, target audience.** All answers pre-written in §5, §6, §7. | **45 min** | Production release. |
| 11 | **Bump `bundleVersion` to `1.0.0`**; bump `AndroidBundleVersionCode` on every upload. | **2 min** | Upload rejection if skipped. |
| 12 | **Apply for production access** on day 14 (Branch B only). | **20 min + up to 7-day wait** | Going live. |
| 13 | **Promote to production and roll out.** | **15 min + 1–3 day review** | Live. |
| 14 | **Sign ADR-0011 Amendment A and ADR-0013**, or amend them, and land both on a shipping branch (§9.4). | **1–2 h** | Shipping any generated art. |

**Items 1–6 are today's work.** Items 7, 9 and 10 fit comfortably inside the 14-day wait.

### Never done by an agent, under any circumstance

- **Uploading to Google Play.** Non-negotiable in this repo.
- **Creating, reading or committing a keystore.**
- **Reading `.env`.**
- Signing an ADR on the human's behalf.
