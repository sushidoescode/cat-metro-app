# TASK 27 — POLISH SPRINT BUILDER BRIEF (paste into a fresh Codex chat; one chat per lane)

You are TASK 27-<LANE> for Cat Metro, a Unity 6000.3.16f1 cat-themed train-routing puzzle for
Android at /Users/sushantsrikrish/cat-metro-app. Replace <LANE> with one letter A–F below; the
human tells you which. This is a fresh chat: assume no shared context.

Read first, in order: AGENTS.md, .claude/rules/unity.md, docs/LOOK.md, docs/reference/
gen-ref-NOTES.md (open every image in docs/reference/), docs/plan/POLISH-SPRINT-2026-09-06.md,
and the frames of the human's actual Pixel playtest in .catshots/playtest-2026-09-05/frames/
(f_001..f_037, chronological, 480 wide) — the contact sheet is contact-sheet-1.png. The full
audit that produced your lane is .catshots/playtest-2026-09-05/polish-audits.json.

## How to work

- Branch feat/polish-<lane-letter> from main (read the tip with `git log --oneline -1`; it moves
  daily). Work in a worktree under .claude/worktrees/. Never `git commit -a`.
- The licensed art lives in the gitignored unity/Assets/Art/Generated/incoming/ of the MAIN
  checkout only; renders and captures that need the real cat or props must run from the main
  checkout, never from a bare worktree (the placeholder is silent).
