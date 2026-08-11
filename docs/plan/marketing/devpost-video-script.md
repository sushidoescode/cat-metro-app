# Cat Metro Devpost video script

Status: **PRODUCTION SCRIPT ONLY — no video has been captured or approved by this document.**

Target master: **1:54**. Hard ceiling for this cut: **1:55**, leaving five seconds below the strict
two-minute limit.

Voice-over: **164 words** (about 1:10 at 140 words per minute, leaving 44 seconds for readable
gameplay and deliberate pauses).

That count covers the preferred VO column. If any named fallback replaces a line, recount the final
narration and update this manifest before export; the 1:54 edit timing remains the hard plan.

This script defines the truthful prescreen cut for the frozen-anchor build. The finished cut must
open on live, on-device gameplay; show the pitch, running app, and target categories inside the
runtime; and make no public-launch claim.

## Delivery and editorial rules

- Export 1920 × 1080 landscape, H.264, with burned captions. Center the native portrait device
  capture against original Warm Paper side fields; do not add a phone bezel or third-party device
  mark.
- Source gameplay through `adb screenrecord` on the named Pixel 9 Pro from one exact candidate
  APK. Record native resolution, then downscale/crop without stretching; do not upscale.
- The frozen anchor has no evidenced runtime audio playback, so its executable default is original
  narration plus silence. Add game SFX or stems only after the exact candidate proves that those
  original assets exist and play on device. No stock footage, licensed music, store badge, real
  transit mark, OS notification, watermark, or third-party logo.
- The committed golden image is never shown. It guides the corrected camera, scale, desk, and
  lighting only. The video may describe the tabletop look only after the final on-device art gate.
- Burn verbatim subtitles for the narration. Target narration at −16 LUFS; only after V-AUDIO/MARKS
  proves an original in-game bed may it sit around −22 LUFS under speech. Otherwise leave silence.
- Public copy is only the **Visual**, **On-screen copy**, and **VO** columns below. Sources, gates,
  and fallbacks are internal production notes and must not appear in the exported cut.

## Internal readiness gates

- **V-BUILD — BLOCKED:** one merged candidate commit; package `com.catmetro.game`; APK SHA-256;
  production `versionCode` and `versionName`; check, test, and build green. Record every value in the
  capture manifest; the revision need not appear in public copy.
- **V-DEVICE — BLOCKED:** fresh Pixel 9 Pro recordings from that APK, with Do Not Disturb on,
  notifications/status chrome absent, no dev console, and raw-clip SHA-256 values. Do not reuse the
  rejected art-lane Pixel baseline.
- **V-ART — BLOCKED:** new on-device frames pass the human tabletop-composition review: low
  three-quarter camera, track-scale cats in open cars, warm wood desk/edge, restrained orange, and
  corrected warm contact lighting.
- **V-STATE — BLOCKED per beat:** the named L001–L010 state is reached through live simulation and
  player input. No golden-frame insert, posed mockup, relocated HUD, or planned feature.
- **V-REACH — BLOCKED for any out-of-band level:** the named candidate exposes the level through
  ordinary player progression or released navigation. The frozen anchor exposes L001–L005 only;
  a development override cannot source a marketing claim or clip. A named in-band fallback drops
  V-REACH.
- **V-QUEUE — BLOCKED:** a fresh exact-candidate device take makes an occupied queue and its route
  decision visually distinct without an overlay or development aid. If it does not, the 0:22–0:45
  beats take their named L002 preview fallbacks, drop V-QUEUE, and do not say “queue.”
- **V-EVIDENCE — VERIFIED at freeze; exact-head rerun required:** ten authored/staged level files,
  L001–L010, exist and pass the repository's content-validation and solver gates; normal progression
  exposes L001–L005 and the ordinary player loop covers Playing, Won/Results, and Next. Synthetic
  FailureReview fixtures do not prove a current campaign failure path. For the exact candidate,
  record authored/staged/gated IDs and ordinary player-reachable IDs separately, then update every
  count and range in the cut from that same commit.
