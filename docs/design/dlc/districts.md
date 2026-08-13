# Paid district catalog — PROPOSED / UNSIGNED / NON-EXECUTABLE

Lane 13 (DLC-DESIGN) design document. **Docs-only. This file activates nothing, creates no Play or
RevenueCat object, and is not a commitment to ship any district.** Direction source: the human-signed
§8 amendment in `docs/plan/specs/monetization_spec.md` (signature block at §8.10, lines 1018–1041).
Every district below except Night Harbor is an **agent proposal awaiting the human's per-slug
signature** required by `monetization_spec.md:622`.

---

## 0. Questions a human must answer (nothing below is decided)

| # | Question | Why an agent must not decide it |
|---|---|---|
| Q-D1 | Which of the proposed districts (if any) are greenlit, and in what order? | Scope + spend; §8.2's pattern row demands a per-slug human signature. |
| Q-D2 | Is the `L9xx` id block reserved for paid districts (see §2.2)? Free expansion bands 31–100 (`product_spec.md:603-607`) and the `^L[0-9]{3}$` id pattern (`docs/plan/data/level_schema.json:10`) share one 1000-id space. | Permanent content-addressing decision; ids are never recycled. |
| Q-D3 | Does All Access include a district's **included livery**, or only its levels? §8.2's packaging ruling says All Access covers "every paid **playable district**" and "does not silently include later standalone cosmetics" — a district-bundled livery is neither. | Changes what "every playable line" honestly promises. |
| Q-D4 | Does the §8.3 Night-Harbor two-price no-scroll copy law generalize verbatim to every district-led sheet? (Proposed here; §8.3 writes it only for Night Harbor.) | Copy law = an honesty promise, human-signed. |
| Q-D5 | Do paid districts award star-milestone badges? This proposal says **no** (see §2.4) to keep CM-R10.3's enumerated free-reachability test clean. | Collection-loop taste + a testable promise to free players. |
| Q-D6 | Does the CM-R10.6/A-27 faucet-parity inequality bind **every** paid district, or only Night Harbor as written? | Economy law; A-27 is explicitly overrulable by the human. |
| Q-D7 | ADR-0011 (Polyfork custody) does not exist in `docs/adr/` on this ref — see §5, SC-5. Is the district art pipeline blocked until it and its amendment are signed? | Licensing/custody; asset merges are gated on it per `state/PROJECT_STATE.md:8`. |
| Q-D8 | The five conflicts in §5 need rulings before any district content contract is frozen. | Spec-authority conflicts; this lane surfaces, never resolves. |

---

## 1. What exists today (build truth — every row verified in this tree)

| Fact | Evidence |
|---|---|
| 17 authored levels, L001–L017, three bands (onboarding / alternation / queue-reading) | `content/levels/L001.json` … `L017.json`; `state/PROJECT_STATE.md:8` |
| The shipped corpus introduces exactly **two** mechanics: `switch` (L001) and `queue` (L004). No level in `content/levels/` declares `second-source` or `wildcard`. | `content/levels/L001.json:12`, `content/levels/L004.json:13`; grep of the corpus |
| Two-source inflow is **UNBUILT** in the shipped corpus; it arrives with L018+ | `state/PROJECT_STATE.md:52` (CM-C12 row, engine sub-contract question) |
| Progression is a flat ordered array that wraps to L001 — **no level select, no district map, no gating, no shop** | `unity/Assets/Scripts/Bootstrap/GameRoot.cs:291-301` |
| Every file in `content/levels/` is staged into the APK, enforced by set-equality **both directions** | `tests/unity/editmode.test.sh:18-24`; `scripts/stage-content.sh:14-15` |
| Level ids are constrained to `^L[0-9]{3}$`; `band` and `mechanics` are closed enums; `newMechanic` is validator-enforced | `docs/plan/data/level_schema.json:10,18,20,21` |
| Only two lighting presets exist (Day / Night); a third needs an art-direction amendment | `docs/plan/specs/product_spec.md:175` |
| Night Harbor L901–L910 is **UNAUTHORED** — no `content/levels/L9*.json` exists | directory listing of `content/levels/` |
| Global boards are rewardless and scoped to Daily Line + District Cup only | `docs/adr/0010-play-games-services-leaderboards.md:32-37` |

