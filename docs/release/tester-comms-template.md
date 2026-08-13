# Tester comms — paste-ready templates

Written 2026-08-13 (Lane 10 RELEASE-PREP). Companion to `docs/runbooks/play-closed-test.md`.
Sending these is a **[HUMAN]** act. Nothing here is sent automatically.

## How to use

1. Fill every `[BRACKET]`. Do not send with a placeholder still in the text.
2. `[OPT-IN URL]` comes from Play Console → Testing → Closed testing → Manage track → Testers tab.
   It "only shows when an app is Published"
   ([Play Console Help](https://support.google.com/googleplay/android-developer/answer/9845334?hl=en), retrieved 2026-08-13) — so you cannot send message A before the first closed release clears review.
3. **Do not add product claims.** Anything about levels, features or performance must already be
   `VERIFIED` in `docs/store/play-store-listing.md` §Current claim gates (Lane 7's pack — read it,
   never edit it). If a claim is not there, cut the sentence rather than softening it.
4. Keep the tester roster (names + Gmail addresses) **out of this repository**: personal data, and
   the repo has no secret-scanning gate today (`state/PROJECT_STATE.md:92`, `.github/workflows/ci.yml:17`).

---

## A. Invite + expectations (send once, per person)

> **Subject:** Two minutes on your Android phone — testing my game before launch
>
> Hi [NAME] — I'm building **Cat Metro**, a one-thumb train puzzle where you throw junction switches
> to route cat commuters to the right station. Before Google lets me publish it, I need **12 people
> opted in to a closed test for 14 straight days**. Would you be one of them?
>
> **What I need (about 3 minutes today):**
> 1. On your **Android phone**, open this link and tap *Become a tester*: **[OPT-IN URL]**
>    (use the same Google account you use on the phone — the invite is tied to **[EMAIL]**).
> 2. Install **Cat Metro** from the Play Store page the link gives you.
> 3. Play for a few minutes.
>
> **What I need over the next two weeks:**
> - **Stay opted in until [DATE = opt-in date + 15 days].** Uninstalling is fine; tapping *Leave the
>   test* is not — Google's clock resets for that seat and I'd have to start the 14 days over.
> - Open it a couple of times a week. Google looks at whether testers actually engage.
> - Tell me anything confusing, boring, or broken. One sentence is enough.
>
> **Fair warning about the build:** it is an early test build. [ONE HONEST LINE ABOUT THE CURRENT
> STATE — e.g. placeholder art / no sound / [N] levels. Fill from the exact release candidate.]
> No ads, no purchases, no account needed.
>
> **Where to send feedback:** [FEEDBACK EMAIL OR LINK] (same address is listed in the Play test page).
>
> Thanks — this genuinely unblocks the launch.
> [YOUR NAME]

---

## B. Three questions (send with, or right after, message A)

> When you've played a bit, could you answer these three? Short answers are perfect.
>
> 1. In your own words: what is the game asking you to do?
> 2. Where did you get stuck, bored, or confused — and on which level?
> 3. Phone model + Android version, and did anything look wrong on your screen (cut off, tiny, laggy)?

Why these three: (1) tests whether the goal reads without help, (2) locates the failure, (3) gives
device coverage you cannot get from one Pixel. Keep replies **verbatim** — paraphrased feedback is
not evidence (`docs/prd/venture-critique.md` V-1 is an evidence request, not a vibe check).

---

## C. Confirmation check (send ~24h after A, to anyone whose opt-in you cannot see in Console)

> Quick check — Play Console still doesn't show you as opted in. Two things it usually is:
> - the link was opened on a computer or with a different Google account than **[EMAIL]**, or
> - the page was opened but *Become a tester* was never tapped.
>
> Link again: **[OPT-IN URL]**. Ping me if it says the test isn't available and I'll fix the roster.

---

## D. New build note (send with every upload; reuse as the Play release notes)

> New Cat Metro test build is live — update from Play (or wait, it'll auto-update).
> **Changed:** [ONE OR TWO LINES]
> **Please look at:** [THE ONE THING YOU WANT TESTED]
> Version [VERSIONNAME] (build [VERSIONCODE]).

Keep the version line — it makes tester reports attributable to a specific artifact, which is the
only build-to-feedback binding this project has today
(`docs/release/release-checklist.md` §5 ledger).

---

## E. Mid-window nudge (day ~7)

> Halfway through the test window. Two asks:
> 1. Don't leave the test yet — the streak has to run to [DATE].
> 2. If you've played since the last build: what's the single most annoying thing about it?
>
> If you've already dropped off, that's useful information too — tell me why and I'll take it as the
> most honest feedback in the batch.

---

## F. Wrap-up (after the window closes)

> The closed test window is done — thank you. You can leave the test any time now without breaking
> anything. [IF APPLICABLE: what happens next / when it goes live.]
> Last ask: one sentence — would you have kept playing if I hadn't asked you to?

---

## Guardrails for whoever sends these

- No promises you have not decided to keep (credits, free content, prizes). If you promise it, write
  it down.
- No claims outside the VERIFIED set in `docs/store/play-store-listing.md`.
- No links to internal docs, the private repo, or unreleased plans.
- If a tester asks what data the app collects, answer from the Data safety form you actually
  submitted — not from `docs/plan/web/privacy/index.html`, which describes SDKs this build does not
  contain (flagged as F-2 in `docs/release/release-checklist.md`).
