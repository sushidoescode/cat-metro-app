# CONTRACT RICH-ASSETS — Meshy/Tripo generation pipeline

**Branch:** `task/RICH-ASSETS`
**Frozen:** 2026-08-14 against `origin/main` `d10509d`, as the FIRST commit on the branch.
**Directive class:** H-1 (human, 2026-08-14): use Meshy AI and Tripo AI — richer assets,
cuter cats, no artificial size ceiling — make a rich, beautiful game. Where this directive
meets product_spec §7's "charming, not childish" guardrail, prompts blend both: chibi/round/
big-eyed per H-1, phrased to stay inside the §7 Families-risk rule (no oversized heads,
no primary-color explosion). The directive is recorded here verbatim-in-substance; the
guardrail note is an assumption, listed below.

## Key-custody rules (binding, the repo's standing security posture)

1. API keys live in the human's env file, which is permission-denied to agents BY DESIGN.
2. `scripts/gen-assets.sh` reads keys ONLY from process environment variables:
   `MESHY_API_KEY` and `TRIPO_API_KEY` (both names documented prominently in the script).
3. The script never reads, sources, or references the key file path; never echoes, logs,
   or persists a key (auth header passed via curl config on stdin, not argv; xtrace off;
   all printed output routed through a redactor); and fails with a clear message NAMING
   the missing env var when unset.
4. The HUMAN arms generation by sourcing their env file in-session and running the script —
   that is the designed custody boundary. Agents never touch the key file.

## Acceptance criteria

1. **Research recorded** in `docs/design/assets/PIPELINE.md` with retrieval dates: current
   Meshy API (api.meshy.ai text-to-3D — create/poll/download shapes, output formats incl.
   GLB, texture/PBR options) and current Tripo API (current host + same shapes); exact
   endpoint paths; auth header NAMES (never a value); polling semantics; rate/queue notes;
   and the LICENSE TERMS each service grants for generated assets on paid accounts, cited.
   Recorded: license terms feed the still-owed asset ADR before anything ships in the
   Play binary.
2. **`scripts/gen-assets.sh`**: subcommands `meshy <prompt> <out.glb>` and
   `tripo <prompt> <out.glb>` implementing create → poll → download with sane timeouts,
   plus `queue <manifest.json>` running a batch. Outputs land under
   `unity/Assets/Art/Generated/incoming/` (gitignored — entry added: generated candidates
   are LOCAL until curated + provenance'd, mirroring the Polyfork custody pattern), with a
   sidecar JSON per asset (prompt, service, task id, timestamp, sha256).
3. **`docs/design/assets/CAT-MANIFEST.json`**: first curated queue — 8–12 cute-cat prompts
   (round, chibi, big-eyed; each cat carries color + symbol redundancy per the colorblind
   rule) and 4–6 board props (depot shed, toy engine, station platforms, trees), matching
   product_spec §7 art direction (wooden desk toy-train aesthetic, warm lighting, the
   12-hex palette).
4. **The human one-liner** printed in the final report and written into PIPELINE.md:
   `! set -a && source .env && set +a && bash scripts/gen-assets.sh queue docs/design/assets/CAT-MANIFEST.json`
   — with the note that the `!` prefix runs it in the human's session so no agent ever
   touches the key file. (The one-liner names the human's file because the HUMAN runs it;
   the script itself contains no such reference.)
5. **Dry-run mode** (`--dry-run`): prints the requests it WOULD make, auth redacted; run
   by the implementing agent as test evidence. Live generation cannot be exercised by an
   agent (no keys by design) — stated plainly in the report. A `tests/**/*.test.sh` leg
   pins the custody behavior (dry-run keyless success; missing-var failure names the var;
   sentinel key never appears in output; script text never references the key file).

## Assumptions (unlisted assumptions are defects)

- A1: "Charming, not childish" (§7, LOCKED) still governs prompt phrasing under H-1's
  "cuter cats"; H-1 raises richness/cuteness, it does not repeal the Families-risk rule.
- A2: Manifest entries split across both services (Meshy and Tripo) so both lanes are
  exercised per H-1; per-asset service choice is curatorial, revisable at curation time.
- A3: Generated GLBs are candidates only. Nothing under `incoming/` ships; promotion into
  the tracked tree requires curation + provenance sidecar + the license/asset ADR (owed).
- A4: No new tool dependencies: bash + curl + python3 (stdlib only) + shasum, all present
  on the dev machine; no jq requirement, no SDK installs, hence no dependency ADR needed.
- A5: Live-API behavior (task shapes as researched 2026-08-14) is pinned by documentation
  citation, not by an integration test — an agent cannot hold keys, so first live run is
  human-observed via the one-liner.

## Out of scope

- Blender cleanup / retopology, import into Unity scenes, materials/shader hookup,
  the golden-frame style gate, and the asset-license ADR itself (owed separately before
  any generated asset ships in the Play binary).
