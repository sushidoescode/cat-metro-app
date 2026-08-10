# Google Play listing — current-build-safe

Status: paste-ready against the STORE-PACK truth baseline frozen on 2026-08-10.
Only the three fenced fields under **Paste-ready fields** are publication copy.

## Counting convention

Counts cover the field text only, including spaces and punctuation. The full-description count also
includes each line feed as one character. Markdown fences and the terminal line feed before a closing
fence are excluded. All paste-ready characters are ASCII, so Unicode code-point and UTF-16 code-unit
counts are identical.

## Paste-ready fields

### App title

- Google Play limit: 30 characters
- Exact count: 23 characters
- Headroom: 7 characters

```text
Cat Metro: Train Puzzle
```

### Short description

- Google Play limit: 80 characters
- Exact count: 79 characters
- Headroom: 1 character

```text
Route cat commuters with one thumb. A tabletop train puzzle with no forced ads.
```

### Full description

- Google Play limit: 4,000 characters
- Exact count: 1,422 characters, including 21 line feeds
- Headroom: 2,578 characters

```text
Cat Metro is a one-thumb train puzzle about routing cat commuters through a tiny tabletop metro. Tap a junction, throw the switch, and guide each color-and-symbol-coded cat to the matching station before the platform overflows.

Fair by design: no forced ads, no energy, no loot boxes, every level solvable free.

HOW IT PLAYS
- Tap junctions to change each route
- Read the next-wave preview and plan ahead
- Use queues carefully before they overflow
- See where a jam began, then retry immediately

TEN HANDCRAFTED LEVELS
Play ten campaign puzzles. Every level passes the project's content validation and solver gates. Each cat puzzle grows from clear first routes into tighter switching challenges. Learn from each attempt, adjust the route, and try the same board again.

BUILT TO BE READ
Color is never the only signal. Cat commuters use color plus symbols, making busy moments easier to follow. Cause-focused failure presentation points back to where the route broke down, and the compact retry loop keeps you close to the puzzle.

A TABLETOP METRO PUZZLE
Cat Metro brings the warmth of a small model railway to a focused route puzzle. Switch the line, watch the next wave, manage the queue, and help every cat reach the right station.

Retry immediately after a failed route. No energy timer stands between attempts. Learn the route, throw the switch, and try again.

One thumb. Small railway. Ten solvable puzzles.
```

## Current claim gates

Only `VERIFIED` claims appear in the paste-ready fields.

| Claim used in the listing | Status | Evidence at the 2026-08-10 freeze | Publication rule |
|---|---|---|---|
| One-thumb junction switching routes cat commuters to matching stations | `VERIFIED` | Implemented gameplay loop and truth baseline | May remain in current copy |
| Next-wave preview, queues, overflow, cause-focused failure, and immediate retry | `VERIFIED` | Implemented gameplay loop and truth baseline | May remain in current copy |
| Ten handcrafted campaign levels | `VERIFIED` | L001–L010 exist on the frozen main anchor | Keep the number at ten until a later build is evidenced |
| Every level passes content validation and solver gates | `VERIFIED` | Repository validation and solver gates cover L001–L010 | Do not generalize beyond the listed current levels |
| No forced ads, energy, or loot-box system; every current level is solvable free | `VERIFIED` | Frozen-tree feature census and solver evidence | Keep the positioning line verbatim |
| Color plus symbol coding for cat commuters | `VERIFIED` | Implemented gameplay and truth baseline | Describe the encoding; do not claim a completed accessibility audit |
| Tabletop metro and model-railway premise | `VERIFIED` | Approved product premise | Describe the premise only; do not claim the current build matches an uncaptured art target |

## Gated future revisions — NOT PASTE-READY

Nothing in this section may be pasted into Google Play until its gate is satisfied and the claim is
reclassified as `VERIFIED` in the cross-pack claim ledger.

| Blocked or future claim | Status | Minimum re-entry gate |
|---|---|---|
| 30 launch levels or six named districts | `BLOCKED` | All claimed levels merged, staged, validated, solver-gated, and present in the release candidate |
| Daily Line, daily streaks, District Cup, or level select | `BLOCKED` | Player-facing implementation merged and exercised in a named release-candidate build |
| Shop, IAP, RevenueCat surfaces, rewarded ads, or premium themes | `BLOCKED` | Production-mode prerequisite satisfied, implementation merged, policy review complete, and UI evidenced on the release candidate |
| Share cards, challenge links, or public social loops | `BLOCKED` | End-to-end share flow merged and verified from a release-candidate build |
| Public Google Play availability | `BLOCKED` | Official Play Console publication receipt for the named track and version |
| Install, retention, revenue, conversion, rating, or posting-corpus results | `FUTURE COPY` | Dated source export; every rate includes its denominator and measurement window |
| A claim that the public build matches the committed golden frame | `BLOCKED` | Corrected on-device art capture from the merged release-candidate build |

## Release-editor checks

1. Paste only the three fenced fields above.
2. Recount after every text edit; the short description has one character of headroom.
3. Re-run every claim against the exact release-candidate commit, not a plan or sibling branch.
4. Keep competitor names, research figures, experiment plans, and future features out of public copy.
5. If a gate does not clear, delete the affected sentence rather than softening it into a present-tense implication.
