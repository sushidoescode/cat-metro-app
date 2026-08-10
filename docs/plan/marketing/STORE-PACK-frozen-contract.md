# CONTRACT STORE-PACK — truthful store and Shipaton prescreen kit

Frozen on 2026-08-10 at `origin/main` `9be8f95`, after fetching `origin/main`, as the
first commit on `docs/store-pack`.

The Wave 2 addendum was not on main at freeze. Its controlling Lane 7 text was read from
`origin/session/wave2-addendum` at `c1d78cc` (PR #69), including the unchanged global
parallel-lane rules.

## Restated contract

Produce a documentation-only STORE-PACK that an art lane can execute without guessing and a
prescreener can trust without installing the app:

1. Google Play title, short description, long description, and an ASO keyword set derived from
   `docs/plan/specs/growth_aso_plan.md`.
2. A 1024×1024 icon brief and exact 1179×2556 portrait screenshot shot list keyed to the
   committed tabletop-diorama golden frame. Every screenshot row names the scene, level, game
   state, HUD state, framing, action, caption, and evidence gate.
3. A Devpost video script that stays below two minutes, opens on real on-device gameplay, names
   the targeted categories inside the runtime, uses only original game audio, and never substitutes
   a mockup for a build claim.
4. A Devpost description draft and a #BuildInPublic post-series plan built around the project's
   actual evidence, including failures and denominators.

The positioning spine is exact and must appear verbatim wherever the full promise is used:

> Fair by design: no forced ads, no energy, no loot boxes, every level solvable free.

## Authoritative inputs

- `state/PROJECT_STATE.md` and
  `state/handoffs/PARALLEL-PUSH-2026-08-09.md`, including the Wave 2 addendum.
- `docs/plan/specs/growth_aso_plan.md`, especially §§1, 3–5, 7–8, 18–23.
- `docs/plan/specs/submission_script.md`, `docs/plan/DECISIONS_BRIEF.md`,
  `docs/plan/FINAL_REPORT.md`, and `docs/plan/EXECUTION_PLAN.md` judging-funnel notes.
- `docs/plan/specs/product_spec.md` §§7, 8, 10–12, 21–22, 26–29.
- `art/diorama-pass` `79195e0`, especially
  `evals/results/ux/art-diorama-2026-08-09/gemini-tabletop-golden.png` and its evidence note.
- Current shipped-tree evidence on the frozen main anchor. Plans and sibling branches are not
  evidence that a player-facing feature exists.

## Truth baseline at freeze

These are the only positive product claims available without a later named evidence gate:

- The current tree contains ten handcrafted campaign levels, L001–L010. Each is run through the
  repository's content validation and solver gates.
- The implemented loop covers Home, LevelIntro, Playing, failure/retry, Won/Results, and Next;
  the first two levels have been won on a Pixel 9 Pro through the real next-level seam.
- Gameplay is one-thumb switch routing with color-and-symbol-coded cat commuters, a next-wave
  preview, queue/overflow behavior, cause-focused failure presentation, and immediate retry.
- The app currently contains no forced-ad, energy, or loot-box system. No monetization code may be
  described as present merely because its specification or an active lane exists.
- The golden frame is a committed visual target. Lane 1A's evidence explicitly says final corrected
  on-device art evidence is still open, so player-facing copy may describe the tabletop premise but
  must not say that an uncaptured frame is what the current public build looks like.

Not built on the frozen anchor and therefore forbidden as present-tense product claims: 30 launch
levels, Daily Line, Daily streaks, District Cup, level select, shop, IAP, RevenueCat paywalls or
placements, rewarded ads, premium themes, share cards, challenge links, OneSignal journeys, public
Google Play availability, published launch metrics, or a completed 56-day public-post corpus.

## Deliverable map

| File | Single responsibility |
|---|---|
| `docs/store/play-store-listing.md` | Paste-ready Google Play copy plus field limits and claim gates |
| `docs/store/aso-keywords.md` | Prioritized query set, placement map, exclusions, and iteration rules |
| `docs/store/creative-shot-list.md` | 1024 icon brief and 1179×2556 capture instructions for the art lanes |
| `docs/plan/marketing/devpost-video-script.md` | Time-coded sub-two-minute on-device video script and capture manifest |
| `docs/plan/marketing/devpost-description.md` | Prescreen-first Devpost draft, category map, and final evidence substitutions |
| `docs/plan/marketing/build-in-public-series.md` | Planned post arc, evidence requirements, cadence, and fallback posts |
| `docs/plan/marketing/claim-ledger.md` | Cross-pack source/evidence status for every load-bearing public claim |

## Acceptance criteria

1. **Listing constraints are exact.** Title is at most 30 characters; short description is at most
   80; long description is at most 4,000. Counts are recorded beside the copy. The title retains
   the growth plan's primary `train puzzle` lane. No competitor brand is used as a keyword.
2. **ASO is a set, not stuffing.** Terms are ranked P0/P1/P2, mapped to indexed fields or visual
   discovery, and paired with exclusions. Metrics and competitive facts retain their 2026-07-31
   vintage. Future listing experiments are plans, never reported results.
3. **Creative can be produced on demand.** The icon is specified at 1024×1024 with a 512-safe crop,
   48 px legibility check, no text, no child-directed treatment, and palette values from product
   spec §7. Each screenshot is an exact 1179×2556 frameless render, never an upscale, with scene,
   level, HUD state, capture moment, safe text region, and a fallback take.
4. **The golden frame governs the look.** Shot direction calls for the low three-quarter tabletop
   view, visible warm wood desk edge, Cream Card/Warm Paper board, Ink Navy rail detail, restrained
   Ticket Orange, track-scale cats seated in open cars, color plus symbol, soft authored contact
   shadows, and desk-margin props. It does not copy the golden frame's invented HUD or treat the
   reference image as a gameplay screenshot.
5. **The Devpost cut survives prescreen.** Target runtime is no more than 1:55, leaving export
   margin below the strict two-minute cap. The pitch, real app on a named device, and explicit
   targeted-category card all land before the end. Every visual beat names its source capture and
   has a truthful fallback if its preferred feature is not evidenced.
6. **Devpost prose is evidence-first.** It distinguishes what is built now, what is actively being
   prepared, and what must be replaced with final launch metrics. No placeholder is phrased as a
   result. No rate appears without a denominator; no benchmark appears without a vintage.
7. **#BuildInPublic is a plan, not a fabricated history.** Each post has a publish trigger, receipt,
   truthful draft shape, and skip/substitution rule. Drafts never imply they were already posted.
8. **Honesty audit is explicit.** The claim ledger classifies every public claim as `VERIFIED`,
   `BLOCKED`, or `FUTURE COPY`. Only `VERIFIED` language may be pasted unchanged. A blocked claim is
   deleted or rewritten in future tense if its evidence does not arrive.
9. **Repository gates remain green.** Run `git diff --check`, listing-count checks,
   `bash scripts/check.sh`, `bash scripts/test.sh`, and `bash scripts/build.sh`; then obtain the
   independently priced review required by `scripts/forge-risk.sh`.

## Assumptions and explicit dispositions

- **A-SP-1 — docs only means briefs, not image generation.** This lane specifies the icon and
  screenshots; art lanes produce raster assets from a merged, device-verified build.
- **A-SP-2 — current-safe copy wins.** The July growth plan is strategic source material, not proof
  that its launch scope exists. Its 30-level, Daily Line, commerce, rewarded-ad, theme, social, and
  launch-result paragraphs are rewritten or claim-gated.
- **A-SP-3 — exact portrait master.** The requested 1179×2556 format is used for every hero still so
  the same truthful capture can serve the frameless Devpost requirement and be downsampled for other
  placements. No upscaling is allowed.
- **A-SP-4 — no invented district art.** L006 `Alternating Line` is the preferred hero because it is
  built and has red/blue alternation, eight commuters, a live switch, queues, and preview activity.
  If the merged art/UI build cannot stage a clean L006 frame, use L005 or L002 and adjust the caption;
  do not fake Harbor scenery or a second mechanic.
- **A-SP-5 — no publication side effects.** This lane drafts posts and submission fields only. It
  does not publish, upload, contact communities, create store experiments, or submit to Devpost.
- **A-SP-6 — state update timing.** Per the Wave 2 global rule, the one Lane 7 state row is a merge
  closeout act. This branch touches only its exclusive docs paths before HC-25.

## Scope and stop conditions

Owned before merge: new files under `docs/store/**` and `docs/plan/marketing/**` only.

Forbidden: game or tool code, tests, existing plan/spec files, generated raster assets, store or
Devpost publication, external messages, metrics invention, sibling-lane state, and any feature claim
whose evidence is only a plan or unmerged implementation.

Stop and surface the issue if a required capture cannot be made from a real build, if the latest
official form contradicts the recorded asset constraints, or if a truthful rewrite removes the core
mechanic/fairness position. Never fill the gap with a mockup or an unqualified future feature.

HC-25 remains closed: pushing and opening a PR are allowed, but no merge may be armed or completed
without the human's fresh in-chat word for this lane after gates and independent review are complete.
Because this lane touches `docs/plan/**`, the Constitution's Amendment 1 also leaves the actual merge
human-only.

## Status log

- 2026-08-10 — contract frozen; drafting and evidence audit are next.
