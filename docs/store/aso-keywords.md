# ASO keyword set — current-build-safe

Status: internal decision support derived from the 2026-07-31 growth plan and the STORE-PACK truth
baseline frozen on 2026-08-10. This file is not a keyword-stuffing block and is not paste-ready copy.
Google Play has no separate keyword field; indexed placement means natural use in the app title,
short description, or full description in `play-store-listing.md`.

## Priority set and placement

Exact-phrase counts below are case-insensitive editorial counts in the approved fields, shown as
`title / short description / full description`. They describe placement, not a guarantee about
Google Play's tokenization or ranking behavior.

| Query or concept | Priority | Placement | Exact-phrase count | Current use rule |
|---|---|---|---:|---|
| `train puzzle` | P0 | Title, short description, opening sentence of full description | `1 / 1 / 1` | Primary discovery lane; preserve the exact phrase in both high-weight fields |
| `cat puzzle` | P1 | Full description only | `0 / 0 / 1` | Secondary cat-plus-puzzle intent; one natural use is enough |
| `metro puzzle` | P1 | Full-description heading only | `0 / 0 / 1` | Association support; never replace `train puzzle` in the title |
| `no forced ads` | P1 | Short description and verbatim fairness line | `0 / 1 / 1` | Complaint-shaped fairness intent; never shorten to the absolute `no ads` |
| `route puzzle` | P2 | Full description only | `0 / 0 / 1` | High-intent mechanic support without repetition |
| `switch puzzle` | P2 | Semantic support in full description | `0 / 0 / 0` | Use switch and junction verbs naturally; do not force the exact phrase |
| `cat game` | P2 | Visual discovery through brand, icon, and truthful screenshots | `0 / 0 / 0` | The cat shelf supports recognition, not the title-ranking bet |

## Indexed-field map

| Surface | Job | Locked placement |
|---|---|---|
| App title, 30-character limit | Brand plus the single primary query | `Cat Metro: Train Puzzle` — 23 characters |
| Short description, 80-character limit | Mechanic, tabletop premise, P0 phrase, fairness | 79-character current-safe sentence in `play-store-listing.md` |
| Full description, 4,000-character limit | Explain the loop and add P1/P2 phrases naturally | One use each of `train puzzle`, `cat puzzle`, `metro puzzle`, and `route puzzle`; one use of `no forced ads` |
| Icon and screenshots | Earn cat-game recognition and explain the switch-routing verb visually | Visual discovery only; do not treat creative assets as an indexed keyword field |

## Exclusions and holds

| Term or claim family | Disposition | Reason or re-entry gate |
|---|---|---|
| Competitor brands, including `Mini Metro` and `Railbound` | Exclude permanently | Never use another brand as a keyword or in listing copy |
| `no ads`, `ad-free`, `no ads puzzle` | Exclude | Absolute wording is broader than the locked fairness promise; use `no forced ads` |
| `daily puzzle`, `Daily Line`, `daily streak` | Hold | Daily features are not built on the frozen anchor |
| `offline puzzle game`, `works offline`, `works on a plane` | Hold | Re-enter only after named release-candidate evidence proves the public offline behavior |
| `accessible puzzle`, `colorblind game`, `reads for everyone` | Hold | Color-plus-symbol coding is verified; outcome-level accessibility claims require their own validation |
| `30 levels`, district names, District Cup, or level select terms | Hold | Campaign expansion and district surfaces are not present on the frozen anchor |
| Shop, IAP, rewarded-ad, RevenueCat, paywall, or premium-theme terms | Hold | Specifications and active lanes are not evidence of player-facing commerce |
| Share, challenge-link, or social-loop terms | Hold | No current player-facing share flow is evidenced |
| `best`, `top`, award, rating, install, retention, revenue, or conversion claims | Exclude from current listing | No published result exists; later rates require a dated source, denominator, and window |
| `kids`, `for children`, `for all ages` | Exclude | The product targets 13+ and must not be framed as child-directed |
| Public-launch or `available now` language | Hold | Re-enter only after a USA-visible public production listing is checked logged out and the matching package/build is installed from Play |

## Internal competitive rationale — NOT PUBLIC COPY

These facts explain the priority order only. They are frozen research observations, not listing
claims, keywords, current market facts, or experiment results. Reverify before any external use.

| Observation | Research vintage | Internal consequence |
|---|---|---|
| `train puzzle` showed only 3–4 real incumbents in the growth-plan review | 2026-07-31 | Make it the sole P0 query and keep it in title plus short description |
| `metro puzzle` was led by Mini Metro at 3.6M installs and 4.63 stars | 2026-07-31 | Keep the phrase in natural full-description prose; do not contest it in the title |
| The `cat game` shelf was hyper-competitive: Cats & Soup 42.7M installs, Cat Snack Bar 29.8M, Neko Atsume 13.6M | 2026-07-31 | Use cat identity for visual discovery instead of spending title characters on the broad query |
| Four Play searches found no cat-themed metro or route-switching puzzle | 2026-07-31 | Support the brand-plus-mechanic combination without turning the observation into a public superlative |

## Iteration rules

1. Treat the growth plan's 2026-08-29 through 2026-09-04 and 2026-09-05 through 2026-09-11 windows as planned review windows, never as completed experiments.
2. Start an ASO text iteration only after the relevant Play track yields usable, dated search-term data.
3. Change one indexed field at a time. Keep title, short-description, and full-description versions with their exact character counts.
4. Hold text constant when data is too sparse; a planned creative test is not evidence that an alternative won.
5. Before every revision, rerun the claim gates in `play-store-listing.md`. A query opportunity never overrides build truth.
6. Do not increase exact-match frequency merely to hit a density target. Every occurrence must read naturally to a player.
7. Record the date range, locale, listing version, impression volume, and conversion denominator before drawing a result.
