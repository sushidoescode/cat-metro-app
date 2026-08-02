# CAT METRO — Product & Game Design Spec (execution grade)

Status: v1.0, 31 Jul 2026. Locked against `deliverables/DECISIONS_BRIEF.md` (single source of truth).
Companion documents: `deliverables/specs/architecture.md`, `deliverables/data/level_schema.json` (v2),
`deliverables/data/example_levels.json`, `deliverables/data/analytics_event_taxonomy.csv`,
`deliverables/data/onesignal_journeys.csv`, `deliverables/data/notification_copy.csv`.
Event window: open now (Jul 31 – Sep 30 2026, verified 2026-07-31). Public 1.0 target: Aug 24–28 2026.

---

## 1. Title and alternative names

Primary title: **CAT METRO**. Verified collision-free on Play/App Store/Steam; catmetro.com/.io/.app all
unregistered per registry RDAP (verified 2026-07-31). Former title "Loopline: Cat Metro" is retired: exact
"Loopline" apps exist on Play + App Store, an active itch game "Loop Line" exists, loopline.com is taken,
and the ASO query is crowded/low-relevance (verified 2026-07-31).

Ten alternative names (ranked backup order):

| # | Name | Screening status | Note |
|---|---|---|---|
| 1 | Meowtro | Verified clean 2026-07-31 | Designated backup brandable; shortest, ownable |
| 2 | Meow Metro | Verified in name screening | Closest semantic twin to primary; weaker as a brand |
| 3 | Whisker Rail | Verified in name screening | Good transit flavor; "rail" reads slightly premium/PC |
| 4 | Purr Transit | Verified in name screening | Warm; "transit" is ASO-generic |
| 5 | Kitty Junction | Verified in name screening | Junction = core verb of the game; mild kids-app reading (Families risk, watch art) |
| 6 | Catnip Express | Verified in name screening | Fun; "express" collides with the post-launch express mechanic naming |
| 7 | Tabby Transit | Coined 2026-07-31 — UNSCREENED | Alliterative; screen before any use |
| 8 | Nine Lives Line | Coined 2026-07-31 — UNSCREENED | Evocative; long for an icon; screen before any use |
| 9 | Pawsenger | Coined 2026-07-31 — UNSCREENED | Single-word brandable pun; screen before any use |
| 10 | Strayline | Coined 2026-07-31 — UNSCREENED | Moody/minimal; tone mismatch with cozy art; screen before any use |

Excluded by verified collision: **Whisker Line** and **Meowtropolis** (name-screening collisions, verified
2026-07-31). Excluded: anything containing "Loopline"/"Loop Line".

- **Decision:** Ship as CAT METRO; hold Meowtro as the only pre-cleared rename path.
- **Evidence:** RDAP + store searches 2026-07-31 (brief, VERIFIED); Loopline collisions confirmed.
- **Action:** Register catmetro.com/.io/.app on Day 1 (Aug 1) before any public build-in-public post; re-run store search on the chosen name at D21.
- **Risk:** Fast-follow cloners (Meowdoku spawned 7+ clones in ~3 months, verified 2026-07-31) squat adjacent names.
- **Fallback:** Flip to Meowtro without art rework (logo is type-driven); coined names 7–10 get screened only if both leaders die.

---

## 2. One-sentence pitch

Cat Metro is a one-thumb portrait puzzle game where you tap track switches to route color-and-symbol-coded
cat commuter trains to their matching stations before the platforms overflow — 45–90 second levels, every
level solvable free, and no forced ads ever.

- **Decision:** The sentence leads with input (one thumb), verb (tap switches), fantasy (cat commuters), and the fairness promise.
- **Evidence:** "No forced ads" attacks the loudest 1-star complaint in every F2P comp (Arrows 4.83★ but ad backlash; Bus Traffic Fever 3.72★ forced 30s ads — verified 2026-07-31).
- **Action:** Use this sentence verbatim as the Play short description ≤80 chars variant: "Tap switches, route cat trains, beat the rush. One thumb. No forced ads." (73 chars).
- **Risk:** "No forced ads" over-promises if any ad surface ever interrupts.
- **Fallback:** The claim is structurally safe: all ad surfaces are player-initiated rewarded placements only (LOCKED, brief).

---

## 3. Six-second ad pitch

Script (6.0 s bumper, portrait 1080×1920, no VO, no third-party music — event rule, verified 2026-07-31):

| t | Visual | Audio |
|---|---|---|
| 0.0–1.5 | Thumb taps a junction; track flips with a chunky clack; a red cat-train swings toward the red station | Clack + purr tick |
| 1.5–3.5 | Three deliveries chain: ding-ding-DING ascending; combo stamp "PERFECT FLOW" | Pentatonic chime run |
| 3.5–5.0 | Blue platform starts to crowd — near-miss flip saves it; cats cheer | Riser → relieved meow |
| 5.0–6.0 | Logo card: CAT METRO — "One thumb. No forced ads. Free." | Logo sting |

- **Decision:** The ad is one uninterrupted gameplay capture (Unity Recorder 5.1.6, 1080×1920 in Editor — verified 2026-07-31), not a montage.
- **Evidence:** Catvertising criteria reward "an experience users don't hate" and honest creative (verified 2026-07-31); comp ads that fake gameplay get flagged in reviews.
- **Action:** Build the Capture scene (architecture.md) so this exact 6 s beat is reproducible from a replay file; produce 3 seed variants by Sep 5 for the $0→$500 creative test ladder.
- **Risk:** 6 s is too short to read the color→station rule for cold audiences.
- **Fallback:** 15 s cut adds a 4 s failure beat (overflow → retry) before the save; keep both in the test set.

---

## 4. 30-second player pitch

"It's rush hour in a city run by cats. Trains full of commuter cats roll out of the depot — red, blue,
yellow, green, each with its own symbol — and every cat needs to reach the station that matches. You don't
drive the trains. You control the junctions: tap a switch and the track flips. Levels last about a minute;
you can retry instantly; there's a daily route that's the same for every player in the world so you can
compare scores with friends. It's free, every level is beatable without paying, and it will never shove an
ad in your face — ads only exist as an option when YOU want a bonus. If you like it, one $6.99 purchase
unlocks everything premium forever. No energy bars, no loot boxes, no subscription."

- **Decision:** The pitch spends its last third on the fairness/monetization promise, not more features.
- **Evidence:** Model B positioning is LOCKED (brief): "Fair by design: no forced ads, no energy, no loot boxes, every level solvable free"; HAMM judging rewards articulated packaging (verified 2026-07-31).
- **Action:** Record as the first 30 s of the <2 min submission video; store-listing long description opens with this paragraph.
- **Risk:** Leading with fairness can read defensive to players who never saw the comps' ad spam.
- **Fallback:** A/B the long-description opener (fantasy-first vs fairness-first) as a Play store listing experiment in Sep ($300 ASO budget line, brief).

---

## 5. Audience

Primary: casual puzzle players, 13+ (Play target-audience declaration 13+ only — do NOT declare under-13
age groups; Families compliance risk if store art reads child-directed, verified 2026-07-31). Core profile:
plays Wordle/Mini Metro/Two Dots-class games in 1–5 minute pockets, hates forced ads, on mid-tier Android.
Secondary: cat-game audience (Neko Atsume 13.6M installs/4.78★ with gentle IAP ≤$3.49 — verified
2026-07-31) who convert on cosmetics. Tertiary: transit/systems nerds (Mini Metro's 3.6M/4.63★ audience,
underserved on Android since Trainyard delisted 2019 and Mini Motorways skipped Android — verified 2026-07-31).

- **Decision:** Design ceiling is "my commute, one thumb, phone in one hand, sound off by default."
- **Evidence:** Whitespace verified: no cat-themed metro/route-switching puzzle on Play (4 searches, 2026-07-31).
- **Action:** Every UX review asks: playable one-handed, silently, in 90 seconds, standing on a train?
- **Risk:** Cute cat art drags perceived audience under 13 → Families listing rejection.
- **Fallback:** Art direction rule "charming, not childish" (Section 7); if Play flags the listing, swap icon/screenshot art to the navy "night line" set which reads older.

---

## 6. Age rating

- IARC questionnaire (expected outcomes): ESRB **Everyone**, PEGI **3**, USK 0, Google Play **Everyone**.
- Questionnaire answers that matter: violence none (cats are inconvenienced, never harmed); no gambling
  mechanics (no loot boxes — LOCKED cut); digital purchases YES (declare IAP $1.99–$9.99); ads YES
  (user-initiated rewarded only, AdMob max ad content rating **G**); user interaction NO in-app (sharing is
  via the OS share sheet; the challenge deep link exchanges only a level seed, no chat, no UGC).
- Play target audience: **13+** (single group). Not enrolled in the Families program (verified 2026-07-31).
- Store listing carries "In-app purchases" and "Contains ads" labels; description states the rewarded-only policy.

