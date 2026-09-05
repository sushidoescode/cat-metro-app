# Store listing copy — verified-only

**Source revision:** `02871d644af1f5a7b632578c73e6ba3e6a899787`
**Claim-ledger SHA-256:** `a822968ad31d19d89cb70d8aa1e99db8dce67492ff239194e546e11bae1e62eb`

This is the paste-ready listing at the stated source revision. Its fields deliberately use only
the stable claims listed below; they do not state a level count.

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

406 characters of 4,000 (3,594 remaining). The count includes the six line feeds between the
three paragraphs and heading, and excludes the terminal line feed.

```text
Cat Metro is an Android train puzzle about routing cat trains. Tap switches to guide each train toward its matching color-and-symbol station.

Read the next waves, choose the route, and finish a level to move to the next. This cat puzzle focuses on switch decisions.

A TABLETOP METRO PUZZLE

Built toward a tabletop model-railway look, Cat Metro pairs a focused route puzzle with a warm miniature premise.
```

The ASO P0 phrase `train puzzle` appears exactly once in the title, once in the short description,
and once in the opening sentence. The full description also uses `cat puzzle`, heading-only
`metro puzzle`, and `route puzzle` exactly once each. The stale `no forced ads` phrase is omitted.

## Claim mapping

| Field text | Claim IDs |
|---|---|
| `Cat Metro is an Android train puzzle about routing cat trains.` | C-01 |
| `Tap switches…` / `choose the route` / `switch decisions` | C-01 |
| `matching color-and-symbol station` | C-04 |
| `Read the next waves` | C-05 |
| `finish a level to move to the next` | C-08 |
| `tabletop model-railway look` / `warm miniature premise` | C-13 |

## Stale ledger rows — source evidence is not publication evidence

The claim ledger is frozen against an older source baseline. The rows below need a refresh before
they can be relied on for an exact candidate. Current code and files establish implementation or
source state only; no item below is promoted for publication by this document.

