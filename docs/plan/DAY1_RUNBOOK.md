# DAY-1/2 CONSOLE RUNBOOK — everything the human clicks, in order

Generated Aug 2 (D2). Every drafted text below is copy-paste ready. Exact field values are
bolded; where a console's UI labels drift, the intent line tells you what to look for.
Domains re-verified unregistered via RDAP minutes before this file was written.

**Standing rule:** if any step throws an identity/verification delay, do NOT stop the others —
every numbered section is independent except 2→3 (testers need the closed track to exist).

---

## 0. Account decision (2 minutes — do first, it can change everything)

Open Play Console with your existing ("pre-verified") personal account → Settings →
Developer account → look for the account **creation date**.
- **Created before Nov 13, 2023** → the 12-tester/14-day requirement does not apply to this
  account. Tell both chats immediately — the critical path re-plans around review times only.
- **Created on/after Nov 13, 2023** (or you're creating a new one) → proceed exactly as below;
  the 14-day clock is the plan's spine.

## 1. Google Play Console (~45 min + verification wait)

1. If new account: play.google.com/console → sign up (personal), **$25 fee**, complete
   identity verification (government ID may be requested — only you can do this).
2. **Device verification:** install the **Play Console app** on a physical Android phone, sign
   in, complete device verification (required for new personal accounts before publishing
   anything — this blocks the seed AAB if skipped).
3. Create app: name **Cat Metro** · default language **en-US** · **Game** · **Free**.
   App will contain **in-app purchases** and **ads** (rewarded only — declare ads = yes).
4. Note: the package name **com.catmetro.game** is set by the first AAB you upload, not in
   the console — it is frozen forever at that moment. The build session owns generating the
   AAB; do not let any test build with a different package id get uploaded first.
5. Release → Testing → **Closed testing** → create track **closed-alpha** → choose email-list
   testers → create list "catmetro-testers" (you'll paste addresses as invites are accepted).
6. Monetization setup can wait until D17 — do not configure products today.
7. Turn ON **Managed publishing** (Publishing overview) so nothing ever goes live by accident.

## 2. Tester recruitment (start now; goal 12/12 opted in TONIGHT, pool of 18–20)

Copy-paste invite (personalize the first line per person):

> Subject: Need 2 minutes of your phone for my 8-week game challenge
>
> Hey [name] — I'm building a cat-themed metro puzzle game ("Cat Metro") in public for an
> 8-week competition, and Google requires 12 real testers opted in continuously for 14 days
> before I'm allowed to launch. Would you be one of my 12?
>
> What it involves:
> 1. Tap this opt-in link on your Android phone and accept: **[OPT-IN LINK from the closed
>    track page]**
> 2. Install the app when it appears (first build is a bare-bones test — it gets better daily)
> 3. **Stay opted in through at least Aug 31** (uninstalling is fine; opting OUT resets your
>    clock) and open it a couple of times a week — daily if you're feeling generous. Google
>    actually grades tester engagement.
> 4. Optional but gold: reply with anything confusing/boring — I read everything.
>
> That's it. You'll be in the credits, and if I win anything you get bragging rights. 🚇🐱

Tracking: keep a sheet of name → invited → accepted → opted-in date. **Backfill within 24h
if anyone drops; the floor is 12 continuous.** Recruit from personal network first; avoid
tester-exchange groups (Google flags accounts by association).

## 3. Devpost + Ship Kit (~15 min)

1. revenuecat-shipaton-2026.devpost.com → **Join hackathon** (your Devpost account).
2. Complete the **participant form** (arrives by email after registering; also
   form.typeform.com/to/Czj9qXJT). Answer honestly: solo developer, Unity/Android,
   Google Play, using RevenueCat SDK for in-app purchases + RevenueCat Ads (beta).
3. Watch email for the first Ship Kit perk drop. **Claim the OneSignal "Growth free up to 3
   months" perk BEFORE creating the OneSignal subscription in section 5.** Also claim Tenjin
   (free 3 mo) and note Noise's $1,000 matching credits for later; some perks are
   redemption-limited.
4. Join **discord.gg/shipaton26**; note the #post-engagement-boost channel for BIP posts.

## 4. RevenueCat (~15 min)

1. app.revenuecat.com → create project **Cat Metro** → add **Google Play app**
   (package **com.catmetro.game**).
2. Dashboard → **Ads** page → request **RevenueCat Ads beta** access (Day-1 task; approval
   latency unknown — request now, build proceeds regardless).
3. Screenshot your project's plan/billing page (should be Pro, free under $2.5k MTR, with
   Experiments included — the build session needs this confirmation for the price test).
4. Do NOT configure products/entitlements/offerings today — that's the D17 runbook
   (data/revenuecat_configuration.csv), driven by the build session.

## 5. Firebase → OneSignal (~20 min, in this order)

1. console.firebase.google.com → create project **catmetro-prod** (Analytics optional) →
   add Android app with package **com.catmetro.game** → download `google-services.json`
   (goes to the build session, NOT committed until reviewed).
2. Firebase Project settings → Service accounts → **generate FCM v1 service-account JSON**.
   ⚠️ This file is a credential: hand it only to the OneSignal dashboard; never commit it.
3. onesignal.com → new app **Cat Metro** → Android (FCM) → upload the service-account JSON →
   **apply the Ship Kit free-Growth perk instead of paying $19/mo** (from section 3.3).
4. If custom events aren't visible on the plan, email support@onesignal.com to enable them
   (known enablement quirk from the 5.2.0 release notes).

## 6. AdMob (~15 min)

1. admob.google.com → create account (same Google account as Play is cleanest) → add app →
   Android → not yet on Play (it isn't public) → name **Cat Metro**.
2. Create ONE rewarded ad unit named **rewarded_test_spike** (the D10 spike uses Google's
   test unit IDs anyway; real units come later).
3. Note App ID + unit ID into the build session.

## 7. Domains + handles (~15 min, ~$50)

1. Buy **catmetro.com** and **catmetro.io** (both re-verified unregistered via RDAP today).
   Any registrar; privacy proxy on.
2. Claim **@CatMetroGame** on X and TikTok (exact handle per the identity freeze; if taken
   since, closest variant and tell both chats — do NOT improvise a new name).
3. Point catmetro.com at a one-page placeholder from the build session by tonight; privacy
   policy goes live at **catmetro.com/privacy** before the first store listing save (the
   build session drafts it; you publish).

## 8. Email to shipaton@revenuecat.com (2 min — send verbatim)

> Subject: Two rules questions — multi-category wins & pre-order listings
>
> Hi team — solo dev building for Shipaton 2026 (registered on Devpost). Two questions the
> Official Rules don't address explicitly:
> 1. Can a single Project WIN prizes in more than one category (e.g., a sponsor category and
>    a RevenueCat category), assuming it's entered in both?
> 2. Does a Google Play pre-registration/pre-order listing count as a "public release" for
>    eligibility purposes, or is eligibility only affected by the first actual public
>    availability of the app?
> Thanks! — Sushant

## 9. Calendar (1 min)

Import **deliverables/catmetro_calendar.ics** (same folder as this file) — it contains the
livestreams (Aug 4 judges session flagged ATTEND), every gate (D7/D14/D15/D21/D24, launch
window, D35/D42/D54), the Devpost-live target (Sep 15), the freeze, the deadline
(Sep 30 11:45pm PDT), and a Monday 9am re-verification + convergence-watch recurring
reminder.

---

## What Claude can drive for you (optional)

If you enable the **claude-in-chrome** extension with site permissions for
app.revenuecat.com, onesignal.com, devpost.com, and admob.google.com — and you're already
logged in — a session can click through sections 3–6 while you watch, leaving you only the
final submits, payments, 2FA prompts, and anything touching identity. Sections 0–2 and 7–8
are yours regardless (physical device, personal network, money, attestations).

## End-of-day success check (tonight)

- [ ] 12/12 testers opted in (pool ≥16 invited) — THE number
- [ ] Seed AAB v0.0.1 live on the closed track (build session delivers it; you upload if CI isn't wired)
- [ ] Play account verified + device verification done
- [ ] Ship Kit registered; OneSignal free-Growth claimed
- [ ] RC project + Ads beta requested
- [ ] Domains bought, handles claimed
- [ ] BIP post 1/56 published with the pre-registered fun-gate bar (#Shipaton)
