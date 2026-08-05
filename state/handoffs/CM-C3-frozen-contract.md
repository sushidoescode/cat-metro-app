# CONTRACT CM-C3 — Fail/retry loop: cause-first camera, sub-1s retry, next-wave preview

**Roadmap:** D4, `docs/plan/data/roadmap_56_days.csv:5`.
**DEPENDS-ON: CM-C2b merged** *(changed by the tranche-2 split — it was "CM-C2 merged"; the render,
input and `FrameLog.cs` deliverables CM-C3 measures against all live in the CM-C2b half).*
**Recut 2026-08-04: Q-G resolved (#19); reviewed against everything landed through CM-C8 — the
contract stands as written. Notes: A-C3-3's motion source is unchanged (no settings screen exists;
read the toggle stub + `ANIMATOR_DURATION_SCALE`); `ui.csv` ownership split (C2b creates, C3 appends)
unchanged; criteria 2/4/7's device legs remain HUMAN-VERIFIED with artifacts. TG disposition
(evaluator D11): greybox ships no palette, so TG-1 (board readability gates) does not bite until the
art pass; TG-3/6/7/8 are post-greybox; TG-2/4/5 already appear as CM-C3 non-goals/stop conditions.**

### Goal

A failed run reframes the board on the node that caused the failure, a single tap returns the player to
`Playing` in under a second without a scene load, and the HUD shows the next two waves before they
arrive.

### Spec reference

`docs/prd/PRD.md` CM-R03.2 · CM-R15 (**D4 subset only**) · CM-R16.1–.3 · CM-R17.1 · CM-R22.3 ·
`docs/prd/ux-flows.md:43,188,254,258-270,287,290` · `docs/adr/0002-*` §9 (retry re-simulates from
tick 0; no snapshot format) · `docs/adr/0007-*` (screen stack, **not** scene loads; motion/haptics).

### Acceptance criteria (11) — unchanged from tranche 1

1. **Cause camera targets the failing node.** On `Failed(reason)` the camera's target equals the node
   id that raised the failure, asserted from the camera controller state. *Check:* one PlayMode test
   per reason — `QueueOverflow` and `TimeOut` driven by a **real Domain run** to the fail tick;
   `PlatformOverflow` **not raisable while Q-J/NEW-Q4 are open** (`Outcomes.cs:40-42`), so driven by a
   **constructed presentation-level outcome**, asserting framing only, with the PR recording that no
   Domain run reaches this state. **That constructed outcome type is test-only and lives under
   `unity/Assets/Tests/**`** — it may never become a shipped parallel outcome type carrying
   `PlatformOverflow` through Presentation, which is exactly the semantics the pin exists to keep out
   of the tree. *Also checked:* a `[CI]` grep asserting **no** type under
   `unity/Assets/Scripts/Presentation/**` or `unity/Assets/Scripts/Application/**` constructs a
   `FailReason.PlatformOverflow` value, with a negative fixture proving the grep fires. Criterion 10's
   `PlatformOverflow` string case uses **the same** test-only type and is bound by the same grep.
   For `TimeOut` the target is **the node with the largest queue at the
   fail tick, ties broken by the lowest node id** — **analyst-authored (A-C3-2), unratified, Q-K**.
2. **Cause visible within 1.5 s.** Time from the fail tick to the frame in which the causal node is
   framed **and** the fail banner is rendered is **≤1500 ms**, p95 over 20 scripted failures
   (`roadmap_56_days.csv:5`), measured from CM-C2b criterion 3's frame log (single named clock source).
   Two legs: **CI gate** = the editor PlayMode measurement with the raw per-failure table attached;
   **HUMAN-VERIFIED** = the same protocol on a **low-tier and a mid-tier device**
   (`docs/prd/ux-flows.md:287`), same artifact requirement. The criterion **fails if the artifact is
   absent**. An editor-only measurement never satisfies it (stop condition 7).
3. **Motion-off is a cut plus a static ring.** With the motion toggle OFF **or**
   `Settings.Global.ANIMATOR_DURATION_SCALE == 0` (two independent cases), the camera reaches its final
   transform in **one frame** and a **static** ring renders on the causal node with alpha > 0 and zero
   animation clips playing (`docs/prd/ux-flows.md:43,254,290`). *Check:* two PlayMode tests.
4. **Motion-on pans and still meets the budget.** With motion on the camera interpolates (>1 frame) and
   criterion 2's budget holds under **both** legs. *Check:* one PlayMode test + the device artifact.
5. **No information is lost at motion-off.** Banner text, causal-node framing and ring are present in
   both motion states; the rendered information set is identical across them (ring vs pulse the only
   difference) (`docs/prd/ux-flows.md:290`). *Check:* one parameterised PlayMode test.
6. **Retry is one input, live from frame 1.** `Try again` is hit-testable on the **first** frame of
   FailureReview (`docs/prd/ux-flows.md:265`; CM-R16.2). *Check:* one PlayMode hit-test on frame 1.
7. **Retry under 1 s, measured.** Tap-down → first frame in `Playing` is **<1000 ms**, p95 over 20
   retries, from CM-C2b's frame log on the editor target, raw table attached (CM-R16.1). The
   **low/mid-tier device** repetition is **HUMAN-VERIFIED** with the same artifact requirement; the
   criterion fails if the artifact is absent.
