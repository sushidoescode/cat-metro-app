# CM-C2b / CM-C3 re-decompose PREP NOTE (P10, session 2026-08-04)

**Scaffold check (2026-08-04):** `unity/Packages/manifest.json` absent; zero `.asmdef` files
anywhere under `unity/`. The Q-G human scaffold does NOT exist, so per the phase-6-10 handoff
this session writes the prep note and STOPS — no Unity file is authored here. The
forge-decompose refresh below runs the day the scaffold lands.

## What the human scaffold must contain (Q-G pins, verbatim from the handoff)

- Unity **6000.3.16f1** · **IL2CPP** · **ARM64** · **URP** · **Input System**
- **minSdk 25 / targetSdk 36** · application id **com.catmetro.game**
- Created **IN PLACE** without deleting `unity/Assets/Scripts/**` or `unity/Assets/Tests/**` —
  the pure-dotnet trees are the product source; the scaffold adds `Packages/`,
  `ProjectSettings/`, asmdefs and `.meta` files AROUND them.

## Parity obligations the scaffold triggers (ADR-0005)

- asmdef ↔ csproj reference/link-glob/test-split parity for **Domain · Content · Services ·
  Application** (Application is NEW since tranche 2 — CM-C7 created it).
- Newtonsoft via `com.unity.nuget.newtonsoft-json` 3.2.x (= the 13.0.2 NuGet pin, ADR-0008) —
  now required by **Content AND Services** (CM-C7 A-C7-6 put the SaveState boundary type in
  Services) and Application.
- An EditMode test asmdef covering `unity/Assets/Tests/EditMode/Pure/**` (NUnit-3 sources are
  dual-host by design; `dotnet test` stays the fast leg).
- `scripts/check.sh`'s runtime-tree solver-reference guard arms itself automatically for
  `unity/Assets/Scripts/Bootstrap` the day it exists.

## What landed since the tranche-2 CM-C2b/C3 contracts were cut (the refresh must absorb ALL of it)

| Landed | Consequence for the recut |
|---|---|
| CM-C5 validator + `scripts/validate-content.sh` (merged #10) | CM-C2b's in-engine content path must NOT re-validate — greybox loads pre-validated corpus bytes through `IContentSource`; stamping stays Q-O/human |
| CM-C6 daily pipeline (PR #14, review done, awaiting human ratify+merge) | `Content/Daily/**` + `DailyTools` + `config/daily_pipeline.json` exist engine-free. CM-C2b's DEVICE limbs stay open and must be recut explicitly: 250 ms bounded on-device salt loop (CM-R46.3), ≤200 ms boot validation + 30-board dated backup pool (CM-R46.4), `daily_overrides.json` / `daily_backup_pool.json` / `catalog.json` / `content.sha256` shipping pipeline, and the REAL `IBoardFactory` (Q-S — the shipped stub is fixed-board, loudly labelled) |
| L002–L005 (PR #15, review done, awaiting human merge) | Corpus 5/30. The `unity/Assets/StreamingAssets/content/**` copy step is CM-C2b's (ADR-0008); nothing ships in-engine yet |
| CM-C7 save v1 (PR #16, in review round) | Bootstrap must implement `IStorageRoot` (persistent-path) + `IContentSource` (StreamingAssets); the Q-Y `config/runtime_bounds.json` ↔ `StreamingAssets/config/` byte-identity `ci` clause is UNSATISFIABLE until CM-C2b owns that copy step; RK-17 (auto-backup posture) must land WITH the scaffold-era manifest work |
| CM-C8 analytics queue (queued next) | `analytics_queue.dat` must be excluded from auto-backup UNCONDITIONALLY (ADR-0006 §5; M-21/Q-U) — the manifest/backup-rules artifact is scaffold-era work tied to the same RK-17 decision |

## Recut checklist for /forge-decompose (backlog is agent-writable; run when Q-G lands)

1. **CM-C2b (bootstrap + content-to-device):** IStorageRoot + IContentSource implementations ·
   StreamingAssets copy steps ×2 (content corpus + runtime_bounds, closing Q-Y) · L001-in-engine
   greybox load · replay-hash parity in an EditMode leg · the daily DEVICE limbs above · RK-17
   manifest/backup-rules (human decision rides it).
2. **CM-C3 (greybox board):** rendering/input per ux-flows stories · the TG-1..8 taste-gate
   hooks · no monetization surface (mode tripwire unchanged).
3. Re-price both through `scripts/forge-risk.sh`; `.github/**` CI additions (unity-editmode,
   android-smoke) stay human PRs (Q-V).

**STOP line honoured:** no `unity/Packages/`, no `ProjectSettings/`, no `.meta`, no asmdef was
authored by this session.
