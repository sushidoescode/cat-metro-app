# Task S-101 — mention extraction

**Goal:** implement `extractMentions` so the provided test file passes.
**Spec:** a mention is `@` followed by 1–30 word characters (`[A-Za-z0-9_]`), case preserved; mentions inside backtick code spans are NOT mentions; result is de-duplicated in first-seen order.
**Acceptance criteria:** 1. All tests in `mentions.test.ts` pass unmodified. 2. No changes outside `mentions.ts`. 3. No new dependencies.
**Scope:** this directory only. **Stop conditions:** the test file appears wrong → stop and say why (do not edit it).
**Budget:** ≤ 15 turns. **Scoring:** evaluator applies the 4-axis rubric; Correct requires criterion 1 verified by an actual run (`npx vitest run` in this dir, or the project's test runner).
