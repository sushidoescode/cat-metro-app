# CM-C3 — build-loop handoff note (session 2026-08-04/05, post-CM-C2b)

**Frozen contract:** `state/handoffs/CM-C3-frozen-contract.md` — verbatim copy of the
recut-stamped CM-C3 (11 criteria) from state/backlog.md, taken at anchor on
`task/CM-C3-fail-retry` off main @ 93a2f52 (CM-C2b merged #21).

## Build plan (sprint pricing, TDD)

Criterion order:
1. **11 (wave preview HUD)** — independent of the fail path: top-band strip, next TWO waves'
   colour+count, zero interactive elements, updates as waves are consumed. PlayMode: 4 asserts.
2. **9 (retry determinism)** — the keystone, engine-free half first: retry = a FRESH
   GameSession over the SAME ImportedLevel (re-simulation from tick 0, ADR-0002 §9; NO snapshot
   format). EditMode/Pure: post-retry replay hash == fresh-entry hash for the identical command
   sequence (ReplayHasher). PlayMode: switches == initialRoute, log empty, Tick == 0.
   **Stop condition 7 armed: a hash mismatch is a retry-path defect — stop; the contract
   golden directory is never touched.**
3. **1 (cause camera)** — causal node derived FROM STATE at the fail tick (A-C3-1 confirmed:
   the outcome carries Kind+Reason only): QueueOverflow → the node whose OverloadTimers hit 0;
   TimeOut → largest queue at fail tick, ties to lowest node id (A-C3-2, Q-K, unratified —
   named in the PR). PlatformOverflow: TEST-ONLY constructed presentation outcome under
   the test tree + the [CI] grep (with negative fixture) banning any shipped
   `FailReason.PlatformOverflow` construction in Presentation/Application.
4. **6/8 (retry input + no scene reload)** — Try again hit-testable frame 1 of FailureReview;
   scene load count delta == 0, same handle. GATE NOTE: CM-C2b's second-input-surface wrapper
   ban gets the gate-follows-contract carve-out for the Retry surface ONLY (the tap handler
   routes through the same one-gesture discipline — Try again is a tap on a full-width thumb
   band region, implementable through TapInput's screen-region path, keeping ONE handler).
5. **3/4/5 (motion legs)** — motion source = toggle stub OR ANIMATOR_DURATION_SCALE == 0
   (A-C3-3, no save field): off → one-frame cut + static ring (alpha > 0, zero clips);
   on → >1-frame interpolation; information parity across states (parameterised test).
6. **2/7 (budgets, CI legs)** — p95 over 20 scripted failures from the FrameLog (single named
   clock): cause visible ≤1500 ms; tap→Playing <1000 ms. Editor legs = the CI gate with raw
   tables attached to the PR; the low/mid-tier DEVICE legs are HUMAN-VERIFIED artifacts
   (criterion fails if absent — they stay open like CM-C2b criterion 8). Stop condition 8
   honoured: editor numbers never satisfy the device legs.
7. **10 (fail strings)** — append-only ui.csv rows for the remaining two LOCKED strings with
   {node}/{station} substitution; zero literals; the PlatformOverflow string case via the
   test-only outcome.

## Session freezes

- **A-C3-7 (session):** the F9 clock note from CM-C2b's round lands here — the retry/cause
  budget measurements read the FrameLog's monotonic clock exclusively; sim advancement clamps
  are irrelevant to the two budgets (both measure UI latency, not tick rate).
- **A-C3-8 (session):** "FailureReview" in greybox = a screen-state string + the banner + the
  Try again surface + the camera framing — no UGUI screen-stack chrome yet (ADR-0007's stack
  arrives with polish; the criteria test state, hit-testability and timing, all satisfiable).

## Status log
- anchor: branch cut off main @ 93a2f52; contract frozen; this note committed.
- keystone: CauseAttribution (A-C3-1 state-derivation; A-C3-2 Q-K rule) + the criterion-9
  hash-equality law + both attribution rules driven by REAL Domain runs — 330/330 dotnet,
  first try.
- presentation: CauseCameraController (motion-off = one-frame cut + static ring, zero clips
  anywhere; motion-on = MoveTowards pan to the identical framing) · WavePreviewStrip (top-band,
  collider-stripped, consumption = last-emission-passed) · reason-keyed banner with
  ShowKeySubstituted ({node}/{station} tokens; components stay literal-free) · retry through
  the ONE gesture handler (thumb-band region during FailureReview → fresh GameSession over the
  same ImportedLevel; view/preview rebuilt; zero scene loads).
- PlayMode 19/19 FIRST RUN (9 CM-C2b + 10 CM-C3 incl. the parameterised motion pair and the
  20-iteration budget harness). Editor-leg budgets: CAUSE_P95=1ms, RETRY_P95=1ms (tables in the
  PR). tests/unity/failure.test.sh statics green (PlatformOverflow construction ban + negative
  fixture; the three LOCKED rows; zero-literal grep).
- OPEN (human, by design): criterion 2/4/7's low+mid-tier DEVICE tables — same session as
  CM-C2b criterion 8's artifact. Q-K (A-C3-2 TimeOut rule): ratified by the human in-session 2026-08-05; recorded by the phases-6-10 agent.
