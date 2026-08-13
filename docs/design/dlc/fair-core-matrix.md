# Fair-core conformance matrix — PROPOSED / UNSIGNED / NON-EXECUTABLE

Every monetizable item proposed by Lane 13 × §8.1 laws 1–4 of the human-signed amendment
(`docs/plan/specs/monetization_spec.md:575-607`). A row that cannot honestly read PASS on all four does
not appear in `districts.md` — §3 below records what was dropped or redesigned and why. **This file and
`districts.md` must agree; if they ever diverge, the stricter reading wins and the divergence is a
defect.**

## Questions a human must answer

| # | Question |
|---|---|
| Q-M1 | Q-D3 (districts.md): if All Access does **not** include a district's included livery, rows L-1/L-2's law-3 cell changes shape — is the livery then a standalone cosmetic requiring its own SKU? |
| Q-M2 | Does the CM-R10.6/A-27 faucet-parity inequality bind every paid district (law-1 cells assume yes)? |
| Q-M3 | Law 4's channel list is exhaustive for **skins/liveries/themes**. Does the human accept this lane's reading that district **board set-dressing** is bound by the same list? |

## The four laws (compressed paraphrase — `monetization_spec.md:579-589` is the authoritative text)

These four lines are a working paraphrase for scanning the matrix, not a quotation. Where a cell's
verdict turns on wording, read the spec. The one place this paraphrase is knowingly **broader** than the
source is law 4: the spec binds paid *skins/liveries/themes*, and this lane reads the same channel list
onto district *board set-dressing* — flagged as **Q-M3** and unresolved.

1. Campaign, Daily Line, District Cup, progression, **every mechanic** and every required route stay
   free; paid districts are optional side content, never a bridge between free districts. (`:579-580`)
2. No energy, loot box, randomized paid reward, subscription, premium currency, ticket pack, paid streak
   saver, forced ad, banner, interstitial, app-open ad, or paid gameplay stat. (`:581-582`)
3. Every durable item is a one-time, permanently restorable non-consumable; "seasonal" describes
   merchandising, not ownership expiry. (`:583-584`)
4. Paid cosmetics cannot alter destination color, symbol tag, silhouette class, hitbox, animation timing,
   queue footprint, or any other gameplay channel; liveries/themes cannot change track, signal, queue,
   switch, hazard or preview legibility. (`:585-589`)

---

## 1. Proposed items

| # | Item | Type | Law 1 | Law 2 | Law 3 | Law 4 | Verdict |
|---|---|---|---|---|---|---|---|
| P-0 | `cm_district_night_harbor` (already signed; restated) | non-consumable, 10 levels | **PASS** — remix of free mechanics; absent from `LevelBand`, from Cup rotation, and from Daily generator inputs/backup pool | **PASS** — flat one-time price, no randomness, no currency | **PASS** — one-time, restorable; revocation retains stars/scores | **inapplicable** — content, not a cosmetic overlay; no readability change | **IN CATALOG (D-0)** |
| P-1 | `cm_district_sardine_sidings` | non-consumable, 10 levels | **PASS** — `switch`+`queue` only, both free since L001/L004; not a bridge | **PASS** — no consumable, no chance, no ad pressure added | **PASS** — permanently restorable; progress survives revocation | **inapplicable** — content; board dressing bound by law 4's spirit (Q-M3) | **IN CATALOG (D-1)** |
| P-2 | `cm_district_lantern_hill` | non-consumable, 10 levels | **PASS** — same free-mechanic remix; optional side content | **PASS** — same as P-1 | **PASS** — same as P-1 | **inapplicable** — content; Night preset reuses the existing LUT | **IN CATALOG (D-2)** |
| P-3 | `cm_district_signal_works` | non-consumable, 10 levels | **BLOCKED, not failed** — its `cooldown`/`gate` vocabulary is UNBUILT and not yet free anywhere; law 1 forbids paid-first mechanics | **PASS** if unblocked | **PASS** if unblocked | **inapplicable** | **BLOCKED — not in the catalog, no SKU proposed (`districts.md` §3b)** |
| L-1 | `livery_sardine_sidings` (included with P-1, never sold alone) | non-consumable cosmetic | **PASS** — grants no route, no progression, no free-content access | **PASS** — not randomized, not a currency, not a stat | **PASS** — durable; attached to the same product set as the district (Q-D3/Q-M1) | **PASS** — paint/decal only; symbol, silhouette, hitbox, timing, queue footprint untouched | **IN CATALOG (D-1)** |
| L-2 | `livery_lantern_hill` (included with P-2, never sold alone) | non-consumable cosmetic | **PASS** — same as L-1 | **PASS** — same as L-1 | **PASS** — same as L-1 | **PASS** — same as L-1, plus CM-R21 five-rater + 64 px silhouette evidence required | **IN CATALOG (D-2)** |
| R-1 | `district_guest_route` applied to a proposed district (one practice-only showcase route) | signed rewarded placement, no purchase | **PASS** — grants zero tickets, stars, progress, medal, Daily/Cup result or rank (`monetization_spec.md:766`) | **PASS** — opt-in only, player-opened preview, no forced/auto surface; 3-decline 24 h mute intact | **inapplicable** — a lease, not a durable item; expiry is the signed design, not ownership loss | **inapplicable** — no cosmetic granted | **IN CATALOG (one route per district)** |

