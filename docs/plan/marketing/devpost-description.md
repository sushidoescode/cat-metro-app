# Devpost description — prescreen-first draft

Status: **DRAFT — not submitted or published.** Product-scope claims are pinned to frozen main at
`9be8f95` on 2026-08-10; the separate Pixel observation is labeled with its earlier, limited
provenance. The section titled “Frozen-anchor description” is paste-ready. Everything after it is
editor-only control text and must be removed before submission.

## Frozen-anchor description

Cat Metro is a one-thumb route-switching puzzle for Android. Tap junctions to send cat commuters
toward matching color-and-symbol stations. A next-wave preview shows what is coming while the player
times each switch.

The current source tree contains **10 authored and staged level files, L001–L010**. Every one is
checked by the repository’s content validator and solver gates. Normal player progression currently
exposes five of them, L001–L005; L006–L010 are not in that band. Shipped boot opens L001 directly;
the ordinary player flow covers active play, Won/Results, and Next. Separately, on 2026-08-09, one
human player completed L001 and L002 on a Pixel 9 Pro and used the real Next path to load L003. That
development APK was based on `b591f46` plus an untracked build shim, and its binary provenance is
recorded as unattested. The session is evidence that the core loop ran on one named device; it is not
proof that the frozen anchor itself was device-tested, a retention result, a compatibility survey,
or a public-release claim.

> Fair by design: no forced ads, no energy, no loot boxes, every level solvable free.

On the frozen anchor, that promise describes the code that exists now: there is no forced-ad,
energy, loot-box, or payment-gated level system; all ten authored levels pass the solver and
validator gates, while the normal player band contains five. It does not claim that a planned
business system or a future store build is already present.

The build is deliberately small enough to inspect. Content is handcrafted, then machine-checked
before it becomes eligible for player-flow integration. The solver and the gameplay loop give the
fairness promise a technical receipt; the Pixel playthrough gives the main interaction path a
hardware receipt. The
most important accomplishment so far is not a roadmap total—it is a complete, testable route from
opening the app to playing, winning, and continuing.

Cat Metro’s visual premise is a tabletop model railway of a cat city. A committed golden frame is
guiding an active art pass, but corrected final on-device art evidence is still open. The reference
frame is therefore a target, not a screenshot of the current player-facing build. Final Devpost
stills and video will come from a real merged build on a named device; if that capture is not ready,
the visual claim will be narrowed rather than mocked up.

Work still in preparation includes the device-verified art pass, the final on-device prescreen
video, exact-size submission stills, and release/submission readiness. Those are active work, not
completed product claims. The final description will be updated only when each new sentence has a
matching build, device, store, or dashboard receipt.

## Category selection — editor only

### Target on the frozen anchor

**Best Game only.** The current evidence supports a functioning game loop, progression through the
real Next seam, handcrafted content, readable route-puzzle mechanics, and solver/validator-backed
level integrity. The category-specific answer must lead with those facts and must answer the final
form’s monetization question using only evidence available at submission.

This is a frozen-anchor working draft, not an authorized final category rescope. PRD CM-R57.2
requires the final entered set to equal Best Game, HAMM, #BuildInPublic, OneSignal, Catvertising,
Design, and Grand Prize. If that complete slate cannot be evidenced and answered, final submission
is blocked until a human amends the criterion; the editor may not silently treat Best Game only as
compliant.

The entry itself still needs to satisfy Shipaton’s release and RevenueCat eligibility requirements
before it can be submitted. Category fit does not waive those event-wide gates.

### Do not target yet

