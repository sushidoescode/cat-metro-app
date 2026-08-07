# CM-UX-07 — contract-delta audit (UX lane → device lane, 2026-08-07)

**Division of labor (human ruling 2026-08-07): the device lane BUILDS CM-UX-07 against its
frozen contract (c429ad0, anchor 32dbecb); the UX lane audits the freeze against the
post-#44 ledger and serves as the fresh-context REVIEWER of the PR.** This note is that
audit. Verdict: **the contract STANDS — build against it.** Three deltas arose from #44
(merged AFTER your freeze); none breaks a criterion; fold them as noted.

## D-1 (add one exclusion line): the two-modals-at-10 ordering law needs an owner

#44 (merged after your anchor) recorded: `LevelIntroSheet.PlayRegionPriority` and
`ResultsPanel.RegionPriority` are BOTH 10 with identical thumb-band rects — the modal-vs-modal
tie-break is registration order, the exact F3 mechanism one layer up (Next steals L002's Play
tap once both are live). Your Q-3 dormancy keeps this UNREACHABLE in CM-UX-07's build (panel
unattached ✓), so nothing in your criteria changes — but the obligation (durable ordering law:
parents 0 / modals 10 / topmost wins via stack-supplied offsets or an ADR line, PLUS a
modal-vs-modal co-registration test) is currently owned by nobody your contract names. Add it
to your EXCLUDED list with owner = **the LoadNext contract** (where the second modal goes
live). Source: #44 review F-2; the ledger line is in `state/handoffs/CM-UX-06.md`.

## D-2 (carry into your tests as a standing law): preconditions are ASSERTED, never `if`'d

#44 review F-1 (fixed in `ScreenCoRegistrationTests`): a positive control guarded by a
containment `if` silently drains a test's red-power when a layout change falsifies the
condition. Your criteria 4c/5/6 tests will have the same shape available — assert every
precondition unconditionally with a "precondition: … otherwise this test proves nothing"
message. This is now lane law, not preference.

## D-3 (cosmetic, fold or ignore): post-#44 wording

- Your EXCLUDED "F4 world-corners read-backs" line: the ledger now records that obligation as
  DISCHARGED for the Home pin + intro chip (#44) and still owed for `RetryCtaView`,
  `HaltVeilView`, `ResultsPanel`, `BannerView` — your exclusion stays right; the scope is just
  smaller than at your freeze.
- Your contract cites "audit M-3/M-4, S1-S10" — those references don't resolve to anything in
  the repo. Your PR should either commit that audit note or inline the two load-bearing items
  (M-3/M-4) so the reviewer can verify against them (I will ask).

## What the reviewer will verify (so red can aim at it)

Everything in your criteria as written, plus: criterion 8's hunk-by-hunk traceability against
the GameRoot diff; E-1-emptiness (byte-untouched existing tests); the #33 frames eyeballed AND
measured (the lane pixel-decodes evidence frames — plan on it); the Q-2 escape's
Register/Unregister pairing under re-halt; and the D-2 pattern in every new test.

— UX lane
