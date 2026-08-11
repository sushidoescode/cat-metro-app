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
- Exact count: 1,132 characters, including 21 line feeds
- Headroom: 2,868 characters

```text
Cat Metro is a one-thumb train puzzle about routing cat commuters through a tiny tabletop metro. Tap a junction, throw the switch, and guide each cat to the matching color-and-symbol station.

Fair by design: no forced ads, no energy, no loot boxes, every level solvable free.

HOW IT PLAYS
- Tap junctions to change each route
- Read the next-wave preview and plan ahead
- Follow color-and-symbol station signs
- Match every cat to the right station

FIVE HANDCRAFTED LEVELS
Play five campaign puzzles. Every level passes the project's content validation and solver gates. Each cat puzzle grows from clear first routes into tighter switching challenges.

BUILT TO BE READ
Stations pair color with a symbol. The next-wave preview shows what is coming before the next routing decision.

A TABLETOP METRO PUZZLE
Cat Metro pairs a focused route puzzle with a small tabletop-railway premise. Switch the line, watch the next wave, follow each route, and help every cat reach the right station.

No energy timer limits play. Read the next wave, throw the switch, and guide every cat home.

One thumb. Small railway. Five solvable puzzles.
```

## Current claim gates

Only `VERIFIED` claims appear in the paste-ready fields.

| Claim used in the listing | Status | Evidence at the 2026-08-10 freeze | Publication rule |
|---|---|---|---|
| One-thumb junction switching routes cat commuters to matching stations | `VERIFIED` | Implemented gameplay loop and truth baseline | May remain in current copy |
| Next-wave preview | `VERIFIED` | Implemented gameplay loop and truth baseline | May remain in current copy; queue readability and overflow/failure need separate real-level receipts |
| Five handcrafted campaign levels in normal player progression | `VERIFIED` | `GameRoot.LevelBand` exposes L001–L005 on the frozen anchor | Recount the normal player path on the exact release candidate |
| Every listed level passes content validation and solver gates | `VERIFIED` | Repository validation and solver gates cover L001–L005 | Do not generalize the listing count to authored but unreachable files |
| No forced ads, energy, or loot-box system; every current level is solvable free | `VERIFIED` | Frozen-tree feature census and solver evidence | Keep the positioning line verbatim |
| Color plus symbol coding for stations | `VERIFIED` | Implemented gameplay and truth baseline | Describe the encoding; do not claim a completed accessibility audit |
| Tabletop metro and model-railway premise | `VERIFIED` | Approved product premise | Describe the premise only; do not claim the current build matches an uncaptured art target |

## Gated future revisions — NOT PASTE-READY

Nothing in this section may be pasted into Google Play until its gate is satisfied and the claim is
reclassified as `VERIFIED` in the cross-pack claim ledger.

| Blocked or future claim | Status | Minimum re-entry gate |
|---|---|---|
| 30 launch levels | `BLOCKED` | All 30 pass exact-candidate validation/solver gates and are reachable through ordinary player flow; confirm with on-device Play-installed traversal evidence |
| Six named districts | `BLOCKED` | Exact-candidate district inventory plus ordinary-flow on-device navigation across all six from the Play-installed build |
| Daily Line, daily streaks, District Cup, or level select | `BLOCKED` | Promote only the specifically implemented limb after its own exact-candidate state tests and end-to-end on-device player-flow receipt |
| Shop, IAP, RevenueCat surfaces, rewarded ads, or premium themes | `BLOCKED` | Production-mode prerequisite satisfied and security review complete; promote each limb only after its own signed configuration and on-device exact-candidate receipt |
| Optional reminders | `BLOCKED` | Exact-candidate enable/disable UI, delivery/config receipt, and on-device opt-in/opt-out behavior; then insert the truthful phrase “optional reminders” and never promise “no notifications” |
| Share cards, challenge links, or public social loops | `BLOCKED` | End-to-end share flow merged and verified from a release-candidate build |
| Public Google Play availability | `BLOCKED` | USA-visible public production listing checked logged out, with the matching package/build installed from Play |
| Install, retention, revenue, conversion, rating, or posting-corpus results | `FUTURE COPY` | Dated source export; every rate includes its denominator and measurement window |
| A claim that the public build matches the committed golden frame | `BLOCKED` | Corrected on-device art capture from the merged release-candidate build |

## Release-editor checks

1. Paste only the three fenced fields above.
2. Recount after every text edit; the short description has one character of headroom.
3. Re-run every claim against the exact release-candidate commit, not a plan or sibling branch.
4. Keep competitor names, research figures, experiment plans, and future features out of public copy.
5. If a gate does not clear, delete the affected sentence rather than softening it into a present-tense implication.