- Test-first for every behaviour; renders after every visual change. Unity `-runTests` never
  with `-quit`; graphics PlayMode, never `-nographics`. Capture rigs: CM_BOARD_LOOK_CAPTURE_DIR /
  CM_UI_CAPTURE_DIR with CM_CAPTURE_SIZE (default 917x2048 = the Pixel's aspect), CM_CAPTURE_LEVEL,
  CM_CAPTURE_TICK, CM_CAPTURE_SWITCHES=S1@12,S2@42, CM_CAPTURE_HUD=on|off.
- Small visible changes beat perfect abstractions. If a change needs a product decision, ask
  the orchestrator in one sentence and keep building everything that does not depend on it.
- Settled, not open: docs/LOOK.md palette; no coin/gem/currency UI; no text station signs; no
  Score/Moves/timer HUD; rewarded video only; cosmetic-only monetization; 60-level campaign;
  Daily after 7 wins; no new licensed 3D assets this sprint; no ladder restructuring.
- Day-1 foundations are shared: lane C's font/TypeScale, lane D's ChromeChip factory, lane B's
  BoardFx/Tween, lane A's grid one-liner, lane F's rig-fallback probe. Whoever owns one merges it
  first (through the slot) and the others rebase onto it; until then, stub the call.
- Report to the orchestrator: branch head SHA, full EditMode + graphics PlayMode counts with XML
  paths, every render path with a one-line "what reads / what does not", and explicitly what you
  did NOT verify. The validation slot re-runs everything at your exact SHA before merge.


## LANE A. Board frame, light and content  (6 days)

Goal: The board fills the portrait on every level, the desk is warm wood to the edge, and the shape-band levels are fair.

Items, in order:

- rank 3 (day 1: land the BoardView.cs:198 GridX/GridY one-liner and merge before lane B branches; days 1-2: SafeHeight/FitPadding/band re-derivation and test pins)
- rank 14 API half only: BoardSceneLook.FitCamera(viewport Rect) + Home fit as a second BoardLookTests case, merged by end of day 2 for lane C
- rank 16 desk albedo / DefocusVeil / shadows / MSAA
- rank 31 track colours
- rank 26 content: L010/L011 route order, L009-L012 limits, validator re-run
- stretch: rank 36 L004 window widening (content half only)

Detail per rank (from the audit synthesis):

### rank 3 — Board fills the portrait: anisotropic grid + reclaim the empty bottom band [high, ~2d]

Why: All 60 levels are width-bound (content w/h after the 38-degree pitch is 0.73-1.90 vs the 0.534 frame aspect), so the slab uses 35-51% of frame height (f_008 51%, f_009 39%, f_033 35%) and 31% of the screen is dead desk. Every cat, badge and lever grows ~25% for free when this lands. gen-ref-board-framing.jpeg runs the board to ~80% of height.

Fix: (1) BoardView.cs:198 `_nodePos[i] = new Vector3(X*GridX, Y*GridY, 0)` with GridX 0.8, GridY 1.25 (min horizontal spacing after compression 1.6 units; same-row stations >= 3.2 units vs 1.65 kiosk roof, so nothing collides). (2) BoardSceneLook: SafeHeight 0.76 -> 0.84, safe band 0.06..0.90, FitPadding 1.05 -> 1.02. (3) Prerequisite: HUD counters folded into the capsule row (rank 30) or clamp the top of the band under the capsule so the depot roof never collides once L001 becomes height-bound. Re-derive RuntimeSceneRigTests.AssertInside bands and BoardTrackIntegrationTests node pins. Land the BoardView one-liner on day 1 and merge before the readability lane branches.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardSceneLook.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Tests/PlayMode/Board/RuntimeSceneRigTests.cs

### rank 14 — Home diorama refit: the board is fitted to the window, not cropped by it; Play dollies from Home framing to play framing [high, ~1.5d]

Why: FitCamera fits the board to the full screen once at load; Home then cuts a 0.075-0.925 window out of it, so the depot/engine are sliced by the top border and stations sit on the edges (f_001/f_021/f_022). Because Home reveals the same tick-0 board, a camera dolly on Play is the cheapest 'premium' transition in the game.

Fix: BoardSceneLook.FitCamera gains an optional viewport Rect (0..1 screen space): adjust orthographicSize by 1/windowHeightFraction and offset the camera by the window centre. GameRoot.ShowHomeForPresentation (650-657) computes DioramaWindowTransform in screen space and refits; Intro.PlayRequested (625-632) tweens back to the full-screen fit over 0.4s (BoardFx.Tween, MotionOff collapses to 0) while the frame/pins fade over 0.25s. Thin the frame: strips 0.025/0.08 -> 0.02/0.05, drop the inner navy border. Add the Home fit as a second BoardLookTests case rather than changing the gameplay law. API half lives in the framing lane (day 2, merged early); call sites in the Home lane.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardSceneLook.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs

Depends on: 3, 12

### rank 16 — Desk light: warm mid-tone wood to the frame edge, kill the ghost DoF lobe, sharper grounding shadows, 4x MSAA [high, ~1d]

Why: WarmDesk albedo x DeskGrain falloff x DefocusVeil EdgeAlpha 0.5 leaves 55% of every gameplay frame near-black, so the board reads as a small window in a black frame; a white blurry cone (LobeAlpha 0.8) sits bottom-left of every frame with no object; shadowStrength 0.45 is documented as 'no remaining justification'; MSAA is off.

Fix: BoardSurface WarmDesk (0.495,0.299,0.137) -> ~(0.62,0.40,0.22); DefocusVeil EdgeAlpha 0.5 -> 0.25, LobeAlpha 0.8 -> 0 (or move the real cup prop under it at 0.3); BoardSceneLook shadowStrength 0.45 -> 0.62, m_ShadowDistance 25 -> 14; CatMetro_URP.asset m_MSAA 1 -> 4. All constants; one device capture each.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardSurface.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/DefocusVeil.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardSceneLook.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Settings/CatMetro_URP.asset

### rank 26 — Content: L010/L011 three-way routes in spatial order; L009-L012 time limits >= 2x solver ticks [medium, ~1d]

Why: L010/L011 levers cycle left-right-centre, breaking the order L009 taught and L015 relies on; L009-L012 limits drop to 120-145 ticks (15-18s) with no clock, and the optimal line already burns 58-65% of the limit so one wrong-shape refusal kills the level with 'The last train left the depot'.

Fix: Reorder the routes arrays in L010.json and L011.json to match station x order (2 lines each; reorder L010's waves to round/square/triangle if the carousel intent is kept). Raise timeLimitTicks: L009 120 -> 160, L010 120 -> 160, L011 145 -> 190, L012 135 -> 180. Re-run scripts/validate-content.sh (Solver/Brittleness/Novelty).

Files: /Users/sushantsrikrish/cat-metro-app/content/levels/L010.json, /Users/sushantsrikrish/cat-metro-app/content/levels/L011.json, /Users/sushantsrikrish/cat-metro-app/content/levels/L009.json, /Users/sushantsrikrish/cat-metro-app/content/levels/L012.json

### rank 31 — Track reads as a white road: RailNavy token, warm sleepers, narrower bed [medium, ~0.5d]

Why: Cream sleepers on a cream bed with near-black InkNavy rails; BoardSceneLook.cs:288-297 already asks for a rail token ~2.3x brighter in linear. Three constants and two material lines.

Fix: Palette.RailNavy (64,73,105) for rails; sleepers WarmWood (or bed warm, sleepers cream); ToyTrackMeshBuilder BedHalfWidth 0.54 -> 0.44. Re-pin the rail colour in BoardLookTests.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/ToyTrackMeshBuilder.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Theme/Palette.cs

### rank 36 — Dead-end sidings read as sidings: buffer-stop prop on station-less leaf nodes, no trees within 1 unit of a node (Daily HOLD, L004/L008/L014 yards) [low, ~1d]

Why: The Daily QueuedFork HOLD spur ends under a tree canopy (f_023/f_024) and decoy yards for colours that never spawn look like destinations. Presentation-only; the content alternative (adding a late cat of the yard's colour) changes the authored ladder.

Fix: BoardPropDecorator: any node with no outgoing edge and no station gets a buffer-stop prop (existing admitted prop or a 3-box procedural stop) and no roof/badge; exclude tree placement within 1 unit of any node. Widen L004's E_IN by 4 ticks to lift the 16-tick first-decision window.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Props/BoardPropDecorator.cs, /Users/sushantsrikrish/cat-metro-app/content/levels/L004.json

Depends on: 7


EVIDENCE GATE (the slot checks exactly this):
PlayMode captures (AdvancedLadderPresentationTests CM_LADDER_CAPTURE_DIR pattern, 917x2048 = the 960x2142 aspect) for L001, L002, L009, L041, L052: board slab occupies >= 65% of frame height on L001-L015 (from 35-51%) with no station/depot outside the safe band; BoardLookTests, RuntimeSceneRigTests, BoardTrackIntegrationTests, TrainConsistTests green after re-pin; scripts/validate-content.sh green; one adb screenshot of L002 on 48121FDAP006X4 downscaled to 960x2142 with desk luminance >= 0.35 sampled at y=5% and y=95% and no white lobe bottom-left.


## LANE B. Board readability and juice  (6 days)

Goal: Cats are the right colour, the right shape and readable; every tap and every arrival is visible.

Items, in order:

- rank 2 magenta shape cats (day 1)
- rank 7 station badge = shape, letters removed
- rank 12 BoardFx/Tween foundation + switch swing + chrome press state (foundation merged by day 2 for lanes C/D)
- rank 17 wrong-station visual
- rank 15 cat scale law (days 4-5, after lane A's grid merge)
- rank 18 train smoke + engine bob + Home breathe
- rank 27 refused-tap feedback + Flips colour law
- stretch: rank 19 delivery mini-beat

Detail per rank (from the audit synthesis):

### rank 2 — Square/triangle cats ride as Color.magenta with a circle pin (L009-L012, L015, Daily) [high, ~0.5d]

Why: A literal error colour in the judge's path from level 9 on. BoardView.cs:569-570 passes the packed CatToken byte to ToyTrainView.SyncSlot -> CatLine.NameOfCode only accepts 1..5 -> "" -> magenta, and the pin derives Circle from "". f_030/f_031/f_033/f_035 all show it.

Fix: BoardView.cs:570: pass CatToken.Color(trains[t].Color) for the tint and CatToken.Shape(trains[t].Color) mapped Round/Square/Triangle -> DestinationShape for the pin (SyncSlot gains a shape parameter, falling back to CatLine.ShapeOf(line) when unauthored). Give CatFaceView.Bind(colour, shape) the same rule from WaveDto.Shape so the HUD badge matches. Delete the floating 'cat-token' TextMesh (BoardView.cs:555-571) and the HUD _tokens letters once the badge carries the shape. Add a PlayMode assertion on L009: no ToyTrainView tint == Color.magenta.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/ToyTrainView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/WavePreview/CatFaceView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Domain/CatToken.cs

### rank 7 — Station badge = authored shape, no letters (R/B/G/Y plates and the superscript 'o/S/T' go) [high, ~1d]

Why: gen-ref-NOTES forbids text station signs and LOOK.md wants 'blue square, red circle'. Today plate shape = line colour (so L009's three stations are identical red 'R' discs) and the only shape signal is a ~6px TextMesh letter. This is the concrete reason mid-campaign levels feel unclear.

Fix: BoardPropDecorator.EnsureProjectOwnedStationPlate: plate mesh = DestinationShapeMesh.ForShape(station.Shape) when authored else CatLine.ShapeOf(line); fill = line colour on the cream keyline; PlateSize +15%. Stop creating the 'Symbol' and 'match-shape' TextMeshes in BoardView.cs:226-244. Mirror the rule in the wave capsule so cat pin, HUD badge and plate are one glyph at three sizes. Re-check with a capture at L009 and the Daily QueuedFork board.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Props/BoardPropDecorator.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/DestinationShapeMesh.cs

Depends on: 2

### rank 12 — Tween + BoardFx foundation, then the switch flip swings and every chip has a press state [high, ~1.5d]

Why: The game's only verb teleports between two Euler poses the same frame (ToySwitchView.cs:33,98; TapInput.cs:112) and no button reacts on screen. There is no tween helper and zero ParticleSystems in Scripts/. Every later juice item builds on this half-day.

Fix: Presentation/Fx/BoardFx.cs: ~40-line unscaled-time Tween (Punch/Shake/EaseOutBack, single Update, no coroutine allocs, no Animator), three runtime 64x64 textures (puff, heart, star), a Materials/Particle.mat so the URP Particles/Unlit shader is not stripped (the F-DEV-2 lesson), a pool of 4 ParticleSystems under the board root, all gated by MotionOff. ToySwitchView.SetDirection: animate to the target yaw over 0.14s with ~8 degree overshoot, lever pivot +-12 degrees, base Punch 1.0->1.12, 6 cream dust particles. Chrome: on UiTapAccepted resolve the region's RectTransform and run 1.0->0.95->1.02->1.0 over 0.14s with a 2-frame 0.9 darken; reused by ResultsPanel, RetryCtaView, LevelIntroSheet, Home pins, Wardrobe cards.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Fx/BoardFx.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/ToySwitchView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Input/TapInput.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/ScreenChromeController.cs

### rank 15 — Cat scale as a law across levels: head+ears >= 5% of frame width (w <= 8 levels), >= 4% elsewhere [high, ~1.5d]

Why: HeadDiameter 0.31 is tuned to hit 5-6% only at L001; on L002/L010 the head is ~2.7% of width and the face is two dots (the human's 'cats too small to see how cute they are'). The parked prop engine and the switch lever are the largest objects on the board.

Fix: After the grid compression (+25% px/unit): HeadDiameter 0.31 -> 0.36, carriage Body 0.34x0.38 -> 0.40x0.44 and Chassis to match (inside the 1.08 bed), PlatformQueueSpacing 0.42 -> 0.48, EyeSize/Muzzle in proportion, HeadCenterZ +0.03 so more of the sphere clears the wall (0.037 spare under switch furniture). Re-run TrainConsistTests' clearance sweep; re-pin BoardLookTests at L001, L002 and L009. Make the HUD capsule faces portraits of the licensed rig (docs/reference/cats-baked-front.png) instead of the two-triangle face if time allows.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/ToyTrainView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Tests/PlayMode/Board/BoardLookTests.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Tests/PlayMode/Board/TrainConsistTests.cs

Depends on: 3

### rank 17 — Wrong-station arrival gets a visual: recoil, badge shake, the cat's own shape drawn large with a cross-bar [high, ~1d]

Why: The only rejection feedback is a thud and the chuff stopping; with three same-colour stations (f_034) a mis-routed train simply stops. Together with rank 7 this is what makes mid-campaign levels readable.

Fix: On the Rejections edge in BoardView.UpdateFrom: recoil the consist 0.08 units along -heading with a 0.3s spring return; shake the station badge +-5 degrees and flash the plate SignalRed for 2 frames; show DestinationShapeMesh of the cat's own shape at 1.6x above the cat with a cross-bar for 0.6s; Vignette 0.25 -> 0.42 -> back over 0.4s via the active volume. Pair with the refused-tap feedback (rank 27).

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/DestinationShapeMesh.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/ToyTrainView.cs

Depends on: 12

### rank 18 — Train smoke from the Funnel + engine bob (also makes Home breathe: parked engine puffs every 4s) [high, ~0.5d]

Why: ToyTrainView builds a Funnel that never smokes; f_001/f_006 are pixel-identical 6s apart and every gameplay still is a frozen diorama. The store video and screenshots are stills of exactly this moment.

Fix: Child ParticleSystem on Funnel per ToyTrainView: 5/s while OnEdge/OnEdgeReverse (expose SetMoving(bool)), soft puff sprite from BoardFx, size 0.07 -> 0.2 over 0.9s, WarmPaper alpha 0.8 -> 0, world simulation space, velocity toward camera + 0.1 opposite heading; engine bob +-0.004 units at 2x chuff rate. Parked engines emit one puff every 4s while ScreensVisible; a +-0.6% orthographicSize breathe over 6s on the board camera on Home. Everything stops on Intro.PlayRequested so sim determinism is untouched.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/ToyTrainView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardView.cs

Depends on: 12

### rank 19 — Delivery mini-beat: hearts burst, station roof/badge pop, pin fade, HUD trophy punch [medium, ~1d]

Why: On delivery the cat walks 0.28s, 'celebrates' 0.48s and vanishes; nothing on the station or the counter reacts. The first delivery is one of the judge's first-five-seconds moments in L001.

Fix: On the deliveryAdvanced branch in BoardView.UpdateFrom: BoardFx.Emit 12 heart+star sprites in the cat's line colour (life 0.7s, gravity -0.3); Punch station roof scale Y 1.0->1.15 over 0.25s and the badge 1.0->1.2 with 60ms delay; pin card pops 1.3x and fades 0.3s; HUD trophy TMP 0.2s scale punch with a one-frame marigold tint.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/ToyTrainView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/WavePreview/WavePreviewStrip.cs

Depends on: 12

### rank 27 — Refused taps (flip cap / cooldown) get a locked tick, lever shake and Flips pulse instead of the accepted clunk [medium, ~0.5d]

Why: TapInput.cs:110-112 fires SwitchTapAccepted before EnqueueToggle and ignores its bool; at 'Flips 2/2' (f_024) every further tap clunks and nothing moves. Same path on L013-L015 and every cooldown level from L021.

Fix: Use the return value: on false play a distinct short locked tick (new 0.08s synth clip), ToySwitchView shake +-4 degrees over 0.2s, pulse the Flips label. Also fix the flip colour law in WavePreviewStrip.cs:343: WarmPaper while RemainingToPerfect > 0, TabbyYellow at 0, SignalRed only when negative (today a perfect 2/2 run flashes red).

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Input/TapInput.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/ToySwitchView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/WavePreview/WavePreviewStrip.cs

Depends on: 12


EVIDENCE GATE (the slot checks exactly this):
New PlayMode assertion on L009 at the tick a square cat rides: no ToyTrainView tint == Color.magenta and its pin is DestinationShape.Square; L001/L009/Daily captures show plates with no TextMesh children and circle/square/triangle silhouettes; BoardLookTests re-pinned so head+ears >= 5% of frame width at L001 and >= 4% at L002 and L009; a 6-frame capture sequence (every 50ms) of one switch throw and one wrong-station arrival showing lever overshoot and the recoil; device screenshot of L009 mid-ride at 960x2142.


## LANE C. Home first frame  (6 days)

Goal: The first five seconds look like gen-ref-menu.png: display type, centred carved sign, lamp-lit backdrop, nothing accidental in the window.

Items, in order:

- rank 1 font import as TMP default + TypeScale sweep (day 1, merged early so every lane inherits it)
- rank 4 HUD bleed / placeholder furniture / speaker glyph
- rank 5 vignette + lamp + blurred shadows
- rank 13 title sign with the icon cat mark
- rank 14 call-site half: Home refit on Show, dolly to play framing on PlayRequested (after lane A's API lands)
- rank 33 half-width Daily/Wardrobe row using ChromeChip from lane D
- rank 32 splash colour + navy fade-in
- stretch: rank 29 Daily locked pin + unlock ring; Home passengers (see rejected note) only if lane F's rank 6 proves the rig admitted

Detail per rank (from the audit synthesis):

### rank 1 — One rounded OFL display font as the TMP default + a TypeScale (kills the 'Unity template' tell on every screen at once) [high, ~1d]

Why: Every label in every frame is LiberationSans regular (the only .ttf in the repo; TMP Settings.asset m_defaultFontAsset). Judges read typography as production-vs-prototype in the first second on Home and again on every intro/win card. Setting the default asset changes ~15 call sites with zero code edits.

Fix: Import Fredoka One (or Baloo 2 ExtraBold) from Google Fonts, bake a 1024^2 SDF via Font Asset Creator (ASCII+Latin-1), set it as m_defaultFontAsset in unity/Assets/TextMesh Pro/Resources/TMP Settings.asset, keep LiberationSans as fallback. Add Presentation/Theme/TypeScale.cs (Display 40dp, Title 28, Body 20, Caption 14; every fontSizeMin >= 12) and sweep HomeScreenView (9pt tally, 10pt chip), WavePreviewStrip (fontSizeMin 6), LevelIntroSheet, ResultsPanel, RetryCtaView, BannerView, HaltVeilView, HintChipView. Add a TMP material preset with a 0.3 DepotNavy underlay for over-board titles. Record the OFL licence next to the Meshy/Tripo record.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/TextMesh Pro/Resources/TMP Settings.asset, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Theme/TypeScale.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/WavePreview/WavePreviewStrip.cs

### rank 4 — Home first-frame cleanup: hide the HUD bleed, delete placeholder furniture, move the 'SFX off' chip [high, ~1d]

Why: f_001/f_021: trophy/people counters and the cream capsule ghost show between the sign and the frame; three coloured pills, two orange 'lamps' and two ghost rects float over the board as debug markers; a text chip 'SFX off' is the first thing in reading order and pushes the title off-centre. All three are deletions or one-line state law, so this is the cheapest first-five-seconds gain.

Fix: (a) GameRoot 508/1145: bind `() => ScreensVisible ? "Home" : ScreenState` and add "Home" to WavePreviewStrip.VisibleInState; one WavePreviewStripTests case. (b) Delete RouteMarkerA/B/C, WindowLampLeft/Right, ParkedDistrictA/C in HomeScreenView.Create (282-290, 256-261, 266-271); update MarkerCount/MarkerColors read-backs and the three HomeScreenStyleTests that pin them. (c) Audio toggle becomes a 44dp round speaker glyph (HudShapeSprites coverage sprite, slashed when off) at top-right mirrored with DailyReminderLayout.GearRect, so HomeLayout.TitleRect can inset both sides equally; keep the ui.csv keys for the accessibility label.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeLayout.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/WavePreview/WavePreviewStrip.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs

### rank 5 — Home backdrop: warm radial vignette + lamp glow + blurred shadows instead of a flat 48% navy tint [high, ~1d]

Why: Outside the window everything is a uniform #23202D mud with crisp rectangle shadows and a grey dome peeking under the frame. gen-ref-menu.png is lamp-lit with a hot-spot behind the sign and dark warm corners. One procedural sprite changes the whole mood of the first frame.

Fix: Replace the four backdrop quads (HomeScreenView.cs 170-178) with one full-screen radial-vignette sprite from HudShapeSprites (alpha smoothstep 0.25 at window centre -> 0.85 at corners, tinted #2A1A10) plus a TicketOrange ~18% radial 'lamp' behind the title; push alpha to >= 0.85 in the 40dp strip under the frame so props cannot peek. Replace HeroShadow/TitlePlaqueShadow rects with a blurred-rounded shadow sprite at 45% alpha, 16dp larger. Keep ExplicitViewport_ShadeCoversOnlyOutsideTheDioramaAperture green by excluding the window quad.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/WavePreview/HudShapeSprites.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Tests/PlayMode/Screens/HomeScreenStyleTests.cs

### rank 6 — Make the licensed-rig fallback loud and verify on the Pixel (gates every 'show the 3D cat' item) [high, ~0.5d]

Why: On the 09-05 device frames the Home holder shows the flat 2D sticker, not the rig, and HomeProfileRigView.UsePortraitFallback (4 call sites, lines 123/129/145/276) logs nothing. A game called Cat Metro currently ships zero 3D cats on Home; nobody knows why on-device.

Fix: Add Debug.LogWarning("HOME_RIG fallback branch=<n> admitted=<CatalogAdmittedEntryCount>") in UsePortraitFallback and a matching 'HOME_RIG mounted=true' on success; extract the holder-agnostic ProfileRigMount while there (parametrise HomeFacingYaw) so the Wardrobe can reuse it. Day 1: build via scripts/build-apk.sh (human), `adb -s 48121FDAP006X4 logcat | grep HOME_RIG`, fix whichever branch fires. If the rig cannot be admitted on device, drop the in-window holder and show the cat only on the Wardrobe chip.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Cats/HomeProfileRigView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Cats/CatModelCatalog.cs

### rank 13 — Home title sign: two-line carved 'Cat / Metro' on a bevelled, centred plaque with nail dots and the icon cat mark [high, ~1d]

Why: f_001: the plaque is 9% right of centre (TitleRect only insets past the SFX chip), the word-mark is one small line in 60% empty navy, the 'nails' are orange pills and the 'carve' is an invisible 2px offset. The launcher icon's conductor-cap cat never appears again in the product.

Fix: HomeLayout: HeaderHeightDp 84 -> ~130, TitleRect symmetric (inset both sides by max(toggle, gear)). HomeScreenView.Create 180-209: 'Cat\nMetro' with fontSizeMax ~96 and -10 line spacing in the display font, lighter InkNavy top strip + DepotNavy bottom lip for the bevel, four 6dp cream-dark corner dots, and a 2D sprite of the icon cat (Store/Icons/cat-metro-icon-foreground-512.png cropped to the head, imported as a Sprite) on the sign's left peg. Update HomeScreenStyleTests.AudioChip_LeavesEightDpAroundThePaintedCarvedTitle and HomeLayoutTests.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeLayout.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Store/Icons/cat-metro-icon-foreground-512.png

Depends on: 1

### rank 14 — Home diorama refit: the board is fitted to the window, not cropped by it; Play dollies from Home framing to play framing [high, ~1.5d]

Why: FitCamera fits the board to the full screen once at load; Home then cuts a 0.075-0.925 window out of it, so the depot/engine are sliced by the top border and stations sit on the edges (f_001/f_021/f_022). Because Home reveals the same tick-0 board, a camera dolly on Play is the cheapest 'premium' transition in the game.

Fix: BoardSceneLook.FitCamera gains an optional viewport Rect (0..1 screen space): adjust orthographicSize by 1/windowHeightFraction and offset the camera by the window centre. GameRoot.ShowHomeForPresentation (650-657) computes DioramaWindowTransform in screen space and refits; Intro.PlayRequested (625-632) tweens back to the full-screen fit over 0.4s (BoardFx.Tween, MotionOff collapses to 0) while the frame/pins fade over 0.25s. Thin the frame: strips 0.025/0.08 -> 0.02/0.05, drop the inner navy border. Add the Home fit as a second BoardLookTests case rather than changing the gameplay law. API half lives in the framing lane (day 2, merged early); call sites in the Home lane.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Board/BoardSceneLook.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs

Depends on: 3, 12

### rank 29 — Daily Line: 'Daily Line unlocked!' ribbon on the 7th win + a locked pin on Home before unlock; hide 'Dailies completed: 0' [medium, ~1d]

Why: f_019->f_020: the 7th win just says 'Home' and sends the player backwards; the mode is invisible before unlock and announces itself with a 9pt zero counter that reads as debug text.

Fix: ui.csv `results.unlock.daily,Daily Line unlocked!` shown as a MetroTeal ribbon on the Banner canvas when _returnHomeAfterCampaignUnlock is set; the Daily pin gets the TicketOrange ring on its first Home show. Always lay out the Daily pin: locked = cream at 60% with a wooden padlock glyph and 'Unlocks after 7 station wins' (lifetime win count, not a streak); unlocked = 'Today's Line is ready', tally shown only when N > 0 at Caption size. DailyWireTests:557 stays true.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Resources/Strings/ui.csv

Depends on: 4

### rank 32 — Boot: splash background InkNavy with the icon cat as a logo; Home fades in from navy [medium, ~0.5d]

Why: Cold boot is the stock Unity splash on #231F20 grey then a hard cut to Home; it is literally the first thing a judge sees and costs settings only.

Fix: ProjectSettings.asset: m_SplashScreenBackgroundColor -> #22304A, add cat-metro-icon-foreground-512.png to m_SplashScreenLogos, set a static androidSplashScreen drawable in the same navy; GameRoot: one full-screen InkNavy Image on ScreensCanvas fading out over 0.3s on first frame. m_ShowUnitySplashLogo off only if the licence tier permits (open question).

Files: /Users/sushantsrikrish/cat-metro-app/unity/ProjectSettings/ProjectSettings.asset, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Store/Icons/cat-metro-icon-foreground-512.png, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs

### rank 33 — Home layout: Daily and Wardrobe as two half-width pills on one row so the diorama gains ~68dp [medium, ~1d]

Why: With Daily unlocked three stacked 60dp bars eat ~212dp and the window shrinks to ~54% of height (f_021 vs f_001).

Fix: HomeLayout: Play full-width 64dp; DailyPinRect = left half, WardrobePinRect = right half at 52dp (WardrobeLayout.EntryRect already delegates); CtaBottomInsetDp 12 + BottomBreathDp 24. Update HomeLayoutTests and UnlockedDaily_ReservesItsRouteSlot_BelowTheDiorama.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeLayout.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Tests/EditMode/Presentation/HomeLayoutTests.cs

Depends on: 10


EVIDENCE GATE (the slot checks exactly this):
UiPhoneCaptureTests Home capture at 917x2048 (fresh profile and Daily-unlocked): zero HUD-capsule pixels between plaque and frame (sample the band), plaque centre within +-2% of screen centre, camera projection of frameBounds fully inside DioramaWindowTransform, backdrop luminance at corners < 0.25 and > 0.45 at the lamp hot-spot; HomeScreenTests/HomeScreenStyleTests/HomeLayoutTests green with the whitelist untouched; adb screenshot of Home on 48121FDAP006X4 at 960x2142 and the HOME_RIG logcat line (mounted=true or the failing branch).


## LANE D. Level flow: intro, win, transitions  (6 days)

Goal: Every level is announced on a cream ticket, every win has a beat, and no screen change pops.

Items, in order:

- rank 10 ChromeChip factory (day 1, merged immediately; adopted by lanes C and F)
- rank 9 intro card restyle + shown on every level + teachingGoal + 'Deliver 1 cat'
- rank 8 win beat (confetti/hop via lane B's BoardFx once merged; stub call until then)
- rank 20 transition veil on LoadNext/Retry
- rank 28 fail banner id leak + Try again chip
- stretch: rank 35 fail mood

Detail per rank (from the audit synthesis):

### rank 8 — Win beat: plaque title above the scrim, warm shade, stitched-pin CTA, 0.35s ease-in, confetti + cat hop [high, ~2d]

Why: f_010/f_020/f_025/f_028: same-frame hard cut to a 45% scrim, a dimmed Liberation Sans 'All cats home!' painted UNDER the scrim (Banner sortingOrder 90 < results 110) and a quarter-screen off-palette green slab. This is the frame judges screenshot and the recording's second weakest moment.

Fix: BannerView sortingOrder 90 -> 115; title as a DepotNavy plaque with CreamCard display text. Scrim = Palette.WithAlpha(DepotNavy, 0.48). CTA painted via ChromeChip (rank 10): 60dp pin, 20dp insets, TicketOrange ring, InkNavy label; registered hit rect stays the full ThumbBand (ResultsPanelTests:255 pin). Code-driven ease-in on Image/TMP alpha + scale 0.92->1 (no CanvasGroup: whitelist 335-340). Delay painted alpha 0.6s while IsVisible stays immediate. On Won: BoardFx.Confetti (100 flat quads in the six palette colours, 2s) from the top edge, keep delivered cats on platforms until dismiss with a staggered 90ms hop (Cat_Celebrate for rigs, scale hop for placeholders), delay the flourish to land with it. One TMP under PanelRoot stays (title lives on the Banner canvas).

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/ResultsPanel.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/BannerView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Cats/CatPresentationTrack.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Tests/PlayMode/Hud/ResultsPanelTests.cs

Depends on: 10, 12

### rank 9 — Level intro card: cream ticket over a full-screen shade, shown before EVERY level, with the teaching line and 'Deliver 1 cat' [high, ~1.5d]

Why: 59 of 60 levels start unannounced (Intro.Show only fires from Home.LevelSelected), meta.teachingGoal is authored on 60/60 and never shown, the card is a hard-edged off-palette navy rect covering only the middle 36% while the HUD stays lit above it, and L001 reads 'Deliver 1 cats' in the judge's first minute.

Fix: GameRoot.LoadNext (when Home != null): after LoadLevel call Intro.Show(name, deliveries, teachingGoal) and Stack.Push("intro"); the existing !ScreensVisible gate already freezes tick 0. LevelIntroSheet visuals: full-screen DepotNavy 0.48 shade, CreamCard RoundedSquare ticket with a 2dp TicketOrange inset, InkNavy display title, goal line, optional third line = teachingGoal (always on newMechanic levels). Play chip via ChromeChip; PlayChipRectPx == ThumbBand pin stays. Add `intro.goal.one,Deliver 1 cat` to ui.csv and pick it when deliveries == 1 (do not edit intro.goal: UiCsvUx06Tests:67 pins it).

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/LevelIntroSheet.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Resources/Strings/ui.csv, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Tests/PlayMode/Bootstrap/LoadNextTests.cs

Depends on: 10

### rank 10 — One shared ChromeChip factory: Home pins, Next, Play, Try again, Home CTAs all become the stitched cream pin [high, ~1d]

Why: Three off-palette full-band slabs (green Next, navy Play, navy Try again) sit next to Home's 60dp orange-ringed pins; side by side they look like two games. HomeScreenView's MakeChip/MakeSurface/ApplyRoundedPaint are private statics today.

Fix: New Presentation/Hud/ChromeChip.cs: PaintPrimary(parent, bandPx, label, ringColour, iconSprite) builds ring (blurred 40% halo, 12dp larger), CreamCard RoundedSquare face, dashed-stitch inset (new sliced DashedRoundedRing in HudShapeSprites), soft shadow 0/-4dp, icon+label measured via TMP preferredWidth and centred as a group; 60dp tall, width = min(safe - 2x20dp, 320dp), BottomBreathDp 24 above the gesture pill. Only Image/TMP components. Ring language: Play/Next TicketOrange, Try again MetroTeal, Home InkNavy. Ship it day 1 from the flow lane, merged immediately; Home and Wardrobe entry adopt it day 2+. Registered hit rects stay the full band everywhere.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/ChromeChip.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/WavePreview/HudShapeSprites.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/RetryCtaView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs

### rank 20 — Transition veil on LoadNext/Retry so the board rebuild is never visible [medium, ~1d]

Why: LoadLevel destroys and rebuilds the board synchronously in the tap callback; Won->Next and Retry pop with no wipe.

Fix: TransitionVeil: full-screen CreamCard Image on a canvas at sortingOrder 130, alpha 0 -> 1 over 0.22s, invoke the pending LoadLevel at the opaque midpoint, 1 -> 0 over 0.28s; instant when MotionOff; ignore NextRequested while in flight (LoadNextTests' same-frame double-tap pin must still advance exactly one level). With rank 9 landed the intro card is the visual bridge and only the fade-in matters.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/ScreenChromeController.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Tests/PlayMode/Bootstrap/LoadNextTests.cs

Depends on: 9

### rank 28 — Fail banner: stop leaking node ids ('Platform overflowed at J1'); Try again via ChromeChip [medium, ~0.5d]

Why: GameRoot.cs:1650-1655 substitutes raw level-JSON ids (J1, SRC_A, H2, LOOP_B, E_SORT) into player copy; gen-ref-NOTES forbids text station signage so the substitution should be the framing + ring, not an id.

Fix: Add `fail.platformoverflow.generic,A platform overflowed` (and a junction variant) to ui.csv and use them; CauseCam framing + the coloured ring carry the where. RetryCtaView paints through ChromeChip with a MetroTeal ring; registered rect stays the band.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Resources/Strings/ui.csv, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/RetryCtaView.cs

Depends on: 10

### rank 35 — Fail mood: desaturate -35 and vignette 0.45 over 0.35s, grey puff from the causal funnel, banner drop-in [low, ~0.5d]

Why: Fail is a camera pan and a text banner; no playtest frame reached it, which suggests it was never tuned. Judges rarely see it in the first minutes, so it ranks below the win beat.

Fix: On the Failed edge in GameRoot: ColorAdjustments.saturation 0 -> -35 and Vignette 0.25 -> 0.45 (revert on Retry); one grey puff via BoardFx from the causal train's Funnel; BannerView drops in from above with a small bounce. Pairs with the music low-pass (rank 11) and fail sting (rank 21).

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Cameras/CauseCameraController.cs

Depends on: 12


EVIDENCE GATE (the slot checks exactly this):
PlayMode captures at 917x2048: intro card on L001 (from Home) and L002 (after Next) showing the ticket, the teaching line on L001/L005/L009/L013 and the string 'Deliver 1 cat'; win frames on L001 at +0.4s and +1.0s after Won with the title readable above the scrim (title pixel luminance > 0.9) and the CTA painted as a 60dp pin; a forced fail on L005 with no node id in the banner text; ResultsPanelTests one-TMP and whitelist pins, LevelIntroSheetTests PlayChipRectPx pin, LoadNextTests double-tap pin and UiCsvUx06Tests all green; 10s device screen recording of L001 win -> Next -> L002 intro.


## LANE E. Sound, haptics, settings  (6 days)

Goal: The build is never silent, the phone answers every tap, and each channel has an opt-out.

Items, in order:

- rank 11 music stems via build-music + MusicDirector (days 1-3)
- rank 21 mews, pitch chain, win cadence, fail sting, chuff fade (days 3-4)
- rank 22 haptics + VIBRATE permission
- rank 23 SettingsSheet + AudioPreferences music/haptics + settings.motion binding; muteOtherAudioSources handling once music plays
- stretch: rank 30 HUD capsule empty state / counters row (only lane touching WavePreviewStrip after lane B's Flips colour change lands)

Detail per rank (from the audit synthesis):

### rank 11 — Music: one 32-bar wooden-toy loop in C major as 4 stems + a MusicDirector that crossfades them on train motion/deliveries/win/fail [high, ~2.5d]

Why: GameAudio owns 7 SFX and no music; all 37 frames and the entire 1:45 submission video are silent between taps (submission-plan.md forbids inventing sound in post). Silence is a named weakest moment. C major keeps the existing G4/D5, G4/C5/E5, A4/E5/A5 chimes consonant without re-synthesis.

Fix: Render with the installed ls-clad build-music skill (Apache-2.0, offline): 96 BPM, music box/kalimba lead, felt piano comp, low pluck bass, shaker/woodblock; A8-B8-A'8-C8, 4 sample-locked stems (bed, shaker, melody, sparkle) + a 16-bar sparse Home variant. Ship to unity/Assets/Resources/Audio/CatMetro/music/*.wav (Vorbis 0.5, Streaming) with SHA-256 rows in PROVENANCE.md. New Presentation/Audio/MusicDirector.cs: one AudioSource per stem, PlayScheduled on one dspTime, per-frame volume lerp (tau 0.6s) toward targets: Home bed 0.5; Playing bed 0.7; shaker -> 1 while HasMovingTrain; melody -> 1 at Deliveries >= 1; sparkle at last cat; Won duck 0.35 for 1.2s; Failed low-pass 22k -> 700Hz over 0.4s. Wire beside Audio.Initialize (GameRoot 473-476) and Observe (1659). Once music ships, flip muteOtherAudioSources handling: query AudioManager.isMusicActive() at boot and start muted if the user has their own audio.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Audio/MusicDirector.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Audio/GameAudio.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Resources/Audio/CatMetro/PROVENANCE.md, /Users/sushantsrikrish/cat-metro-app/unity/ProjectSettings/ProjectSettings.asset

### rank 21 — Cat voice + musical SFX: synthesized mews pitched per line, pentatonic delivery chain, win cadence, fail sting, chuff fade [high, ~1.5d]

Why: No cat vocal exists; the delivery chime is the same two notes for the first and last cat; Failed plays nothing; StopChuff() clicks. The emotional hook of a cat game is audible, and the trailer must use in-build audio.

Fix: Extend scripts/generate-game-audio.py (GENERATOR_VERSION v2, regenerate PROVENANCE.md via --check): 5 mews (250-400ms glide 650->950->720Hz, 6Hz vibrato, two formant band-passes), 2 purrs, 1 grumble; a 2.5s C-major win cadence and a 0.9s E4-D4-C4 felt-mallet fail sting; chuff re-rendered so 4 puffs = 2 beats at 96 BPM. GameAudio: 4-source round-robin pool with pitch 0.96-1.04; chime pitch = 2^(steps[min(Deliveries-1,5)]/12), steps {0,2,4,7,9,12}; GameplayAudioCues.Failed edge; mews on CatBoarded/Delivery(+3st)/WrongStation(grumble)/Won (3-mew chorus 80ms apart) and every 8-14s on Home from the rig blink clock; 90ms chuff fade instead of Stop(). Skip the full 11-event list; these five beats carry the video.

Files: /Users/sushantsrikrish/cat-metro-app/scripts/generate-game-audio.py, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Audio/GameAudio.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Resources/Audio/CatMetro/PROVENANCE.md

Depends on: 11

### rank 22 — Haptics: Android VibrationEffect helper on the existing SFX hooks, gated by settings.haptics [medium, ~1d]

Why: Zero vibration calls while SaveDefaults already carries settings.haptics = true; on a Pixel 9 Pro the absence is felt within three taps. ADR 0007 already specified this.

Fix: Presentation/Haptics/IHaptics + AndroidHaptics (VibratorManager.getDefaultVibrator on API 31+, createPredefined EFFECT_TICK/CLICK/HEAVY_CLICK on 29+, createOneShot fallback for 25-28) + NullHaptics; <uses-permission android:name="android.permission.VIBRATE"/> in LauncherManifest.xml. Map: UI tap = tick; switch flip = 18ms@120; over-par flip = two 8ms ticks; delivery = waveform [0,12,40,20]/[0,90,0,170]; wrong station = 45ms@200; win = three rising pulses; fail = 80ms@120; purchase = delivery; no haptic on chuff/departure. Subscribe exactly where GameAudio does.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Haptics/GameHaptics.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Plugins/Android/LauncherManifest.xml, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs

### rank 23 — Settings sheet (Sound / Music / Haptics / Reduce motion) replacing the lone 'SFX off' chip; bind settings.motion to MotionOffToggle [medium, ~1d]

Why: Music and haptics need an opt-out before they ship (Play review + accessibility); settings.motion and MotionOffToggle exist but are never bound so reduce-motion is unreachable; a lone SFX chip reads as a prototype when judges open settings.

Fix: New Presentation/Screens/SettingsSheet.cs built like DailyReminderSheet (StackedModalPriority modal), four teal/cream pill rows + the Daily reminder row when unlocked + a 'Restore purchases' text link; AudioPreferences gains settings.music and settings.haptics via the same TryUpdate path (settings.audio keeps SFX semantics, no schema bump); GameRoot binds settings.motion -> MotionOffToggle at boot. Opened from a gear glyph on the title sign's right peg (Home lane registers the region; audio lane owns the sheet).

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/SettingsSheet.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Application/Save/AudioPreferences.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Bootstrap/GameRoot.cs

Depends on: 11, 22

### rank 30 — HUD capsule: never an empty pill; counters inside the capsule on one row; lever glyph instead of the word 'Flips' [medium, ~1d]

Why: The capsule shows upcoming cats only and sits as a bare cream pill for the tail of every level (5 of 27 gameplay frames); counters float 25px below on the dark desk in a second style; the stacked height blocks the SafeHeight gain in rank 3.

Fix: When FaceCount == 0 collapse to a slim chip of delivered faces greyed with a check. Put trophy/people/flips in the capsule's right third (CounterRowFraction 0.030 goes away). Replace 'Flips' with a lever glyph from HudShapeSprites. Keep it presentation-only in WavePreviewStrip.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/WavePreview/WavePreviewStrip.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Hud/WavePreview/HudShapeSprites.cs


EVIDENCE GATE (the slot checks exactly this):
scripts/generate-game-audio.py --check regenerates PROVENANCE.md with SHA-256 rows for every new clip and the four stems; EditMode tests for the pitch-step table and MusicDirector target vectors per state; scripts/test.sh green; a 60s recording captured on the Pixel with internal audio (the phone's own screen recorder, since adb screenrecord has no audio) covering Home -> L001 -> first delivery -> win, in which the bed is audible on Home, the shaker enters with the first moving train and the cadence lands on Won; toggles persist across app relaunch; no vibration when settings.haptics is false.


## LANE F. Wardrobe and rig  (6 days)

Goal: The 3D cat is visible where it matters and the store reads as a store.

Items, in order:

- rank 6 loud rig fallback + ProfileRigMount extraction + Pixel logcat read-back (day 1, merged early; lane C consumes the logcat, not the file)
- rank 24 rig hero on a wooden plinth
- rank 25 CTA always present, Restore text link, cards fill the rail, empty tab hidden
- stretch: rank 34 selection/badge vocabulary

Detail per rank (from the audit synthesis):

### rank 6 — Make the licensed-rig fallback loud and verify on the Pixel (gates every 'show the 3D cat' item) [high, ~0.5d]

Why: On the 09-05 device frames the Home holder shows the flat 2D sticker, not the rig, and HomeProfileRigView.UsePortraitFallback (4 call sites, lines 123/129/145/276) logs nothing. A game called Cat Metro currently ships zero 3D cats on Home; nobody knows why on-device.

Fix: Add Debug.LogWarning("HOME_RIG fallback branch=<n> admitted=<CatalogAdmittedEntryCount>") in UsePortraitFallback and a matching 'HOME_RIG mounted=true' on success; extract the holder-agnostic ProfileRigMount while there (parametrise HomeFacingYaw) so the Wardrobe can reuse it. Day 1: build via scripts/build-apk.sh (human), `adb -s 48121FDAP006X4 logcat | grep HOME_RIG`, fix whichever branch fires. If the rig cannot be admitted on device, drop the in-window holder and show the cat only on the Wardrobe chip.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Cats/HomeProfileRigView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Cats/CatModelCatalog.cs

### rank 24 — Wardrobe hero = the licensed rig on a wooden plinth with a slow turntable, not the flat 2D sticker [medium, ~2d]

Why: The largest surface in the game (~47% of screen height) shows the least on-look art; the rig already renders cute on the phone (device-2026-09-05/pixel-home-with-rig.png). Monetisation surface, not first-five-seconds, but it is where the 'real 3D cats' claim is proven.

Fix: Mount ProfileRigMount (extracted in rank 6) under LargePortraitMount in WardrobeScreenView.BuildPanel, Layout(canvas.worldCamera) at the end of LayoutForViewport, +-15 degree auto-yaw. Add Palette.WarmWood/WoodShadow; stand = two stacked Disc surfaces + navy plaque carrying the cat's display name; PortraitGlow -> TicketOrange 0.20 radial vignette. Log 'WARDROBE_RIG mounted=<bool> admitted=<n>' on Open. blue_siamese/yellow_longhair keep the 2D fallback this sprint (see rejected). Guard WardrobePurchaseFlowTests hero pixel asserts (364-372) on !Mounted.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Cats/HomeProfileRigView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Theme/Palette.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Tests/PlayMode/Screens/WardrobePurchaseFlowTests.cs

Depends on: 6

### rank 25 — Wardrobe store shape: a Buy/Equip CTA is always present, Restore becomes a text link, cards fill the rail with the item drawn large, empty Accessory tab hidden [medium, ~2d]

Why: For the whole 24s visit the only CTA was 'Restore purchases'; the single 112dp card floats in a 70%-empty rail with the coat as a navy blob; a judge who taps every tab hits 'Nothing is waiting at the depot yet.'

Fix: Auto-select rows[0] for the current slot on Open/OnTabTapped/OnCatTapped so PrimaryActionChip always paints; Restore -> 44dp InkNavy text-link row; fold the status label into the CTA subtitle (40dp status band goes). CosmeticItemCardView: zoom the portrait mount to the item region under a RectMask2D (outfit anchors (-0.15,-0.55)-(1.15,0.75); frame shown alone at 0.8 with base layer suppressed), name on top at 18pt min 16, rail 156dp, width = (rail - 2 gaps)/3. Hide any tab whose slot has zero catalog items for the selected cat; keep the empty-state string for the runtime-filtered case and retarget AssertEmptyBand.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/WardrobeLayout.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Cosmetics/CosmeticItemCardView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Tests/PlayMode/Screens/WardrobePurchaseFlowTests.cs

Depends on: 10

### rank 34 — Wardrobe state vocabulary: identity colour stays on cat chips, navy outline for selection, 28dp corner badges for equipped/owned/rewarded [low, ~1d]

Why: TicketOrange currently means selected-cat, selected-tab, selected-card AND the CTA; equipped/owned states are 15px grey text with no badge.

Fix: PaintSelectors: selection = 3dp InkNavy outline + 2dp shadow, 32dp mini portrait at left; tabs selected = CreamCard face + 3dp orange underline. CosmeticItemCardView: corner badge (equipped = navy disc + cream star; owned = cream chip; rewarded = teal chip with play triangle + 'Watch to borrow'); 'Equipped' ribbon on the hero plaque.

Files: /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs, /Users/sushantsrikrish/cat-metro-app/unity/Assets/Scripts/Presentation/Cosmetics/CosmeticItemCardView.cs

Depends on: 25


EVIDENCE GATE (the slot checks exactly this):
UiPhoneCaptureTests Wardrobe capture at 917x2048 with Mounted == true asserted when AdmittedEntryCount == 1; PrimaryActionChip painted on Open with no tap; three cards spanning >= 90% of the rail width with the item region filling >= 60% of each tile; no Accessory tab when the catalog slot is empty; WardrobePurchaseFlowTests and UiPhoneCaptureTests green; adb screenshot of the Wardrobe on 48121FDAP006X4 at 960x2142 with 'WARDROBE_RIG mounted=true' in logcat.


## Device pass (lane G, after A–F merge; orchestrator-run)

Rebuild from main (scripts/build-apk.sh; the licensed-art guard must log
CLI_BUILD_ASSETS licensedRig=present), install on 48121FDAP006X4 only, walk Home → L001 → L002
→ L009 → a win → a forced fail → Wardrobe → settings, capture each frame with
`adb exec-out screencap -p`, and pair every frame with its f_0xx counterpart from
.catshots/playtest-2026-09-05/ in a contact sheet. Every lane gate is re-checked on the device
frames; regressions become the next day's fixes.
