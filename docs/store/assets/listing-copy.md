# Store listing copy — verified-only

**Source reviewed:** `72c0a862d41fd158d892c97e7b3f0fb01e94ef63` (2026-09-10)

The paste-ready fields below retain the stable gameplay claims and omit a level count. The source
notes distinguish the merged campaign from evidence still needed from the final Android build.
This document update did not build, install, or publish a release candidate.

## Paste-ready fields

### Title

23 characters of 30 (7 remaining).

```text
Cat Metro: Train Puzzle
```

### Short description

80 characters of 80 (0 remaining).

```text
Tap switches to route cat trains. A train puzzle with color-and-symbol stations.
```

### Full description

403 characters of 4,000 (3,597 remaining). The count includes the six line feeds between the
three paragraphs and heading, and excludes the terminal line feed.

```text
Cat Metro is an Android train puzzle about routing cat trains. Tap switches to guide cat trains toward matching color-and-symbol stations.

Read the next waves, choose the route, and finish a level to move to the next. This cat puzzle focuses on switch decisions.

A TABLETOP METRO PUZZLE

Built toward a tabletop model-railway look, Cat Metro pairs a focused route puzzle with a warm miniature premise.
```

### Play privacy policy URL

Verified live over HTTPS on 2026-09-10 (HTTP 200).

```text
https://sushidoescode.github.io/cat-metro-app/privacy/
```

The ASO P0 phrase `train puzzle` appears exactly once in the title, once in the short description,
and once in the opening sentence. The full description also uses `cat puzzle`, heading-only
`metro puzzle`, and `route puzzle` exactly once each. The stale `no forced ads` phrase is omitted.

## Apple metadata

These fields reuse the verified claims above. They add no platform-availability or feature claim.

### Subtitle

- Apple limit: 30 characters
- Exact count: 25 characters
- Headroom: 5 characters
- Title-word overlap: none

```text
Route by Color and Symbol
```

### Keywords

- Apple limit: 100 characters
- Exact count: 100 characters
- Headroom: 0 characters
- Format: comma-separated with no spaces; no word from `Cat Metro: Train Puzzle`

```text
switches,stations,routing,tabletop,railway,miniature,waves,level,guide,matching,warm,decisions,model
```

### Description

394 characters of 4,000 (3,606 remaining). This is the existing full description with only the
platform-specific wording removed; the claims and paragraph structure are otherwise unchanged.

```text
Cat Metro is a train puzzle about routing cat trains. Tap switches to guide cat trains toward matching color-and-symbol stations.

Read the next waves, choose the route, and finish a level to move to the next. This cat puzzle focuses on switch decisions.

A TABLETOP METRO PUZZLE

Built toward a tabletop model-railway look, Cat Metro pairs a focused route puzzle with a warm miniature premise.
```

### Support URL

```text
https://sushidoescode.github.io/cat-metro-app/privacy/
```

### Privacy Policy URL

```text
https://sushidoescode.github.io/cat-metro-app/privacy/
```

## Claim mapping

| Field text | Claim IDs |
|---|---|
| `Cat Metro is an Android train puzzle about routing cat trains.` | C-01 |
| Apple description: `Cat Metro is a train puzzle about routing cat trains.` | C-01 |
| `Tap switches…` / `choose the route` / `switch decisions` | C-01 |
| `matching color-and-symbol stations` | C-04 |
| `Read the next waves` | C-05 |
| `finish a level to move to the next` | C-08 |
| `tabletop model-railway look` / `warm miniature premise` | C-13 |
| Apple subtitle: `Route by Color and Symbol` | C-01, C-04 |
| Apple keywords: `switches`, `routing`, `guide`, `matching`, `decisions` | C-01 |
| Apple keyword: `stations` | C-04 |
| Apple keyword: `waves` | C-05 |
| Apple keyword: `level` | C-08 |
| Apple keywords: `tabletop`, `railway`, `miniature`, `warm`, `model` | C-13 |