- **V-PARITY — BLOCKED:** at the Sep 26–30 freeze, the production `versionCode` installed from the
  USA-visible Play listing equals the video APK's recorded `versionCode`. Show that same code either
  inside the video from the running build or in a paired screenshot from that build, and retain the
  Play receipt, package, version name/code, APK hash, device, date, and clip hashes together. Hold
  the export and submission if the equality or visible-code receipt is unavailable.
- **V-AUDIO/MARKS — BLOCKED at export:** the frozen anchor has no audio clips or runtime playback,
  so narration plus silence is the current-safe route. If the exact candidate adds original game
  audio, inventory the clips and retain on-device playback evidence before using it. In either case,
  the rights check covers narration and every included sound, and visual inspection finds no
  third-party mark. Missing runtime audio selects silence; it does not authorize an invented bed.
- **V-CATEGORY — BLOCKED:** the submitter confirms the actual Devpost selections before export.
  The frozen-anchor draft names only **Best Game**, matching the current Devpost description, but
  that draft does not satisfy PRD CM-R57.2's final committed slate: Best Game, HAMM, #BuildInPublic,
  OneSignal, Catvertising, Design, and Grand Prize. Every retained category needs its complete answer
  and evidence. If the slate cannot clear, the final export/submission remains blocked unless a
  human amends CM-R57.2; never silently shrink the final slate or substitute “entered” before submission.
- **V-FINAL — BLOCKED:** `ffprobe` reports a runtime no longer than 1:55, 1920 × 1080 video, and
  the intended codecs; a full muted watch confirms that burned captions carry the story.
- **V-HOST — BLOCKED / HUMAN ACTION:** after the master is approved, a human uploads it to YouTube
  or Vimeo using the visibility allowed by the live rules, verifies start-to-finish playback while
  logged out on another device, and records the canonical URL used by Devpost. A local file or draft
  upload does not clear this gate; this lane does not publish it.

## Time-coded master

