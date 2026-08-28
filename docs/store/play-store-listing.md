# Google Play listing — exact-candidate template

Status: **template, not paste-ready by itself**. `scripts/build-aab.sh` replaces
`__CAMPAIGN_LEVEL_COUNT__` with the campaign count reported by `GameRoot.LevelBand`, verifies every
named level exists inside the exact AAB, and writes a sibling `*-play-listing.md`. That generated file
is count-bound candidate copy, not automatic clearance for the release-gated monetization claims.
Paste its fields only after the exact binary passes every gate in the table below.

This deliberately keeps TASK 15 (`feat/level-variety`) as a sequencing dependency rather than a
release-branch merge dependency. A production build cut after TASK 15 lands on `main` is expected to
render 19, but only the exact-AAB receipt proves the publishable number. Never change it by hand.
TASK 15 landing on `main` is mandatory before the production cut; a lower artifact-derived count does
not waive that sequencing requirement.

## Counting convention

Generated counts cover field text only, including spaces and punctuation. Multiline fields count
each line feed once. Markdown fences and the terminal line feed before a closing fence are excluded.
All publication characters are ASCII, so Unicode code-point and UTF-16 code-unit counts are
identical. The build refuses a rendered field over its Play limit and prints the exact counts; also
recount after any manual Console edit.

## Candidate fields — paste only after release gates clear

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
- Exact count: generated from the exact AAB
- Headroom: generated from the exact AAB

```text
Cat Metro is a one-thumb train puzzle about routing cat commuters through a tiny tabletop metro. Tap a junction, throw the switch, and guide each cat to the matching color-and-symbol station.

Fair by design: no forced ads, no energy timers, no loot boxes. Campaign play is free.

HOW IT PLAYS
- Tap junctions to change each route
- Read the next-wave preview and plan ahead
- Follow color-and-symbol station signs
- Match every cat to the right station

__CAMPAIGN_LEVEL_COUNT__ HANDCRAFTED LEVELS
Play __CAMPAIGN_LEVEL_COUNT__ campaign puzzles, from clear first routes into tighter switching challenges.

BUILT TO BE READ
Stations pair color with a symbol. The next-wave preview shows what is coming before the next routing decision.

A TABLETOP METRO PUZZLE
Cat Metro pairs a focused route puzzle with a small tabletop-railway premise. Switch the line, watch the next wave, follow each route, and help every cat reach the right station.

No energy timer limits play. Read the next wave, throw the switch, and guide every cat home.

One thumb. Small railway. __CAMPAIGN_LEVEL_COUNT__ handcrafted puzzles.
```

### What's new

- Google Play limit: 500 characters
- Exact count: generated from the exact AAB

```text
First release of Cat Metro.

__CAMPAIGN_LEVEL_COUNT__ handcrafted campaign levels, a next-wave preview so you can plan ahead, and stations that pair color with a symbol so every route stays readable.

No forced ads. No energy timers. No loot boxes. Campaign play is free.
```

## Current claim gates

Only claims whose publication gates are stated below appear in the generated fields. Build-derived
copy still requires the listed release gate before it is pasted.

| Claim used in the listing | Status | Exact-candidate evidence | Publication rule |
|---|---|---|---|
| One-thumb junction switching routes cat commuters to matching stations | `VERIFIED` | Implemented gameplay loop and truth baseline | May remain in current copy |
| Next-wave preview | `VERIFIED` | Implemented gameplay loop and truth baseline | May remain in current copy; queue readability and overflow/failure need separate real-level receipts |
| Exact campaign count in normal player progression | `BUILD-DERIVED` | The AAB log reports `GameRoot.LevelBand`; the wrapper verifies every named JSON exists inside that same bundle before rendering the copy | Paste only the generated sibling listing; never type a count into this template |
| No forced ads, energy timers, or loot-box system; campaign play is free | `RELEASE-GATED` | Exact-candidate feature census plus purchase/product review | Keep only if monetization lands as named cosmetics without a campaign pay gate |
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

1. Build the exact candidate and paste only from its generated `*-play-listing.md` sibling.
2. Confirm the AAB SHA-256 and campaign receipt at the top of that generated file.
3. Re-run every claim against the exact release candidate, not a plan or sibling branch.
4. Keep competitor names, research figures, experiment plans, and future features out of public copy.
5. If a gate does not clear, delete the affected sentence rather than softening it into a present-tense implication.
