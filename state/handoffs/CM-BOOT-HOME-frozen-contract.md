# CM-BOOT-HOME — tranche-2 boot-to-Home frozen contract

This is the human-authored tranche-2 contract that CM-UX-07 stop-condition-1 and
CM-LOADNEXT criterion 6 explicitly deferred this change to.

## Authority

The human's 2026-08-14 directive: "promote the menu to the shipped launch screen,"
**explicitly re-confirmed in-session via a structured decision: "Authorize — build it,
human-merge"** — the human's ratification of (a) superseding the interim Q-5 criterion and
(b) the declared regression-pin inversions below (an agent must not do either on an inferred
authorization; this contract records the explicit one). The PRODUCT question is settled and
aligns with ADR-0007's ratified topology "Boot → Home ⇄ Game" — **no new ADR is required.**

**Q-5, superseded here (verbatim, CM-LOADNEXT criterion 6):** "shipped boot stays L001. A
real GameRoot.Launch() (no BootToHome, no LoadNext call) starts at CurrentLevelId == 'L001'
… a regression pin against a future mistake of routing boot through the band."
**New law:** Home IS the shipped launch screen; boot still lands the LEVEL at L001 with Home
composed OVER it, and the sim is HELD at tick 0 until the first Play tap.

## Acceptance criteria

1. **Shipped boot composes Home.** Lift the `#if DEVELOPMENT_BUILD || UNITY_EDITOR` fence
   around the composer (rename `ComposeDevScreenFlow`→`ComposeScreenFlow`, the Home/Intro/
   Stack props, the `ScreensVisible` read — dropping the `#else return false`). Call it from
   `InitializeFromSeam` (both the dev-level early-return and the shipped branch), NOT from
   `Wire` (Wire is the LaunchWith fixture seam the gameplay tests use — composing there would
   mount Home under all of them).
2. **Tick-0 hold (the one genuinely new behavior).** Append `&& !ScreensVisible` to the
   Update advance guard, mirroring the board-input gate at GameRoot.cs:182, so L001 does not
   auto-run/fail behind Home before the first Play tap.
3. **Decouple the dev seam.** Remove `BootToHome || _bootToHomeFileOverride` as the compose
   gate; keep BootToHome / DevBootOverride(boot.json) / DevLevelOverride / DevFrameCapture
   dev-only and fenced. Repurpose the dev flag as an inverted "skip Home → boot straight to
   L001 gameplay" hatch so the Launch()-gameplay fixtures flip one SetUp flag.
4. **Priority-debt fix (else it ships as a live tap bug).** The home pin registers at
   ParentPriority(0) while ResultsPanel is at ModalPriority(10), but ScreensCanvas paints at
   sortingOrder 120 above ResultsPanel's 110 — a visually-on-top pin can be outranked on tap.
   Dev-only today; SHIPS live if Home ships. Fix here (raise the Home pin's registration
   priority above the modal tier, or the minimal correct ordering).
5. **Commerce-free through the shipped path.** The promoted Home builds NO shop/daily/
   paywall/night/harbor node; NEW-Q30 stays untouched (no Night Harbor tile); no billing/
   iap/ads glob touched → no production mode-flip, no monetization security review.

## Declared pin inversions (the never-weaken migration — EXPLICIT, human-authorized)

Human-authored negative pins that asserted the OLD interim Q-5 design, inverted here as a
contract-level test migration under the authorized design change (each becomes its positive
counterpart for the new topology; none merely deleted/loosened; exact line ranges pinned at
implementation and enumerated in the PR's never-weaken table):
- `DevScreenFlowTests.cs` — "shipped/fallback boot has no Home / ScreensVisible false".
- `DevBootOverrideTests.cs` — boot.json-gated compose (the seam is now unconditional on boot).
- `GameRootWiringTests.cs` — "Home Is.Null in shipped boot".
The Launch()-gameplay pins (FailureTests, ChromeStateTests, DeviceConfigTests,
GameRootWiringTests halt) stay GREEN via the dev skip-Home SetUp hatch — NOT inverted.

## New tests (RED-first)

- Shipped-boot integration via the REAL Launch() seam (hatch OFF): ScreensVisible==true,
  Home.IsVisible==true, breadcrumb ["home"], Session level id=="L001".
- **Tick-0-hold pin (primary correctness proof):** Home up → sim Tick stays 0 across pumped
  frames; after the Play tap → Tick advances. Proves no sim leakage behind Home.
- Boot→Home→pin→Intro→Play→L001 round-trip on the real seam.
- Tripwire-clean + whitelist-clean + motion-off, all THROUGH the shipped boot path (reuse
  HomeScreenTests' walks).
- Q-5-restated pin: shipped boot to Home still has CurrentLevelId=="L001".

## Scope / process

One TDD contract; independent review; **HUMAN-MERGE** (the human ratifies the Q-5
supersession at merge, not agent auto-merge). Diff confined to GameRoot.cs (~4 edits) +
ChromeRegions/HomeScreenView priority fix + the declared test migration + new tests. Stacked
on #90 (the restyle) so the shipped Home is the beautiful one; rebased onto post-#90 main
before merge. NEW-Q30 / billing strictly out of scope.

## Assumptions (unlisted assumptions are defects)

- The composer references only already-shipped Presentation.Screens types + Canvas/Camera
  (no Newtonsoft, no DevCapture) — verified before lifting the fence.
- The Launch()-gameplay fixtures can be migrated to the skip-Home hatch via a one-line SetUp
  flag without editing their assertion bodies (never-weaken on THOSE stays intact).
- Visual verification (the standing rule): a shipped-config APK renders the beautiful Home at
  boot, holds at tick 0, and reveals L001 on the Play tap — captured on the emulator.