| Time | Visual / action | On-screen copy | VO | Exact source | Readiness gate | Truthful fallback |
|---|---|---|---|---|---|---|
| **0:00–0:10** | Cold open, no logo: native on-device L006 Playing capture. A touch throws S1 and the approaching commuter visibly takes the matching branch. Hold long enough to show the input/result relationship. | `REAL ON-DEVICE GAMEPLAY` → `TAP THE SWITCH` | “Tap a junction. Throw the switch. Send each cat to the matching station.” | Pixel 9 Pro `adb screenrecord`; final candidate APK; `Game`; L006 — Alternating Line; live player input | V-BUILD + V-DEVICE + V-STATE + V-AUDIO/MARKS; add V-REACH only for L006 | Use L002 — Colour Split from the same APK. The VO and copy remain true; do not use an Editor, development override, or golden-frame substitute. |
| **0:10–0:22** | Continue a clean L006 run. Show the real next-wave preview, a red delivery, then a blue approach and one well-timed switch. No speed ramp hides the input/result relationship. | `ONE-THUMB TRAIN PUZZLE` → `READ THE NEXT WAVE` | “Cat Metro is a one-thumb train puzzle. Read the next wave, then time one tap to alternate red and blue.” | Same device/source; L006 Playing; actual four-red/four-blue authored wave sequence | V-BUILD + V-DEVICE + V-STATE + V-AUDIO/MARKS; add V-REACH only for L006 | Continue the same L002 fallback take through its real red delivery and blue approach; the VO and copy remain true. |
| **0:22–0:35** | L004 Playing capture: let a real queue become visibly occupied while the next-wave preview and active route remain readable. Do not force an overflow or wrong-station arrival. | `WATCH THE QUEUE` | “Incoming cats wait when a route is busy. The queue stays visible, so the next tap is a timing decision.” | Same device/source; `Game`; L004 — Platform Queue; reproducible occupied-queue Playing state | V-BUILD + V-DEVICE + V-STATE + V-AUDIO/MARKS; add V-QUEUE only for L004/L005 | Use L002 Playing with the real preview visible before a blue approach; on-screen copy becomes `READ THE NEXT WAVE`; VO becomes: “The next-wave preview keeps the next routing decision visible.” |
| **0:35–0:45** | Continue that Playing take: throw the real switch, hold through the queued commuter's advance, and show the matching route. | `READ · TAP · ROUTE` | “One tap changes the branch. The waiting cat moves, and the route keeps flowing.” | Continuation of the preceding Pixel clip; real Playing input and resulting movement | V-BUILD + V-DEVICE + V-STATE + V-AUDIO/MARKS; add V-QUEUE only for L004/L005 | Continue the same uncut L002 take through the switch and matching route; keep `READ · TAP · ROUTE`; VO becomes: “One tap changes the branch, and the cat follows the matching route.” |
| **0:45–0:58** | L001 initial Playing state: the real teach pulse marks the only switch; show both red commuters approach, the player's first tap, and the matching route. | `ONE SWITCH · ONE TAP` → `LEARN BY PLAYING` | “The first lesson begins with one switch and two commuters. A pulse marks the switch while the station signs show the goal.” | Same device/source; L001 — First Switch; initial state through first input | V-BUILD + V-DEVICE + V-STATE + V-AUDIO/MARKS | Use L002's first approach and replace the first sentence with: “The early levels keep each decision clear.” |
| **0:58–1:10** | Finish L002, hold the real **All cats home!** / **Next** result, tap Next, and show the L003 Playing board load through the real seam. | `WIN · NEXT · KEEP PLAYING` | “Win, tap Next, and the next built level loads through the same progression seam players use.” | Same device/source; L002 Won → Results → Next → L003 Playing; uninterrupted take | V-BUILD + V-DEVICE + V-STATE + V-EVIDENCE + V-AUDIO/MARKS | Use L001 → L002, the seam already device-verified at freeze, but re-record it from the final candidate. |
| **1:10–1:23** | Let a corrected L006 run breathe during a narration pause. Show the warm desk edge, Ink Navy rails, restrained orange switch accent, and small cats seated inside open cars while the board remains live. | `A CAT RAILWAY ON YOUR DESK` | “The visual direction is a hand-built cat railway on a warm wooden desk, with track-scale cats seated in open cars.” | Fresh final-candidate Pixel capture; L006 Playing; never the committed golden image | V-BUILD + V-DEVICE + V-STATE + V-AUDIO/MARKS; add V-REACH for L006 and V-ART for the preferred art-direction line | If V-REACH fails, use L005 with the same evidenced art. If V-ART fails, use ordinary clean L005 gameplay, set copy to `SWITCH · ROUTE · STATION`, use VO “The live board keeps the switch, route, and matching stations in the same frame,” and drop V-ART. Never describe or show the unverified target look. |
| **1:23–1:36** | Fast, honest sequence: on-device L001, L003, and L005 live frames from the same APK, followed by two seconds of plain local terminal output showing the exact-head ten-file content/solver pass. No level-select or access to L006–L010 is implied. | `5 LEVELS IN NORMAL PROGRESSION` → `10 AUTHORED FILES · VALIDATED · SOLVER-CHECKED` | “Normal progression exposes five handcrafted levels. Ten authored files, L001 through L010, pass content validation and solver gates.” | Same APK's live L001/L003/L005 levels plus locally captured exact-commit gate output; terminal contains no service logo or unrelated data | V-BUILD + V-DEVICE + V-STATE + V-EVIDENCE + V-AUDIO/MARKS | If exact-head gate output is unavailable, retain only the five-level normal-progression claim after rechecking the candidate. Never substitute the 30-level plan, a menu mockup, or a development-override capture. |
| **1:36–1:47** | One uninterrupted on-device Win → Next transition under a full-width Warm Paper statement card; return to the live board before the sentence ends. | `Fair by design: no forced ads, no energy, no loot boxes, every level solvable free.` | “Fair by design: no forced ads, no energy, no loot boxes, every level solvable free.” | Final candidate app flow plus exact-head solver/content evidence and frozen truth baseline | V-BUILD + V-DEVICE + V-STATE + V-EVIDENCE + V-AUDIO/MARKS | If any named system exists, any built level lacks the solver gate, or a real Win → Next take cannot clear V-STATE, cut the beat. Do not stage the transition or soften an untrue full promise. |
| **1:47–1:54** | Full-screen original Cat Metro wordmark on Warm Paper, then the explicit target-category card. No store badge, availability CTA, conductor animation, or external logo. | `CAT METRO`<br>`TARGETING: BEST GAME` | “Cat Metro is targeting Best Game.” | Original project wordmark/type and static palette card produced for this cut | V-CATEGORY + V-PARITY + V-AUDIO/MARKS + V-FINAL | The Best-Game-only card is a frozen-anchor draft, not a CM-R57.2-compliant final slate. Replace it only with the exact fully answered, evidenced committed slate or a human-amended slate; otherwise hold the export. |

