# Cat Metro — Shipaton submission plan

For this submission, **the media is the product**. Judges may decide from the description, one
screenshot, and the first two minutes of video without installing the app. The final assets must
therefore show a real, reachable release-candidate state clearly enough that they stand on their
own.

Two eligibility gates outrank every creative decision:

1. Cat Metro must be publicly downloadable from a store by **2026-09-30 23:45 PDT**. “In review”
   and TestFlight do not count.
2. The public binary must use the RevenueCat SDK for a real purchase or ad. This plan uses a
   named StoreKit 2 purchase and entitlement through RevenueCat.

Ship iOS first. Google Play remains a follow-on because its 12-testers-for-14-continuous-days path
plus review needs roughly 25 days. Store uploads, signing, asset-distribution approval, and public
release are human-only steps.

The primary tracks are **Best Game**, **OneSignal — Keep Them Coming Back**, **Design**, and
**HAMM**. **Do not enter Catvertising on the current candidate:** its rewarded placements are
disabled and no ad network is wired.

This plan mines the earlier submission draft, the supplied Shipaton research brief, and
`docs/store/*`, and uses `docs/LOOK.md` plus the human-curated
`docs/reference/gen-ref-*` pack as the polish ceiling. Those generated references are direction,
not submission evidence. Every submitted pixel must come from Cat Metro.

---

## 1. Video script

**Final runtime:** 1:45 (105 seconds), leaving 15 seconds below the two-minute cap.

**Master:** 1920×1080 landscape H.264. Place clean portrait iPhone capture at maximum readable
height on a quiet warm-paper field. No device bezel. Use burned captions, original narration, and
no third-party music. Host the final on YouTube or Vimeo and verify the exact link plays at 1080p
for a logged-out viewer.

### 1.1 The first five seconds

The opening is one unbroken, real-device cause-and-effect loop:

| Time | Exact picture and sound |
|---|---|
| **0:00.0–0:00.7** | Already in play. One engine and its single occupied carriage approach a junction on the warm wooden board. The cat's colour-and-shape destination pin and its matching station badge are both readable. Locked camera; no title card. |
| **0:00.7–0:01.2** | A small editorial touch ring marks the recorded tap. The shipped lever turns and the point rails visibly change. Do not show a physical thumb in a native screen recording. VO begins: “Tap the switch…” |
| **0:01.2–0:04.4** | The train takes the newly selected branch, reaches the matching station, clears from the route, and the shipped delivered counter increments. No cut and no speed ramp. VO completes: “…Match the cat.” |
| **0:04.4–0:05.0** | Hold the successful board state. `CAT METRO` appears small at lower left. No new speech. |

If a reachable authored state cannot complete that loop by 0:04.4 at shipped speed, the opening is
not ready to record. Choose or tune the gameplay state; do not accelerate footage or invent an
arrival animation.

This opening is deliberate for four reasons:

- It completes the entire verb—tap, switch, route, match, feedback—before a judge can leave. The
  old draft only began the tap at second four; that was a setup, not a five-second payoff.
- It front-loads Cat Metro's scarce asset: a tactile wooden object with motion inside a locked
  frame. The visual craft and the game rule arrive together.
- One unbroken response from actual input is stronger product evidence than a “real gameplay”
  caption.
- It puts the project's best claim in front of a panel that includes Scott Cameron of Pok Pok,
  whose work makes him unusually fluent in wooden-toy interaction. The actual render must earn
  that comparison; the reference art cannot do it for us.

### 1.2 Shot-by-shot cut

Times are cumulative and exact. The fixed cut contains no placeholder block for an unbuilt award
feature.

