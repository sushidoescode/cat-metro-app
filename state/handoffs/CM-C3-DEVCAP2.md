# CM-C3-DEVCAP2 — status log

- 2026-08-06 red: solver API corrected; red 3-fail/2-pass. Demo v1 finding: no-input play WON —
  and the sim read (Simulation.cs:12-24,89-149) proves WHY: one mouth release per tick means a
  single-source depot drains at a fixed rate regardless of input, so QueueOverflow cannot be a
  player-skill failure in this topology (it is either unavoidable, as in the T901 double-flood
  fixtures, or unreachable). VISIBLE contract amendment: criterion 5(a) loss = TimeOut (slow
  default route vs the clock); burst waves add queue pressure (peaks at 3 of 4 — review F1 corrected the earlier claim: the overload state never arms here, and NO overload ring is rendered anywhere in Presentation; CM-R02.5's countdown ring is spec'd but unbuilt — follow-up filed).
  Demo v2: fast route 6 ticks vs slow default 28; timeLimit 60; win.deliveries 13.

- 2026-08-06 green: override + boot hook implemented; 5/5 filtered + devcap scanner green over the new file. VISUAL LEG (criterion 5): five demo beats captured from the real scene camera (early/burst/crawl/FailureReview/post-retry, PNGs in session scratchpad; fail + retry frames coordinator-verified by eye — orange cause ring on the depot, LOCKED TimeOut banner "The last train left the depot", post-retry board pristine at rest pose). Probe deleted before commit. Full suite next, then PR + review round.

- 2026-08-06 review round 1 (PR #34): NOT-mergeable-by-agent — F1 the overload-ring claim was
  FALSE (queue peaks 3/4, ring never arms; and OverloadTimers has ZERO Presentation consumers —
  the CM-R02.5 countdown ring is unbuilt). Claim deleted in all four places. F2 the scanner's SYM
  regex does not name DevLevelOverride (probe-proven); wording corrected, SYM extension +
  CM-R02.5 ring + F9 retry-HUD one-tick dropout + F3 suite-level DirectoryOverride fixture filed
  as follow-ups for PROJECT_STATE debt at merge. Criterion-5(a) amendment routed to the HUMAN for
  the one-line ratification per the 4fbc57c pattern. Reviewer independently CONFIRMED the
  amendment's engineering core (1/tick drain, input-independent) by source read + a Domain trace
  (no-input: TimeOut at t60, 11/13 deliveries — margin is 2 ticks, knife-edge by design note F7).
- 2026-08-06 amendment RATIFIED (human, in-session, one line per the 4fbc57c pattern): TimeOut is criterion 5(a)'s loss. F1/F2 doc corrections landed pre-ratification. Merging.
