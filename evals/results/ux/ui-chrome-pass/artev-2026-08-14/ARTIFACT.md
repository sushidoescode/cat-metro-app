# ART-EVIDENCE — six taste-gate frames + the RT-vs-back-buffer comparison

Captured 2026-08-14 with Unity 6000.3.16f1 at tree `6fde975` (art/evidence-pass, after the
chrome-canvas unification commits AND the shader warm-up fix landed — reproducing at
`7575496`, which lacks the warm-up, yields the cyan placeholder bug described below;
attribution corrected per the #86 round-1 review F-2) via the committed `ArtEvidenceCaptureTests`
capture rig (`unity/Assets/Tests/PlayMode/Bootstrap/ArtEvidenceCaptureTests.cs`,
`CaptureEvidence_SixChromeFrames_WhenRequested`), armed with `CM_ARTEV_CAPTURE_DIR`.
Command: `Unity -batchmode -projectPath unity -executeMethod
CatMetro.Editor.CatMetroDioramaAuthoring.ConfigurePortraitEvidenceGameView -runTests
-testPlatform PlayMode -testFilter ArtEvidenceCaptureTests` (graphics ON — no `-nographics`;
`ConfigurePortraitEvidenceGameView` is Lane 1A's own existing, unedited public method, called
from this new file rather than duplicated). All seven PNGs read exactly **900×2000** on disk
(verified from the PNG IHDR chunk).

## Method

Every frame is a genuine composited-screen back-buffer read
(`ScreenCapture.CaptureScreenshotAsTexture()` after `WaitForEndOfFrame`), not a camera
`Render()`-into-`RenderTexture` — the whole point of this contract (PR #68 round-2 review
finding E-1: an RT capture only ever paints what one camera draws, and every chrome canvas
this session unified to `ScreenSpaceOverlay` composites straight onto the display, outside
any camera's render target). One continuous dev-flow session (`GameRoot.BootToHome`, a real
queue-overflow fixture) produces Home → LevelIntro → Playing/wave → first warning → second
warning via real taps (`TapInput.HandleTapAtScreen` at the views' own painted-rect centers)
and a real, un-nudged queue overflow; a second, fresh, deliberately winnable session produces
Won/Results.

**Method finding, disclosed rather than hidden:** the very first captures in a fresh batch
process (Home/LevelIntro) initially came back as a flat, fully-saturated cyan fill behind
correctly-positioned TMP text — a placeholder color Unity's async shader compiler paints for
the first use of a given shader/material pair in a process, before that variant finishes
compiling, self-correcting a few frames later. Isolated by comparison: the *same* technique
at the Playing/wave moment (several real frames later in the *same* session) already read
correctly on the first capture attempt, and a *second* session (Won/Results) needed no
warm-up at all, because the shared UI material's shader was already compiled from the first
session's own rendering. The fix (committed in `ArtEvidenceCaptureTests.cs`) is 30 empty
frames (`WarmupFrames`) pumped once before the run's first capture only. This is a capture-
harness timing artifact, not a canvas/render-mode/product defect — it reproduced identically
whether or not the 900×2000 resize was applied, and had nothing to do with sortingOrder.

## The RT-vs-back-buffer pair (E-1, confirmed empirically)

`03-playing-wave.png` (back buffer) and `03-playing-wave-camera-rt-comparison.png`
(`Cam.Render()` into a matched `RenderTexture`, same live moment, same session, captured
seconds apart) are the direct comparison item 3 asked for. Inspected side by side: the back-
buffer frame shows the wave-preview strip — two Warm Paper trays, red cat faces, "x6" counts —
in the top status band, sitting above the diorama board. The RT frame shows the *identical*
board (track, switch, stations, scenery, desk) with the **top status band's chrome content
absent** — the band background itself is identical warm-paper in both frames (the tray, if
separable, is not demonstrably absent; corrected per review F-8); what differs: no red cat
chips, no "x6" count glyphs, no divider. The wave strip (and every other Overlay chrome surface, by the same
mechanism) is invisible to the RT route and visible on the back-buffer route, exactly as E-1
predicted.

## The six frames — inspected, not just captured

| # | File | What is visible (as eyeballed) |
|---|---|---|
| 1 | `01-home.png` | Warm Paper background; Ink Navy tracked "CAT METRO" title with a small cat-mark glyph and a teal/orange rail accent; three grey rounded-rect district-silhouette cards in a staggered layout (undecorated placeholders — see Limitation below); the orange/navy L001 pin low in frame. Matches the Home composition described in prior evidence (`polish-pass-2026-08-08/ARTIFACT.md`), modulo the missing decorative texture. |
| 2 | `02-levelintro.png` | Ink Navy staging scrim over the board (board silhouette — switch, red/blue stations — visible through it at reduced opacity); rounded Warm Paper route card; teal route rail with an orange marker; "ArtEvidence Overflow Fixture" name and "Deliver 99 cats" goal (this fixture's own copy, injected — not a wrong string); full-width orange "Play" CTA. |
| 3 | `03-playing-wave.png` | Screens cleared; the diorama board (source depot, switch with lever, track fork, red/blue stations, scenery); the wave-preview strip in the top band showing two pending "red x6" entries (this fixture's authored wave). |
| 4a | `04a-fail-1st-no-hint.png` | First FailureReview entry: "Platform overflowed at SRC" cause banner over a visibly overflowed queue of red cats backed up the track; full-width orange "Try again" CTA; **no hint chip** (the rule requires a 2nd entry — correctly absent). |
| 4b | `04b-fail-2nd-with-hint.png` | Second FailureReview entry, same attempt-run (via `Retry()`): identical banner + CTA, **plus** the teal "Tap the flashing switch" hint chip in its own band — no overlap among banner/hint/CTA. |
| 5 | `05-won-results.png` | ResultsPanel: "All cats home!" win banner; the rounded Warm Paper completion card with the teal route motif, navy cat silhouette, orange/teal confetti pieces either side; full-width orange "Next" CTA. No score/stars/tickets fabricated (A-UI-3 / CM-UX-04 law), matching the panel's frozen contract. |

PNGs and SHA-256:

```
e131975666bda65e47fd0bd60714f8c059f028f58b64620da145c1794255edce  01-home.png
475dc092e999d5b8f1aa1b194107f9ad60ed85d1ca8925df54738a51db5348d0  02-levelintro.png
707aaa7ef1f31adf038319d82a630eb71155c7d81623f9e95e08ce0376842058  03-playing-wave.png
ffb85cf828a59f65ece3f52668d68c3ecaffddb59130acd3f54aa324959f0ca2  03-playing-wave-camera-rt-comparison.png
2babef709b5a379cd7906bb8e51449afa303ccddf2e8e330f6f4e5aa55c751f5  04a-fail-1st-no-hint.png
08d379274b05583ab705e34217a385b0c77a85626d9039c31a844abe93e49d10  04b-fail-2nd-with-hint.png
45739b341466bb5d034ec3c628c2339e2e778927c93da33c7a9cbf2703bc86c8  05-won-results.png
```

## Limitation, stated honestly (not papered over)

The Polyfork asset models are absent from this local checkout by design (custody: PR #65's
containment — public-repo custody forbids committing the licensed FBX derivatives; they are
gitignored local inputs the diorama-authoring script consumes when present, per
`unity/Assets/Art/Polyfork/PROVENANCE.md` and ADR-0011). This session never ran Lane 1A's
`CatMetroDioramaAuthoring.Build()` and did not need to — these six frames are ART-EVIDENCE's
own deliverable, not Lane 1A's. Two visible consequences, both already true of the committed
`Game.unity` scene independent of anything this session changed:

1. Home's three "district" cards render as flat grey rounded rectangles with no decorative
   art (no Polyfork prop silhouettes) — the layout/count/shape/rounding is real chrome; the
   art dressing is not present to evidence.
2. The diorama board (visible in frames 3/4a/4b) shows its code-authored geometry (desk,
   tracks, switch, stations, trees, a pencil and mug) without the Polyfork prop set (depot
   shed, toy engine, lamp, bench, sign, etc.) Lane 1A's build step would add.

**What these frames DO evidence honestly:** the UI/UGUI chrome this contract's canvas
unification and capture-path work is actually about — palette, typography, layout, CTA/hint/
banner/results composition, and the now-real cross-canvas paint order (the wave strip
genuinely painting above/inside the correct band, the halt-adjacent surfaces never
inverted). Taste judgment on the board/prop art itself is Lane 1A's evidence to supply.


## Disclosure (per the #86 round-1 review F-1): two main-landed RT evidence rigs are now blind

The chrome-canvas unification (this branch) means `ResultsPanelTests.CaptureEvidence_ResultsFrame_WhenRequested`
and `ChromeStateTests`' `CaptureEvidence_*` rigs — both env-armed, both main-landed, both
capturing via `Cam.Render()` into a RenderTexture — no longer see their subjects (ResultsPanel's
and ScreenChromeController's canvases are now Overlay, which never enters a camera's render
target). Worse, the #39 M-2 anti-vacuity probe in the ResultsPanel rig no longer discriminates:
the reviewer pixel-sampled this directory's own frames at the probe's two coordinates and
showed it PASSES on a chrome-less frame. Until the named follow-up lands, any armed run of
those two rigs produces NON-PROBATIVE frames and their outputs must not be treated as evidence.
NAMED FOLLOW-UP CONTRACT: retrofit both rigs to back-buffer capture (the
`ArtEvidenceCaptureTests` pattern, including the cold-process shader warm-up) and restore the
M-2 probe's discriminating power with a chrome-only-differentiated coordinate pair. Those two
test files were NOT edited in this PR — they are outside this contract's scope (hard rule 4).