| # | Time | Dur | Picture and action | Caption | Voiceover / audio | Proof required before shooting |
|---|---|---:|---|---|---|---|
| **1** | **0:00–0:05** | 5s | The unbroken tap → moving lever/points → matching arrival → delivered-counter increment described above. One engine, one occupied carriage; no invented three-car consist. | `TAP THE SWITCH` at 0:00.8; `MATCH THE BADGE` at 0:03.3; `CAT METRO` at 0:04.4. | “Tap the switch. Match the cat.” | The whole causal loop fits at shipped speed in a reachable candidate state. Cats, pin, station badge, switch, and counter are readable at laptop playback size. |
| **2** | **0:05–0:14** | 9s | Three short candidate clips: the wave capsule previews the next cat; a second junction decision; the real clear/result state. | `ONE CONTROL. MANY ROUTES.` | “That is the control scheme. The route gets harder; the gesture does not.” | Actual wave UI and win state; no hand-posed state or editor-only flag. |
| **3** | **0:14–0:23** | 9s | A wrong delivery produces the shipped failure review. Tap the real retry control and show the board restart. | `WRONG STATION` → `RETRY` | “A wrong station ends the attempt. Retry starts the board again.” | Candidate failure and retry path recorded end to end. Do not promise an exact retry duration. |
| **4** | **0:23–0:31** | 8s | Four distinct authored boards, two seconds each: different routes, station pairs, switch counts, and furniture. Match camera and exposure so the cuts feel like opening a box of layouts. | `19 AUTHORED BOARDS` / `CHECKED IN THE GAME'S SIMULATION` | “Nineteen authored boards use the same small rule in different ways.” | All 19 levels pass final validation and are reachable through ordinary candidate progression. Otherwise replace the number with the proven count. |
| **5** | **0:31–0:49** | 18s | Wardrobe: large profile cat without the coat; named `Conductor's Coat` card with localized price; player taps Buy; the native StoreKit confirmation appears; purchase completes; return to the same portrait with coat and hat visibly equipped. Keep tap → sheet → unlock uncut. | `CONDUCTOR'S COAT` → `REVENUECAT-POWERED PURCHASE` → `PERMANENTLY UNLOCKED` | “The filmed purchase is Conductor's Coat. RevenueCat loads the product and carries its permanent entitlement through StoreKit.” | Production product, offering, entitlement, public SDK config, real iPhone transaction, and visible unlock. The StoreKit sheet—not RevenueCat—provides the native confirmation UI. |
| **6** | **0:49–0:58** | 9s | After an off-camera delete/reinstall under the same App Store test account, open Wardrobe. If the candidate presents the coat locked, tap the shipped Restore control; entitlement returns and the coat reappears. If RevenueCat restores automatically on initialization, film that truthful behavior and caption it `ENTITLEMENT RESTORED` instead of staging a locked state. | `RESTORES WITH REVENUECAT` or `ENTITLEMENT RESTORED` | “The entitlement returns on a clean install.” | Real reinstall-and-restore behavior on the same signed iOS candidate. A relaunch, fixture backend, or Unity test still is not proof. |
| **7** | **0:58–1:11** | 13s | Daily entry → a real Daily completion → the cumulative lifetime tally increments by one. Then a clean lock screen receives the OneSignal reminder within the player's chosen morning/afternoon/evening window; tapping it opens that day's Daily route. | `A DAILY ROUTE` → `LIFETIME TALLY — NEVER EXPIRES` → `OPENS TODAY'S BOARD` | “Daily adds one dated route and a lifetime tally that never resets. If the player chooses a reminder window, one notification opens today's board.” | Daily is unlocked through the real campaign rule, tally persistence is device-proven, and a real OneSignal notification deep-links correctly. |
| **8** | **1:11–1:30** | 19s | Three held candidate beauty beats: 5s Home/menu diorama; 8s board-filling low portrait view with seated cat head, destination pin, thick track, lever, and warm desk; 6s purchased Wardrobe portrait. Use shipped UI only where it belongs. | Last 5s only: `A WOODEN PUZZLE YOU CAN TOUCH` | “The menu, board, train, and wardrobe belong to the same object: navy, cream, warm wood, and late-afternoon light.” | Each surface reaches the purpose of its matching `gen-ref-*` target and survives phone-size inspection. |
| **9** | **1:30–1:39** | 9s | Cat Metro wordmark on warm paper, plain public store URL beneath it. No store badge or borrowed logo. Add only after the URL resolves for a logged-out viewer. | `PUBLICLY LIVE ON THE APP STORE` / public URL | “Cat Metro is publicly available on the App Store.” | Public product URL. Never render “available” from an approval email, TestFlight page, or review state. |
| **10** | **1:39–1:45** | 6s | Static category card. | `BEST GAME · KEEP THEM COMING BACK · DESIGN · HAMM` | Silence. | Exact set of tracks actually entered. No Catvertising on the current candidate. |

