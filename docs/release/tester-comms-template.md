# Tester comms — paste-ready templates

Updated 2026-08-26. Companion to `docs/release/play-release-runbook.md`.
Sending these is a **[HUMAN]** act. Nothing here is sent automatically.

## How to use

1. Fill every `[BRACKET]`. Do not send with a placeholder still in the text.
2. `[OPT-IN URL]` comes from Play Console → Testing → Closed testing → Manage track → Testers tab.
   It "only shows when an app is Published"
   ([Play Console Help](https://support.google.com/googleplay/android-developer/answer/9845334?hl=en), retrieved 2026-08-13) — so you cannot send message A before the first closed release clears review. Send **A0** in the meantime; it needs no URL.
3. **Do not add product claims.** Use only wording whose gate is cleared in
   `docs/store/play-store-listing.md` for the exact test AAB. If a claim is not cleared, cut it.
   Any campaign quantity comes from that AAB's generated listing sibling, never this template.
4. Keep the tester roster (names + Gmail addresses) **out of this repository**. It is personal data.
5. **Account gate on the 12/14 wording:** messages A0 and A state Google's 12-testers/14-days
   requirement as fact. It binds only personal developer accounts created **after** 2023-11-13.
   Organization accounts and personal accounts created **on or before** 2023-11-13 follow the exempt
   path. Confirm the account branch in `docs/release/play-release-runbook.md` before sending. For an
   exempt account, reword "Google requires…" to "I want a real two-week test group" — never give
   people a requirement-shaped reason that does not bind.

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
> - Staying opted in until I confirm Google has granted production access. That will be at least
>   **two weeks**; leaving early breaks the continuous period for that seat.
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
> - **Stay opted in until I explicitly confirm production access is granted — never earlier than
>   [DATE = exact opt-in timestamp + 15 calendar days].** Please don't tap *Leave the test* — that
>   breaks the continuous period for that seat. Keep it installed to avoid accidental churn; Google
>   documents opt-in continuity, not what uninstalling does.
> - Open it a couple of times a week and actually play. When I apply to launch, Google asks me to
>   describe the engagement I got from testers and what I changed because of their feedback.
> - Tell me anything confusing, boring, or broken. One sentence is enough.
>
> **Fair warning about the build:** it is an early test build. [ONE HONEST LINE ABOUT THE CURRENT
> STATE — e.g. placeholder art / no sound / [N] levels. If naming a count, copy it from the exact
> AAB's generated listing.]
> No forced ads. No energy timers. No loot boxes. Campaign play is free. [KEEP ONLY IF THE EXACT
> TEST AAB CLEARS THESE CLAIMS.]
>
> **Where to send feedback:** [FEEDBACK EMAIL OR LINK] (same address is listed in the Play test page).
>
> Thanks — this genuinely unblocks the launch.
> [YOUR NAME]

**Sourcing for the three Google-behaviour statements in message A** (the 12-testers-for-14-consecutive-days requirement plus the two points below; retrieved 2026-08-13):

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

## C. Confirmation check (send ~24h after A to anyone who has not self-confirmed)

> Quick check — Play Console shows only the total opted-in count, not individual names. Could you
> reply **opted in** and send a screenshot of the tester page? Two common snags are:
> - the link was opened on a computer or with a different Google account than **[EMAIL]**, or
> - the page was opened but *Become a tester* was never tapped.
>
> Link again: **[OPT-IN URL]**. Ping me if it says the test isn't available and I'll fix the roster.

Treat replies and screenshots as private, self-reported coordination evidence. The Play Console
aggregate and its production-access eligibility state are authoritative for the 12/14 gate.

---

## D. New build note (send with every upload; reuse as the Play release notes)

> New Cat Metro test build is live — update from Play (or wait, it'll auto-update).
> **Changed:** [ONE OR TWO LINES]
> **Please look at:** [THE ONE THING YOU WANT TESTED]
> Version [VERSIONNAME] (build [VERSIONCODE]).

Keep the version line — it makes tester reports attributable to a specific artifact, which is the
build-to-feedback binding recorded in `docs/release/release-checklist.md` §Candidate receipt.

---

## E. Mid-window nudge (day ~7)

> Halfway through the test window. Two asks:
> 1. Don't leave the test yet — the earliest buffer date is [DATE], and I will confirm separately
>    when Google grants production access.
> 2. If you've played since the last build: what's the single most annoying thing about it?
>
> If you've already dropped off, that's useful information too — tell me why and I'll take it as the
> most honest feedback in the batch.

---

## F. Wrap-up (only after production access is granted)

> Google has granted production access — thank you. You can leave the test any time now without
> affecting this application. [IF APPLICABLE: what happens next / when it goes live.]
> Last ask: one sentence — would you have kept playing if I hadn't asked you to?

---

## Guardrails for whoever sends these

- No promises you have not decided to keep (credits, free content, prizes). If you promise it, write
  it down.
- **Never tie an incentive to an install, an opt-in, a rating, or a review.** Paying for or rewarding
  those is the shape of a policy violation and it corrupts the only signal the test exists to
  produce. Thanks and credits are fine; "opt in and I'll send you £10" is not.
- No claims whose exact-candidate gate is uncleared in `docs/store/play-store-listing.md`.
- No links to internal docs, the private repo, or unreleased plans.
- If a tester asks what data the app collects, answer from the Data safety form and privacy policy
  submitted for that exact AAB, not from a future plan or a different build.