**Consequence that shapes every row below:** because staging is set-equality in both directions, a paid
district's level JSON **ships inside the free APK bytes**. Access is an entitlement check at load time,
not asset absence. That is honest and matches §8.7's "no product may be sold before its asset/content is
in the same production build," but it must never be described as anti-piracy — the client cannot enforce
it, the same honesty ADR-0010 states about anti-cheat (`0010-…md:25-28`).

---

## 2. Catalog laws this proposal adopts (all PROPOSED)

**2.1 Remix-only, by force of §8.1 law 1.** Law 1 keeps "every mechanic" free. A paid district may
therefore only remix mechanics that are already free somewhere. Until a mechanic ships free, no paid
district may contain it. Wave-1 paid districts are consequently limited to `switch` + `queue` (the only
mechanics in the corpus today) unless L018+ lands first.

**2.2 Id allocation (PROPOSED, needs Q-D2).** Night Harbor holds L901–L910 (`product_spec.md:592`).
Proposal: reserve L900–L999 for paid districts, ten ids per district, allocated in signature order.
Because the signed block starts at L901, the ten-id stride yields **nine** contiguous districts
(L901–L910 … L981–L990), leaving L900 and L991–L999 as a ten-id remainder that is not a contiguous
district slot. **The paid catalog therefore ceilings at nine ten-level districts** — Night Harbor plus
eight — unless the human amends the `^L[0-9]{3}$` pattern or accepts a non-contiguous allocation. A real
ceiling on "deep catalog," recorded now rather than discovered at district nine.