**Total: 1:45.** Do not pad the remaining 15 seconds.

### 1.3 Why the purchase is at 0:31

The RevenueCat-powered purchase begins **31 seconds in**, within the first third of the video,
and receives 27 seconds when purchase and restore are counted together. It is early because
RevenueCat runs the competition, it is the HAMM argument, and it visibly supports the second hard
eligibility gate.

The proof chain must be legible and truthful:

1. A named item and localized store price are visible before the tap.
2. The action is player-initiated; it is not an ambush paywall.
3. RevenueCat supplies the product/offering path and observes the entitlement while StoreKit 2
   displays the native confirmation.
4. The same profile cat visibly changes after the transaction.
5. Restore recovers that entitlement from a clean state.

`REVENUECAT-POWERED PURCHASE` is an editorial caption set in Cat Metro's own type. Do not paste a
third-party logo into the video. Do not claim revenue unless a dated RevenueCat report supports
the number.

### 1.4 Edit fallbacks

If a proven take runs long, cut in this order:

1. Reduce shot 8 from three beauty beats to one, recovering up to 12 seconds.
2. Reduce shot 4 from four boards to three, recovering 2 seconds.
3. Shorten the Daily gameplay portion of shot 7, but preserve tally increment and notification
   deep link if entering Keep Them Coming Back.

Never cut the five-second gameplay loop, the purchase, the visible unlock, the public-live card,
or the final track card. If purchase/restore cannot fit in its allotted footage, take time from
beauty—not by speeding or splicing the transaction.

### 1.5 Audio, captions, and rights

- Use original narration normalized consistently. The audited candidate contains no game audio,
  so the honest default is narration over silence.
- Do not invent switch clicks, arrival chimes, or other product sounds in post. If original audio
  is implemented before release, record it from the candidate.
- Burn captions and complete a full muted watch.
- Clear every unrelated notification before the lock-screen take. Remove personal account data,
  test-user emails, debug overlays, and store sandbox credentials from every frame.
- Use no third-party music, transit marks, award laurels, or borrowed store/RevenueCat logos.

---

## 2. Hero screenshot specification

Submit **exactly one image**: `1179×2556`, opaque sRGB PNG, portrait, full bleed, with **no device
frame**. Do not upload an alternate, comparison, reference image, or captioned second version.

### 2.1 The one composition

Use the richest reachable mid-band board that remains readable at thumbnail size. The decisive
moment is one engine and its **single occupied carriage** entering a junction whose lever and
points are visibly set toward the station matching the cat's colour-and-shape pin. Select a
reachable moment with no second consist visible. Do not invent the old draft's three-carriage
train.

The frame should contain:

| Element | Composition target |
|---|---|
| Board | Fills the portrait width and most of the height; low, square three-quarter framing rather than a remote plan view. Leave only a warm desk margin. |
| Decision | Junction and lever near the optical centre. Track direction from the cat to its matching station can be reconstructed at a glance. |
| Cat | Head and ears clearly above the carriage wall, with the destination pin separated from the silhouette. It must read as a cat at 20% size. |
| Stations | At least two distinct, reachable destinations identified by both colour and shape. No text station signs. |
| Shipped HUD | Wave capsule and delivered/rider counters may remain. No added Score/Moves panel and no marketing copy. |
| World detail | Warm wooden board, thick navy rails and cream sleepers, depot and admitted props, soft upper-left key, contact shadows. Use desk clutter only if it ships and does not compete with the junction. |

### 2.2 Reference hierarchy

The new pack is the ceiling for purpose and finish, not a layout to copy literally:

