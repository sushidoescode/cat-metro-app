# Benchmark 04 — seeded-defect reviewer audit (ready-made round 1)

Run `code-reviewer` (exactly as `/forge-review` would) on `diff.patch` with the frozen contract at the top of the patch. Then score recall/precision against `keys/04-key.md`.

**Blinding:** the reviewer must NOT read `keys/` during the run (state it in the review prompt; verify in the transcript afterwards — reads are visible). For stricter blinding on later rounds, keep new keys outside the repo per `../seeded-defects.md`. The `[D#]` markers exist only in this shipped teaching round so humans can discuss it; **rounds you author yourself must never mark the plants.**

Healthy: recall ≥ 4/5, ≤ 2 false positives, and the weakened/vacuous-test class caught. Log results to `../../results/`.