**2.3 Naming.** Districts are place nouns (Yard, Cross, Platforms, Gardens, Terminus, Harbor). Cosmetic
SKUs own the "`<X>` Line" form (Sakura Line, Neon Line, Harvest Line, Snowbell Line, Brass Line —
`monetization_spec.md:653-669`), so no proposed district uses it: a player must never confuse a $2.99
**theme** with a $2.99 **district**. The retired name "Rooftop Line" is not reused (CM-R10.1 keeps a CI
grep gate at zero occurrences). **The same anti-confusion duty runs in the copy direction (PROPOSED):**
because "All playable lines" is deliberately narrower than "everything," any district-led sheet that
shows the All Access row must also name what that row does **not** contain — Harvest Line and Snowbell
Line, the cat-skin wave, and the standalone livery wave — **while naming Sakura + Neon as the included
cosmetic pair** (`monetization_spec.md:619` grants them under `all_access`; `:628-629` REQUIRES naming
them as included) — in the same sheet. §8.2 already makes this the
substance of All Access ("every playable line," not "the complete Cat Metro," and it "does not silently
include later standalone cosmetics" — `monetization_spec.md:619`, `:627-630`); this rule is what keeps
that true at the surface a player actually reads. Q-S4's generalization of the two-price no-scroll law is
**conditional on this exclusion line shipping with it** — the two-price row without the exclusion row
would make the comparison less honest, not more.

**2.4 Cosmetics inside a district.** Each district includes exactly **one** line livery, granted by the
same product set as the district itself, never sold standalone, never imitating an earned Cup/Founder
item (§8.1 law 5). **No star-milestone badge is awarded inside a paid district** — CM-R10.3 tests that
every row of the cosmetic-milestone list is reachable with `all_access` absent, and a paid-only badge row
would either break that test or require partitioning the list (Q-D5).

**2.5 A district is not a theme.** District set-dressing is not equippable elsewhere and does not alter
the board palette. Purchase copy must not imply a theme is included.

---

## 3. The catalog

### D-0 · Night Harbor — SIGNED SLUG, UNAUTHORED CONTENT (reference row)

- **Theme:** working night harbor; Night lighting preset (the preset already exists —
  `product_spec.md:175`). Ships as the signed reference shape, not as new Lane-13 design.
- **Content shape:** L901–L910, remix spread `difficultyTarget` 0.30–0.55, launch mechanics only, no new
  mechanic (`product_spec.md:592-593`; CM-R10.2). **Status: not authored** — no L9xx file exists.
- **Cosmetic set:** none beyond what §8.2 already signs. This lane proposes no addition to it.
- **Player-facing promise:** "Ten more night routes. Nothing in the free game needs them."
- **Commercial shape (already signed, still non-executable):** `cm_district_night_harbor` $2.99 à la
  carte, entitlement `district_night_harbor`, also attached to every All Access product
  (`monetization_spec.md:621`, `:701-706`).
- **Fair-core boundary, concretely:** never inserted into `GameRoot.LevelBand` (`GameRoot.cs:296-300`);
  never the source of a District Cup round (`product_spec.md:469`, and `:471` "No purchases interact with
  the Cup in any way"); neither the Daily generator's board/asset inputs nor the 30-board hand-validated
  backup pool shipped in StreamingAssets (`product_spec.md:462`) references it by id, topology or
  dressing; no global-board channel exists for district levels (ADR-0010 scopes boards to Daily/Cup);
  revocation relocks content and retains every star/score (CM-R10 / §8.3 matrix); first-clear
  tickets-per-minute ≤ the highest-yield free level's (CM-R10.6/A-27).
- **Honesty rule carried verbatim in behavior:** every Night-Harbor-led sheet, including `post_level_5`,
  shows both "Night Harbor only — `{localized_price}`" and "All playable lines — `{localized_price}`"
  without scrolling (`monetization_spec.md:712`), and the All Access comparison states that All Access
  does not credit or prorate an earlier district purchase (`:713`).

### D-1 · Sardine Sidings — PROPOSED (buildable with today's mechanic vocabulary)

- **Theme:** dawn goods yard behind the fish market — crates, gulls, a tin-roof shed, Day preset. Reads
  as the "working" counterpart to Night Harbor; sits inside the LOCKED 12-hex palette
  (`product_spec.md:137-157`) with Ink Navy keylines.
- **Content shape:** 10 levels, proposed ids **L911–L920**; bands drawn from the existing enum
  (`alternation`, `queue-reading`), `newMechanic: null` on all ten, `mechanics` limited to
  `["switch","queue"]`; remix spread 0.30–0.55 mirroring Night Harbor's ruling; one designated
  practice-only **guest route** (proposed L911) for the signed `district_guest_route` rewarded placement
  (`monetization_spec.md:766`).
- **Cosmetic set:** one included livery, `Sardine Sidings` line livery (paint + decal only). No badge
  (§2.4). No theme.
- **Player-facing promise:** "Ten more routes in the goods yard, yours forever. No new rules to learn,
  and nothing in the free game is behind it."
- **Fair-core boundary, concretely for this district:**
  1. **Not a bridge:** `GameRoot.LevelBand` (`GameRoot.cs:296-300`) contains only free ids; a testable
     rule is "`LevelBand` contains no `L9xx` id." Free progression L001→…→L030 never routes through it.
  2. **Not required by any free mode**, stated against the two modes as they actually work:
     **(a) District Cup** — a paid district is never the "one rotating district" a Cup round remixes
     (`product_spec.md:469`), which is what `:471`'s "No purchases interact with the Cup in any way"
     already demands; **(b) Daily Line** — the Daily is generated on-device from a pinned seed and
     generator params, with a 30-board hand-validated backup pool shipped in StreamingAssets
     (`product_spec.md:450`, `:462`). The invariant is therefore an **input** rule, not a pool-exclusion
     rule: neither the generator's board/asset inputs nor any of the 30 backup boards may reference a
     paid district by level id, board topology, or district dressing. A free player's Daily can never
     demand paid content.
  3. **Paid content never introduces a mechanic** — machine-checkable: every `L9xx` level has
     `newMechanic: null` **and** its `mechanics` array is a subset of the union of `mechanics` declared
     by all non-`L9xx` levels in `content/levels/`. This is the testable form of §8.1 law 1's
     "every mechanic … remain free."
  4. **No gameplay channel touched:** the included livery is paint/decal on the existing train capsule —
     it cannot change destination color, symbol tag, silhouette class, hitbox, animation timing, queue
     footprint, or preview legibility (§8.1 law 4; `product_spec.md:154-162`).
  5. **Durable and restorable:** one-time non-consumable; revocation relocks levels while retaining every
     star, score and completion; repurchase/Restore resumes exactly (§8.3 matrix, `monetization_spec.md:720`).
  6. **No faucet advantage:** first-clear tickets-per-minute ≤ the highest-yield free level's, printed in
     the test output (CM-R10.6/A-27 extended — Q-D6).
  7. **No rank channel:** district runs are not submitted to any global board (ADR-0010).
- **Unbuilt dependencies (labelled, not assumed):** the district map / level-select surface is UNBUILT
  (`GameRoot.cs:291-292` defers campaign gating; the story exists only as design in
  `docs/prd/ux-flows.md`); the entitlement gate is monetization code and therefore behind the production
  tripwire; the art is unproduced and Polyfork custody is unsigned (Q-D7).

### D-2 · Lantern Hill — PROPOSED (buildable with today's mechanic vocabulary)

- **Theme:** stepped hillside neighbourhood at dusk — paper lanterns, stacked platforms, cats on garden
  walls; Night preset (reuses the existing navy LUT, no third preset). Distinct silhouette language from
  Night Harbor's flat quayside.
- **Content shape:** 10 levels, proposed ids **L921–L930**; same enum discipline as D-1
  (`newMechanic: null`, `mechanics: ["switch","queue"]`); remix spread 0.30–0.55; guest route proposed
  L921.
- **Cosmetic set:** one included livery, `Lantern Hill` line livery. No badge. No theme.
- **Player-facing promise:** "Ten evening routes up the hill. Optional, permanent, and no new rules."
- **Fair-core boundary:** identical seven clauses as D-1, with one addition — Lantern Hill's Night preset
  must pass the same readability gates as the free Night district (Midnight Terminus): color always
  paired with symbol and silhouette, CM-R21's five-rater protocol and the silhouette-at-64px check
  (`PRD.md:381-386`; §8.7's "every paid district gets the same accessibility and performance evidence as
  a free district").
- **Unbuilt dependencies:** same three as D-1.

---

## 3b. Blocked — NOT in the catalog

### BLOCKED · D-3 · Signal Works — no SKU proposed, not shippable under §8.1 law 1

- **Theme:** signal box and repair shed; Day preset.
- **Why blocked:** its intended remix vocabulary is `cooldown` and `gate`. Both exist **only** as
  schema enum values (`level_schema.json:20`) and as a post-launch plan for free bands 31–60
  (`product_spec.md:603-607`). **Neither mechanic is built, authored, or shipped anywhere today.** Under
  §8.1 law 1 a paid district may not be the first place a mechanic appears, so Signal Works is
  **blocked** until those mechanics ship free in the campaign.
- **Content shape (if it ever unblocks):** 10 levels, proposed ids L931–L940, `newMechanic: null` (the
  mechanics would already be free), guest route L931, one included livery.
- **Recorded here for one reason only:** the deep-catalog direction needs a stated rule for future
  mechanics, and the rule is "free first, paid remix second." No date, no promise, no SKU proposed.

---

## 4. Considered and rejected (this section must agree with `fair-core-matrix.md`)

| Idea | Disposition | Reason |
|---|---|---|
| District season pass / "future districts included" pass | **DROPPED** | §8.2 bans season pass, cosmetic club, bundles; All Access already carries every future signed district (`monetization_spec.md:619`). |
| 3-district discount bundle | **DROPPED** | "no … bulk bundle"; every product keeps its own exact store price and button (`monetization_spec.md:671-673`). |
| Star-milestone badge earned inside a paid district | **REDESIGNED to none** | Would put a cosmetic-milestone row out of reach with `all_access` absent (CM-R10.3). |
| Rewind pack bundled into a district SKU | **DROPPED** | Mixes a consumable into a durable non-consumable and breaks §8.1 law 3's permanently-restorable promise. |
| Paid district rounds in the weekly District Cup | **DROPPED** | §8.1 laws 1 and 6: Cup stays free and no purchase confers medal/rank eligibility. |
| Paid district as the home of a new mechanic | **DROPPED (see §3b, D-3)** | §8.1 law 1 keeps every mechanic free. |
| Timed "launch-week" district discount | **DROPPED** | The anti-dark-pattern rules ban countdown timers of any kind precisely because "we run no time-limited sales at launch, so none can be real" (`monetization_spec.md:415`). |

---

## 5. Surfaced conflicts — RECORDED, NOT RESOLVED (contract STOP condition)

**SC-1 — CM-R10 is absent from §8.7's authority-replacement table.** `docs/prd/PRD.md:236` is a MUST
titled "Night Harbor (Bonus District 7), L901–L910, **All Access content**"; criterion 4 requires the map
tile to render the label "All Access" for a non-owner and criterion 5 keys the owner test to
`all_access`. §8.2 (`monetization_spec.md:621`) makes Night Harbor separately purchasable via
`cm_district_night_harbor`, and §8.3 (`:701-706`) forbids the client from evaluating
`all_access || district_<slug>`. §8.7's table (`:913-936`) replaces CM-R23…R37 and CM-R50 but **never
names CM-R10**. §8.7 clause 4 (`:909-911`) says partial supersession is invalid. Ruling needed:
does CM-R10 join the coordinated supersession, and what does the tile say to a district-only owner?

**SC-2 — `product_spec.md:592` (§22) is outside the §8.7 product_spec replacement scope.** That line
reads "Bonus district (**All Access, LOCKED**): Night Harbor L901–L910." §8.7's product_spec row names
"§§4, 12, 16–20, 28" — §22 is not in the list, so the All-Access-only claim would survive a
"complete" supersession. Same partial-supersession exposure as SC-1.

**SC-3 — two "without scrolling" budgets on the same surface.** CM-R26.7 (`PRD.md:448`) requires 3
disclosure lines visible without scrolling on 720p 16:9; §8.3 (`monetization_spec.md:712`) requires both
priced options visible without scrolling on the same `post_level_5` sheet. §8.7's CM-R26 row asks for
copy changes but is silent on the combined budget. Ruling needed on what must fit before any district
paywall copy is designed.

**SC-4 — judge-code storage: two live instructions, but they are not evenly weighted.** CM-R31.5
(`PRD.md:530`) names `ops/judge_codes.md` **inside this repo** for the remaining 23 codes and the
redemption guide — **and gates itself**: "Secret-handling is risks RK-26: gitignore + secret-scan rule
must exist before the file is created." §8.3 (`monetization_spec.md:744-746`) says codes and redemption
URLs "live only in the human-controlled secret store and the judge-only Devpost field—never in this
repository." Both are live until §8.7's supersession lands, so this is surfaced, not resolved — but the
weight runs one way, and recording it evenly would itself be a false balance. Four citations point the
same direction: (i) the §8.10 signature block's own condition, "no code secret may enter the repository"
(`monetization_spec.md:1029-1030`) — human-signed; (ii) CM-R31.5's self-blocking precondition above,
**currently unmet** — secret-scan CI is still TODO (`state/PROJECT_STATE.md:79`, `:92`), so the in-repo
file is not authorized to be created yet even under its own requirement; (iii) §8.7's CM-R31 replacement
row, which already schedules "human secret-store handling" as the successor (`monetization_spec.md:924`);
(iv) the standing repo rule that nothing key-shaped may be committed while secret-scan CI is absent
(`state/PROJECT_STATE.md:79`). **Ruling needed** on which text binds now and on retiring the
`ops/judge_codes.md` line — but no agent may create that file in the meantime, and this lane proposes no
district-code artifact of any kind.

**SC-5 — ADR-0011 does not exist in `docs/adr/` on this ref.** The contract and
`state/PROJECT_STATE.md:8` treat a Polyfork-custody ADR-0011 amendment as owed and unsigned; a glob of
the tree finds no `0011-*` file at all (`docs/adr/` stops at `0010-play-games-services-leaderboards.md`).
The only in-repo statements of the custody obligation are `state/PROJECT_STATE.md:8,55,77` and
`state/handoffs/PARALLEL-PUSH-2026-08-09.md:79-83`. Recorded as **pending and unwritten**, never as
signed.

**Pointer (pre-existing, already on the human's queue, not re-opened here):** the band-extent conflict
between the LOCKED L011–L017 table and `product_spec.md:350`'s "L011–L015" prose
(`state/PROJECT_STATE.md:8,52`). District difficulty ladders inherit whichever the human ratifies.

---

## 6. Sources

`docs/plan/specs/monetization_spec.md` §§8.1–8.11 (human-signed at §8.10, 2026-08-09) ·
`docs/plan/specs/product_spec.md` §§7, 14, 16, 17, 22, 23, 24 · `docs/prd/PRD.md` CM-R10, CM-R21,
CM-R23–CM-R33 · `docs/adr/0010-play-games-services-leaderboards.md` ·
`docs/plan/data/level_schema.json` · `content/levels/` · `unity/Assets/Scripts/Bootstrap/GameRoot.cs` ·
`scripts/stage-content.sh` · `tests/unity/editmode.test.sh` · `state/PROJECT_STATE.md`.
