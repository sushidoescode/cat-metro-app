# Tester comms — paste-ready templates

Written 2026-08-13 (Lane 10 RELEASE-PREP). Companion to `docs/runbooks/play-closed-test.md`.
Sending these is a **[HUMAN]** act. Nothing here is sent automatically.

## How to use

1. Fill every `[BRACKET]`. Do not send with a placeholder still in the text.
2. `[OPT-IN URL]` comes from Play Console → Testing → Closed testing → Manage track → Testers tab.
   It "only shows when an app is Published"
   ([Play Console Help](https://support.google.com/googleplay/android-developer/answer/9845334?hl=en), retrieved 2026-08-13) — so you cannot send message A before the first closed release clears review. Send **A0** in the meantime; it needs no URL.
3. **Do not add product claims.** Anything about levels, features or performance must already be
   `VERIFIED` in `docs/store/play-store-listing.md` §Current claim gates (Lane 7's pack — read it,
   never edit it). If a claim is not there, cut the sentence rather than softening it.
   Message A contains **exactly one** product claim — "No forced ads, energy, or loot-box system;
   every current level is solvable free" — reproduced **verbatim** from the VERIFIED row at
   `docs/store/play-store-listing.md:76`, whose publication rule is "Keep the positioning line
   verbatim". Do not reword it, and do not add a second claim beside it.
4. Keep the tester roster (names + Gmail addresses) **out of this repository**: personal data, and
   the repo has no secret-scanning gate today (`state/PROJECT_STATE.md:92`, `.github/workflows/ci.yml:17`).
5. **Q0-1 gate on the 12/14 wording:** messages A0 and A state Google's 12-testers/14-days
   requirement as fact. That requirement binds only personal developer accounts created on/after
   2023-11-13 (runbook §0 Q0-1, §1 — currently UNKNOWN for this account). Answer Q0-1 BEFORE
   sending; if the account predates the cutoff, reword "Google requires…" to "I want a real
   two-week test group" — never hand external people a requirement-shaped reason that does not bind.

---

## A0. Pre-publish ask (send TODAY — no opt-in URL exists yet)

Use this while the first closed release is still in review. It collects the Gmail addresses you need
for the tester list so that the moment the opt-in URL appears you can send message A to a roster that
is already assembled — the review wait then costs you nothing.

> Hi [NAME] — quick favour with a deadline. I'm about to put my game **Cat Metro** into a Google Play
> closed test, and Google requires **12 people opted in for 14 straight days** before I'm allowed to
> launch. I'd like you to be one of them.
>
> **Right now I just need your Gmail address** — that's the account Google ties the test to. Nothing
> to install yet; Google is still reviewing the first build.
>
> **What it will involve, so you can say no now rather than later:**
> - One link to tap on an **Android phone** (it won't work on iPhone), then install from Play.
> - Staying in the test for **two weeks** — leaving early restarts the clock for that seat.
> - Opening it a couple of times a week and telling me what's confusing or boring.
>
> I'll send the link as soon as Google approves the build — probably [DATE ESTIMATE], possibly up to
> a week. Reply with your Gmail if you're in.
>
> [YOUR NAME]

Keep the replies in your own private notes — see the roster-custody rule in step 4 above.

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
> - **Stay opted in until [DATE = opt-in date + 15 days].** Please don't tap *Leave the test* — that
>   breaks the 14-day streak for that seat and it has to start over. Keep it installed to be safe.
> - Open it a couple of times a week and actually play. When I apply to launch, Google asks me to
>   describe the engagement I got from testers and what I changed because of their feedback.
> - Tell me anything confusing, boring, or broken. One sentence is enough.
>
> **Fair warning about the build:** it is an early test build. [ONE HONEST LINE ABOUT THE CURRENT
> STATE — e.g. placeholder art / no sound / [N] levels. Fill from the exact release candidate.]
> No forced ads, energy, or loot-box system; every current level is solvable free.
>
> **Where to send feedback:** [FEEDBACK EMAIL OR LINK] (same address is listed in the Play test page).
>
> Thanks — this genuinely unblocks the launch.
> [YOUR NAME]

**Sourcing for the two Google-behaviour statements in message A** (retrieved 2026-08-13):

- *"breaks the 14-day streak … has to start over"* — the requirement page is explicit that the 14
  days "must be consecutive to count" and that testers who opt out are not counted
  ([App testing requirements](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en)).
  Whether **uninstalling** (as opposed to tapping *Leave the test*) affects the count is **not stated
  in any retrieved source — UNVERIFIED**. That is why the message asks people to keep it installed
  rather than promising that uninstalling is harmless.
- *"Google asks me to describe the engagement … and what I changed"* — the same page's
  production-access questions ask developers to "Provide information about the engagement you
  received from testers during your closed test", to "summarize the feedback that you received from
  testers, and let us know how you collected this feedback", and to state "what changes you made to
  your app or game based on what you learned during your closed test".

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
- **Never tie an incentive to an install, an opt-in, a rating, or a review.** Paying for or rewarding
  those is the shape of a policy violation and it corrupts the only signal the test exists to
  produce. Thanks and credits are fine; "opt in and I'll send you £10" is not.
- No claims outside the VERIFIED set in `docs/store/play-store-listing.md`.
- No links to internal docs, the private repo, or unreleased plans.
- If a tester asks what data the app collects, answer from the Data safety form you actually
  submitted — not from `docs/plan/web/privacy/index.html`, which describes SDKs this build does not
  contain (flagged as F-2 in `docs/release/release-checklist.md`).
