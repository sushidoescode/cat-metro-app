# CM-C9 — status log

- 2026-08-06 loop start (post-restart session; merge delegation re-confirmed by the human at
  session start in-conversation, HC-25). Criterion-13 baseline captured BEFORE the wrapper is
  written: N=11 wrappers discovered on this branch's anchor by scripts/test.sh:18's own rule
  (find tests -name '*.test.sh'); the full-suite N/N green run is pasted at the merge gate.
  Target after tests/taxonomy/taxonomy.test.sh lands: N+1=12.
- 2026-08-06 red: fixtures parser + 3 test files + product stubs. dotnet leg: 327 cases,
  316 red / 11 green — the green 11 are CSV-side self-checks (46 lines, 124 required pairs,
  7 domains, bijection-helper negative), independently confirming the contract's counts.
  Green next: the 45-row table + TryBuild + 45 factories (local-executor lane, red tests as
  check_cmd; frontier review follows).