| Reference | Take from it | Deliberately reject |
|---|---|---|
| `docs/reference/gen-ref-board-framing.jpeg` | **Primary framing target:** board fills the portrait frame, camera is frontal enough for carriage occupants to read, tracks have weight, and the desk establishes scale. | Text station signs, Score/Moves HUD, invented routes, or any detail the game cannot render. |
| `docs/reference/gen-ref-cats-on-train.jpeg` | Cat-to-carriage scale: head well above the wall, faceted silhouette, face readable before fine texture. | Its exact train construction if it conflicts with the one-carriage runtime. |
| `docs/reference/gen-ref-menu.png` | Carved navy identity, stitched/soft cream controls, dense lamp-lit diorama, coherent palette. This informs shot 8, not the hero subject. | Literal buttons or decoration that do not exist in the shipped Home screen. |
| `docs/reference/gen-ref-wardrobe.png` | One large cat portrait on a stand, legible item card, clear Equip/Shop hierarchy. | Coin/gem price chips. The shipped named item uses its localized real-money store price. |
| `docs/reference/gen-ref-NOTES.md` | Navy/cream/warm wood/teal/tomato/marigold palette; matte late-afternoon lighting; shape badges and wave capsule. | Currency, text station labels, Score/Moves furniture, or reference-only mechanics. |

`docs/LOOK.md` remains the palette and material law. The generated images never appear in the
submission and never count as proof that the release candidate reached them.

### 2.3 Exclusions

- No caption band, headline, marketing text, logo, store badge, or award mark.
- No device bezel, status bar, home indicator, editor chrome, debug overlay, or touch indicator.
- No hand, finger, fail/result panel, menu, paywall, or second mechanic.
- No hand-placed train, hidden editor flag, synthetic replacement, paint-over, or reference-art
  composite.
- No motion blur or post effect beyond what ships.

“No added text” does not mean deleting shipped UI. It means the accepted PNG is an unmodified
candidate frame after the status/navigation chrome is handled by the app's real full-screen
presentation.

### 2.4 Pixel and acceptance workflow

Preferred source order:

1. **Exact-size iPhone screenshot.** Use the signed candidate on an iPhone whose captured PNG is
   confirmed by inspection to be exactly 1179×2556. This gives the strongest artifact truth.
2. **Exact-size Unity rig.** Add a deterministic 1179×2556 option to the board-look rig and render
   the same reachable state. Use it only after side-by-side comparison with the signed candidate
   proves the camera, materials, safe area, and UI are identical.
3. **Aspect-preserving crop from a larger iPhone capture.** Inspect the actual source dimensions,
   crop symmetrically around the intended composition, and never stretch or upscale. Do not reuse
   the old Pixel crop arithmetic for an unknown iPhone.

The current Unity still rigs output 917×2048, so they are composition rehearsals, not valid final
sources until the exact-size option exists.

Acceptance checks:

- PNG inspector reports exactly 1179×2556, opaque sRGB; no accidental alpha or resampling halo.
- At 20% (236×511), a cold viewer can still identify toy railway, seated cat, destination pin,
  matching station, and the switch decision.
- Compare against a screenshot from the signed candidate for missing materials, placeholder cats,
  absent props, safe-area drift, and UI mismatch.
- Record commit, build number, device/rig, level ID, simulation state/tick, capture date, and file
  SHA-256 in the media receipt.

---

## 3. Written submission copy

Publish no sentence until its row in the claim ledger is proven against the exact public
candidate. If a feature slips, delete the sentence; do not turn it into an implication.

### 3.1 Main description

> Cat Metro is a tabletop train-routing puzzle for iPhone. Cats leave the depot carrying
> colour-and-shape destination badges. Tap a junction to move the points and route each little
> train toward the matching platform.
>
> Deliver every cat correctly to clear a board. A wrong delivery ends the attempt, and retry
> starts the board again. The release includes nineteen authored boards checked against the same
> simulation the game runs, plus a Daily route derived from the date.
>
> The game is a low-poly wooden railway on a warm desk: navy rails, cream sleepers, chunky
> stations, open carriages, and late-afternoon light. Each destination is identified by both a
> colour and a shape badge.
>
> The filmed purchase is Conductor's Coat, a named permanent Wardrobe entitlement with a
> localized App Store price. RevenueCat powers the product, purchase, entitlement, and restore;
> StoreKit 2 provides the native confirmation. There is no paid randomness, energy, subscription,
> forced ad, or interstitial.
>
> Daily keeps a cumulative lifetime tally. Missing a day expires nothing.