8. **No scene reload on retry.** Scene load/unload count across a retry is **0** and the scene handle
   is unchanged (ADR-0007 §Navigation). *Check:* one PlayMode test on the load-counter delta.
9. **Retry restores tick-0 state and stays deterministic.** After retry every switch equals its level
   `initialRoute` (`LevelGraph.cs:32`; `SimulationState.cs:65`), the command log is empty,
   `state.Tick == 0`, and replaying the identical post-retry command sequence produces the identical
   replay hash as the same sequence from a fresh level entry (CM-R16.3; ADR-0002 §9 + CM-R01).
   *Check:* one PlayMode test + one `EditMode/Pure` hash-equality test. **If the two hashes differ,
   that is stop condition 7 — stop and report; never touch `tests/contract/`.**
10. **Fail strings render with substitution.** Each fail reason renders its LOCKED string with the
    node/station name substituted — `"Platform overflowed at {node}"` / `"{station} platform
    overflowed"` / `"The last train left the depot"` — read from
    `unity/Assets/Resources/Strings/ui.csv` (created by CM-C2b; CM-C3 **appends rows only**), with
    **zero literal strings in UI components** (CM-R03.2; `docs/prd/ux-flows.md:188`).
    *Check:* one test per reason (3) + a grep assertion. The `PlatformOverflow` case is driven by a
    constructed presentation-level outcome (Q-J).
11. **Next-wave preview HUD.** At tick 0 the strip displays the **next two waves'** colour and count,
    contains **zero** interactive elements, sits in the top 0–15% band, and updates as waves are
    consumed (CM-R17.1; `docs/prd/ux-flows.md:184`; CM-R07.4). *Check:* four assertions in one PlayMode
    test.

### Scope boundary

**In scope:** the paths in the ownership table for CM-C3, including its **own** wrapper
`tests/unity/failure.test.sh` and **append-only rows** in `ui.csv`.

**Explicit non-goals:** no rewind sheet/chip/eligibility (CM-R08) — and the invariant CM-C3 must not
break: on attempt 1 **no** paywall/ad surface may even be constructed (`docs/prd/PRD.md:208`); no
monetization of any kind; no ghost replay, blame chip, A-23 ambiguity predicate or
`ATTRIBUTION_MAX_RESIMS` re-simulation; no results-screen rollup (UX-OPEN-03 / TG-4); no scoring, stars
or tickets (pins NEW-Q5, NEW-Q7); no settings screen (reads motion state only); no planning pause
(TG-2); no edits to CM-C1 Domain sources, CM-C2a's importer or CM-C2b's board code; no `Compile
Include` append; no edit to an existing `ui.csv` row; no writes to immutable paths.

### Assumptions

- **A-C3-1** The Domain's failure outcome carries the failing node id. The shipped `SimOutcome`
  (`Outcomes.cs:22-45`) carries **`Kind` + `Reason` only — no node id.** *This is now a confirmed
  finding, not an open assumption:* criterion 1 must derive the causal node from the **state at the
  fail tick** (the node whose `OverloadTimers[n]` reached 0, `Simulation.cs:142-145`), not from the
  outcome. If that derivation is judged insufficient, it is a CM-C1 amendment → stop condition 1.
- **A-C3-2** `TimeOut` has no single causing node; criterion 1's rule is **analyst-authored and
  unratified — Q-K**. Overruling costs `Presentation/Camera/**` only.
- **A-C3-3** The motion state source is `(Settings motion toggle) OR (ANIMATOR_DURATION_SCALE == 0)`
  (`docs/prd/ux-flows.md:43`, PC-14). CM-C3 introduces no save field.
- **A-C3-5** "Instant retry" is re-entry to `Playing` by re-simulation from tick 0 (ADR-0002 §9), not a
  snapshot restore — no snapshot format exists and none may be created.
- **A-C3-6** The frame log is **CM-C2b's deliverable** (criterion 3). If absent or lacking
  `monotonicMs`/`simTick`, CM-C3 stops (stop condition 8) rather than writing a second clock source.

### Stop conditions

Defaults apply. Plus:
1. Criterion 1 requires the Domain to report a node id it does not report (**it does not — A-C3-1**) →
   stop before changing the Domain; the derivation-from-state route is the sanctioned path.
2. Any criterion appears to require ghost replay, blame chip or the ambiguity predicate → stop.
3. Any commerce/ad surface, placement fetch or entitlement check appears in the fail path → stop
   immediately (CM-R08.1 invariant + monetization tripwire).
4. The <1 s retry cannot be met without a scene load or a snapshot format → stop and report the
   measurement; do not weaken criteria 7 or 8.
5. TG-5 or TG-4 must be resolved to render a required string or CTA → stop and ask.
6. Motion-off behaviour would remove information (not just easing) to hit a budget → stop.
7. The post-retry replay hash differs from the fresh-entry hash → **stop and report**; never touch
   `tests/contract/` — a mismatch is evidence of a retry-path defect, not a stale golden.
8. **No device available to evidence criteria 2, 4 or 7** → hand those to the human as explicitly open;
   never mark a device-dependent budget met from an editor measurement. Likewise if CM-C2b's frame log
   is missing or single-clock-source cannot be shown.

---