| Row | Current source evidence at the stated revision | Precise promotion or reclassification evidence |
|---|---|---|
| C-02 | `content/levels/` contains L001–L019, not the ledger's ten files. | Reclassify the source fact to the exact 19-file census only after recording the revision and ID list. Do not publish a count until an exact AAB contains the same 19 IDs and its retained artifact census proves it. |
| C-03 | `CorpusValidator.cs` and `LevelSolver.cs` remain in source; `QueueReadingBandTests.cs` explicitly derives the L001–L019 corpus and validates it. The frozen ten-level certification is therefore stale. | Re-run and retain the validator/solver result over the exact candidate's complete level census, including every published ID; bind that output and the AAB SHA-256 to a refreshed row. |
| C-09 | The former absence-based proof is stale: source now contains `RewardedAdsComposition.cs`, `LevelPlayRewardedAdProvider.cs`, and the LevelPlay package. `CosmeticComposition.cs` currently selects `DisabledCosmeticRewardedRoute`, but source inspection cannot establish an exact binary has no forced surface. | Reclassify only after an exact production-binary census and ordinary-flow on-device inspection prove that every ad entry is player-initiated and that no interstitial, banner, app-open, or other forced surface exists. |
| C-11 | `GameRoot.LevelBand` and `LoadNextBandTests.cs` now define and test L001–L019; purchase and cosmetics services now also exist. The frozen claim's five-player-level/ten-authored-level basis is stale. | Refresh the ordinary-flow reachable-ID census against the exact candidate, retain its full solver/validator result, and inspect the same binary for a normal-progression purchase gate. Promote only the payment-free normal-progression limb that this evidence proves. |
| C-14 | `GameRoot.LevelBand` lists L001–L019 and `LoadNextBandTests.cs` asserts the same ordered, wrapping campaign. The frozen five-level wording is stale. | Reclassify to the exact count only with an exact AAB ID census plus an ordinary-player-flow, on-device traversal from L001 through the claimed endpoint and wrap behavior. |
| C-18 | `ToyTrainView.PlatformQueueSpacing` now gives queued cars a presentation lane, and queue-reading levels L011–L017 are in the current campaign band. The frozen overlap rationale is no longer a complete source description. | Keep blocked until an exact-candidate on-device Playing capture shows a distinct occupied queue, its associated route decision, and clean HUD without development aids. |
| C-21 | The licensed animated cat rig, `CatModelCatalog`, and line-specific cat presentation are now integrated on main, so “implemented on the art branch” is stale. | Keep blocked until exact-candidate line-to-silhouette assignment tests and asset inventory, on-device frames for every line identity, and the ledger's named CM-R21.3/CM-R21.6 non-author rater receipts all pass. |
| C-22 | Current source contains 19 authored level files and the 19-ID band, not 30 ordinary-flow levels. | Keep blocked until a production 30-level census, exact-candidate solver/validator pass, ordinary-player reachable-ID census for all 30, and same-build Play-installed traversal evidence exist. |
| C-23D | `GameRoot.SelectDaily`, `DailyBoardCatalog`, `DailyBoardFactory`, and `DailyRuntimeInputs` implement a Daily entry, precomputed-board lookup, and validated fallback in source. | Keep blocked until an exact candidate has a released Daily entry surface and a clean on-device Daily run through its result; retain matching generation/validation evidence. |
| C-23P | `DailyLineSeed`, `DailyBoardFactory`, and the shipped UTC-keyed catalog now implement deterministic source behavior with parity tests; “only the pre-validation substrate exists” is stale. | Keep blocked until same-date/seed parity on at least two clean installs, consecutive-date rollover with date-to-seed/board variation, and exact-candidate algorithm/config plus network evidence prove the specific worldwide/no-server limbs. |
| C-23S | `DailyProgressTracker.cs` and `GameRoot` record and expose lifetime Daily completion state, but source alone is not a player-facing persisted streak receipt. | Keep blocked until exact-candidate streak persistence/state tests and multi-day on-device increment, lapse, and displayed-state receipts prove the specific streak limb. |
| C-24 | `WardrobeScreenView.BackRequested`, its `GameRoot` binding, and `ScreenStack` now implement a Back path for the Wardrobe limb; the frozen “lane queued” rationale is stale. No level-select proof follows from this. | Promote only the Back limb after exact-candidate route tests and an ordinary-flow on-device navigation receipt. Keep level select blocked until its own route tests and receipt exist. |
| C-25 | Source now includes `PurchaseCatalog.cs`, `PurchaseService.cs`, `RevenueCatBackend.cs`, and Wardrobe purchase wiring. It does not prove a human production-mode decision, signed catalog/config, or a live store flow. | Keep each named limb blocked until the human mode flip, security-reviewed exact candidate, signed catalog/config, relevant on-device flow, and backend/store receipt exist. Purchase or restore evidence promotes no sibling limb. |
| C-26 | Rewarded-ad service, coordinator, LevelPlay provider, and RevenueCat ad-reporting source exist; current cosmetic composition installs `DisabledCosmeticRewardedRoute`. No device opt-in, reward ledger, or dashboard receipt is present here. | Keep blocked until post-mode-flip ad code is configured in the exact candidate, an on-device opt-in surface and reward-ledger proof are retained, and RevenueCat dashboard evidence is recorded. |
| C-27 | Rewarded provider, coordinator, and Wardrobe placement paths now exist in source, but the shipped composition currently disables the route. The frozen claim that optional surfaces do not exist at all is stale, while the public wording remains unproved. | Keep blocked until C-26 clears and an exact-production-binary census proves every live ad entry is player-initiated and that no interstitial, banner, app-open, or other forced surface exists; reverify C-09 on the same candidate. |
| C-30 | `OneSignalMessaging.cs`, `OneSignalRuntimeConfig.cs`, and `GameRoot.InitializeMessaging` implement the adapter, initialization seam, scheduling, and Daily deep-link handling in source. Source does not prove a deployed journey or delivery. | Keep blocked until the merged exact candidate, exact live campaign/message configuration, device delivery receipt, and delivery counts with denominators for every claimed limb are retained. |
| C-34 | `DailyReminderPreferences.cs`, `DailyReminderSheet.cs`, and `GameRoot` implement enable/disable, permission, scheduling, and cancellation paths in source. There is no on-device opt-in/opt-out or delivery receipt in this source snapshot. | Keep blocked until an exact candidate has the optional enable/disable surface, delivery/config evidence, and on-device opt-in/opt-out behavior. Only then may listing copy say `optional reminders`. |

### 60-level ladder status

The 60-level ladder is unmerged and has **zero publication evidence**. It must not be used to
replace the current 19-file/19-ID source census, and it cannot support a store-count claim. C-22
remains blocked until its separate 30-level promotion evidence is complete. After the ladder is
merged, a 60-level claim would require an exact-AAB census of 60 distinct IDs, a solver and
validator pass over all 60, ordinary-flow reachability of all 60, and same-build traversal from a
Play-installed candidate.
