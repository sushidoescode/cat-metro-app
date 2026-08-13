# District SKU / price proposal — PROPOSED · UNSIGNED · NON-EXECUTABLE

Every row this lane **proposes** — every SKU, price, entitlement and attachment below except the two
explicitly-restated signed rows — is **PROPOSED / UNSIGNED / NON-EXECUTABLE**. The restated rows
(`cm_district_night_harbor`, and the umbrella attachments §8.3 already signs) are marked
**SIGNED but still NON-EXECUTABLE** because that is what they are; this lane does not re-propose them and
does not sign anything. Nothing here creates a Play Console
product, a RevenueCat object, an Offering, an entitlement, a price, or a line of code. The `$2.99`
per-10-level district **pattern** is human-signed (`docs/plan/specs/monetization_spec.md:622`); each
concrete slug "returns for its own human signature" per that same row, and this file **is** that return
request — not the signature.

## Production tripwire — restated verbatim from the §8 callout (`monetization_spec.md:570-573`)

> **Production tripwire — no exception:** before any billing, IAP, ad, payment, catalog adapter,
> purchase UI, reward-grant, or equivalent monetization code merges anywhere, a human-authored commit
> must change `state/mode` to `production`. This documentation amendment does not flip, satisfy, or
> bypass that gate.

**This lane is docs-only and activates nothing.** It writes no code, touches no `**/billing/**`,
`**/iap/**` or `**/ads/**` path, and does not modify `state/mode`. Agents never run
`fastlane supply` or any Play upload/publish (`AGENTS.md` "Never run"). The §8.10 signature itself
records the same thing: "this signature alone activates nothing" (`monetization_spec.md:1033-1035`).

## Questions a human must answer

| # | Question |
|---|---|
| Q-S1 | Sign, amend or reject each proposed slug + price row in §1. Unsigned slugs cannot be created. |
| Q-S2 | Does each district's included livery entitlement attach to the All Access products (Q-D3)? §2's attachment table shows both readings. |
| Q-S3 | **Judge packet — no inventory change is implied.** §8.3's signed 15-code packet is *not* one code per SKU: it is one `cm_supporter_pack` code that already covers All Access and **every signed district**, plus per-SKU codes only where the umbrella does not reach (six cat skins, four liveries, Harvest, Snowbell, two rewind packs) — `monetization_spec.md:733-740`. Night Harbor has no dedicated code today, and new districts add **zero** codes. The only residual: does a judge need a **standalone à-la-carte district code** to demonstrate the district purchase path, or does the supporter code suffice (as it already does for Night Harbor)? |
| Q-S4 | Does the Night-Harbor two-price no-scroll copy law generalize to every district-led sheet (Q-D4)? §3 proposes yes **conditional on** the All-Access exclusion line shipping with it (`districts.md` §2.3) — the two-price row alone would read as a completeness claim the catalog does not honor. Note SC-3: the no-scroll budget on `post_level_5` is already contested. |
| Q-S5 | SC-1/SC-2 in `districts.md` (CM-R10 and `product_spec.md:592` still say Night Harbor is All-Access-only) must be ruled before any district SKU is created. |

---

## 1. Candidate SKUs

| SKU id | Type | Status | Candidate US reference | Content | Notes |
|---|---|---|---|---|---|
| `cm_district_night_harbor` | non-consumable | **SIGNED 2026-08-09, still NON-EXECUTABLE** (`monetization_spec.md:621`) | $2.99 | L901–L910 (unauthored) | Restated for context. This lane proposes no change to it. |
| `cm_district_sardine_sidings` | non-consumable | **PROPOSED / UNSIGNED / NON-EXECUTABLE** | $2.99 (the signed 10-level pattern price) | L911–L920 (unauthored) | Requires its own human signature per `monetization_spec.md:622`. |
| `cm_district_lantern_hill` | non-consumable | **PROPOSED / UNSIGNED / NON-EXECUTABLE** | $2.99 (same pattern) | L921–L930 (unauthored) | Same. |
| *(Signal Works)* | — | **NO SKU PROPOSED** | — | — | Blocked on `cooldown`/`gate` shipping free first (`districts.md` D-3). |

Pricing-template note: `$2.99` already has an existing signed template (`monetization_spec.md:678`), so
these rows need **no new template** — unlike the six `$0.99` cosmetics, which needed `tpl_cm_099`. **If a
district ever ships a level count other than 10, the signed `$2.99 per 10-level pack` pattern does not
cover it and the price returns to the human unpriced.**

---

## 2. Entitlement-attachment design (design-level only)

Future replacement targets in §8.7's authority table (`monetization_spec.md:913-936`):

- **CM-R23** → "exact signed SKU inventory, types, active/inactive status, prices/templates, and
  no-hard-coded-price checks." The rows in §1 are candidate inputs to that replacement. Today CM-R23
  (`docs/prd/PRD.md:403-412`) still locks a **6-SKU** catalog with `cm_all_access_499` inactive; these
  rows do not amend it and must not be implemented against it.
- **CM-R24** → "expanded entitlement graph with exactly one entitlement check per district/item and
  umbrella dashboard attachments; overlap/restore/refund matrix." The table below is a candidate input to
  that replacement. Today CM-R24 (`PRD.md:416-423`) still specifies **four** entitlements.

Marking for the table below: the `district_night_harbor` row restates the signed §8.3 shape
(**SIGNED, NON-EXECUTABLE**); **every other row is PROPOSED / UNSIGNED / NON-EXECUTABLE.**