Every PASS in the law-1 column additionally depends on the district-level invariants restated per
district in `districts.md` §3, corrected here to how the two free modes actually work:

1. no `L9xx` id in `GameRoot.LevelBand`;
2. **District Cup** — a paid district is never the "one rotating district" a Cup round remixes
   (`product_spec.md:469`; `:471` "No purchases interact with the Cup in any way");
3. **Daily Line** — there is no district rotation pool to exclude from: the Daily is generated
   on-device from a pinned seed/params with a 30-board hand-validated backup pool in StreamingAssets
   (`product_spec.md:450`, `:462`), so the invariant is that neither the generator's board/asset inputs
   nor any backup board references a paid district by level id, topology, or dressing;
4. every `L9xx` level has `newMechanic: null` and `mechanics` ⊆ the union of `mechanics` declared by all
   non-`L9xx` levels (the machine-checkable form of "every mechanic … remain free");
5. the CM-R10.6/A-27 faucet-parity inequality (Q-M2);
6. no global-board channel (ADR-0010 scopes boards to Daily/Cup).

---

## 2. Laws 5–8 residual checks (not the required matrix; the cells that still bite)

- **Law 5 (earned items stay earned, `:590-592`):** each included livery must be visually distinct from
  the Cup participation/gold-trim liveries and the Founder livery. Enforcement is a **HUMAN taste gate**,
  not a lint — recorded in `production-checklist.md`.
- **Law 6 (no purchased score/rank, `:593-596`):** no district run is submitted to any global board; the
  guest route writes no leaderboard evidence.
- **Law 7 (named local exceptions only, `:597-600`):** this lane adds **no** new economy exception. The
  only rewind/economy exceptions remain the ones §8.1 already names.
- **Law 8 (purchases only reduce commerce pressure, `:601-607`):** owning a district must **suppress**,
  never arm, further system commerce; a district owner is a payer for suppression purposes; unowned
  districts are browsable only from a player-initiated Shop/preview tap.

---

## 3. Dropped or redesigned before reaching the catalog

| Idea | Failing cell | One-line reason | Disposition |
|---|---|---|---|
| District season pass ("all future districts") | Law 2 | Subscription-shaped; §8.2 additionally bans season passes outright (`:671-673`) | **DROPPED** |
| 3-district discount bundle | Law 3 / §8.2 | Bulk bundle banned; each product keeps its own exact price and button | **DROPPED** |
| Star-milestone badge inside a paid district | Law 1 | Would make a cosmetic-milestone row unreachable with `all_access` absent (CM-R10.3) | **REDESIGNED — no badges in paid districts** |
| Rewind pack bundled into a district SKU | Law 3 | A consumable inside a durable breaks the permanently-restorable promise | **DROPPED** |
| Paid-district rounds in the weekly District Cup | Laws 1, 6 | Cup stays free; "No purchases interact with the Cup in any way" (`product_spec.md:471`) | **DROPPED** |
| A new mechanic debuting in a paid district | Law 1 | "Every mechanic … remain free" — paid-first mechanics are forbidden | **DROPPED (see `districts.md` §3b — blocked, not in the catalog)** |
| Timed "launch-week" district discount | §8.2 / anti-dark-pattern rules | No countdowns or time-limited sales exist to be honest about (`monetization_spec.md:415`) | **DROPPED** |

---

## 4. What this matrix does not certify

It certifies **design conformance on paper**. It is not evidence of behavior: no district exists, no
entitlement code exists, and no purchase path exists. Runtime conformance is proven by the tests §8.7
enumerates (purchase, pending, cancel, offline cache, restore, overlap-refund, duplicate callback,
account mismatch, reinstall; silhouette-at-64px and deutan/protan/tritan under CM-R21's five-rater
protocol) — all of which sit **after** the human-authored `state/mode` → `production` commit.
