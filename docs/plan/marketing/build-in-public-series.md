# #BuildInPublic series — Aug 10 through Shipaton submission

Status: **PLANNED — no row below is evidence that a post was published.** Publishing, replies, and
community contact remain human actions. The series begins on 2026-08-10; it does not backdate a
Day-1 post or imply that an earlier daily streak exists.

Planning basis: an initial Devpost submission is intended around 2026-09-15 and may be edited until
the 2026-09-30 11:45 p.m. PDT freeze. The cadence is approximately two evidence-led posts per week,
with an extra post only when a real incident or milestone earns one. Missing a slot is preferable to
manufacturing a receipt.

Binding disposition: PRD CM-R56 requires one post on every calendar day of the original 56-day
window and requires its unchanged four-metric fun gate in post 1 before tester data exists. Aug 1–9
cannot be recovered or backdated. This fifteen-slot plan is an honest salvage log; it does **not**
clear CM-R56 or the #BuildInPublic category unless a human formally amends that criterion. Actual
posts and skipped days remain visible either way.

## Rules for every post

1. **Receipt before prose.** A post becomes publishable only after its named trigger and receipt both
   exist. A branch, roadmap, mockup, or scheduled date is not a product receipt.
2. **Numbering starts at publication.** If the Aug 10 draft is the first public post, it is post 1 of
   this series. Never call it Day 10 of a streak or count planned slots as published posts.
3. **Rates expose their shape.** Write raw numerator and denominator before a percentage, define the
   cohort and exact date range, and say when a cohort is incomplete. One device or one player remains
   one device or one player.
4. **Benchmarks keep their vintage and population.** In particular, the GameAnalytics 2025
   D1/D7/D30 figures are all-genre medians. Do not relabel them as puzzle benchmarks. Do not use a
   benchmark unless its source and vintage are recorded beside it.
5. **Build claims name provenance.** Content and test counts name the commit; footage names the build,
   device, and capture date. Sibling branches and design targets are preparation, not shipped scope.
6. **Store states are not synonyms.** Closed test, production access, submitted for review, held by
   managed publishing, and publicly available are reported as distinct states.
7. **Drafts stay drafts.** The “draft shape” below describes what to write after the trigger. It is
   not copy that claims publication or a future result. No receipt means use the stated substitute or
   skip.
8. **Archive the public act.** After a human publishes, record the public URL, timestamp, channel,
   source receipts, and any substantive replies. Report engagement as raw counts with the observation
   time; do not infer sentiment or quote a rate without impressions as its denominator.
9. **Scan before publication.** Inspect text, images, video frames, and linked public manifests for
   promo codes, credentials, account identifiers, tokens, private URLs, and other secrets. Redact
   private material without changing a claimed count or state; a judge-only code never appears in a
   BIP post or screenshot.

## Planned post arc

### 1. Aug 10 — the honest starting line

- **Publish trigger:** Frozen-anchor facts at `9be8f95` and the separate 2026-08-09 Pixel play record
  are reconciled without presenting them as the same build. For any prospective-gate language, no
  observation has begun in the named window and the human has recorded how the missed original
  window and currently unavailable authored failure path are dispositioned.
- **Required receipt:** The frozen STORE-PACK contract, the source anchor, and the dated state entry
  recording L001 and L002 won on a Pixel 9 Pro with L003 loaded through Next, including the
  `b591f46`-plus-untracked-shim and unattested-binary caveats. Also retain the prospective window,
  pushes-disabled check, event definitions, named outside confirmer, and the human's gate
  disposition; figures must come from a client-authoritative event stream with 12 known testers.
- **Truthful draft shape:** Open with “I am starting the public build log today.” State that the tree
  contains ten authored and validated level files but normal progression exposes five, describe the
  one-thumb switch loop, give the limited Pixel result, and name the two open presentation gaps:
  final device art and final submission capture. Close with one falsifiable next receipt rather
  than a release call to action. If a new observation window is authorized and still prospective,
  include the source gate verbatim in this first post: (i) ≥6/12 testers open the app unprompted on a
  second calendar day during D5–D7, pushes disabled; (ii) ≥4/12 replay an already-**won** level
  (`level_started` with attempt>1 on a completed level — excludes fail-retries by construction);
  (iii) median session ≥3 levels; (iv) quit-without-retry after failure <50%. Use the unchanged
  decision rule: **YELLOW** (2 of 4 metrics missed) = 48h mechanic surgery + re-gate D9; **RED** (3+
  of 4, or metric (i) alone) = execute the Plan-B runbook. A changed metric, denominator, window, or
  rule requires a recorded human amendment.