## Current source and remaining release evidence

The claim IDs above remain useful references for the unchanged copy. The older claim ledger is
historical context, not an approval process or a prerequisite for release. Use the actual candidate
and the evidence below when adding claims; no frozen contract or external rater sign-off is needed.

At the reviewed revision, `content/levels/` and its StreamingAssets mirror contain **60** level JSON
files, L001–L060, with matching IDs and bytes. `GameRoot.LevelBand` lists those same IDs in order
and wraps L060 to L001. `LoadNextBandTests` asserts that sequence and wrap; `CampaignArtifactTests`
and `FullCampaignGateTests` in `QueueReadingBandTests.cs` cover the mirrored corpus and full
validation/solver run. Reading those tests is not a claim that they ran on the final candidate.

| Claim area / historical IDs | Source basis at the reviewed revision | Evidence needed before adding the public claim |
|---|---|---|
| Campaign count and progression — C-02, C-11, C-14, C-22 | The 60-level campaign is merged. The former 19-level and unmerged-ladder descriptions are stale. | Inspect the final AAB for the same 60 IDs, retain its content validation and solver results, and verify ordinary progression through the claimed campaign and wrap on the Play-delivered build. Verify payment-free progression separately before claiming it. |
| Solver-checked content — C-03 | `CorpusValidator`, `LevelSolver`, and the full-campaign tests are present. | Retain a successful validator/solver run over the exact content shipped in the candidate. |
| Cat silhouettes and queue readability — C-18, C-21 | `CatModelCatalog` and queued-train presentation are integrated. Paid cat/prop resources live in the main checkout's ignored asset install. | Inspect the candidate's catalog admission counts and phone renders for visible textured cats, readable pins, distinct destinations, and an occupied queue without HUD overlap. |
| Daily route and date parity — C-23D, C-23P | `SelectDaily`, `DailyBoardCatalog`, `DailyBoardFactory`, and UTC-keyed Daily inputs are present. | Record a real Daily run and completion. For worldwide parity/date-change claims, compare clean installs on the same date and across a rollover; inspect the candidate's generation and network behavior. |
| Daily lifetime tally — C-23S | `DailyProgressTracker` and `GameRoot` implement completion state. The intended public claim is a lifetime tally, not an expiring streak. | Verify exactly one increment per completion, relaunch persistence, and behavior after a missed day on the candidate. |
| Wardrobe navigation — C-24 | `BackRequested`, its `GameRoot` binding, and `ScreenStack` implement the Back path. | Exercise entry, Back, purchase, and return through ordinary device navigation. Verify any level-select claim separately. |
| Purchase and restore — C-25 | `PurchaseCatalog`, `PurchaseService`, `RevenueCatBackend`, and Wardrobe wiring are present. | Verify the final Android configuration, product/offering/entitlement mapping, real purchase and visible unlock, clean-state restore, and corresponding RevenueCat record. Test a judge promo code separately. |
| Optional rewarded ads — C-09, C-26, C-27 | Rewarded service/coordinator, LevelPlay provider, and RevenueCat reporting code are present. Source alone does not establish which services are configured in a release build. | Inspect the candidate's enabled configuration and ordinary flows. Claim working ads/reporting only after device delivery, reward/failure behavior, and RevenueCat reporting are demonstrated. Verify any no-forced-ads claim against the same binary. |
| Optional OneSignal/reminders — C-30, C-34 | Messaging, reminder preferences, permission handling, and Daily deep-link paths exist in source. | Verify the deployed campaign and candidate opt-in, opt-out, delivery, reminder window, and deep link before claiming reminders or entering the OneSignal category. |

### 60-level campaign status

The source census is **60 distinct levels, L001–L060**. The tests inspected here expect all 60.
The unchanged store fields intentionally make no numerical promise until the final AAB's content,
validation results, and Play-installed progression have been checked. This source refresh provides
no new device, purchase, public-store, or full-campaign test result.
