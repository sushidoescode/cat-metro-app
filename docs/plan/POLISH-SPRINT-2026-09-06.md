# Cat Metro — Polish Sprint 1 (2026-09-06 → 2026-09-13)

Written by the orchestrator from the human's Pixel 9 Pro playtest of 2026-09-05
(`.catshots/playtest-2026-09-05/`, 225 s recording) and a seven-judge audit of those frames
against `docs/LOOK.md` and `docs/reference/`. Full audit: `.catshots/playtest-2026-09-05/
polish-audits.json`; ranked synthesis: `polish-plan.synth.json`.

## The human's verdict (verbatim intent)

1. Menu and overall look are functional but "basic", not top-25 mobile. Big improvement over
   10 days ago; direction is right.
2. Gameplay: the board reads horizontal and should fill the portrait; cats are too small to be
   cute; some mid-campaign levels feel arbitrary.
3. Sound, music, VFX, SFX need a major makeover — "that is where the emotion is".

## What the audit found that the human could not see from the phone

- **Type is the loudest template tell.** Every label is TextMesh Pro's stock Liberation Sans.
  One OFL rounded display font set as the TMP default fixes every screen at once (rank 1).
- **The board is not horizontal; it is small.** Levels L001–L015 are all taller than wide in
  authored units (e.g. L001 4×7). The wooden slab is authored wider than the phone
  (`BoardView.cs:198` uniform grid, `BoardSurface.cs` slab margins) so the camera's width-bound
  fit runs the slab off both screen edges and leaves desk above and below. Fix the grid
  anisotropy and the slab proportions and the height fit takes over; cats grow with it (rank 3).
- **Shape-band cats ride as magenta** (L009–L012, L015, Daily): square/triangle cats have no
  tint and no matching pin (rank 2). This is the single biggest "level makes no sense" cause.
- **Station badges are letters** (R/B/G/Y plates) which `gen-ref-NOTES.md` forbids (rank 7).
- **The licensed 3D rig is NOT in the Home holder on the device** even with the rig installed
  (recording f_001, made after the 23:30 reinstall): the holder falls back to the 2D sticker
  silently. Rank 6 makes that fallback loud and proves the mount on the Pixel via logcat.
- **Zero VFX exist** (no ParticleSystem anywhere), no music, seven synthesized SFX, no haptics.
- **Home HUD bleed**: gameplay counters peek between the plaque and the diorama at the Pixel's
  0.448 aspect; Home was only ever validated at 0.558.

## Sprint shape: six parallel lanes, one device pass

Each lane is one Codex chat (TASK 27-A … 27-F) working in its own worktree from main. Lanes are
file-disjoint by design; the two shared foundations (font/TypeScale, ChromeChip factory,
BoardFx/Tween) are merged on day 1–2 so the others inherit them. Every lane ships with an
evidence gate rendered at the Pixel's aspect (917×2048 rig = 0.448) and goes through the
validation slot before merge. Merge order: A → B → C → D → E → F (F last because it touches
Wardrobe + Home holder after C).

| Lane | Goal | Items (rank) | Days |
|---|---|---|---|
| A. Board frame, light, content | board fills ≥65% of frame height on L001–L015; warm desk to the edge; shape-band levels fair | 3, 14-API, 16, 31, 26 (+36) | 6 |
| B. Board readability + juice | right-colour right-shape cats; shape badges; switch swing; wrong-station recoil; cat scale law; smoke | 2, 7, 12, 17, 15, 18, 27 (+19) | 6 |
| C. Home first frame | display type; centred carved sign; lamp-lit backdrop; no HUD bleed; refit diorama; splash | 1, 4, 5, 13, 14-callsite, 33, 32 (+29) | 6 |
| D. Level flow | ChromeChip factory; intro ticket every level; win beat; transition veil; fail banner | 10, 9, 8, 20, 28 (+35) | 6 |
| E. Sound, haptics, settings | 4-stem loop + MusicDirector; mews + musical SFX; haptics; settings sheet | 11, 21, 22, 23 (+30) | 6 |
| F. Wardrobe + rig | loud rig fallback proven on Pixel; rig hero on plinth; store shape | 6, 24, 25 (+34) | 6 |
| G. Device pass | rebuild, install, walk, before/after sheet vs playtest-2026-09-05 | — | 1 |

Full per-lane item text, evidence gates and file lists: `.catshots/playtest-2026-09-05/
polish-plan.synth.json` (`sprint[]`), mirrored into the TASK 27 builder brief.

## Calendar

- **Sep 6 (Sat)**: lanes launched; day-1 foundations (font, grid one-liner, ChromeChip, BoardFx,
  rig-fallback probe) merged same day.
- **Sep 7–12**: lanes build; slot validates each at an exact SHA; orchestrator merges.
- **Sep 13**: device pass G on the Pixel; before/after contact sheet; regressions become Sep 14.
- **Sep 14–15**: fix day + second device pass; image references v2 curated in.
- **Sep 16**: freeze main; human GUI-builds the signed AAB (licensed-art guard active).
- **Sep 17–18**: Play production submission (Android primary per ruling). Apple after.
- **Sep 22–25**: public-live target; Sep 26–27 capture final media; **Sep 28 submit**; 29–30 buffer.

## Decisions (made 2026-09-06 by the orchestrator from the round-2 references; human may veto)

Font Fredoka + Nunito (OFL). Music and SFX: commercial-free-use libraries or Gemini generation,
each with a provenance row; synth set stays as fallback. Ladder content edits in lane A allowed;
budget levels get the L014 lever-start fix, no rules change. Unity splash off. Daily teased with
a locked pin and seven pips. Accessory tab hidden while empty. MSAA 4x, verified on device.
Restore purchases as a text link. Haptics needs no data-safety change. Full list and the
per-image take/don't-copy table: docs/reference/gen-ref-NOTES.md, "Round 2 references".

## Explicitly rejected this sprint (see synth.json `rejected[]`)

Visible level timers; rig retextures or new licensed cats; new cosmetic SKUs; paid music
sign-ups; level-dossier harness and Android-emulator loops (no judge-visible pixel); merging
ladder levels; toy-engine consist swap; texture overlays; prop wobble; rules-adjacent flip-budget
changes.

## Standing rules that do not change

`docs/LOOK.md` palette and materials; no coin/gem UI; no text station signs; no Score/Moves
HUD; rewarded video only; cosmetic-only monetization; 60-level campaign; Daily after 7 wins;
never `git commit -a`; Unity `-runTests` never with `-quit`; graphics PlayMode only; builds
refuse to run without the licensed art (`CatMetroLicensedArtGuard`).