| Entitlement | Granted by (à la carte) | Also dashboard-attached to | Client check |
|---|---|---|---|
| `district_night_harbor` | `cm_district_night_harbor` | `cm_all_access`, `cm_all_access_499`, `cm_supporter_pack` (signed, `monetization_spec.md:701-706`) | exactly `district_night_harbor` |
| `district_sardine_sidings` | `cm_district_sardine_sidings` | same three umbrella products | exactly `district_sardine_sidings` |
| `district_lantern_hill` | `cm_district_lantern_hill` | same three umbrella products | exactly `district_lantern_hill` |
| `livery_sardine_sidings` | `cm_district_sardine_sidings` | **reading A (proposed):** the same three umbrella products, because the livery ships as part of the district · **reading B (see the consequence below):** nothing — All Access buys lines, not looks | exactly `livery_sardine_sidings` |
| `livery_lantern_hill` | `cm_district_lantern_hill` | same choice as above (Q-S2) | exactly `livery_lantern_hill` |

**Reading B's consequence, stated plainly for the human's ruling (Q-S2).** While the included liveries
have **no standalone SKU**, reading B means an All Access or Supporter owner — the highest-paying
customer — can never obtain a district livery at all, except by buying the à-la-carte district a second
time at full price, with the signed no-credit/no-proration copy explaining that nothing is refunded
(`monetization_spec.md:713`). That is an upsell that punishes the umbrella buyer, and this lane treats it
as **NOT VIABLE as drawn**. Reading B becomes coherent only if the human also authorizes a standalone
livery SKU (a new signed product, a new price row, and a new §8.2 catalog entry) — which is a different
proposal than the one in §1. Reading A is what this document proposes.

Rules this design does not get to bend, restated so the implementer inherits them:

1. **One entitlement check per feature.** The client never evaluates `all_access || district_<slug>`;
   umbrella coverage is a dashboard attachment (`monetization_spec.md:701-706`).
2. **Manifest allowlist.** A dashboard product absent from the signed catalog manifest cannot render or
   grant; a manifest item missing from the returned Offering shows "Unavailable," never a hard-coded
   price (`:696-699`).
3. **Existing-owner proof before activation.** Attaching a new district entitlement to an
   already-sold All Access product requires sandbox/device evidence that an existing owner receives the
   attachment after CustomerInfo refresh/Restore, and that refunding either overlapping product cannot
   revoke the other (`:723-726`). **Actor split:** authoring and running the test harness and capturing
   the evidence is **[AGENT]** work; provisioning license testers and Google accounts, issuing the
   refund, and every RevenueCat/Play **Console state change** are **[HUMAN]** acts — an agent never
   mutates dashboard or Console state, and all of it sits after the `state/mode` flip.
4. **No proration, no credit.** A district owner buying All Access sees the signed no-credit copy before
   confirmation (`:713`).
5. **Consumables stay out.** No district SKU may contain rewinds or any consumable.

---

## 3. Placement, offering and copy (design-level, PROPOSED)

- New districts join the **existing** `bonus_district` placement / `ofr_districts` offering — no new
  placement is proposed. §8.3 already caps seven commerce placements; adding one would reopen §8.7's
  CM-R25 replacement row.
- **Honesty rule, proposed as a generalization (Q-S4):** every **district-led** sheet shows both
  "`{District}` only — `{localized_price}`" and "All playable lines — `{localized_price}`" without
  scrolling, exactly as §8.3 already requires of every Night-Harbor-led sheet including `post_level_5`
  (`:712`). No district-led sheet may imply a district is obtainable only through All Access.
  **Conditional (see `districts.md` §2.3):** this generalization is proposed *together with* the
  exclusion line — a sheet showing the "All playable lines" row must also name what that row does not
  contain (standalone `<X>` Line themes, cat-skin and livery waves), because "every playable line" is
  deliberately narrower than "everything" (`monetization_spec.md:619`, `:627-630`). Generalizing the
  two-price row **without** the exclusion line is not what this document proposes.
- **Unchanged, and not touched by this lane:** `post_level_5` is the one scripted exposure, **once per
  install ever**, with dismissal available on frame 1 (CM-R26; `monetization_spec.md:919`); three
  consecutive declines mute every ad row for 24 hours, a decline consumes no cap and grants nothing
  (`:769-770`, `:785-786`). Nothing in this proposal adds a system-initiated surface, a countdown, a
  timed sale, or an exit-intent offer.
- Prices are always rendered from the store's localized price string; the literals in §1 are US
  reference values for the human's decision only and must never appear in any shipped string
  (CM-R23.2's regex gate).

---

## 4. Downstream disclosure this proposal creates (for the coordinated supersession)

Adding district products adds their product/entitlement ids to flows §8.8 already enumerates: RevenueCat
(fulfillment, Restore, experiments), GameAnalytics (`cosmetic_unlocked`, purchase and entitlement
fields) and OneSignal (`purchase_completed` fields, RC integration tags). The coordinated supersession
must list the new ids there rather than assume coverage (`monetization_spec.md:955-974`). **UNVERIFIED by
this lane:** whether the existing signed retention/deletion settings cover additional product ids without
a new privacy amendment — a human/privacy review question, not an agent finding.

## 5. Order of operations before any of this becomes real

Per §8.9 (`monetization_spec.md:1001-1012`), unchanged by this lane: human signs the values → a separate
**human-authored** commit flips `state/mode` to `production` → §8.7's single coordinated supersession
commit lands (partial supersession is invalid) → the catalog manifest ships with new products
**inactive** → license/restore/refund/overlap testing → activate one at a time on signed dashboard
evidence. District content "activates only with its validated routes" (`:1010-1011`) — and no district
route is authored today.
