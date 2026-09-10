# Cat Metro — Shipaton submission plan

For this submission, **the media is the product**. Judges may decide from the description, one
screenshot, and the first two minutes of video without installing the app. The final assets must
therefore show a real, reachable release-candidate state clearly enough that they stand on their
own.

**Reviewed 2026-09-10 against source `72c0a862`.** This refresh inspects source and official
requirements; it does not verify a signed candidate, purchase, final media, or public release.

Cat Metro must be fully published, downloadable in the United States, and first released during
the event's submission period by **2026-09-30 23:45 PDT (2026-10-01 06:45 UTC)**. Internal/closed
testing, TestFlight, and a build still in review do not qualify. The public app must use RevenueCat
to power a purchase or RevenueCat Ads. This plan uses **Conductor's Coat through Google Play
Billing and RevenueCat**. [Official rules](https://revenuecat-shipaton-2026.devpost.com/rules),
[RevenueCat submission guide](https://www.revenuecat.com/blog/engineering/how-to-submit-your-app-for-shipaton)

**Android is primary; iOS is secondary.** The human has decided that the generated 3D assets ship.
Check their inclusion and rendering in the candidate. Keystore passwords, uploads, and any
submit-for-review action remain human-only. Play production access is still awaiting the human's
Console check; the account-specific timing branch is in §5.

The primary tracks are **Best Game**, **Design**, and **HAMM**. **OneSignal — Keep Them Coming
Back** and **Catvertising** are optional: include them only when their deployed integrations work
on the public candidate. Neither is required for purchase-based eligibility.

This plan mines the earlier submission draft, the supplied Shipaton research brief, and
`docs/store/*`, and uses `docs/LOOK.md` plus the human-curated
`docs/reference/gen-ref-*` pack as the polish ceiling. Those generated references are direction,
not submission evidence. Every submitted pixel must come from Cat Metro.

---

## 1. Video script

**Planned runtime:** 1:45 (105 seconds); omit unproven optional footage and shorten the cut. Keep
the final video under two minutes.

**Master:** 1920×1080 landscape H.264. Place clean portrait Pixel 9 Pro capture at maximum readable
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

Times below describe the full 1:45 cut. If Daily is not ready to demonstrate, remove shot 7 and
close the gap. OneSignal footage is an optional replacement within that slot, never a release
dependency.

| # | Time | Dur | Picture and action | Caption | Voiceover / audio | Proof required before shooting |
|---|---|---:|---|---|---|---|
| **1** | **0:00–0:05** | 5s | The unbroken tap → moving lever/points → matching arrival → delivered-counter increment described above. One engine, one occupied carriage; no invented three-car consist. | `TAP THE SWITCH` at 0:00.8; `MATCH THE BADGE` at 0:03.3; `CAT METRO` at 0:04.4. | “Tap the switch. Match the cat.” | The whole causal loop fits at shipped speed in a reachable candidate state. Cats, pin, station badge, switch, and counter are readable at laptop playback size. |
| **2** | **0:05–0:14** | 9s | Three short candidate clips: the wave capsule previews the next cat; a second junction decision; the real clear/result state. | `ONE CONTROL. MANY ROUTES.` | “That is the control scheme. The route gets harder; the gesture does not.” | Actual wave UI and win state; no hand-posed state or editor-only flag. |
| **3** | **0:14–0:23** | 9s | A platform overflow produces the shipped failure review. Tap the real retry control and show the board restart. | `PLATFORM OVERFLOW` → `RETRY` | “A platform overflow ends the attempt. Retry starts the board again.” | Candidate failure and retry path recorded end to end. Do not promise an exact retry duration. |
| **4** | **0:23–0:31** | 8s | Four distinct authored boards, two seconds each: different routes, station pairs, switch counts, and furniture. Match camera and exposure so the cuts feel like opening a box of layouts. | `60 AUTHORED BOARDS` / `CHECKED IN THE GAME'S SIMULATION` | “Sixty authored boards use the same small rule in different ways.” | All 60 levels pass final validation and are reachable through ordinary candidate progression. Otherwise replace the number with the proven count. |
| **5** | **0:31–0:49** | 18s | Wardrobe: large profile cat without the coat; named `Conductor's Coat` card with localized price; player taps Buy; the native Google Play purchase confirmation appears; purchase completes; return to the same portrait with coat and hat visibly equipped. Keep tap → sheet → unlock uncut. | `CONDUCTOR'S COAT` → `REVENUECAT-POWERED PURCHASE` → `PERMANENTLY UNLOCKED` | “The filmed purchase is Conductor's Coat. RevenueCat loads the product and carries its permanent entitlement through Google Play Billing.” | Production product, offering, entitlement, public SDK config, real Pixel transaction, and visible unlock. Google Play Billing provides the native confirmation UI. |
| **6** | **0:49–0:58** | 9s | After an off-camera delete/reinstall under the same Google Play test account, open Wardrobe. If the candidate presents the coat locked, tap the shipped Restore control; entitlement returns and the coat reappears. If RevenueCat restores automatically on initialization, film that truthful behavior and caption it `ENTITLEMENT RESTORED` instead of staging a locked state. | `RESTORES WITH REVENUECAT` or `ENTITLEMENT RESTORED` | “The entitlement returns on a clean install.” | Real reinstall-and-restore behavior on the same signed Android candidate. A relaunch, fixture backend, or Unity test still is not proof. |
| **7** | **0:58–1:11** | 13s | Daily entry → a real Daily completion → the cumulative lifetime tally increments by one. Reopen Daily to show persisted state. | `A DAILY ROUTE` → `LIFETIME TALLY — NEVER EXPIRES` | “Daily adds one dated route and a lifetime tally that never resets.” | Daily unlock, completion, and tally persistence must be proven on the candidate. If entering OneSignal, replace the closing beat with a real reminder and deep link from the deployed campaign; use §3.4 copy only after that flow works. |
| **8** | **1:11–1:30** | 19s | Three held candidate beauty beats: 5s Home/menu diorama; 8s board-filling low portrait view with seated cat head, destination pin, thick track, lever, and warm desk; 6s purchased Wardrobe portrait. Use shipped UI only where it belongs. | Last 5s only: `A WOODEN PUZZLE YOU CAN TOUCH` | “The menu, board, train, and wardrobe belong to the same object: navy, cream, warm wood, and late-afternoon light.” | Each surface reaches the purpose of its matching `gen-ref-*` target and survives phone-size inspection. |
| **9** | **1:30–1:39** | 9s | Cat Metro wordmark on warm paper, plain public store URL beneath it. No store badge or borrowed logo. Add only after the URL resolves for a logged-out viewer. | `PUBLICLY LIVE ON GOOGLE PLAY` / public URL | “Cat Metro is publicly available on Google Play.” | Public product URL. Never render “available” from an approval email, TestFlight page, or review state. |
| **10** | **1:39–1:45** | 6s | Static category card. | `BEST GAME · DESIGN · HAMM` | Silence. | Show only tracks actually entered; add OneSignal or Catvertising only with working candidate evidence. |

**Full cut: 1:45; without shot 7: 1:32.** Do not pad omitted features or the remaining headroom.

### 1.3 Why the purchase is at 0:31

The RevenueCat-powered purchase begins **31 seconds in**, within the first third of the video,
and receives 27 seconds when purchase and restore are counted together. It is early because
RevenueCat runs the competition, it is the HAMM argument, and it visibly supports the second hard
eligibility gate.

The proof chain must be legible and truthful:

1. A named item and localized store price are visible before the tap.
2. The action is player-initiated; it is not an ambush paywall.
3. RevenueCat supplies the product/offering path and observes the entitlement while Google Play Billing
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
3. Shorten the Daily gameplay portion of shot 7. Preserve its verified tally increment and, only
   if entering Keep Them Coming Back, the actual notification/deep link.

Never cut the five-second gameplay loop, the purchase, the visible unlock, the public-live card,
or the final track card. If purchase/restore cannot fit in its allotted footage, take time from
beauty—not by speeding or splicing the transaction.

### 1.5 Audio, captions, and rights

- Use original narration normalized consistently. `GameAudio` and its bootstrap wiring exist in
  source; record and listen to the actual candidate audio before including it.
- Do not invent switch clicks, arrival chimes, or other product sounds in post. Record the
  implemented original music and SFX from the candidate; source presence does not prove playback.
- Burn captions and complete a full muted watch.
- Clear every unrelated notification before the lock-screen take. Remove personal account data,
  test-user emails, debug overlays, and store sandbox credentials from every frame.
- Use no third-party music, transit marks, award laurels, or borrowed store/RevenueCat logos.

---

## 2. Hero screenshot specification

Plan one hero image: `1179×2556`, opaque sRGB PNG, portrait, full bleed, with **no device frame**.
The rules require at least one screenshot at that size, not exactly one. Additional images must
also show the real app. [Official rules](https://revenuecat-shipaton-2026.devpost.com/rules)

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

1. **Exact-size Android screenshot.** Inspect the Pixel's actual capture dimensions. Use a native
   frame directly only if it is exactly 1179×2556.
2. **Exact-size Unity rig.** The board/UI still paths already accept
   `CM_CAPTURE_SIZE=1179x2556`. The board path also accepts `CM_CAPTURE_LEVEL`, `CM_CAPTURE_TICK`,
   `CM_CAPTURE_SWITCHES`, and `CM_CAPTURE_HUD`. Run with the main checkout's admitted art and
   compare the same reachable state against the signed Pixel candidate for camera, materials,
   safe area, and UI parity. Keep the shipped HUD on for the hero.
3. **Aspect-preserving crop from a larger Android capture.** Inspect the source dimensions and
   composition first. Crop without stretching or upscaling; reject a crop that removes gameplay
   information or changes the intended frame.

`CaptureRig.cs`, `BoardLookTests.cs`, and the shared screenshot path in `UiPhoneCaptureTests.cs`
contain the configurable-size implementation. Some dedicated diagnostic captures still use fixed
sizes. Select the intended path and inspect its actual PNG; this document did not run a capture.

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

The following is draft copy, not a statement that these features have passed release checks.
Compare each claim with the actual public candidate and the supporting evidence below; delete a
sentence whose feature or count is not proven. The stable store listing remains in
`docs/store/assets/listing-copy.md`.

### 3.1 Main description

> Cat Metro is a tabletop train-routing puzzle for Android. Cats leave the depot carrying
> colour-and-shape destination badges. Tap a junction to move the points and route little
> trains toward matching platforms.
>
> Meet each board's delivery goal to clear it. A wrong station rejects the cat; retry
> starts a failed board again. The release includes sixty authored boards checked against the same
> simulation the game runs, plus a Daily route derived from the date.
>
> The game is a low-poly wooden railway on a warm desk: navy rails, cream sleepers, chunky
> stations, open carriages, and late-afternoon light. Each destination is identified by both a
> colour and a shape badge.
>
> The filmed purchase is Conductor's Coat, a named permanent Wardrobe entitlement with a
> localized Google Play price. RevenueCat powers the product, purchase, entitlement, and restore;
> Google Play Billing provides the native confirmation. There is no paid randomness, energy, subscription,
> forced ad, or interstitial.
>
> Daily keeps a cumulative lifetime tally. Missing a day expires nothing.

### 3.2 Evidence for the draft claims

| Public sentence | Evidence required |
|---|---|
| “for Android” / publicly available | Signed candidate, installed-device proof, and a US-downloadable public Google Play URL. |
| Cats carry colour-and-shape badges | Candidate render of the actual runtime cat, pin, and independent station badge. |
| Sixty authored boards | Final AAB census of L001–L060, successful corpus/solver validation, and ordinary player progression through the same Play-delivered campaign. A source file count alone is insufficient. |
| “checked against the same simulation” | Final solver/corpus run on the candidate content tree with retained output. |
| RevenueCat purchase and restore | Production configuration and dashboard mapping, native Google Play purchase on Pixel, visible entitlement, clean-state restore, and public binary parity. |
| No paid randomness / energy / subscription / forced ad / interstitial | Exact-candidate catalog and ordinary-flow inspection. |
| Daily route and lifetime tally | Real completion recorded once, relaunch persistence, and increment without an expiring streak. |

Do not add an accessibility outcome such as “works for colour-blind players” until that outcome has
been tested. The defensible product fact is that colour and shape are both present.

### 3.3 Best Game

> Tap a switch to route cats carrying colour-and-shape destination badges. The opening records one
> tap, the points moving, and a correct delivery inside five seconds. Later boards add route
> pressure without adding a second control scheme.
>
> Sixty authored boards are checked against the game's own simulation. A wrong destination is
> legible, retry restarts the board, and the monetization shown is a permanent named cosmetic
> rather than a consumable interruption.
>
> The board is built as a warm wooden object, not a skin over a grid: thick rails, raised
> stations, open carriages, seated cats, and a fixed planning camera.

### 3.4 Optional: OneSignal — Keep Them Coming Back

> A dated Daily route gives a returning player something genuinely new to solve. Daily unlocks
> after completing seven different campaign boards. After the player completes a Daily and chooses a morning,
> afternoon, or evening reminder window, OneSignal can send one reminder that opens directly to
> that day's board.
>
> The game records a cumulative lifetime Daily tally instead of an expiring streak. Missing a day
> removes nothing, resets nothing, and never gates content. The notification supports a reason to
> return; it does not manufacture one.

Use this blurb only after the opt-in timing, chosen reminder window, receipt, and deep link are
proven on the Android candidate. Entry also needs a deployed OneSignal campaign and the OneSignal
App ID. Omit this track and its footage if they are not ready.
[Official rules](https://revenuecat-shipaton-2026.devpost.com/rules)

### 3.5 Optional: Catvertising

**Omit this track unless the public candidate demonstrates working RevenueCat Ads.** Rewarded
service, LevelPlay provider, and RevenueCat reporting code exist, but this source review does not
establish enabled runtime configuration, ad delivery, or reporting. No absence-of-ads blurb follows
from that uncertainty.

Only if a player-initiated rewarded surface, reward grant, failure path, privacy treatment, and
RevenueCat Ads reporting all ship and are filmed may the entry replace that note with:

> Cat Metro never interrupts a board with an ad. Ad surfaces are deliberately opened by
> the player for a named reward, grant only after a completed view, and report through
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
> localized store price. The video shows the item before purchase, the native Google Play Billing
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

The release schedule targets a validated Android candidate on September 16 and final submission
media on September 26–27. Capture the Play listing assets from the validated candidate before its
September 17–18 review submission; the later media window must not delay the store submission.
Refresh those assets only if the public candidate changes visibly.

Final dynamic proof comes from the signed Android candidate on Pixel. Unity rigs support art
iteration and matched stills; fixture screenshots cannot prove Billing, RevenueCat, OneSignal,
public availability, or device behavior.

### 4.1 Capture sources

| Source | Use | Source support / remaining verification |
|---|---|---|
| **Pixel 9 Pro native recording / ADB** | Primary gameplay, failure/retry, purchase, restore, optional Daily/reminders, and beauty footage | Verify `adb devices -l` and the Pixel model before any device command. Record the exact Android build and actual output dimensions. Never install on Quest or Pico. |
| **`CM_BOARD_LOOK_CAPTURE_DIR`** | Board iteration and hero still | `BoardLookTests` accepts size, level, tick, switch receipts, and HUD controls through `CaptureRig`; it waits after binding the render target. Run with admitted main-checkout art and verify Pixel parity. |
| **`CM_UI_CAPTURE_DIR`** | Home, failure, wave capsule, safe-area and typography stills | The shared screenshot path accepts `CM_CAPTURE_SIZE`; dedicated diagnostics may retain fixed sizes. Inspect the output dimensions and compare with the candidate. |
| **Wardrobe still rig** (`CM_WARDROBE_CAPTURE_DIR`) | Before/purchased/restored composition rehearsal | Fixture-backed frames are not purchase proof. Record the final Google Play purchase and restore on Pixel. |
| **Deterministic still replay** | Repeatable reachable hero state | Existing `CM_CAPTURE_LEVEL/TICK/SWITCHES/HUD` controls replay the game session. They do not replace ordinary-device gameplay footage or allow invented actors/outcomes. |
| **iPhone recording / QuickTime** | Secondary iOS release media, if that build ships | Verify the separate signed iOS candidate and StoreKit flow before making any Apple availability/purchase claim. It is not required for the Android submission. |

A native screen recording cannot contain a physical thumb. Use the tiny editorial tap ring in shot
1, synchronized to the actual recorded touch, and let the lever/points response provide product
proof. A separately filmed device introduces bezel, reflections, moiré, and hand occlusion; it is
not the default.

### 4.2 Recording recipe

1. Freeze the candidate commit and build number. Capture no “final” media from a moving branch.
2. Run the Unity still rigs to compare Home, board, cat scale, Wardrobe, failure, and HUD against
   the reference purposes. These are rehearsals and defect detectors.
3. Prepare the signed Android candidate; the human supplies signing passwords and performs Play
   uploads. Install the Play-delivered candidate on Pixel and confirm
   full-screen safe area, material bindings, admitted assets, profile, and clean UI before
   recording.
4. Bank at least five unbroken opening takes. Reject any take whose tap-to-counter loop exceeds
   five seconds or whose touch annotation misses the real input frame.
5. Record purchase and restore early, on the same build, after clearing unrelated notifications
   and personal account details. Prepare restore with a real delete/reinstall under the same Google
   Play test account; capture the candidate's actual manual or automatic restore behavior.
   Preserve an unedited source recording even if the final cut is shorter.
6. Record Daily/tally if including that feature. Record a real OneSignal receipt/deep link only
   if entering that optional track; verify the chosen window without promising an exact minute.
7. Capture the exact hero PNG and run its dimension, colour, opacity, candidate-parity, and 20%
   thumbnail checks.
8. Record montage and beauty frames after the art and camera are frozen.
9. Add the public-live slate only after its URL resolves from a logged-out device. Perform a final
   frame-by-frame privacy, claims, trademarks, and audio sweep.

For every accepted source, retain a media receipt containing commit, build number, device or rig,
OS, level/state/tick, date, source filename, and SHA-256. Keep the untouched source capture beside
the edit project.

---

## 5. Remaining release work

At source `72c0a862`, `GameRoot.LevelBand` and the content/StreamingAssets trees contain the merged
60-level campaign, L001–L060. `LoadNextBandTests` asserts its sequence and L060→L001 wrap;
`CampaignArtifactTests` and `FullCampaignGateTests` cover the mirrored corpus and full solver
validation. Cat catalogs, audio, commerce, Daily, optional ads/messaging, and configurable capture
paths exist. This source review ran no Unity or device tests and establishes no final render or
store state. Machine-local art and production configurations must be checked in the build checkout.

| Remaining work | Concrete evidence to retain |
|---|---|
| Play production access and app setup | Human check of this app's Dashboard/Production status, any account verification tasks, listing, privacy/Data safety, content rating, audience, ads/app access, and US availability. |
| Android candidate | Signed AAB with version code, source revision, and SHA-256; exact embedded content/configuration; device cold boot, progression, failure/retry, and visible cats/materials with catalog admission read-back. |
| Purchase and judge access | The candidate's Conductor's Coat purchase, visible unlock, cancel/failure behavior, clean-state restore, and matching RevenueCat entitlement record; a tested Play promo-code redemption and separate working judge codes. |
| Campaign and visuals | Candidate census of 60 IDs, retained full corpus/solver results, progression through the claimed campaign, and Pixel renders of Home/board/Wardrobe. Check normal states and the intended five-second opening. |
| Optional features | Daily completion/persistence if shown; deployed OneSignal campaign, App ID, delivery/deep link if entering that track; device rewarded-ad behavior and RevenueCat Ads reporting if entering Catvertising. Omit unproven features from copy and footage. |
| Public launch and submission | Human release; public US store download and install; media matching that build; text, public video link, 1024² icon, at least one 1179×2556 frameless screenshot, RevenueCat project ID, and judge access. |

The human's decision to ship the generated 3D assets is settled. The remaining art work is to make
sure the intended files are included and render correctly in the candidate.

### Production-access timing

The human's account/Production check is pending. Personal accounts created **after November 13,
2023** require at least 12 testers continuously opted in to this app's closed test for 14 days,
then a production-access application. That review usually takes up to seven days but may take
longer. An existing account or internal test does not by itself prove access. If the qualifying
12 first opt in on September 10, the earliest application is approximately September 24; a
seven-day access review reaches October 1 before release review. The September plan therefore
depends on an exempt account or sufficiently advanced/approved production access.
[Google testing requirements](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en)

Once production access is available, allow at least a week for release review. Google warns that
review can exceed seven days and that further submissions during review can move the app back in
the queue. [Publishing review guidance](https://support.google.com/googleplay/android-developer/answer/9859654?hl=en)

### September targets

These are working targets, not guarantees of store-review timing:

- **Sep 10–13:** land and validate the remaining polish; human confirms production access and
  completes Console setup while device and visual work continue.
- **Sep 11–16:** finish the visible improvements and candidate device checks.
- **Sep 16:** freeze the candidate, prepare the signed AAB, and capture the required Play listing
  assets. Check API 36, supported Billing Library, and 16 KB native/package compatibility in the
  final artifact; source settings alone are insufficient.
- **Sep 17–18:** human uploads and submits the production release when access and setup are ready.
- **Sep 22–25:** target public launch; verify an actual US download and purchase/restore path.
- **Sep 26–27:** capture/edit the final Devpost hero and video against the public candidate; refresh
  Play graphics only where necessary to match it. Finalize truthful copy and working judge access.
- **Sep 28:** human completes the Devpost submission. Keep Sep 29–30 for contingency.
- **Sep 30, 23:45 PDT:** submission deadline. Keep judge access working through **Oct 13, 12:00 PDT**.

The current Android requirements are API 36 for new apps/updates, Billing Library 8 or newer
unless an applicable extension exists, and 16 KB compatibility for native code targeting Android
15+. [Target API](https://support.google.com/googleplay/android-developer/answer/11926878?hl=en),
[Billing deadlines](https://developer.android.com/google/play/billing/deprecation-faq),
[16 KB compatibility](https://developer.android.com/guide/practices/page-sizes)

Submit the real public store link and working judge access, not a review status. The formal entry
can be edited until the deadline; subsequent portfolio edits do not update the judged submission.
The required media, access period, and deadline are in the
[official rules](https://revenuecat-shipaton-2026.devpost.com/rules); the
[submission guide](https://www.revenuecat.com/blog/engineering/how-to-submit-your-app-for-shipaton)
includes the RevenueCat project ID and current form steps.

Not verified by this update: production-access status, final AAB contents/signature/compliance,
full-campaign test results, candidate phone renders, purchase/restore or judge codes, deployed
optional integrations, public availability, or submission completion.
