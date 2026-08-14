# ORIENT-LOCK — frozen contract

Frozen 2026-08-14 by the ORIENT-LOCK implementer session at main=`d10509d` (SHA verified
post-fetch, `git rev-parse origin/main`).

## Directive (human, in-session, 2026-08-14 — H-1-class relay)

The human's own words, verbatim as given in-session: **"when I try to switch to a wide screen
view on the phone it doesn't change orientation, it needs to stay straight."** Read plainly:
rotating the phone to landscape must never change the app's on-screen orientation — it must
stay portrait, always. This is an in-conversation directive, agent-relayed here — the same
evidentiary class as every other H-1 relay in this repo's history (confirmable only by the
human's own commit or GitHub comment, never by this transcription).

**Verified current defect (main, pre-fix):** `unity/ProjectSettings/ProjectSettings.asset` had
`defaultScreenOrientation: 4` (Unity's `UIOrientation.AutoRotation`) with all four
`allowedAutorotateTo*` flags set to `1` — the app rotates on device today, contradicting the
directive.

## CROSS-LANE NOTE (read before touching `unity/ProjectSettings/**` again)

`unity/ProjectSettings/**` is Lane 1A's **exclusive** surface per the parallel-push charter
(`state/handoffs/PARALLEL-PUSH-2026-08-09.md:54`: "`Game.unity`, `ProjectSettings/**`, URP
assets" listed under Lane 1A's owned paths; line 45's base rule: no other lane may touch them
for any reason while 1A is unmerged). This contract is a **superseding exception**, authorized
by the human's direct 2026-08-14 order above — the same class of override the parallel-push doc
itself anticipates for Lane 5's Android player-settings edge (NEW-B), applied here instead to a
device-behavior bug report that cannot wait for the art chain (#65) to land.

**Verified fact, not an assumption (checked by non-destructive `git merge-tree --write-tree`
probe of this branch's tip against `origin/art/diorama-pass`, no working tree or branch ref
touched):** the art branch (`art/diorama-pass`, PR #65) carries its **own**, human-authored
orientation-lock commits, done independently on 2026-08-09 in the Unity Editor —
`1dedcca` "fix: round gameplay exteriors and lock portrait" (`defaultScreenOrientation: 4→1`,
all four autorotate flags + `useOSAutorotation` → `0`) and `056b75d` "fix: pin Unity portrait
enum correctly" (`defaultScreenOrientation: 1→0`). Net effect on that branch:
`defaultScreenOrientation: 0`, **all four** `allowedAutorotateTo*` flags `0`, and
`useOSAutorotation: 0` — a strictly more complete lock than this contract's criteria require
(it also blocks portrait-upside-down and disables OS-driven autorotation entirely).

This means at #65's landing there **is** a real `unity/ProjectSettings/ProjectSettings.asset`
merge conflict — not "no-op because both sides picked the same value" for every line. The probe
shows why precisely:

- `defaultScreenOrientation`: **both branches change `4`→`0`** — identical resulting line, git
  auto-merges this cleanly, confirmed in the probe (no conflict marker on this line).
- The 4-line `allowedAutorotateTo{PortraitUpsideDown,LandscapeRight,LandscapeLeft}` +
  `useOSAutorotation` block: **conflicts**, because it is one contiguous diff hunk on both
  sides and the two hunks disagree on 2 of 4 lines (`allowedAutorotateToPortraitUpsideDown` and
  `useOSAutorotation` stay `1` on this branch, since this contract's criteria only require the
  two *landscape* flags; the art branch already set them to `0`). Git's line-based 3-way merge
  cannot partially resolve a hunk, so the whole 4-line block conflicts even though the two
  *landscape* lines it contains already agree.

Resolution is mechanical and low-risk for whoever lands second (expected: #65, since it is the
open PR): keep the art branch's values for `allowedAutorotateToPortraitUpsideDown` and
`useOSAutorotation` (both `0`) — a superset of this contract's lock, not a contradiction of it.
No semantic judgment call is needed; this note exists so the conflict is not a surprise.

## Criteria

1. **Portrait locked:** `unity/ProjectSettings/ProjectSettings.asset`'s
   `defaultScreenOrientation` reads `0` (Portrait), not `4` (AutoRotation) or any other value.
2. **Autorotate flags normalized (irrelevant once locked, but no dangling truthy flags):**
   `allowedAutorotateToLandscapeRight` and `allowedAutorotateToLandscapeLeft` both read `0`.
   `allowedAutorotateToPortrait` and `allowedAutorotateToPortraitUpsideDown` MAY stay as
   authored (`1`) — locking `defaultScreenOrientation` to `0` makes them unreachable regardless;
   this contract does not touch them, to keep the diff minimal and byte-diff-clean.
3. **CI-runnable shape gate:** `tests/unity/orientation.test.sh` (device-config.test.sh house
   style — grep-based, fail-closed on a missing file or missing/duplicated field) pins both
   facts above and runs under `scripts/test.sh`'s `tests/**/*.test.sh` discovery.
4. **Byte-diff discipline:** `git diff -- unity/ProjectSettings/ProjectSettings.asset` touches
   exactly the three lines above and nothing else — no Unity-editor re-serialization noise.
5. **No Unity run required for this contract.** This is a textual YAML settings change; opening
   the file in the Unity Editor would rewrite unrelated fields (as every prior device-config
   contract in this repo's history has observed), so criteria 1-2 are edited textually and
   criterion 3's wrapper is the enforcement gate.

## Behavioral leg — explicitly deferred, not this contract's job

This contract proves the **setting** is correct (static YAML fact + a CI-runnable pin). It does
**not** prove the phone actually stays portrait when physically rotated — that requires an
emulator or device rotation test, which belongs to the downstream **EMU-RIG** stream per the
standing "render + look at real frames for anything visual" verification rule. Recorded here so
the gap is visible, not silently assumed closed.

## Assumptions (none load-bearing beyond what's stated above)

- "Portrait" means Unity's `UIOrientation.Portrait` (enum `0`), matching both this repo's own
  prior art (the art branch's independent, human-driven fix converged on the same value) and
  Unity's documented enum ordering (Portrait=0, PortraitUpsideDown=1, LandscapeLeft=2,
  LandscapeRight=3, AutoRotation=4).
- The two *landscape* flags are the behaviorally load-bearing ones for "never rotates to wide
  screen"; the portrait-family flags are cosmetically irrelevant once `defaultScreenOrientation`
  is pinned off AutoRotation, so leaving them untouched satisfies the directive without widening
  the diff. If a future contract wants the fuller lock the art branch already has (blocking
  portrait-upside-down, disabling `useOSAutorotation`), that is a superset change, not a
  contradiction — see the CROSS-LANE NOTE's resolution guidance above.

## Status log

- 2026-08-14 — contract frozen (this commit). RED run of `tests/unity/orientation.test.sh`
  against the unfixed `ProjectSettings.asset` captured before any edit (see the PR body / final
  report for the exact output). Fix + GREEN run + `scripts/check.sh` follow in the next
  commit(s).