- **Denominator/vintage rule:** Say one player, one Pixel 9 Pro, and two completed levels for the old
  device session. For the prospective gate, label 12 as known planned testers, name the exact future
  window, and preserve the client-authoritative-event-stream provenance; do not turn either receipt
  into an observed completion, retention, or compatibility rate.
- **Substitute/skip rule:** If no public-safe image exists, publish text with repository receipts or
  skip the image. Never use the golden art target as if it were a gameplay capture. If observations
  already began, state that preregistration was missed and that later data cannot be labeled
  preregistered; do not backdate the gate or invent a three-metric replacement.

### 2. Aug 12–14 — handcrafted levels, two gates

- **Publish trigger:** A fresh validator-and-solver run succeeds for every staged level present at
  one named commit.
- **Required receipt:** Unedited command output, the commit hash, and a complete content census for
  that same commit. At the frozen anchor, the census is L001–L010.
- **Truthful draft shape:** Show one compact level artifact, then explain the order: handcrafted
  content first, validation and solver checks second, eligibility for player-flow integration only
  after both. State separately how many levels the named build actually exposes. End with what a
  gate can prove and what it cannot prove about fun or reachability.
- **Denominator/vintage rule:** At `9be8f95`, report ten of ten. If the named commit has changed,
  report the exact passing count over its complete current census instead. Do not extrapolate the
  pass to planned levels, other builds, or player enjoyment.
- **Substitute/skip rule:** If either gate fails, replace the success post with the exact failing
  stage and its disposition. If the log cannot be shared safely, skip rather than paraphrase a pass.

### 3. Aug 15–17 — the core loop on a real phone

- **Publish trigger:** A public-safe recording or still sequence reproduces the core path on a named
  Pixel build.
- **Required receipt:** Build provenance, device model, capture date, and footage showing play plus
  Won/Results/Next without an Editor substitute. Add failure/retry only after a separate authored-
  level reachability receipt exists.
- **Truthful draft shape:** Walk through the visible sequence—tap a junction, read the next wave,
  finish, then continue. State explicitly which limbs the clip does and does not demonstrate.
- **Denominator/vintage rule:** Treat this as one device/build observation. Quote timing only if the
  clip or profiler supplies the measured interval.
- **Substitute/skip rule:** Use a dated still sequence if video capture fails. Skip if only an Editor
  simulation or an untraceable APK is available.

### 4. Aug 18–20 — visual target versus device reality

- **Publish trigger:** The committed tabletop golden frame and a provenance-backed current device
  frame can be shown side by side with their roles unmistakably labeled.
- **Required receipt:** Golden-frame commit and evidence note, current build hash, device/capture
  details, and a short list of visible gaps or resolved deltas.
- **Truthful draft shape:** Label one image “visual target” and the other “current device build.”
  Explain one art decision, one mismatch, and the next acceptance check. If corrected art has merged,
  say exactly what changed; do not silently relabel the target as the result.
- **Denominator/vintage rule:** Avoid subjective percentage-complete claims. Count only inspected
  frames or named acceptance checks when those totals are available.
- **Substitute/skip rule:** Without a traceable device frame, publish a text-only process note that
  says final device evidence is open, or skip the slot.

### 5. Aug 21–23 — a failure with a receipt

- **Publish trigger:** A real post-Aug-10 defect or failed gate has a reproducible observation and a
  recorded fix, decision, or still-open status.
- **Required receipt:** Issue or log, reproduction steps, affected build, before/after evidence when
  fixed, and the discriminating test or validation output.
- **Truthful draft shape:** Tell the sequence in four beats: what broke, the first hypothesis, what
  the evidence changed, and the current disposition. If unresolved, end unresolved and ask one
  concrete question.
- **Denominator/vintage rule:** State reproduction attempts and affected devices/builds as raw
  counts. Never call an intermittent problem fixed from one successful retry.
- **Substitute/skip rule:** If no qualifying new incident exists, use a clearly dated retrospective
  about a merged earlier failure and say it predates this series. Otherwise skip; do not invent a
  crisis for cadence.

### 6. Aug 24–26 — the latest content census

- **Publish trigger:** Either new level content has merged and passed both gates, or the window
  arrives with the frozen count unchanged.
- **Required receipt:** Exact merged commit, fresh content census, validator output, and solver
  output.
- **Truthful draft shape:** Compare the authored/validated count with the ten-file Aug 10 baseline
  and report the normal-progression count separately. If either grew, show one concrete change. If
  neither changed, explain the blocker or deliberate tradeoff without converting planned content
  into player-reachable scope.
- **Denominator/vintage rule:** Count only levels present and gated at the named commit. State
  duplicates, exclusions, or known caveats rather than hiding them inside a total.