## Audio map

- Frozen-anchor/default mix: original narration and silence throughout; no logo sting or music bed.
- If V-AUDIO/MARKS proves original runtime playback in the exact candidate, retain only sounds heard
  in the captured takes at 0:00–0:45 and 0:58–1:23. Keep them below narration; do not reconstruct a
  tap, switch, movement, arrival, failure, or result sound in post.
- 1:23–1:47: narration over silence unless the evidenced candidate supplies its own unobtrusive bed.
- 1:47–1:54: silence by default. An original in-game arrival chime is eligible only if V-AUDIO/MARKS
  records it playing in that candidate. Never add a licensed or newly invented sting.

## Final prescreen audit

1. Frame one is real on-device gameplay, and the core verb lands before 0:10.
2. The app is visibly running from one named APK; the capture manifest records device, commit,
   package, APK hash, raw-clip hashes, and each level/state.
3. The exact evidenced CM-R57.2 slate, or its recorded human-amended replacement, appears by 1:54
   and matches the live form; every category-specific answer is complete. When replacing the
   Best-Game-only draft card, recount VO under the manifest rule above and confirm every category is
   readable in a full-resolution muted watch. Reallocate earlier beats if needed; never shrink the
   slate silently or extend the cut past 1:55.
4. No frame or line implies a Daily mode, 30 levels, commerce surface, rewarded placement, theme,
   level select, social feature, messaging integration, store availability, or launch result.
5. The golden reference is absent, all HUD is shipped HUD, and every art-direction sentence has
   passed V-ART.
6. Audio/marks review passes: narration plus silence is allowed, and any game audio has an exact-
   candidate V-AUDIO/MARKS receipt; captions remain readable when muted.
7. The encoded master probes at 1:55 or shorter. Target 1:54; do not fill the spare second.
8. V-HOST records a working canonical YouTube/Vimeo URL and independent playback check before the
   URL is pasted into Devpost.
9. Every level count and ID range matches the final V-EVIDENCE authored census or ordinary-player
   reachability census, whichever the sentence names.
10. V-PARITY binds the video APK and live Play production build to the same visible `versionCode`,
    package, candidate manifest, device, and freeze date.

## Sources

- `docs/plan/marketing/STORE-PACK-frozen-contract.md`
- `docs/plan/specs/submission_script.md` §5
- `docs/plan/specs/growth_aso_plan.md` §§4–7 and §23
- `docs/plan/specs/product_spec.md` §§7–12 and §22
- `docs/prd/PRD.md` CM-R57
- `art/diorama-pass:evals/results/ux/art-diorama-2026-08-09/EVIDENCE.md`