- **Decision:** Rate Everyone via IARC, declare 13+ target audience, stay out of Families.
- **Evidence:** Brief platform facts (verified 2026-07-31): do not declare under-13; Play may reject child-appealing listings that aren't Families-compliant.
- **Action:** Complete IARC in Play Console when the closed-test listing is created Aug 1–2; set AdMob max ad content rating to G the same day.
- **Risk:** "Contains ads" label deters the ad-hating audience we court.
- **Fallback:** First screenshot caption reads "Ads only when you ask for them — never forced"; this is also the Catvertising narrative.

---

## 7. Art direction — tabletop diorama spec

Fantasy: a hand-built model railway of a cat city on a wooden desk. Premium tabletop diorama, modular
low-poly (Meshy/Tripo generation + Blender cleanup), 1 lighting rig, 1 toon shader family (LOCKED, brief).
Readability outranks beauty (LOCKED). Not childish — no oversized heads, no drooling babies, no primary-
color explosion (Families risk, verified 2026-07-31).

### Palette (hex, authoritative)

| Role | Name | Hex |
|---|---|---|
| Board/table base | Cream Card | #F2EAD9 |
| Paper highlight / UI panels | Warm Paper | #FAF6EC |
| Primary dark / outlines / night UI | Ink Navy | #22304A |
| Deep shadow / night sky | Depot Navy | #131C30 |
| Accent 1 / success / water | Metro Teal | #3BAFA8 |
| Accent 2 / CTA / warnings-soft | Ticket Orange | #F08A3C |
| Line RED | Signal Red | #E15A47 |
| Line BLUE | Harbor Blue | #3E7CC9 |
| Line YELLOW | Tabby Yellow | #EFC13D |
| Line GREEN | Garden Green | #4FA36A |
| WILD | Catnip Violet | #A06BD8 |
| Fail/overflow | Alarm Coral | #D93A2B |

Rules: line colors never appear alone — always paired with symbol (● red, ■ blue, ▲ yellow, ◆ green,
★ wild) and a distinct cat silhouette per color (red = round-eared tabby, blue = slim siamese, yellow =
fluffy longhair, green = sleek shorthair, wild = scruffy alley cat with bent ear). Cream/navy/teal/orange
is the LOCKED base palette (brief); line colors are content, base palette is chrome.

### Shape language

- Everything rounded: min corner radius 12% of element size; zero sharp exterior angles on gameplay objects.
- Trains are capsules with cat-ear cab silhouettes; stations are rounded kiosks with awnings in line color + giant symbol signage; switches are chunky lever discs with a visible thrown-direction arm readable at 6 mm on-screen.
- Diorama dressing (desk-margin props: coffee cup, pencil, tape roll) confined to ≤6% of screen area, outside the board's safe rect, never animated during Playing.
- Miniature scale sold by: soft contact shadows, slight base-board bevel with visible "cardboard edge," vignette at 8% corners.

### Camera

- Portrait lock. Orthographic projection (true ortho at launch), pitched **30° from vertical** (accept range 25–35°; per-level override forbidden), yaw 0.
- Board grid is x ∈ [0,6], y ∈ [0,10] (matches example_levels.json coordinates); ortho size auto-fits grid + 0.5-unit margin inside device safe area; no pan, no zoom, no rotate at launch.
- P2 (high tier only): swap to physical camera FOV 10° at distance for micro-parallax; feature-flagged, off by default.

### Lighting/shader

- One directional key (warm, 35° elevation, from top-left), one fill (cool sky ambient), baked AO on static board pieces. One toon ramp shader family: 3-step ramp + rim; no realtime shadows on low tier (blob shadows).
- Two lighting presets only: Day (cream board) and Night (navy board — used by Midnight Terminus district and Neon theme). Same rig, different gradient LUT.

- **Decision:** Diorama-on-a-desk with the 12-hex palette above; ortho 30°; one shader family; readability first.
- **Evidence:** Art constraints LOCKED in brief; Unity 6 URP Android frametime regression reports (verified 2026-07-31) argue for the cheapest possible lighting.
- **Action:** Build one "golden frame" test scene by Aug 6 with all 5 line colors + all symbols on a low-tier device in sunlight; colorblind sim pass (deutan/protan/tritan) is a merge gate for palette changes.
- **Risk:** Meshy/Tripo output drifts stylistically across generation batches.
- **Fallback:** Style bible = the golden frame; any generated asset that fails silhouette-at-64px or palette-distance checks is re-topologized in Blender or cut — the game works with 9 modular board pieces minimum.

---

## 8. One-thumb control model

- **Single verb: tap.** Tapping a junction node toggles its switch to the next route (2 routes at launch; the 3-route variant appears only in expansion levels). Nothing is ever dragged, pinched, held-to-aim, or multi-touched (LOCKED: tap only, brief).
- Hit targets: switch tap zone ≥ **48dp** (≈9 mm) radius, expanded beyond the visual disc; simultaneous-tap disambiguation picks the nearest center; EnhancedTouch (Input System) for multi-safe hit tests (architecture.md).
- Command timing: a tap enqueues `ToggleSwitchCommand(switchId, tick)` applied at the next tick boundary (deterministic; architecture.md). Visual lever starts animating immediately (≤50 ms perceived latency) even though state flips at the boundary.
- Thumb-zone layout: all interactive UI (retry, pause, rewind sheet) in the bottom 25% of screen; the next-wave preview strip is display-only at top; boards are authored so no switch sits in the top 15% of the safe area (validator warning).
- Tap-and-hold anywhere (400 ms) = **planning pause** when the accessibility mode is enabled (Section 24): sim freezes, switches remain tappable, release resumes after a 3-2-1 quarter-second countdown.
- Back gesture: pause menu in Game; never exits mid-purchase (architecture.md).

- **Decision:** One verb (tap junction), one optional hold (planning pause), nothing else, forever at launch.
- **Evidence:** LOCKED controls (brief): tap only, ≥48dp, planning-pause accessibility mode.
- **Action:** Fat-finger test in the D7 fun gate: 5 testers, smallest supported device (720p), mis-tap rate <3% of taps or hit zones grow.
- **Risk:** Two adjacent junctions inside 96dp create ambiguous taps.
- **Fallback:** Validator rejects boards with junction centers <1.2 grid units apart; L-shaped hit-zone splitting is the escape hatch, never smaller targets.

---

## 9. Core rules — the tick simulation, spelled out

Fixed-tick deterministic sim: **8 ticks/s (125 ms)**, pure C#, seeded PCG32, command log; presentation
interpolates between snapshots (LOCKED, brief + architecture.md). Wall clock never read inside the tick.

### Board objects

- **Nodes**: points on the grid. A node may have a `queueCapacity` (1–8): trains arriving faster than they can depart wait here in FIFO order.
- **Edges**: directed track segments with `travelTicks` (1–40). One-way at launch.
- **Sources** (depots): emit trains according to the level's `waves` script. Each wave: start tick, color, count, `spacingTicks` between trains.
- **Switches**: sit on a junction node; hold a current route (one outgoing edge among 2–3); `initialRoute` defined per level; toggled by tap. Launch switches have no cooldown (`cooldownTicks: 0`); cooldown is a post-launch mechanic.
- **Stations**: terminal nodes that `accept` specific colors; have platform `capacity` (1–12).
- **Trains ("cat commuters")**: one cat per train at launch scale; color ∈ {red, blue, yellow, green, wild}.

### Per-tick order of operations (authoritative; solver and game share this exact function)