- **Substitute/skip rule:** An unchanged-baseline post is acceptable once if it teaches a real scope
  lesson. Skip if it would merely repeat post 2.

### 7. Aug 27–29 — fun-gate readout or missed-gate disposition

- **Publish trigger:** Either a valid post-1 preregistration predates every observation in its named
  window and that window has closed, or the human has recorded that the original gate was missed,
  blocked, or formally amended.
- **Required receipt:** Post-1 URL and timestamp, enrollment and exclusion log, observation dates,
  event definitions, pushes-disabled check, named outside tally reviewer, and any human amendment.
  A valid run retains all four locked metrics and the 2-of-4/3-of-4 decision rule.
- **Truthful draft shape:** For a valid run, report each preregistered raw result, then the locked
  YELLOW/RED verdict and decision. If the authored failure path or preregistration was missing, state
  that the four-metric gate could not be graded and name the human disposition; never drop metric
  (iv), recompute the verdict over three metrics, or call the account prospective after the fact.
- **Denominator/vintage rule:** Use the actual eligible cohort in results while retaining the planned
  12-tester target, exact window, and client-authoritative-event-stream provenance. Define every
  denominator and exclude fail-retries from the replay count.
- **Substitute/skip rule:** Without a valid preregistration or human disposition, skip. A later post
  may describe the protocol only as retrospective methodology, never as a recovered gate.

### 8. Aug 31–Sep 3 — playtest result or data-quality lesson

- **Publish trigger:** The prospective window closes and the outside reviewer confirms the raw
  tally, or a documented data-quality problem prevents a verdict.
- **Required receipt:** De-identified raw counts, eligibility/exclusion log, exact date range,
  instrumentation version, and reviewer confirmation.
- **Truthful draft shape:** Report the predeclared bar, raw result, verdict, and resulting decision in
  that order. If the data is invalid or incomplete, make the invalidation and what will change the
  story rather than forcing a pass/fail.
- **Denominator/vintage rule:** Use actual eligible testers in every result even if it differs from
  the planned cohort. Show numerator and denominator beside each rate; do not compare to an unrelated
  benchmark.
- **Substitute/skip rule:** If the window is not mature, publish only the reason it is not mature or
  skip. Do not publish preliminary retention as final.

### 9. Sep 4–7 — failure should teach

- **Publish trigger:** A real-build capture clearly shows queue/overflow failure, cause-focused
  presentation, and immediate retry.
- **Required receipt:** Build and device provenance, uncut source clip, and the level/state used.
- **Truthful draft shape:** Show the failure once at normal speed, point to the cause the game
  surfaces, then show the retry. Explain the design lesson: losing should reveal the decision to
  revisit, not merely display a score.
- **Denominator/vintage rule:** Make no “instant” or sub-second claim unless the footage supplies a
  measured interval. This is a design exhibit, not player-success data.
- **Substitute/skip rule:** Use annotated real-build stills if motion capture is unclear. Skip if the
  cause cannot be read without adding mock UI.

Freeze note: no L001–L010 path satisfies this trigger on `9be8f95`; synthetic T904 fixture coverage
is not a public receipt. This slot is therefore blocked unless a later candidate supplies the named
authored-level path.

### 10. Sep 8–11 — building the prescreen evidence pack

- **Publish trigger:** At least one candidate Devpost still and one candidate video cut come from the
  same traceable real build.
- **Required receipt:** Capture manifest, exact image dimensions, video runtime, build hash, device,
  date, and provenance for narration plus every included sound; silence needs no invented bed.
- **Truthful draft shape:** Explain that prescreeners may rely on text and the first two minutes, then
  show how one claim maps to one visual receipt. Label every candidate as work in progress until the
  final export passes.
- **Denominator/vintage rule:** Report file dimensions and measured runtime exactly. View counts or
  audience response are not relevant evidence at this stage.
- **Substitute/skip rule:** If capture is blocked, publish the checklist and the blocker without
  showing a mockup as an app frame, or skip.

### 11. Sep 12–14 — category scope, with omissions

- **Publish trigger:** The current build and evidence ledger have been checked against every
  category-specific question visible in the live Devpost form.
- **Required receipt:** Dated form capture, claim ledger, qualifying integration receipt, and the
  exhibit list for every category retained.
- **Truthful draft shape:** Name only categories with complete evidence and explain one deliberate
  omission. On the frozen Aug 10 evidence, only Best Game is supportable, while the committed
  CM-R57.2 slate remains a final-submission blocker. Do not present this current evidence subset as
  an authorized final slate; require every committed category or a recorded human amendment.