### 3.2 Claim ledger

| Public sentence | Evidence required |
|---|---|
| “for iPhone” / publicly available | Signed candidate, installed-device proof, and a public App Store URL. |
| Cats carry colour-and-shape badges | Candidate render of the actual runtime cat, pin, and independent station badge. |
| Nineteen authored boards | Final corpus validation plus ordinary player progression reaching L001–L019. A file count alone is insufficient. |
| “checked against the same simulation” | Final solver/corpus run on the candidate content tree with retained output. |
| RevenueCat purchase and restore | Production configuration and dashboard mapping, native iPhone purchase, visible entitlement, clean-state restore, and public binary parity. |
| No paid randomness / energy / subscription / forced ad / interstitial | Exact-candidate catalog and ordinary-flow inspection. |
| Daily route and lifetime tally | Real completion recorded once, relaunch persistence, and increment without an expiring streak. |

Do not add an accessibility outcome such as “works for colour-blind players” until that outcome has
been tested. The defensible product fact is that colour and shape are both present.

### 3.3 Best Game

> Tap a switch to route cats carrying colour-and-shape destination badges. The opening records one
> tap, the points moving, and a correct delivery inside five seconds. Later boards add route
> pressure without adding a second control scheme.
>
> Nineteen authored boards are checked against the game's own simulation. A wrong destination is
> legible, retry restarts the board, and the monetization shown is a permanent named cosmetic
> rather than a consumable interruption.
>
> The board is built as a warm wooden object, not a skin over a grid: thick rails, raised
> stations, open carriages, seated cats, and a fixed planning camera.

### 3.4 OneSignal — Keep Them Coming Back

> A dated Daily route gives a returning player something genuinely new to solve. Daily unlocks
> after seven campaign completions. After the player completes a Daily and chooses a morning,
> afternoon, or evening reminder window, OneSignal can send one reminder that opens directly to
> that day's board.
>
> The game records a cumulative lifetime Daily tally instead of an expiring streak. Missing a day
> removes nothing, resets nothing, and never gates content. The notification supports a reason to
> return; it does not manufacture one.

Use this blurb only after the opt-in timing, chosen reminder window, receipt, and deep link are
proven on the iOS candidate.

### 3.5 Catvertising

**Current submission status: do not enter this track.** The honest current blurb is:

> Cat Metro contains no interstitial, banner, or app-open ad. Rewarded placements are disabled in
> this release candidate and no ad network is wired, so this build makes no Catvertising claim.

Only if a player-initiated rewarded surface, reward grant, failure path, privacy treatment, and
RevenueCat Ads reporting all ship and are filmed may the entry replace that note with:

> Cat Metro never interrupts a board with an ad. The only ad surface is deliberately opened by
> the player for a named reward, grants only after a completed view, and reports through
> RevenueCat Ads. Declining or failing the ad leaves play unchanged, and the reward is never gated
> behind an ATT prompt.

### 3.6 Design

> Cat Metro treats the puzzle as a physical object: thick navy rails on cream sleepers, raised
> stations, open carriages, warm desk light, and a low three-quarter camera. The board fills the
> portrait frame so the switch decision reads before the decoration.
>
> Destination information repeats as colour and shape on the cat's pin, the station, and the wave
> capsule. The camera stays fixed during play so a route can be planned without the object moving
> under the player's eye.
>
> Menu, board, train, and Wardrobe share the same navy, cream, warm-wood, teal, tomato, and
> marigold material language. Soft edges and contact shadows give the pieces the rounded, tactile
> proportions of a wooden toy.

### 3.7 HAMM — Help Apps Make Money

> The filmed purchase is Conductor's Coat: a named, visible, permanent entitlement with a
> localized store price. The video shows the item before purchase, the native StoreKit 2
> confirmation, the same cat wearing it after unlock, and a clean-state restore.
>
> RevenueCat supplies the product/offering and entitlement path around that transaction. The
> design rejects soft currency, consumable energy, subscriptions, and paid randomness; the player
> buys the item they can see.
>
> Any revenue claim will use the dated RevenueCat report with its denominator. Until that report
> exists, the submission claims a working model and a verified transaction—not traction.

