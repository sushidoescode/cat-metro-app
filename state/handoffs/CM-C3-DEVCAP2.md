# CM-C3-DEVCAP2 — status log

- 2026-08-06 red: solver API corrected; red 3-fail/2-pass. Demo v1 finding: no-input play WON —
  and the sim read (Simulation.cs:12-24,89-149) proves WHY: one mouth release per tick means a
  single-source depot drains at a fixed rate regardless of input, so QueueOverflow cannot be a
  player-skill failure in this topology (it is either unavoidable, as in the T901 double-flood
  fixtures, or unreachable). VISIBLE contract amendment: criterion 5(a) loss = TimeOut (slow
  default route vs the clock); burst waves keep the overload-ring tension, recoverable by design.
  Demo v2: fast route 6 ticks vs slow default 28; timeLimit 60; win.deliveries 13.

- 2026-08-06 green: override + boot hook implemented; 5/5 filtered + devcap scanner green over the new file. VISUAL LEG (criterion 5): five demo beats captured from the real scene camera (early/burst/crawl/FailureReview/post-retry, PNGs in session scratchpad; fail + retry frames coordinator-verified by eye — orange cause ring on the depot, LOCKED TimeOut banner "The last train left the depot", post-retry board pristine at rest pose). Probe deleted before commit. Full suite next, then PR + review round.