| Category | Why it is not supported at the frozen anchor | Evidence required before adding it |
|---|---|---|
| Design | The tabletop golden frame is a visual target; corrected final on-device art evidence is open. | Merged player-facing art plus a traceable capture from the final build on a named device. |
| #BuildInPublic | A post plan is not a public history; Aug 1–9 cannot be backdated, so the frozen evidence cannot satisfy CM-R56's 56/56 rule. | Exactly 56 one-per-day public URLs with the unchanged four-metric gate in post 1 before data, or a recorded human amendment; plus archived receipts, actual engagement, and concrete published lessons. |
| HAMM | No implemented purchase/paywall evidence exists on the frozen anchor. | A qualifying RevenueCat purchase path, on-device flow, configuration/dashboard receipts, and raw funnel counts. |
| Catvertising | No implemented RevenueCat Ads path or player-choice data exists on the frozen anchor. | Live qualifying integration, on-device placement evidence, and opt-in/decline counts with denominators. |
| OneSignal | No deployed campaign evidence exists on the frozen anchor. | App ID, at least one deployed campaign, device delivery evidence, and dashboard receipts. |
| Grand Prize | There is no public release or RevenueCat/Play growth record on the frozen anchor. | Eligible US-accessible store release plus exact RevenueCat and Play counts for a stated date range. |

Do not name a held category in the submitted category card or video until its gate passes and its
category-specific Devpost answer is complete. An empty category-specific answer means the category
is not judged; unsupported prose is not a substitute.

## Final-submission replacement gates — editor only

These are deletion-or-replacement gates, not result placeholders. If a receipt does not arrive, keep
the current-safe sentence or delete the claim.

1. **Build census gate.** From the exact submitted commit, record both (a) every authored/staged ID
   and its validator/solver result and (b) every ID reachable through ordinary player flow. Replace
   every “five,” “ten,” and ID-range sentence from those two counts; never use a roadmap total,
   sibling branch, or development override.
2. **Device gate.** Record device model, build provenance, and capture date for every gameplay clip
   or still. Keep the limited Pixel statement unless stronger device evidence actually exists.
3. **Art gate.** Replace the visual-target paragraph only after corrected final art is merged and
   captured on device. Never present the golden reference as gameplay.
4. **Release gate.** Use “available,” “released,” or a store call to action only after the exact
   package is publicly reachable in the United States and the store receipt is archived. Distinguish
   closed testing, production access, submitted-for-review, managed-publishing hold, and public
   availability.
5. **Integration gate.** Confirm the submitted package name exactly matches the live app and that
   RevenueCat’s qualifying integration is verifiable in that package. If not, do not submit.
6. **Metrics gate.** Add launch or experiment numbers only from the final dashboards. Lead with raw
   counts and date range; every rate must include its numerator and denominator. Label each benchmark
   with its vintage and population. Treat GameAnalytics 2025 figures as all-genre medians, never as
   puzzle medians, and exclude cohorts that have not fully matured.
7. **Public-story gate.** Add #BuildInPublic claims only from an index of real public URLs. Report the
   actual number published during a stated period, including skips; do not convert a planned cadence
   into a completed history.
8. **Category gate.** Re-read the live form and answer every category-specific question for the
   committed CM-R57.2 slate. Each category needs its required integration and exhibits in the
   submitted text/video. If any limb is missing, block the final submission pending a human
   amendment; do not silently remove a category, add another one, or soften missing evidence into
   aspiration.
9. **Prescreen integrity gate.** Verify that the description, the first two minutes of video, the
   category card, and the downloadable app all describe the same build. Remove all editor notes and
   all unfilled result language before the Devpost page goes live.
10. **Build/video parity gate.** At the Sep 26–30 freeze, record the production `versionCode` and
    `versionName` installed from the USA-visible Play listing. They must match the video APK manifest,
    and the same `versionCode` must be visible in the video or a paired screenshot from that build.
    Bind the Play receipt, package, APK hash, device, date, and capture hashes in one manifest; hold
    submission if any link is missing.
11. **Judge-access gate.** Satisfy PRD CM-R31 and CM-R57.6 against the exact submitted package:
    secret scanning is active before the ignored code file exists; 25 codes are split 15 judge, 5
    press, and 5 spare; two codes redeem on clean devices and record the required promo purchase
    event; the remaining 23 plus redemption/restore guide go only in Devpost’s judge-only
    free-trial/promo-code field and remain valid through judging. Keep every code out of the public
    description, BIP posts, screenshots, repository, and video. Missing access blocks submission.