---

## 4. Capture plan

Final dynamic proof comes from the signed iOS candidate. Unity rigs are valuable for controlled
art iteration, but a fixture screenshot cannot prove StoreKit, RevenueCat, OneSignal, public
availability, or device behavior.

### 4.1 Capture sources

| Source | Use | What exists now | Required addition or rule |
|---|---|---|---|
| **iPhone native screen recording or tethered QuickTime capture** | Primary video: gameplay, failure/retry, purchase, restore, Daily, notification/deep link, beauty frames | The repo can generate an Xcode project; it has no automated iOS recorder or signed-device evidence | Install the exact signed candidate. Record portrait at native resolution and stable frame rate. Log device, OS, build, and commit. Keep external camera footage out unless a deliberate phone-in-hand shot is needed. |
| **`CM_BOARD_LOOK_CAPTURE_DIR`** | Board/camera/material iteration and hero rehearsal | Fixed 917×2048 PNG | Add a deterministic 1179×2556 option, reachable level/state/tick selection, and a candidate-parity comparison. Let a frame elapse after binding the RenderTexture before laying out screen-space UI. |
| **`CM_UI_CAPTURE_DIR`** | Home, failure, wave capsule, safe-area and typography review | Fixed 917×2048 PNG | Use for review only until exact-size support exists. It does not prove native device behavior. |
| **Wardrobe purchase still rig** (`CM_WARDROBE_CAPTURE_DIR`) | Before/purchased/restored composition rehearsal | 917×2048 frames driven by a filmable fixture backend | Never use it as RevenueCat evidence. The final purchase and restore must be recorded from StoreKit on iPhone. |
| **Bespoke capture mode** | Repeatable opening, hero state, HUD-on/HUD-off beauty state | No dedicated gameplay movie recorder and no exact hero-state seam | Add only deterministic state selection, capture resolution, and optional non-shipping touch telemetry. It must not create cats, props, routes, or outcomes unavailable in ordinary play. |
| **Pixel 9 Pro / ADB** | Android parity or later Play listing | Existing Android device workflow | Secondary only. Run `adb devices -l` and verify the Pixel model before any command. Never use Quest or Pico hardware. Pixel footage cannot prove the iOS/StoreKit critical path. |

A native screen recording cannot contain a physical thumb. Use the tiny editorial tap ring in shot
1, synchronized to the actual recorded touch, and let the lever/points response provide product
proof. A separately filmed device introduces bezel, reflections, moiré, and hand occlusion; it is
not the default.

### 4.2 Recording recipe

1. Freeze the candidate commit and build number. Capture no “final” media from a moving branch.
2. Run the Unity still rigs to compare Home, board, cat scale, Wardrobe, failure, and HUD against
   the reference purposes. These are rehearsals and defect detectors.
3. Build, sign, and install the iOS candidate through the human release workflow. Confirm
   full-screen safe area, material bindings, admitted assets, profile, and clean UI before
   recording.
4. Bank at least five unbroken opening takes. Reject any take whose tap-to-counter loop exceeds
   five seconds or whose touch annotation misses the real input frame.
5. Record purchase and restore early, on the same build, after clearing unrelated notifications
   and personal account details. Prepare restore with a real delete/reinstall under the same App
   Store test account; capture the candidate's actual manual or automatic restore behavior.
   Preserve an unedited source recording even if the final cut is shorter.
6. Record Daily/tally and OneSignal receipt/deep link. The reminder may arrive within the
   configured morning/afternoon/evening window; do not claim an exact minute.
7. Capture the exact hero PNG and run its dimension, colour, opacity, candidate-parity, and 20%
   thumbnail checks.
8. Record montage and beauty frames after the art and camera are frozen.
9. Add the public-live slate only after its URL resolves from a logged-out device. Perform a final
   frame-by-frame privacy, claims, trademarks, and audio sweep.

