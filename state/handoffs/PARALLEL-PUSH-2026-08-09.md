# PARALLEL PUSH — 2026-08-09 (the "close the distance to the desk" wave)

Ground truth for the five parallel lanes launched 2026-08-09. On any boundary question,
THIS FILE supersedes the chat prompts that spawned the lanes. Read `state/PROJECT_STATE.md`
first (mandatory), then this, then `SESSION-HANDOFF-2026-08-08.md` §Operating notes (the
sandbox/trap list — all of it still binds).

## The rulings this wave executes (human, in-session 2026-08-09, ~02:30–03:00)

1. **Keep pushing** — after the first device play and the concept renders, verbatim: "I
   challenge you to optimize, enhance, polish, and evolve this to the next level as much as
   possible. The next time I load my APK on the Pixel phone, it should have cat assets, and
   the entire art details ready to go."
2. **Monetization direction ACCEPTED** (the session's recommended option, accepted verbatim
   "I accept the ruling on the monetization direction"): deep cosmetic microtransactions +
   DLC district packs + expanded RevenueCat rewarded-ad placements — **fair-core intact**
   (no energy, no loot boxes, no subscriptions, no premium currency). Lane 4 drafts the
   spec amendment for the human's signature.
3. **Parallel lanes** requested explicitly, with a no-merge-conflict mandate — hence the
   ownership boundaries below (the SESSION-HANDOFF-ux.md precedent, hardened).
4. Art reference: the two 2026-08-09 concept renders (Gemini + ChatGPT, in ~/Downloads; the
   Gemini render matches product_spec.md §art-direction nearly clause-for-clause). Palette
   hexes in product_spec.md are authoritative; color never appears without its symbol.

All four channel notes: in-conversation directives, agent-relayed here (H-1-class caveat).

## Lanes, ownership, and the conflict rules

**Global rules (every lane):**
- Own git worktree; contract frozen as the FIRST commit on the lane branch; forge TDD; risk
  gate; fresh-context review legs per verdict; mutation proofs for load-bearing asserts;
  rendered-frame evidence for anything visual (standing rule).
- **HC-25**: no lane arms or completes any merge without the human's fresh in-session word
  in ITS chat. Every merge outcome is census material (next append records it).