1. **Apply commands** enqueued since last boundary (switch toggles), in receipt order.
2. **Emit waves**: any wave whose emission tick matches spawns a train at its source node (enters the source's outgoing edge, or the source queue if the edge mouth is occupied this tick).
3. **Advance trains** along edges by 1 tick; a train arriving at edge end this tick is delivered to the node.
4. **Node arrival resolution**: at a junction, the train departs immediately on the switch's current route if the edge mouth is free; otherwise it enters the node queue (FIFO). At a station, go to step 5.
5. **Station acceptance**: matching color (or wild, accepted anywhere) → **delivered**: +score, combo update, cat hops off with a chime. Non-matching → **rejected**: the cat occupies one platform slot for 8 ticks looking confused, then rides back up the same edge to the previous node and re-enters routing there (the only reverse traversal in the launch rules). Rejection: −25 score, breaks the combo chain.
6. **Overflow checks**:
   - Node queue: a train arriving at a full queue puts the node in **Overload** — a 16-tick (2 s) countdown ring appears; if the queue is still over capacity when it hits zero, the level **fails** (`fail_reason: queue_overflow`). Clearing space cancels Overload.
   - Station platform: rejected cats exceeding station `capacity` → immediate **fail** (`fail_reason: platform_overflow`).
7. **Score/combo update** (Section 9.1).
8. **Win/time check**: `win.deliveries` reached → **win** (freeze sim, results). Tick counter reaching `win.timeLimitTicks` before that → **fail** (`fail_reason: time_out`, "the last train left").

Determinism contract: (levelId, seed, commandLog) → identical outcome on every platform; CI asserts replay
hash stability (architecture.md).

### 9.1 Scoring — perfect-flow combo

- **Delivery:** +100 per correctly delivered cat.
- **Rejection:** −25 per wrong-station arrival; resets chain to 0.
- **Perfect-flow chain:** consecutive deliveries with no rejection and no Overload since the previous delivery increment the chain. Chain bonus per delivery = 10 × chain length, capped at +50 (so chain 1..5+ → +10/+20/+30/+40/+50). The chain meter is the "purr meter": the board hums, cats' tails sync.
- **Time bonus at win:** + floor(remainingTicks ÷ 2).
- **Perfect Flow stamp:** win with zero rejections, zero Overloads, and `switchesUsed ≤ win.perfectMaxSwitches` → flat +50 score and the level's `economy.perfectBonus` tickets.
- **Stars:** 1★ = any win. 2★/3★ = score ≥ `win.stars.two` / `win.stars.three`. Thresholds are authored per level and validator-checked to be solver-achievable (schema v2). Worked check against shipped examples: L001 (2 deliveries): base 200 = 2★ floor; +Perfect 50 +time ≈55 → 305 ≥ 300 = 3★ ✓. L018 (10 deliveries): 1000 base allows ≤4 rejections for 2★ (900); full chain + perfect + time clears 1200 ✓.

- **Decision:** Rules above are final for launch; rejected cats are recoverable (bounce-back), so every emitted cat can always be delivered and win.deliveries always equals total cats emitted.
- **Evidence:** Sim constraints LOCKED (brief: 8/s tick, PCG32, command log, solver-validated JSON, schema v2); star/score fields already shipped in schema + examples.
- **Action:** Implement Domain step function + replay hash test first (D3); the solver reuses it verbatim (no parallel implementation, architecture.md).
- **Risk:** Bounce-back rule creates degenerate stalling strategies (park cats at wrong stations deliberately).
- **Fallback:** −25 and chain reset already punish it; if telemetry shows abuse, validator adds a "max concurrent rejected cats" solver constraint — a data change, not a rules change.

---

## 10. Win / failure conditions

| Outcome | Condition | Player-facing line | fail_reason enum |
|---|---|---|---|
| WIN | Deliveries = `win.deliveries` before `timeLimitTicks` | "All cats home!" | — |
| FAIL 1 | Node Overload countdown expires | "Platform overflowed at {node}" | queue_overflow |
| FAIL 2 | Rejected cats > station capacity | "{station} platform overflowed" | platform_overflow |
| FAIL 3 | Time limit reached | "The last train left the depot" | time_out |

Failure UX (LOCKED, brief): instant retry <1 s; **cause-first failure camera** — on fail the camera pans
to the causing node, ghost-replays the final 3 seconds at 60% speed with the causal cat highlighted, and
shows one chip: "This blue cat needed the switch flipped here." Then the retry/rewind sheet. The rewind
offer never appears after a player's first failure on a level (LOCKED paywall rule, brief).

- **Decision:** Three fail reasons only; every fail names its cause before offering anything.
- **Evidence:** Brief LOCKED: instant retry <1 s, cause-first failure camera; `level_failed` event already carries `fail_reason` + `congested_node` (analytics taxonomy).
- **Action:** Cause attribution = walk the sim log backward from the failing node to the last routing decision affecting the causal cat; ship in vertical slice (it's also the rewind target, Section 20).
- **Risk:** Ghost replay costs a second against the "<1 s retry" promise.
- **Fallback:** Retry button is live DURING the ghost replay; replay is skippable by the same tap.

---

## 11. Tutorial sequence — 3 no-text teaching levels

No tutorial text, no hand icons holding text, no modal popups. Teaching is done by board shape, a pulsing
switch affordance, and wave design. (Levels L001–L003; `tutorial_started/step/completed` events per taxonomy.)

| Level | Name | Teaches | Design that does the teaching |
|---|---|---|---|
| L001 "First Switch" | One tap flips the route | Board: 1 source, 1 junction, 2 stations (shipped in example_levels.json). Switch starts routed at the WRONG station (initialRoute: 1 → blue side) while 2 red cats approach slowly (minActionWindowTicks 16 = 2 s). The switch pulses. The first tap is the first lesson. Failure is nearly impossible (capacity 6, generous time). |
| L002 "Two Trains" | Timing matters; watch the preview strip | Same board mirrored; waves alternate red, blue, red with wide gaps. The next-wave preview strip animates its first entry sliding in — eyes are drawn up. One switch, three flips, no queue yet. |
| L003 "The Platform" | Queues exist, overflow ends the run (safely) | A queueCapacity-2 node is placed before the junction; wave spacing forces one cat to wait visibly (queue bubble animation). A scripted near-overload triggers the countdown ring once with enough slack that any tap saves it. Player sees the fail-threat UI without failing. |

Exit: L003 win fires `tutorial_completed` (OneSignal receives within 60 s per taxonomy QA). Target: ≥90%
of installs complete L003; total tutorial time ≤2:15.

- **Decision:** Teach switch → timing/preview → queue/overflow in 3 wordless levels; queue as a formal new mechanic still headlines L004.
- **Evidence:** Schema meta enforces one-new-mechanic-at-a-time (validator rule); L001 is already authored and validated (example_levels.json, validatedAt 2026-08-02 planned).
- **Action:** Author L002/L003 to schema v2 by Aug 4; D7 fun gate observes 5 testers with zero verbal help.
- **Risk:** Wordless teaching fails for a minority; they churn silently.
- **Fallback:** After 2 fails on any tutorial level, a single 1-line hint chip appears (localizable string, exists in the string table but hidden by default) — measured via `tutorial_step.retries`.

---

## 12. First-session walkthrough, 0:00–5:00

| Time | Beat | Detail |
|---|---|---|
| 0:00–0:04 | Cold start | Boot ≤3.5 s to Home (budget, architecture.md). Logo card doubles as load screen. No permission prompts, no login, no GDPR wall beyond the required consent (one screen, pre-checked nothing). |
| 0:04–0:10 | Home, first look | District map: Whisker Yard glowing, 5 level pins, everything else visibly parked ("depot" silhouettes — curiosity, not lock icons). Single CTA: the pulsing L001 pin. No shop badge, no daily badge yet. |
| 0:10–0:45 | L001 | As Section 11. Expected first win ~0:35–0:45. Win screen: confetti-light, 3 stars (nearly guaranteed), +20 tickets counter rolls up, "Next" pulses. |
| 0:45–1:35 | L002 | Alternation. Possible first fail (time_out) — instant retry <1 s. Expected clear by 1:35. |
| 1:35–2:40 | L003 | Queue lesson + scripted near-overload. `tutorial_completed`. Win screen adds the first cosmetic tease: conductor cap "unlocks at Whisker Yard ★★★" (stars meta visible now). |
| 2:40–3:40 | L004 | First real mechanic card (one illustration, 3 words: "Platforms fill up") — queue is `newMechanic`. First honest failure likely here; cause-first camera does its job. |
| 3:40–4:40 | L005 | Onboarding capstone (diff 0.16). Clear rate target 90% first attempt. |
| 4:40–5:00 | Post-L5 moment | Win celebration, then the ONE scripted paywall exposure: RC Paywalls v2 `post_level_5` placement — celebratory framing ("You finished Whisker Yard!"), All Access $6.99 presented as the complete edition, dismiss X is immediate and obvious (LOCKED, brief). Decline → straight to Harbor Line map reveal. No re-ask. |

Session-1 exit states measured: `level_completed` L005 (healthy), `paywall_viewed post_level_5`,
session length ~5 min. Push soft-prompt does NOT appear in session 1 (it waits for the Daily Line value
moment, Section 15 / taxonomy `push_soft_prompt_viewed` cap 1/build).

- **Decision:** Five levels + one dismissible paywall inside the first 5 minutes; zero permission asks before value.
- **Evidence:** Paywall exposure order LOCKED (brief: first exposure after L5 win, celebratory, dismissible); RC Paywalls v2 has 3 open Android crash issues #745/#736/#732 → device-test heavily (verified 2026-07-31).
- **Action:** Script this exact walkthrough as the PlayMode test `FirstSessionJourney` (architecture.md tests); paywall crash test on all 3 device tiers at D14 SDK spike.
- **Risk:** Paywalls v2 crash on some Android devices at the worst possible moment (post-L5 high).
- **Fallback:** Custom Unity paywall behind the same `post_level_5` RC Placement, feature-flagged; flip without a store update via Offerings metadata (LOCKED fallback, brief).

---

## 13. Player-emotion curve

| Moment | Session time | Target emotion | Design lever |
|---|---|---|---|
| First tap (L001) | 0:15 | "Oh, I get it" | Wrong-way initial route + pulsing switch |
| First chain chime | 0:40 | Small delight | Ascending pentatonic ding per chain step |
| First near-overload (L003) | 2:00 | Alarm → relief | Countdown ring designed to be saved |
| First real fail (L004) | ~3:00 | "My fault, retry" (not "cheated") | Cause-first camera names the exact miss |
| L005 win + map reveal | 4:40 | Pride + curiosity | District map pans across parked districts |
| Paywall | 4:50 | Respected | Instant dismiss, no timer, no dark pattern |
| L007 → Daily unlock | Day 0–1 | Belonging | "Same route as everyone today" framing |
| First 3★ chase | Day 1–2 | Mastery itch | Star thresholds visible pre-level with best score |
| L018 two-source | Day 2–4 | Stretch | Interleaved sources; difficulty 0.38 |
| Streak day 3 badge | Day 3 | Habit pride | Badge on share card; streak is cosmetic-only (no loss anxiety by design — copy always says progress is safe, notification_copy.csv) |
| L030 capstone | Week 1–2 | Completion + "what's next" | Post-launch district teaser + Daily Line as endgame |

- **Decision:** The curve engineers exactly one negative beat per session (a fail attributed to the player's own read), everything else warm.
- **Evidence:** Mini Metro endgame complaint is micromanagement stress (verified 2026-07-31); our fixed-length levels structurally avoid it.
- **Action:** D7 fun gate scores each beat 1–5 with 5 testers; any beat under 3 gets a named owner and a fix before D14.
- **Risk:** Curve assumes failure feels fair; a single misattributed cause-camera ruins trust.
- **Fallback:** Cause attribution has a deterministic unit-test suite (every fail_reason × board archetype); when ambiguous, the camera shows the node without assigning blame text.

---

## 14. First day / first week experience

**Day 0:** Session 1 = L001–L005 + paywall (Section 12). Session 2 (if same day): L006–L007; L007 win
fires `daily_unlocked` → Daily Line intro card → play today's Daily → **then** the push soft-prompt
(pre-permission IAM: "Want tomorrow's route when it opens?" — value-moment trigger per taxonomy). First
daily completion pays 100 tickets (LOCKED economy, brief).

**Day 1–2:** Daily Line push (journey `daily_challenge`, default 10:00 local, learned send-time later —
onesignal_journeys.csv). Campaign reaches Market Cross (L011–L015, queue-reading). First ticket cosmetic
affordable around Day 2 (Marmalade theme, 600 tickets: ~L012 pace + 2 dailies + gifts).

**Day 3:** Streak badge tier 1 (3 days). Share card push moment #1: streak badge appears on the daily
share card. `streak_risk` journey armed from now (streak_days ≥ 3 eligibility, journeys CSV).

**Day 4–5:** Two-source district (L016–L020). Difficulty first bites at L018 (0.38, 65% first-attempt
target); `hard_level_help` journey may fire (2 fails, 30–60 min later, free rewind granted — never an
upsell, journeys CSV).

**Day 6–7:** Combo district with wildcard (L021+). Second paywall-adjacent surface appears organically:
theme_preview from map (Sakura petals fall on the preview board — LOCKED placement). Week-1 target state:
L020–L025 reached, streak ≥4, 1–2 cosmetics owned, push opted in ≥35% of actives, D7 retention goal ≥12%
(top-10% GameAnalytics 2025 band; current median is ~4% — verified 2026-07-31, ranges labeled).

- **Decision:** Week 1 is campaign-led with the Daily as the habit spine; no event content in week 1 (District Cup starts Week 5, LOCKED).
- **Evidence:** Retention benchmarks (verified 2026-07-31): D1 median ~22%/top-10% ≈40; D7 median ~4%/top ≈12 — targets set at top-10% band, honestly labeled aspiration.
- **Action:** Instrument day-cohort funnels (level_started/completed by install_age_days) in the analytics backend before closed test Day 1.
- **Risk:** L018 spike churns Day-4 players if the 65% target is optimistic.
- **Fallback:** Difficulty retune is a JSON-only change (levels in StreamingAssets, content hash in save — architecture.md); L017.5-style bridge level insertable without code.

---

## 15. Core loop, 45–90 s (diagram-as-table)

| Phase | Seconds | Player does | System responds | Feel |
|---|---|---|---|---|
| 1. Read | 0–8 | Scans board; reads station colors/symbols; reads next-wave preview strip | Preview strip shows first 2 waves; switches show thrown direction | Planning calm |
| 2. First route | 8–20 | 1–3 taps set opening routes | Trains emit; first delivery chime | Competence |
| 3. Rhythm | 20–50 | Taps in tempo with wave alternation; watches queues breathe | Chain meter (purr) builds; queue bubbles grow/shrink | Flow |
| 4. Crunch | 50–75 | Handles the interleaved/peak wave; maybe saves an Overload at the ring | Countdown ring; near-miss audio riser | Controlled alarm |
| 5. Resolve | 75–90 | Final deliveries | Win: score rollup → stars → tickets → next. Fail: cause camera → retry <1 s (back to Phase 1 with knowledge) | Pride / "one more" |
| Exit ramps | at 90 | Next level; OR Daily Line; OR share card (post-daily); OR shop browse | Results screen offers exactly one primary CTA (Next), others quiet | Momentum |

Loop invariants: level duration 45–90 s (LOCKED); retry <1 s (LOCKED); rewarded offers appear only inside
the fail path per eligibility (Section 21), never in Phases 1–4.

- **Decision:** The loop's monetization surface is confined to Phase-5 fail path and post-win results footer.
- **Evidence:** Session/retry constraints LOCKED (brief); "ad every other level" is the #1 comp complaint (Arrows, verified 2026-07-31).
- **Action:** Loop timing audited per level by the validator (solver optimal time must land 40–75 s so real play lands 45–90).
- **Risk:** Read phase collapses to zero on replays, shortening loops below the fun threshold.
- **Fallback:** Retry keeps switch states from the previous attempt's tick 0 (initialRoute), not mid-run states — the read phase stays meaningful.

---

## 16. Meta progression

- **Stars** (1–3 per level, 90 total at launch): gate nothing mechanically; district cosmetic milestones read them (e.g., conductor cap at Whisker Yard 15★). Best score + stars shown on every level pin.
- **Districts** (6 launch, LOCKED 30 levels = 6×5): Whisker Yard → Harbor Line → Market Cross → Twin Platforms → Catnip Gardens → Midnight Terminus. Sequential unlock by completing the previous district (any stars). Bonus district **Rooftop Line** (L901–L910) is All Access content (LOCKED product).
- **Tickets** (soft currency, client-side — RC Virtual Currency NOT used at launch, verified/LOCKED): earn 20–50/level base + perfect bonus (matches examples: 20/25/30/40/50), 100 first daily, 30–80 daily gift; sinks: cosmetic variants 600–1200; nothing gameplay-gated; no premium currency (all LOCKED, brief).
- **Cosmetics**: themes (board+train reskins), liveries (train paint), badges (profile/share card). Full catalog in Section 17.
- **Streak badge**: consecutive Daily Line days; tiers at 3/7/14/30 (bronze/silver/gold/opal collar tags). Cosmetic-only, no gameplay power, repairable (Section 20) — loss-aversion deliberately blunted (journeys CSV harm note).

- **Decision:** All meta is horizontal (cosmetic/completion); zero power progression.
- **Evidence:** Economy LOCKED (brief); Cats&Soup shows a cosmetics spine carrying 42.7M installs (verified 2026-07-31).
- **Action:** Ticket faucet/sink model spreadsheet check: a no-IAP, no-ad player owns their first 600-ticket cosmetic by ~Day 2–3 and one per ~4–5 days after — tune gift sizes to hold that line.
- **Risk:** 90 stars is a thin completionist surface for week-3+ players.
- **Fallback:** Daily Line + streak is the designed endgame until post-launch bands 31–60 ship in Sep.

---

## 17. Collection loop

Launch cosmetic catalog (every item: id, acquisition, price):

| Item | Type | Acquisition |
|---|---|---|
| Day Cream (default) | Theme | Default |
| Marmalade | Theme variant | 600 tickets |
| Slate Night | Theme variant | 800 tickets |
| Mint Line | Theme variant | 1200 tickets |
| Sakura | Premium theme | cm_theme_sakura $2.99 (ent: theme_sakura) or included in All Access |
| Neon | Premium theme | cm_theme_neon $2.99 (ent: theme_neon) or included in All Access |
| Conductor Cap | Badge | Whisker Yard 15★ |
| Brass Whistle | Badge | Market Cross 15★ |
| Midnight Lantern | Badge | Midnight Terminus 15★ |
| Streak collar tags ×4 | Badge | Streak 3/7/14/30 |
| Gold Conductor Badge | Badge | All Access (LOCKED) |
| Founder Livery | Livery | Supporter Pack exclusive (LOCKED) |
| Supporter Badge | Badge | Supporter Pack (LOCKED) |
| Name-a-cat cameo | Flair | Supporter Pack; local rename of a recurring commuter cat (LOCKED, local-only) |
| District Cup participation livery | Livery | Weekly event completion (from Week 5, LOCKED) |

Loop: earn tickets every level → browse shop (always available, never interrupts — LOCKED) → preview any
theme on a live mini-board → buy/equip → theme visible in Daily share card → social surface feeds desire.
Theme rental via rewarded ad (3 levels, 1/theme/day — LOCKED) is the try-before-buy rung under $2.99.

- **Decision:** 15 catalog items at launch; every acquisition route (tickets/IAP/stars/streak/event) feeds one visible surface: the share card.
- **Evidence:** Catalog and prices LOCKED (brief); Neko Atsume demonstrates gentle-collection retention (verified 2026-07-31).
- **Action:** `cosmetic_unlocked/equipped` events + `preferred_theme` OneSignal tag already specced (taxonomy) — wire at shop build (Week 2).
- **Risk:** 3 ticket sinks too few; hoarding kills the earn loop's meaning.
- **Fallback:** Livery color variants are cheap content (material swap); add 2 per post-launch update, price 600–1200 per LOCKED band.

---

## 18. Daily Line spec

- **Seeded + shared**: one board per calendar date, identical for every player worldwide. Seed = lower 32 bits of SHA-256("CM-DAILY-" + UTC date "YYYY-MM-DD"). Generated on-device by the pinned generator+validator (no server — offline-first LOCKED). Generator params are version-pinned; a params change ships only with an app update and takes effect on a future date embedded in the update (no mid-day board forks). `daily_started.seed` verified identical across 2 devices in QA (taxonomy).
- **Unlock**: after L007 win (`daily_unlocked`, taxonomy). One play scores; unlimited practice replays (practice marked, doesn't change score).
- **Difficulty ramp by weekday**: Mon 0.30 → Tue 0.35 → Wed 0.38 → Thu 0.42 → Fri 0.45 → Sat 0.50 → Sun 0.55 (launch-mechanics only until the expansion ships; then rotates one post-launch mechanic in from bands the player base has reached).
- **Rewards**: first completion of the day: 100 tickets (LOCKED); `double_tickets` rewarded offer eligible here (3/day cap, LOCKED).
- **Streak**: consecutive completed days; local-midnight boundary with DST/timezone-change QA (taxonomy `streak_changed`).
- **Share card**: auto-offered post-score (Section 27): 1080×1350 PNG — date, score, stars, streak badge, a stylized trace of the player's route (switch-state timeline as a metro-map ribbon), equipped theme, and the challenge link. No PII (taxonomy QA rule).
- **Messaging**: journey `daily_challenge` (P0) + `streak_risk` (P0) exactly per onesignal_journeys.csv; local-notification backup for streak expiry via Unity Mobile Notifications (LOCKED messaging architecture).

- **Decision:** Daily is deterministic client-side generation — zero server dependency, same-for-all preserved by pinned params.
- **Evidence:** Offline-first LOCKED (brief); daily journey + copy already authored (CSVs); Wordle-pattern shared-puzzle virality motivates the share card.
- **Action:** Build generator param freeze + cross-device seed test into CI before Daily ships (Week 3); embed "params effective date" logic Day 1 of that work.
- **Risk:** A validator/generator bug ships an unwinnable daily to everyone simultaneously.
- **Fallback:** The app pre-validates tomorrow's + today's board on device at boot (solver-lite pass ≤200 ms); on failure it falls back to the dated backup pool (30 hand-validated dailies shipped in StreamingAssets, indexed by date hash) — players still share one identical board.

---

## 19. Weekly District Cup spec (from Week 5, LOCKED)

- **Cadence**: Monday 17:00 local → Sunday 23:59 local (matches `event_start` journey trigger).
- **Format**: 3 remixed levels from one rotating district with a weekly modifier (launch modifiers: Night palette; +10% wave density; queue capacity −1 — one modifier per week). Async score = sum of best scores across the 3 routes. No realtime leaderboard at launch (leaderboard feature flag OFF — architecture.md).
- **Ranking**: rank buckets (Top 10% / 25% / 50% / participant) computed against par tables baked from prior-week telemetry percentiles, shipped in the event config of each update (no backend). `daily_completed.rank_bucket` pattern reused (taxonomy).
- **Reward**: participation livery for completing all 3 routes (LOCKED: participation cosmetic); bucket result is bragging-rights text on the share card only. No purchases interact with the Cup in any way.
- **Messaging**: `event_start` (P1) + `event_ending` (P2) journeys exactly per CSVs; entry `event_joined`, completion `event_completed` (taxonomy).
- **Eligibility**: highest_level ≥ 8 (journeys CSV).

- **Decision:** Cup is a content remix + baked par tables — one day of authoring per week, no server, no prizes beyond cosmetics.
- **Evidence:** Weekly mini-event from Week 5 with async score + participation cosmetic is LOCKED (brief); subscription rejection rationale (no sustainable content cadence) caps event ambition too.
- **Action:** Build the remix pipeline (level JSON + modifier overlay → validator) in Week 4; first Cup targets Aug 31 (Week 5, Mon).
- **Risk:** Percentile par tables from small cohorts are noisy → unfair-feeling buckets.
- **Fallback:** Week-5–6 Cups show only "Finished / Personal Best" (no buckets); buckets turn on when weekly participants >1,000.

---

## 20. Comeback mechanics

- **Rewind** (the in-level comeback, LOCKED): rewinds to the last safe decision tick — the tick before the routing decision that caused the failing chain (computed from the deterministic command log; restore = snapshot at that tick). 1 free/day for everyone; extra via rewarded ad (`rewind_failure` placement, 2/session, 5/day) or consumables cm_rewind_5 $1.99 / cm_rewind_20 $4.99; daily free rewind for All Access owners; never required; never offered after a first failure (all LOCKED).
- **Session comeback (48 h+)**: welcome-back board on Home: daily gift doubled once + 1 free rewind pre-loaded (matches inactivity copy "a free rewind is waiting", notification_copy.csv). Journey `inactivity_48h` (P1).
- **7-day lapse**: `winback_7d` — content-led, zero-pressure copy (CSV); in-app: gift + 1 theme-rental token (3 levels).
- **14-day lapse**: `winback_14d` — final message, permanently ends the ladder (CSV); in-app: free premium-theme trial for 24 h (matches variant C promise). One journey implements the whole 48h→7d→14d ladder (Growth-plan 3-journey budget, LOCKED).
- **Stuck-player comeback**: `hard_level_help` — after 2 fails on one level, a tip push 30–60 min later + free rewind granted; never reads as upsell (journeys CSV, LOCKED); plus in-game: after 3 fails on any level, the planning-pause accessibility mode is offered inline.
- **Streak repair**: broken streak restorable within 24 h via `streak_saver` rewarded ad (1/day) or free grace token (1/month). Never an IAP (LOCKED: streak saver = rewarded/free only).

- **Decision:** Every comeback path gives, never asks; purchases appear only in the rewind sheet's secondary row.
- **Evidence:** Rewind economy + streak-saver rules LOCKED (brief); lapse ladder consolidated to one journey per OneSignal Growth-plan limits (3 journeys/6 steps, verified 2026-07-31).
- **Action:** Implement the returning-player state machine (last_seen gap → grant table) in Application layer with EditMode tests; grants are idempotent per calendar day.
- **Risk:** Grant stacking (48 h gift + daily gift + double offer) inflates the ticket faucet.
- **Fallback:** Grants share one daily cap (max 2× normal daily faucet); ledger-logged (`ticket_earned.source`) so the economy dashboard catches drift within a day.

---

## 21. Difficulty architecture — B/E/C/T/H/R model

Every level's `meta.difficultyTarget` (0–1, schema v2) is computed, not vibes:

| Axis | Meaning | Measured from | Weight |
|---|---|---|---|
| **B** — Board complexity | nodes + edges + switches, normalized to band caps | Static level JSON | 0.20 |
| **E** — Emission pressure | peak trains/10 s window; color interleave entropy | Waves script | 0.25 |
| **C** — Concurrency | max simultaneous pending decisions in the solver's winning trace | Solver | 0.20 |
| **T** — Time slack (inverted) | solver-optimal ticks ÷ timeLimitTicks | Solver + JSON | 0.15 |
| **H** — Headroom (inverted) | min(queue+platform slack) during solver trace peak load | Solver | 0.15 |
| **R** — Recovery (inverted) | fraction of single-mistake perturbations that remain winnable | Solver perturbation pass | 0.05 |

difficultyTarget = Σ wᵢ·axisᵢ. Validator computes all six; a level whose computed value differs from its
authored `difficultyTarget` by >0.05 fails CI. `minActionWindowTicks` (accessibility floor, default 6 ≈
750 ms) is a hard constraint on top, not an axis.

Launch band table (30 levels, **launch mechanics only: switch, queue, second-source, wildcard** — LOCKED):

| Band (schema enum) | Levels | difficultyTarget | First-attempt clear target | Mechanics available |
|---|---|---|---|---|
| onboarding | L001–L005 | 0.05–0.16 | ≥90% | switch; queue from L004 |
| alternation | L006–L010 | 0.18–0.28 | 80–88% | switch, queue |
| queue-reading | L011–L017 | 0.28–0.36 | 68–78% | switch, queue |
| two-source | L018–L020 | 0.38–0.42 | 60–65% | + second-source (new at L018) |
| combo | L021–L025 | 0.42–0.48 | 52–62% | + wildcard (new at L021) |
| multi-line | L026–L028 | 0.48–0.51 | 48–52% | all four |
| pressure | L029 | 0.53 | ~46% | all four |
| capstone | L030 | 0.55 | ~45% | all four |

- **Decision:** Difficulty is a computed six-axis score with CI enforcement; bands own target ranges.
- **Evidence:** Schema v2 already carries difficultyTarget/band/mechanics/newMechanic; shipped examples anchor the curve (L001 0.08, L006 0.20, L018 0.38); 4 launch mechanics LOCKED.
- **Action:** Implement axis computation inside the existing batch validator (Editor asmdef) by Aug 10; backfill all authored levels.
- **Risk:** Model weights mis-rank perceived difficulty (players feel E and T far more than B).
- **Fallback:** Weights live in one config file; after closed-test telemetry (first-attempt clear vs target), refit weights by regression at D21 — levels don't change, the model calibrates.

---

## 22. 30-level launch progression table

Anchors L001/L006/L018 ship exactly as in example_levels.json. Diff = computed difficultyTarget; FA = first-attempt clear target.

| Level | Name | District | Band | New element | Diff | FA |
|---|---|---|---|---|---|---|
| L001 | First Switch | Whisker Yard | onboarding | switch (mechanic) | 0.08 | 97% |
| L002 | Two Trains | Whisker Yard | onboarding | preview-strip reading | 0.10 | 96% |
| L003 | The Platform | Whisker Yard | onboarding | overflow countdown (safe demo) | 0.12 | 94% |
| L004 | Waiting Room | Whisker Yard | onboarding | queue (mechanic) | 0.14 | 92% |
| L005 | Yard Capstone | Whisker Yard | onboarding | — (mix) | 0.16 | 90% |
| L006 | Alternating Line | Harbor Line | alternation | rhythm alternation | 0.20 | 87% |
| L007 | Ferry Timing | Harbor Line | alternation | long/short edge asymmetry | 0.22 | 85% |
| L008 | Double Berth | Harbor Line | alternation | two switches, one line | 0.24 | 83% |
| L009 | Tide Tables | Harbor Line | alternation | 3-color waves | 0.26 | 81% |
| L010 | Harbor Capstone | Harbor Line | alternation | — (mix) | 0.28 | 80% |
| L011 | Market Morning | Market Cross | queue-reading | queue as buffer (intentional holding) | 0.28 | 78% |
| L012 | Stall Rows | Market Cross | queue-reading | chained queues | 0.30 | 76% |
| L013 | Fish Rush | Market Cross | queue-reading | burst wave (count 4+) | 0.31 | 75% |
| L014 | Cross Traffic | Market Cross | queue-reading | shared mid-node for 2 lines | 0.32 | 73% |
| L015 | Market Capstone | Market Cross | queue-reading | — (mix) | 0.34 | 72% |
| L016 | Mirror Tracks | Twin Platforms | queue-reading | symmetric board misdirection | 0.35 | 70% |
| L017 | Tight Headways | Twin Platforms | queue-reading | min-spacing waves (window 10 ticks) | 0.36 | 68% |
| L018 | Two Platforms | Twin Platforms | two-source | second-source (mechanic) | 0.38 | 65% |
| L019 | Split Shift | Twin Platforms | two-source | asymmetric source rates | 0.40 | 63% |
| L020 | Twin Capstone | Twin Platforms | two-source | — (mix) | 0.42 | 60% |
| L021 | Stray Guest | Catnip Gardens | combo | wildcard (mechanic) | 0.42 | 62% |
| L022 | Garden Party | Catnip Gardens | combo | wildcard-as-slack planning | 0.44 | 58% |
| L023 | Pollen Rush | Catnip Gardens | combo | wild + burst interleave | 0.46 | 56% |
| L024 | Hedge Maze | Catnip Gardens | combo | 3-switch chain routing | 0.47 | 54% |
| L025 | Garden Capstone | Catnip Gardens | combo | — (mix) | 0.48 | 52% |
| L026 | Night Shift | Midnight Terminus | multi-line | two independent lines, shared junction | 0.48 | 52% |
| L027 | Last Ferry | Midnight Terminus | multi-line | dual-source + wild convergence | 0.50 | 50% |
| L028 | Signal Storm | Midnight Terminus | multi-line | max concurrency (C-axis peak) | 0.51 | 48% |
| L029 | Rush of Rushes | Midnight Terminus | pressure | min headroom (H-axis peak) | 0.53 | 46% |
| L030 | Midnight Capstone | Midnight Terminus | capstone | full launch vocabulary | 0.55 | 45% |

Bonus district (All Access, LOCKED): **Rooftop Line** L901–L910, remix spread 0.30–0.55, launch mechanics
only, no new mechanics (nothing a free player needs is here — extra content, not gated progression).

- **Decision:** Table above is the launch content plan of record; every level solver-validated to schema v2 before authoring is "done."
- **Evidence:** 30 levels / 6 districts LOCKED; anchor levels already validated (example_levels.json); D14 gate requires 20 validated levels (brief schedule).
- **Action:** Author order: L002–L005 (Aug 4), L007–L017 (Aug 5–12), L019–L030 (Aug 12–18), L901–L910 (Aug 18–21) — 20 validated by D14 Aug 14 ✓ gate.
- **Risk:** FA targets are pre-telemetry guesses; the L018 and L028–L030 cliffs are the likely misses.
- **Fallback:** JSON-only retune path (Section 14 fallback); each capstone has a pre-authored "-easier" variant on the shelf (wave spacing +2 ticks, capacity +1) that can swap in within one content update.

---

## 23. 100-level expansion framework (bands 31–100)

Post-launch mechanics enter in LOCKED order: cooldown + gates in bands 31–60 (Sep updates), express +
reversible in expansion 61–100 (post-event). One new mechanic per band start; validator's
one-new-mechanic rule continues to apply.

| Levels | Band (schema enum) | New mechanic | Diff range | Ships |
|---|---|---|---|---|
| L031–L041 | combination | cooldown (L031: switches lock cooldownTicks after toggle) | 0.50–0.58 | Sep update 1 (districts 7–8) |
| L042–L055 | timed-gates | gate (L042 "Rush-Hour Gate" — already authored, example_levels.json) | 0.55–0.68 | Sep update 2 (districts 9–10) |
| L056–L060 | pressure | — (cooldown+gate mastery) | 0.68–0.72 | Sep update 2 |
| L061–L075 | combination | express (L061: express cats cannot wait in node queues) | 0.70–0.80 | Post-event |
| L076–L090 | expert | reversible (L076: reversible edges flip flow direction) | 0.78–0.88 (anchor: L088 "Last Local" 0.83, authored) | Post-event |
| L091–L100 | capstone | — (full vocabulary) | 0.86–0.95 | Post-event |

Notes: not every level uses every unlocked mechanic (L088 ships without express/reversible — legal and
intended). Daily Line begins rotating a post-launch mechanic in only after the median active player has
reached its introducing band. Schema v2 already reserves all four mechanics, gates, express flags, and
reversible edges — zero schema migration needed for the entire 100-level arc.

- **Decision:** 70 expansion levels in 6 bands, mechanics in LOCKED order, schema-stable.
- **Evidence:** Mechanics sequencing LOCKED (brief); L042/L088 anchors already authored and solver-validated in the examples file.
- **Action:** Bands 31–60 authoring starts Sep 1 (after 1.0 submission), targeting Sep 12 and Sep 22 updates inside the event window (Growth-by-numbers criteria reward live momentum, verified 2026-07-31).
- **Risk:** Expansion authoring competes with launch-week firefighting.
- **Fallback:** Bands 31–60 slip cleanly (Daily Line + Cup carry retention); nothing in the event submission depends on them.

---

## 24. Level validation strategy (summary)

Pipeline (Editor asmdef `CatMetro.Editor`; runs per content PR in CI — architecture.md), referencing
schema v2 as the contract:

1. **Schema gate** — JSON Schema v2 validation (level_schema.json), including id pattern, band enum, mechanic enums.
2. **Static analysis** — graph connectivity; every station reachable from a source able to emit its colors; no orphan switches; junction spacing ≥1.2 grid units (fat-finger rule, Section 8); switch-in-top-15% warning.
3. **Lower-bound feasibility** — min travel ticks per required delivery ≤ timeLimitTicks with slack.
4. **Solver pass** — BFS for ≤2-switch boards, beam search (widths 1k/2.5k/5k) beyond; shares the exact Domain step function (architecture.md). Must find a win.
5. **Triviality reject** — a zero-input run must NOT win (exception: none; even L001 requires its one tap by design).
6. **Brittleness / accessibility** — perturbation pass: winning command logs jittered ±1 tick must retain ≥70% win rate; no solution may require action windows below `minActionWindowTicks` (floor 6 ticks ≈ 750 ms; onboarding uses 12–16).
7. **Star check** — 3★ threshold achievable by solver within band slack (schema v2 rule).
8. **Difficulty check** — computed B/E/C/T/H/R within ±0.05 of authored target (Section 21).
9. **Novelty check** — feature-vector distance (board topology + wave signature) vs all prior levels above threshold; kills recycled-level drift (Bus Traffic Fever's core complaint, verified 2026-07-31).
10. **Staleness** — `meta.validatedAt` older than the last sim/schema change fails CI (schema v2 rule).
11. **Human playtest** — every shipped level played by a human on device; capstones by 3 testers.

- **Decision:** Nothing ships that hasn't passed all 11 stages; the validator is the second product.
- **Evidence:** Solver-validated JSON levels LOCKED (brief); schema v2 encodes stages 1/6/7/10 already.
- **Action:** Stages 1–5 running by Aug 8; 6–10 by Aug 12 (ahead of the D14 "20 validated levels + solver" gate, brief schedule).
- **Risk:** Beam search misses wins on high-C boards → false "unwinnable" rejections late in the expansion.
- **Fallback:** Width escalation to 5k then a human-provided witness replay (a recorded human win is admissible proof and is stored with the level).

---

## 25. Balancing telemetry plan

Sources: analytics taxonomy events (already specced): `level_started/completed/failed/quit`,
`switch_toggled` (10% sample), `rewind_used`, `daily_started/completed`, `perf_sample` (1%).

| Metric | Definition | Target | Alert threshold |
|---|---|---|---|
| First-attempt clear per level | completed(attempt=1)/started(attempt=1) | Section 22 FA column | ±10 pts from target |
| Attempts-to-clear p90 | per level | ≤4 (≤6 for capstones) | >6 (>8 capstones) |
| Fail-reason mix | share of queue_overflow/platform_overflow/time_out | no reason >70% on any level | >80% |
| Quit-without-retry rate | level_quit after fail / fails | <8% | >15% (rage signal) |
| Level duration p50 | duration_s on wins | 45–90 s | outside 40–100 s |
| Rewind attach | rewind_used/eligible fails | 10–25% | >40% (level too brittle) |
| Daily participation | daily_started/DAU with daily_unlocked | ≥35% | <20% |
| Funnel survival | % of installs reaching L010/L018/L030 | 60%/35%/15% | −10 pts |

Cadence: closed test (Aug 1–14) reviewed daily; post-launch retune windows Tue/Fri (JSON-only content
updates). Every retune is logged level_id → change → expected metric delta, and validated against the
FA target the following window. `switch_toggled` heatmaps (tick histograms per switch) diagnose WHERE a
level fails, not just that it fails. Difficulty-model refit (Section 21 fallback) at D21 and D42.

- **Decision:** Balance is managed to the Section 22 targets with named alert thresholds and a twice-weekly retune cadence.
- **Evidence:** Event taxonomy already carries every needed param (attempt, fail_reason, congested_node, duration_s); retune-transparency is even a notification variant (hard_level_help B, copy CSV).
- **Action:** Build the per-level dashboard (one page: FA vs target, fail mix, duration) before closed test Day 1; it is the D7 fun-gate's data half.
- **Risk:** Closed-test n (12 testers, verified Play requirement 2026-07-31) is far too small for per-level stats.
- **Fallback:** Closed test judges only tutorial completion + session length + qualitative; per-level thresholds arm at >500 installs post-launch.

---

## 26. Audio / haptics / juice priority table

| Item | What | Priority |
|---|---|---|
| Switch clack + light haptic tick (VibrationEffect tier 1) | The core verb must feel chunky | P0 |
| Delivery chime, pentatonic ascending per chain step | The reward channel; sells combo without reading numbers | P0 |
| Overload countdown ring + riser audio | The threat channel | P0 |
| Fail thud + cause-camera pan | Failure clarity | P0 |
| Win rollup: score ticks, star pops, ticket count-up | Results juice | P0 |
| Mute-friendly design pass (all critical info visual) | Sound-off commuters are the primary persona | P0 |
| Purr meter: board hum + cat tail-sync at chain ≥3 | Flow-state feedback | P1 |
| Layered combo music stems (central mixer, stems add per chain tier — architecture.md) | | P1 |
| District ambience beds (harbor gulls, market chatter, night crickets) | | P1 |
| 3★ purr + cat pile-up celebration on capstones | | P1 |
| Haptic tier 2/3 (overload warning, win) with master toggle | | P1 |
| Per-cat meow variety (5 silhouettes × 3 meows) | | P2 |
| Day/night lighting cross-fade on Midnight Terminus entry | | P2 |
| UI whoosh/paper-slide transitions | | P2 |
| Idle cat animations on stations (grooming, loafing) | | P2 |
| Cut: dynamic music system beyond stems, positional audio, voice lines | | Cut |

- **Decision:** P0 list is exactly the vertical-slice juice budget; P1 lands by launch; P2 is post-launch polish only.
- **Evidence:** Audio architecture LOCKED as AudioSource pools + mixer, no FMOD/Wwise (architecture.md); haptics via VibrationEffect JNI, 3 tiers + toggle (architecture.md).
- **Action:** P0 audio assets sourced/licensed by Aug 8 (original or CC0 only — submission video forbids third-party music, verified 2026-07-31; keep game audio clean for capture reuse).
- **Risk:** Juice creep eats the D14 gate.
- **Fallback:** The game must pass the D7 fun gate with P0 items only; if it can't, the problem is design, not missing juice — that triggers the brief's hard-cut process, not more polish.

---

## 27. Social / sharing

- **Share card PNG**: generated on-device (RenderTexture composite, 1080×1350). Contents: CAT METRO wordmark, date or level name, score + stars, streak badge, route-ribbon visualization (player's switch timeline drawn as a metro-map ribbon), equipped theme colors, QR-free short link. No PII, no username (none exists) — taxonomy QA rule "no PII in image". Surfaces: post-Daily (primary), any 3★ win (secondary), District Cup result.
- **Challenge deep link**: `catmetro://challenge/{seed}` + https App Link `https://catmetro.io/c/{seed}` (domain registered Day 1, Section 1). Opening with the app installed → straight into that seeded board (`challenge_opened`, source=link); without the app → Play listing with the seed preserved through install referrer. Invalid/expired seed → home, gracefully (taxonomy QA).
- **Share targets**: Android share sheet only — no in-app social network, no friends list, no chat (age-rating and scope, Sections 6/29).
- **Events**: `scorecard_shared` (mode, seed_or_level, channel_if_known), `challenge_opened` (taxonomy). Share rate target: ≥8% of daily completions share; challenge-link K-factor measured from install_referrer.
- **Build-in-public tie-in**: the share card is also the daily #BuildInPublic asset (real scorecards as progress posts — P0 award target, $30k, verified 2026-07-31).

- **Decision:** One share artifact (the card) + one viral loop (seed challenge), both serverless.
- **Evidence:** Daily-seed sharing is the Wordle-proven loop; Most Viral is a P1 award target (brief); events already specced (taxonomy).
- **Action:** Card renderer built Week 3 (after Daily ships); App Links verified (assetlinks.json on catmetro.io) before public launch; test cold/warm/killed-state routing (taxonomy QA).
- **Risk:** `.io` App Link verification or install-referrer seed passthrough fails on some OEM browsers.
- **Fallback:** The card itself carries a human-readable code ("Route CM-0824"); manual code entry field on Home (`challenge_opened`, source=code — already in taxonomy).

---

## 28. Scope ladder: MVP → launch → post-launch → cut

**MVP / vertical slice (D14 gate, Aug 14):** Domain sim + replay determinism; L001–L020 authored (≥20
validated — brief gate); tutorial trio; cause-first fail camera; instant retry; stars/score/tickets;
Home map District 1–4; save v1 (atomic, versioned); closed-test build with RC + OneSignal + AdMob SDK
spikes passing on device (brief D14); placeholder-quality art on final palette.

**Launch scope (1.0, Aug 24–28):** 30 levels + Rooftop Line (L901–L910); full IAP catalog (cm_all_access
$6.99, cm_supporter_pack $9.99, cm_theme_sakura $2.99, cm_theme_neon $2.99, cm_rewind_5 $1.99,
cm_rewind_20 $4.99) with RC entitlements + Placements (post_level_5, theme_preview, bonus_district, shop,
rewind_failure); RC Paywalls v2 on post_level_5 with custom fallback; 5 rewarded placements with LOCKED
caps + RC AdTracker on every ad event; Daily Line + streak + share card + challenge links; 3 OneSignal
journeys live (daily/streak in one, lapse ladder, hard-level help) + IAM + local-notification streak
backup; shop + 15-item cosmetic catalog; accessibility set (Section 24 P0s); offline-first everything;
feature flags per architecture.md.

**Post-launch (Sep, event window):** District Cup weekly from Aug 31 (Week 5); bands 31–60 in two Sep
content updates; price A/B $4.99 vs $6.99 via RC Experiments if plan allows, else sequential offerings
(LOCKED); Noise-platform go/no-go ~Day 21; Stripe Funnel Vision go/no-go Day 35 (P2 stretch, LOCKED);
learned notification send-times; difficulty-model refit; livery variant drops.

**Post-event (Oct+):** bands 61–100 (express, reversible); leaderboard flag evaluation; iOS evaluation;
Virtual Currency server migration evaluation (brief: revisit post-launch); subscription re-evaluation only
if weekly cadence proved durable (LOCKED condition).

**Explicit cut list (LOCKED unless noted):** currency packs; streak-saver IAP; chapter packs; audio
packs; monthly club; season pass; ANY subscription; interstitials; banners; app-open ads; bundle SKU
(decoy confusion — All Access IS the bundle); premium currency; energy system; loot boxes; realtime
leaderboards at launch (architecture flag OFF); Addressables; remote config beyond RC Offerings; FMOD/
Wwise; cloud-save backend (Play auto-backup only); Unity IAP package (duplicate BillingClient risk,
verified 2026-07-31).

- **Decision:** Scope ladder above is the plan of record; anything not listed is out.
- **Evidence:** Monetization catalog, caps, placements, schedule gates all LOCKED (brief); D7/14/21/28/35/42 hard cut gates per roadmap.
- **Action:** Each rung gets a one-page gate review on its date; the D14 review explicitly signs off MVP completeness before commercial polish begins.
- **Risk:** Rooftop Line (10 paid-content levels) is the most cuttable launch item and the most tempting to slip.
- **Fallback:** If D21 is red, Rooftop Line ships in the first Sep update instead; All Access buyers before then get it as an automatic content drop (entitlement already grants it — messaging: "bonus district arrives this week"), and the paywall copy says "includes the Rooftop Line district (arriving this week)" — never sell what isn't dated.

---

## 29. Vertical-slice acceptance test (D14 checklist)

Run on: low-tier (Mali, API 24-29, 720p), mid-tier (Pixel 6a-class), 16 KB-page emulator (matrix per
architecture.md). ALL must pass:

- [ ] Replay determinism: 3 recorded command logs × 3 devices → identical outcome hashes.
- [ ] L001–L020 all pass validator stages 1–8; ≥20 levels have `validatedAt` current (D14 gate, brief).
- [ ] Tutorial: 5 fresh testers finish L003 with zero verbal help; ≥4/5 finish ≤3:00.
- [ ] Fun gate evidence: ≥5 testers replayed voluntarily (D7 gate carried forward, brief).
- [ ] Retry latency <1 s from fail-acknowledge tap to sim tick 0 (measured, mid-tier).
- [ ] Cause-first camera names the correct cause in 20/20 scripted failure scenarios.
- [ ] Frame time ≤16.6 ms p50 / ≤22 ms p95 during L018 peak wave on mid-tier; zero GC allocs/frame in Playing after warm-up (budgets, architecture.md).
- [ ] Cold start ≤3.5 s to Home on mid-tier; AAB ≤60 MB.
- [ ] Save: process-death during save/win/purchase-mock leaves a consistent state (adb kill suite, architecture.md); save migrates v0→v1.
- [ ] RC spike: sandbox purchase + restore of cm_all_access grants `all_access` entitlement; offline entitlement cache honored; consumable cm_rewind_5 credits ledger exactly once under duplicate-callback fire (taxonomy QA).
- [ ] RC Paywalls v2 renders post_level_5 on all 3 test devices without crash (issues #745/#736/#732 watch — verified 2026-07-31); custom fallback flag flips cleanly.
- [ ] AdMob rewarded test ad: load, show, reward exactly once; airplane-mode failure path graceful; AdTracker events visible in RC dashboard.
- [ ] OneSignal: test device receives a push; deep link catmetro://daily routes from cold/warm/killed; `$onesignalUserId` attribute visible in RC.
- [ ] One EDM4U instance; Force Resolve diff clean; no duplicate BillingClient; release-minified R8 build boots (risk zones, architecture.md).
- [ ] 16 KB-page emulator boots and plays L001–L005.
- [ ] Colorblind sim pass on the golden frame (deutan/protan/tritan): all 5 lines distinguishable by symbol+silhouette alone.
- [ ] Analytics: first-session event stream (first_open → tutorial_completed → level_* → paywall_viewed) arrives with correct params per taxonomy.

- **Decision:** This checklist IS the D14 gate; a red line item blocks commercial-beta work.
- **Evidence:** Gate dates and contents from LOCKED schedule spine (brief); budgets and kill-tests from architecture.md.
- **Action:** Automate items 1, 5, 7, 8, 9 in CI by Aug 10; the rest are a scripted 3-hour manual pass on Aug 13–14.
- **Risk:** SDK-related items (RC/OneSignal/AdMob) fail for environment reasons and stall the gate.
- **Fallback:** Feature flags (architecture.md) let the slice pass with any single commercial SDK dark; the D21 commercial-beta smoke is the true SDK gate, and the brief's fallback ladder (custom paywall, AppLovin MAX 8.6.4 fallback ad SDK) is pre-approved.

---

## 30. Ten features that look impressive but must NOT be built

| # | Tempting feature | Why it's a trap | Do instead |
|---|---|---|---|
| 1 | Level editor / UGC sharing | Moderation, UGC rating implications, months of UI; zero event value | Seed-challenge links share boards without authoring tools |
| 2 | Realtime multiplayer races | Netcode + servers vs an offline-first LOCKED architecture | Async District Cup scores + shared Daily |
| 3 | Endless/procedural marathon mode at launch | Unvalidated levels break the "every level fair" promise; validator can't certify infinity in Aug | Daily Line IS the procedural mode — one validated board/day |
| 4 | Free-draw track building (Mini Metro-like) | Explicitly excluded by LOCKED sim (no free-drawing); different game, different solver | Switch-routing is the identity |
| 5 | Meta city-builder (spend tickets on buildings) | Second economy, art explosion, Families-reading risk | Cosmetic themes give the same "my metro" feeling |
| 6 | Cat gacha / collectible cat pulls | Loot-box = LOCKED cut; age-rating and trust damage | Fixed-price cosmetics + earnable variants |
| 7 | Custom account system + cloud save backend | Server, auth, GDPR surface for zero launch value | Play auto-backup + RC restore covers device moves |
| 8 | 3D camera orbit/zoom | Breaks one-thumb + readability contract; re-QA every level's tap zones | One perfect 30° angle, art-directed once |
| 9 | Story campaign with cutscenes | Content cost stolen from levels; skippable by 95% | Character flavor via district names, cameo cat, share cards |
| 10 | In-house replay-video export (mp4 sharing) | Encoder rabbit hole (weeks) for marginal share lift | Static share card + deterministic replays feeding the Capture rig for OUR ad creative |

- **Decision:** These ten are pre-refused; any reappearance requires overturning the brief, not a vibe.
- **Evidence:** Each collides with a LOCKED decision (offline-first, no loot boxes, tap-only, no free-drawing, solo-dev 8-week budget) or the 2025 calibration (Grand winner shipped focused: 17k users/$30k — verified 2026-07-31).
- **Action:** List pinned in the repo README's "NOT building" section — it is also #BuildInPublic content (scope honesty posts perform).
- **Risk:** Post-launch success pressure resurrects #3 (endless mode) as the loudest player request.
- **Fallback:** The 100-level framework + Daily rotation is the standing answer; if demand persists post-event, a validator-certified "generated marathon" gets a proper spec — after Oct 13 judging ends.