For every accepted source, retain a media receipt containing commit, build number, device or rig,
OS, level/state/tick, date, source filename, and SHA-256. Keep the untouched source capture beside
the edit project.

---

## 5. GAP LIST — ordered remaining build

This is the sequencing document for the remaining release. Baseline: source at `eaf18e9` was
audited read-only; no Unity render, signed iOS build, physical-device flow, purchase, push, or
public store page was verified in that audit. “Present in source” below is not “proven in the
artifact.”

The commercial/iOS lane and visual lane should run in parallel, but their numbered acceptance
points are the order in which claims become safe.

| # | Must become true | Current exact-artifact gap | Acceptance evidence / media unlocked |
|---|---|---|---|
| **0 — human go/no-go** | The human approves commercial distribution of every paid Meshy/Tripo asset intended for the App Store binary. | Provider-delivered art licensing is a real distribution decision; local development possession is not approval to ship. | Written asset list and human decision. If an asset is not approved, replace it before visual polish—not after capture. |
| **1 — iOS + RevenueCat skeleton** | A 1024×1024 icon, Apple signing/team/profile, App Store record and privacy answers, 13+ declaration with Kids Category off, IAP capability, `cm_outfit_conductor`, `cosmetics` offering, permanent entitlement, RevenueCat public iOS key, and StoreKit 2 configuration all exist. Purchase, cancel, relaunch, network failure, and restore behave correctly on a physical iPhone. | iOS project generation exists, but tracked signing is empty and icon slots are unassigned. Only example RevenueCat config is committed; without human-supplied production config the null backend leaves cosmetics locked. | Signed candidate install plus retained purchase/restore recordings and dashboard/store receipts. Unlocks shots 5–6 and the hard SDK gate. Start this lane immediately; it is not dependent on final art polish. |
| **2 — real cats and admitted props** | Actual distributable cat models replace the procedural sphere head/ears in moving carriages; prop prefabs and URP base maps are present and admitted. Cat head/ears remain readable above the carriage wall. | Runtime currently builds one engine, one carriage, and a primitive cat. There is no runtime cat catalog in the audited artifact. The prop catalog has no resource assets to admit, so furniture and prop-dependent presentation do not appear. A focused PlayMode run in this worktree skipped with `needs the licensed local prop install`; the same test passed in the main checkout, confirming an ignored local-asset difference rather than source proof. | Release-candidate render plus material inspection and non-zero catalog read-back. Match the purpose of `gen-ref-cats-on-train.jpeg`; never assume built-in mesh size or accept a silent placeholder. |
| **3 — station language and board framing** | Station colour-and-shape badges render independently of optional kiosk art; destination pins match them; the low, squarer board fills portrait while cats, pins, points, and levers remain legible. | The fallback station is a cube with a first-letter text label. The shape plate is created only after a prop kiosk succeeds, so an empty catalog removes the badge. Existing rig output is closer to plan view and has not been render-checked here. | Reachable board screenshot at full size and 20%, plus deliberate missing-prop mutation proving badges survive. Use `gen-ref-board-framing.jpeg` as the framing ceiling and reject its text signs/Score/Moves details. Unlocks shots 1–4 and the hero. |
| **4 — Home/menu surface** | The shipped Home establishes the same object and palette as the board: carved/navy identity, tactile cream controls, lamp-lit depth, and a board-world preview that still reads as real UI. | Home exists as a polished 2D card/rail/cat-silhouette composition, materially short of the dense diorama in `gen-ref-menu.png`. No final render was inspected in this audit. | Candidate capture reviewed beside `gen-ref-menu.png` for hierarchy, depth, light, and palette—not copied decoration. If it stays flat, omit the menu beauty beat rather than imply the reference was reached. |
| **5 — Wardrobe surface and visible delivery** | A large, readable profile cat, named item card, localized price, clear Buy/Equip/Restore states, and a coat/hat that visibly appears on that same cat after entitlement. No coin or gem UI. | The purchase/restore surface exists, but its current profile cat and outfit are flat procedural graphics rather than the textured portrait/stand treatment in `gen-ref-wardrobe.png`. The checkout catalog has several products even though the current filmable UI exposes Conductor's Coat. | Real candidate before/purchase/after/restore capture. Use `gen-ref-wardrobe.png` for portrait scale and hierarchy only; localized money replaces its coin chip. Unlocks the polished form of shots 5–6. |
| **6 — core content proof** | L001–L019 pass final corpus/solver validation, all are reachable through normal progression, and the chosen opening/hero states occur in ordinary play. Failure, retry, clear, and counters match the script. | Nineteen level assets and an ordered band exist; this audit did not run reachability or the suite. The repo overview's older count of 17 is stale. | Retained headless validation output, clean progression receipt, and actual source takes. Unlocks the numerical claim and shot 4. |
| **7 — exact capture tooling** | The board rig can output 1179×2556 after one bound RenderTexture frame; deterministic level/state/tick selection reproduces the opening and hero without hand placement; signed-iPhone capture procedure is rehearsed. | `CM_BOARD_LOOK_CAPTURE_DIR` and `CM_UI_CAPTURE_DIR` are fixed at 917×2048. There is no video recorder or exact hero-state seam. | Rig output, device-parity comparison, and five successful opening rehearsals. Unlocks final hero production and lowers re-shoot risk. |
| **8 — Daily + OneSignal proof** | Seven campaign completions unlock Daily; a real Daily completion increments a durable lifetime tally exactly once; the permission ask follows demonstrated value; the OneSignal notification arrives within the chosen window and deep-links to Daily. | Daily/tally and routing are present in source, but the committed OneSignal app ID is blank and no runtime/device behavior was verified. | Physical-device sequence with relaunch persistence, notification receipt, and deep link. Unlocks shot 7 and Keep Them Coming Back. |
| **9 — Catvertising fork** | Either a fully player-initiated rewarded flow ships with completed-view grant, failure handling, privacy/ATT independence, and RevenueCat Ads reporting, or the track remains absent everywhere. | All rewarded placements are disabled and explicitly say no ad network is wired. | Default acceptance is deletion: no video beat, category card entry, or promotional blurb. Only end-to-end candidate evidence changes this decision. |
| **10 — signed release candidate** | The exact signed iOS build boots from cold, shows no missing material/placeholder art, survives purchase/restore and core progression, contains the approved assets, and matches every planned shot. | No Xcode archive, installation, iOS device run, or candidate render was verified here. | Candidate receipt, device matrix, visual inspection, purchase/restore, Daily/push evidence, and final claims ledger. Final media shooting may begin. |
| **11 — public-live gate** | The human submits and releases the candidate; the App Store page is publicly downloadable before the deadline. | No public URL is proven. TestFlight, “Waiting for Review,” and approval without release do not qualify. | Logged-out install from the public URL. Unlocks shot 9 and eligibility. No automated or agent-run upload. |
| **12 — final media and submission** | All footage and the one hero PNG come from the accepted candidate; captions, claims, privacy, audio, dimensions, track names, public URL, and runtime receive a cold review. | Nothing has been captured in this docs task. | 1:45 master, exactly one 1179×2556 hero, final text, media receipts, and a submission completed before 23:45 PDT. |

### Internal latest-safe calendar

These are buffer targets, not store-review guarantees:

- **Sep 1–4:** close gates 0–1 while restoring the visual asset path.
- **Sep 5–9:** close cats/props, board, Home, Wardrobe, and exact capture tooling.
- **Sep 10:** physical-device RevenueCat purchase/restore and OneSignal proof.
- **Sep 11:** freeze and validate the signed iOS candidate.
- **Sep 12:** human submits to App Store review.
- **Sep 22:** target public-live date, leaving eight days of contingency.
- **Sep 23–26:** final candidate capture and edit.
- **Sep 27:** lock screenshot, copy, video, and links.
- **Sep 28:** target Shipaton submission; Sep 29–30 are contingency, not planned work time.

If the public candidate, RevenueCat purchase, real cats/props, five-second causal loop, or exact hero
cannot be proven, the corresponding media claim is not ready. Say plainly what slipped; never let
a narrow source check print “OK” over an unobserved render or device path.