- **`state/PROJECT_STATE.md`**: append/update EXACTLY ONE row (your lane's) at merge;
  whoever lands second takes the update-branch merge. Never touch another lane's row.
- **`unity/Assets/Scenes/Game.unity`, `unity/ProjectSettings/**`, URP/lighting assets:
  Lane 1A EXCLUSIVE.** No other lane may touch them for any reason.
- The build shim `unity/Assets/Editor/CatMetroCliBuild.cs` is untracked on every ref —
  Lane 1A commits it (finally). Until that lands, copy it from the main checkout to build.
- New dependencies need an ADR referenced in the PR (AGENTS.md rule 2). Monetization CODE
  is forbidden until the human flips `state/mode` to production (tripwire).

| Lane | Branch | Owns (exclusive) | Must not touch |
|---|---|---|---|
| **1A ART-DIORAMA** | `art/diorama-pass` | `unity/Assets/Art/**` (new), `unity/Assets/Materials/**`, `unity/Assets/Prefabs/**` (new), `unity/Assets/Scripts/Presentation/Board/**`, `Game.unity`, `ProjectSettings/**`, URP assets, `unity/Assets/Editor/CatMetroCliBuild.cs` | `Scripts/Domain|Content|Bootstrap/**`, `Presentation/Hud|Screens|Input/**`, `content/**`, tests it doesn't add |
| **1B UI-CHROME** | `art/ui-chrome-pass` | `unity/Assets/Scripts/Presentation/Hud/**`, `Presentation/Screens/**`, `unity/Assets/Resources/Strings/ui.csv` (append-only), TMP font/style assets (new), tiny `Presentation/Audio/` stinger manager (new) + its clips | Scene, ProjectSettings, `Presentation/Board/**`, Domain/Content/Bootstrap, `Presentation/Input/**` |
| **2 SOLVER** | `task/solver-tiebreak-fix` | `unity/Assets/Scripts/Domain/Solver/**`, solver/domain test files, the corpus pins it must re-record | `Presentation/**`, `content/**` except updating recorded expectations, ValidationStages thresholds (human-ratified) |
| **3 BAND-L011** | `task/CM-C12-queue-reading-band` | `content/levels/L011..L017.json` + staged copies via the stager, new `Pure/Corpus/` test file(s), `tests/corpus/queue-reading-band.test.sh` | Everything else; MERGES AFTER Lane 2 (declared dependency) |
| **4 MONETIZATION-DOCS** | `docs/monetization-amendment` | `docs/plan/specs/monetization_spec.md` (amendment), new ADR(s), `docs/prd/` leaderboard-contract draft, art-reference note | All code, all state except its one row |

**Merge order**: 1A, 1B, 4 — independent, any order. 2 before 3 (Lane 3 authors boards
that need the fixed tie-break to pass brittleness; it may design earlier, validate after
rebasing on 2). PROJECT_STATE update-branch rule handles the row collisions.

## Lane notes (contract seeds — each lane freezes its own from these)

**1A ART-DIORAMA** — the flagship. Polyfork founders MCP (verify with ToolSearch
"polyfork" at session start; tools: search_assets/get_asset/get_variant; GLB → Unity).
Deliver the Gemini-render look: wooden desk surface, cream/ink-navy train-set track +
bevel, depot shed, station platforms with color+symbol plates, chunky teal/orange switch
lever (the tap affordance), low-poly trees/props, cat commuters (round, chibi, color +
symbol tag — the colorblind rule is absolute), warm lighting + contact shadows, palette
hexes from product_spec.md. Fix the device finding while in BoardView: replace
`GameObject.CreatePrimitive` with collider-free construction (kills the on-device console
spam — stack recorded in PROJECT_STATE Known debt). Definition of done: EM/PM green,
editor frames AND a dev APK built + device screencap evidence; TG-1..8 remain the human's
judgment on the result.

**1B UI-CHROME** — restyle every screen to the palette + typography: Home (title, district
silhouettes), LevelIntro, fail/hint chrome, ResultsPanel, wave-preview strip (fix the
recorded illegibility observation), plus win/fail/tap audio stingers (assets from the
recorded gen resources or CC0; new AudioManager stays in Presentation/Audio). ui.csv is
append-only. Prose landmines: failure.test.sh scans source for literal UI strings; the
vocabulary guard substring-matches (a comment containing "closes" trips on "lose").

**2 SOLVER** — fix `LevelSolver.CompareWins`' earliest-tick tie-break (prefer mid-window
safe ticks so non-tick-0 decisions keep jitter margin). KNOWN CONSEQUENCE, by design: the
L006 characteristic pin ((7,0,13)/35, both enforcement points) goes RED when retention
improves — that is the pin working; surface it, get the human's re-pin ruling in-chat,
record it in CM-C11.md §RULING's style. Re-measure the F4-trigger numbers (L002/L003/L005
and L006) and update the recorded rows with the human's ack. BFS-exactness and solve-time
budgets must hold (stress boards; stop condition 7's wall-clock lesson is in CM-C11.md).

**3 BAND-L011 (CM-C12)** — L011–L017 per product_spec.md §21 queue-reading band:
queue-as-buffer, chained queues, burst waves (4+), shared mid-node, symmetric-board
misdirection, min-spacing waves; difficulty 0.28–0.36, ±0.05 computed-vs-authored, REAL
multi-decision boards (the whole point post-tie-break-fix — do NOT ship seven more one-tap
boards; the #62 review's duplicate-boards finding is the anti-pattern, novelty distances
go in the PR). Runtime wiring of bands beyond L001–L005 into `GameRoot.LevelBand` is IN
scope for this contract (small, Bootstrap seam — coordinate: it is the one sanctioned
exception to Lane 3's no-Bootstrap rule, one file, declared here).

**4 MONETIZATION-DOCS** — amendment implementing ruling 2 (cosmetics catalog depth: cat
skins/liveries/seasonal themes; DLC district packs at $2.99 pattern; expanded rewarded
placements; RC Experiments plan), an ADR for Play Games Services leaderboards (new
dependency — human signs), and the leaderboard contract draft (District Cup + Daily Line
exist in spec; global boards via PGS, no own backend). Docs only; prices/SKUs stay
human-signed; note the mode-flip tripwire before any monetization code anywhere.

## Standing obligations this wave inherits

- The census fourth append (in the same PR as this file) records #61/#62's outcomes; each
  lane's merge is the census's next entry — ask, record, append.
- Device findings recorded in Known debt: collider-strip spam (Lane 1A fixes),
  persistentDataPath-is-EXTERNAL (threat-model premise falsified on-device; runbook
  command wrong — unassigned, do not silently fix in a lane that doesn't own Bootstrap).
- The venture-critic's clock: Play closed-test start by ~Aug 15 (human act — testers).
- Mode flip to production (human-authored commit) before monetization code lands anywhere.
