# Shipped boot → Home — device evidence (2026-08-14, CM-BOOT-HOME)

Agent claim (not attested): the promoted Home composes on the **shipped boot path** and the
sim is genuinely **held at tick 0** behind it until the first Play tap. Captured on the
Android emulator against a real build.

## Provenance

- Build: dev APK from `task/CM-BOOT-HOME` @ `7e95b53` (CLI_BUILD_RESULT Succeeded, 0 errors
  — the fence-lift compiles). Stacked on the BEAUTIFUL-MENU restyle, so the Home shown here
  is the warm-tabletop one.
- Device: AVD `catmetro-test` (pixel_7, arm64-v8a, headless SwiftShader), emulator-5554,
  portrait 1080x2400 (portrait lock from ORIENT-LOCK is in this build too).
- **`<persistentDataPath>/devcap/boot.json` was DELETED before this run** — so Home here is
  NOT the dev boot.json seam. It composes because `InitializeFromSeam` now calls
  `ComposeScreenFlow()` unconditionally on the real boot path. That deletion is the control
  that makes this a shipped-path proof rather than a dev-flow proof.

## Frames (sha256)

| # | frame | what it proves |
|---|-------|----------------|
| 01 | `09f8050400648aa47d6bf1daca3fac471935f0f1cacd121a2c142e374b784465` 01-shipped-boot-composes-home.png | Cold start with no boot.json lands on the restyled Home (warm paper, ink-navy "Cat Metro", cream base board, parked silhouettes, ticket-orange CTA ring around the L001 pin). The board is not visible — Home's opaque ground covers it. |
| 02 | `bd6e28f9cb0cae06754a896a486b21f6294ed30580ea9aff12d4eff6a2050453` 02-pin-tap-intro-over-FRESH-board.png | **The tick-0 hold.** Pin tap → Home hides, the Intro sheet shows the real level name ("First Switch") and goal ("Deliver 2 cats"), and the board revealed behind it is **FRESH** — the x2 counter is full, no cat has spawned or moved, no "Signal fault" halt banner — ~80s after boot. Pre-hold, L001 halts within seconds of Wire; this frame is the direct evidence the sim did not advance behind Home. |
| 03 | `9e4ee875ca0263d8e45a900277c171c86112a019b712b18f0313f151724a5f78` 03-play-tap-sim-advances.png | **The release.** Play tap → screens dismissed (ScreensVisible false), sim advances: cats spawn, run the line, and the run halts on the misrouted red cat at station B ("Signal fault — the line stopped") — the correct NEW-Q4 domain guard on default switch state, identical to the pre-promotion gameplay pass. |

Together 02+03 are the paired proof of criterion 2: **held at 0, then advancing** — the one
genuinely new behavior in this contract.

## What is dev-build noise, not the feature

The Development Console overlay and its six "CapsuleCollider doesn't exist" lines are the
known art-chain greybox debt; the console only auto-shows in a Development Build on error. A
release build shows none of it. The halt in frame 03 is correct gameplay, not a defect.

## Not proven here

The Unity EditMode/PlayMode suites (CI is authoritative — including the new
`ShippedBootHomeTests`, the declared pin migration, and the DailyWireTests re-seam). A true
release-config (non-DEVELOPMENT_BUILD) build was not produced; this dev build exercises the
same `InitializeFromSeam` compose path, and the shipped-config compile of the fence-lift is
argued structurally in the PR plus verified by review. No claim is made about taste — the
restyle's look is the human's gate, recorded separately with the BEAUTIFUL-MENU frames.

## Blinded-rigs disclosure

Captured and described by the same session that authored the change. The tick-0 claim rests
on a falsifiable visual: had the sim advanced, frame 02 would show a spawned/halted board,
which every pre-promotion capture in this repo's device evidence does.