- **Denominator/vintage rule:** Do not use prize size or number of categories as proof of fit. Any
  supporting result follows the global raw-count and vintage rules.
- **Substitute/skip rule:** If the live form is unavailable, publish the current Best-Game-only
  rationale or skip. Never guess a category question or leave a targeted answer blank.

### 12. Around Sep 15 — initial Devpost submission

- **Publish trigger:** Devpost confirms a public, editable submission URL and every required field is
  complete; the store URL, package name, and qualifying integration match the downloadable app; and
  the category set matches CM-R57.2 or its recorded human amendment.
- **Required receipt:** Public Devpost URL, timestamped confirmation, submitted category list and
  answers, store/package verification, and current asset manifest.
- **Truthful draft shape:** Say the submission is live and remains editable, list only the categories
  actually entered, and name the next evidence update. Do not turn “submitted” into a public-store
  claim unless the store receipt independently proves that state.
- **Denominator/vintage rule:** Report only the actual category count and current evidenced build
  census. Do not attach launch metrics unless their cohorts and date ranges are ready.
- **Substitute/skip rule:** If submission is blocked, publish the exact blocker and revised decision
  point without linking a private draft. Skip any “we submitted” wording.

### 13. Sep 17–20 — first evidence-backed revision

- **Publish trigger:** One material build, store, device, or dashboard fact changes after the initial
  submission and the Devpost text is updated to match.
- **Required receipt:** Before/after description excerpt, new source receipt, edit timestamp, and the
  still-current build/package identity.
- **Truthful draft shape:** Lead with what changed in the evidence, then show the sentence that was
  added, narrowed, or deleted. Treat a deletion prompted by missing proof as a useful lesson.
- **Denominator/vintage rule:** Preserve the original observation window when comparing numbers; do
  not combine cohorts or silently refresh a denominator.
- **Substitute/skip rule:** Skip if no material fact changed. An editorial rewrite alone is not an
  evidence update.

### 14. Sep 23–26 — the prescreen cut

- **Publish trigger:** The public video is at most 1:55, opens on real on-device play, states the
  targeted categories, uses original narration plus silence by default and only evidenced original
  in-game audio if present, and matches the submitted build, Play production `versionCode`, and
  description.
- **Required receipt:** Public video URL, file runtime, capture manifest, category card, device/build
  identity, visible-code or paired-screenshot parity receipt, provenance for narration and every
  included sound, and a completed text-video-app consistency check.
- **Truthful draft shape:** Describe the constraint of telling the whole evidenced story in under two
  minutes and identify one planned claim that was cut because its receipt was not ready.
- **Denominator/vintage rule:** Give the measured runtime and count of targeted categories only.
  Treat public views as a timestamped raw count, not success evidence.
- **Substitute/skip rule:** If the cut is not final, publish a truthful reshoot/failure note or skip.
  Never call a draft export the submitted video.

### 15. Sep 30 — final freeze and honest retro

- **Publish trigger:** Devpost shows the final saved submission before 11:45 p.m. PDT and the public
  post index has been reconciled.
- **Required receipt:** Final submission confirmation and timestamp, final description/video/store
  manifest, every public post URL from this series, and the list of planned slots skipped.
- **Truthful draft shape:** Report what the submitted build demonstrably contains, what was cut or
  remained preparation, the most useful failure, and the actual public-post count. Link the evidence
  index; do not rewrite the series as uninterrupted.
- **Denominator/vintage rule:** Report published posts out of the fifteen planned slots, with skipped
  slots visible, and separately state that this total does not equal the original 56/56 criterion.
  Launch or conversion rates require raw counts, cohort/date range, and benchmark vintage under the
  global rules.
- **Substitute/skip rule:** If the entry is not submitted, publish a non-submission retrospective only
  after the human makes that call. Never imply a completed entry from an unfinished draft.

## Distribution and engagement discipline

- Use `#BuildInPublic` and `#Shipaton` on each primary public post. Optional HackerNoon recaps,
  sanctioned Shipaton check-in threads, or Discord sharing happen only after the original post has
  a public URL and a human verifies the destination’s current rules.
- Ask one specific question when feedback could change a decision. Record substantive replies and
  the resulting action; “engagement” means the conversation and lesson, not an inflated impression
  count.
- A weekly recap may combine already-published posts, but it is a new public artifact only after it
  receives its own URL and archive receipt. Republishing does not multiply the underlying product
  evidence.
- The final #BuildInPublic category claim uses the actual archive produced by this plan. It never
  claims a daily streak, a launch history, or a post total that the URL ledger cannot reproduce.
  Without a human amendment, the missed 56/56 criterion keeps that category blocked even if all
  fifteen remaining slots publish.
