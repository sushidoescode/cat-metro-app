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

- 2026-08-06 green: local-executor lane delivered the transcription in 7 turns (328/328 filtered; check_cmd-verified). Wrapper OK (7, 8-static, 9, 11-static, 13 — one construction site confirmed in Taxonomy.cs, dark set locked, bound literals absent). Hybrid run log committed as provenance. Meta generation + full N+1 suite + fresh-context review next.

- 2026-08-06 review round 1 (PR #37): NOT-mergeable — B1 three factories wrote user_properties-column tokens as params (closed-set wall correctly rejected them; the factory swallowed the failure: silent nameless events); B2 two required params exposed as optional args; B3 my criterion-12 test could not catch a default event (5 nameless records were being Logged while green); B4 my CanonicalArgsFor resolved domains across rows; B5 TryBuild aliased the caller JObject (post-validation mutation could smuggle keys past the wall); B6 >0-gated optionals could not express zero; B7 JSON-null passed the required check. ALL FIXED: factories corrected to the CSV columns; nullable optionals (recorded assumption: absence means not-instrumented, null-arg-for-required yields the swallowed default per the facade pattern and criterion 12 now asserts identity pre-Log); DeepClone at construction; null-token = missing + test; row-scoped canonical args; byte test uses the real {id,ord,name,params} shape (L2); L1 recorded: the one construction site is Taxonomy.cs (the builder), not Events.cs — functionally stronger than the frozen table row, visible drift note. 329/329.
