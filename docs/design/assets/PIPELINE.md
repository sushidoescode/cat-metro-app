# Asset generation pipeline — Meshy + Tripo (RICH-ASSETS)

Contract: `state/handoffs/RICH-ASSETS-frozen-contract.md` (H-1 directive 2026-08-14:
richer assets, cuter cats, rich beautiful game). Tooling: `scripts/gen-assets.sh`.
First curated queue: `docs/design/assets/CAT-MANIFEST.json`.

All API facts below were retrieved **2026-08-14** from the vendors' live docs; re-verify
before relying on them after a vendor version bump. Auth headers are recorded by NAME
only — never a value.

## Key custody (the repo's standing posture)

- Keys live in the human's env file, permission-denied to agents by design.
- `scripts/gen-assets.sh` reads ONLY the process environment variables
  **`MESHY_API_KEY`** and **`TRIPO_API_KEY`**; it never reads any file for keys, never
  prints a key (redactor on every output path, auth via curl stdin-config so keys touch
  no argv and no file, xtrace off), and fails naming the missing variable.
- **The human arming one-liner** (run it yourself, in your session):

  ```
  ! set -a && source .env && set +a && bash scripts/gen-assets.sh queue docs/design/assets/CAT-MANIFEST.json
  ```

  The `!` prefix executes this in YOUR interactive session shell, so no agent ever
  touches the key file — that is the designed custody boundary. `set -a` exports what
  the file defines; `set +a` turns auto-export back off.

## Meshy API (retrieved 2026-08-14, docs.meshy.ai)

- **Base:** `https://api.meshy.ai/openapi/v2`
- **Auth header (name):** `Authorization`, shape `Bearer <key>`
- **Create task:** `POST /openapi/v2/text-to-3d` — two-stage:
  - `mode: "preview"` — fields: `prompt` (≤600 chars), `ai_model`
    (`meshy-5` | `meshy-6` | `latest`), `model_type` (`standard` | `lowpoly`),
    `topology` (`triangle` | `quad`), `target_polycount` (100–300,000, default 30,000),
    `should_remesh`, `pose_mode`, `target_formats`
    (`glb` `obj` `fbx` `stl` `usdz` `3mf`), `auto_size`, `origin_at`.
  - `mode: "refine"` — fields: `preview_task_id`, `enable_pbr` (default false),
    `texture_resolution` (`2k` | `4k` | `8k`), `texture_prompt`, `remove_lighting`.
  - Response: `{ "result": "<task-id>" }`
- **Poll:** `GET /openapi/v2/text-to-3d/<task-id>` (SSE stream variant:
  `GET /openapi/v2/text-to-3d/<task-id>/stream`). Task object: `status`
  (`PENDING` → `IN_PROGRESS` → `SUCCEEDED` | `FAILED` | `CANCELED`), `progress` (0–100),
  `preceding_tasks` (queue position while `PENDING`), `task_error.message`,
  `model_urls` (`glb` `fbx` `obj` `mtl` `usdz` `stl` `3mf`), `texture_urls`
  (`base_color`, plus `metallic`/`normal`/`roughness` when PBR).
- **Download:** `model_urls.glb` is a signed, time-limited URL; assets are retained
  ~3 days on non-Enterprise plans — download promptly.
- **Rate/queue:** HTTP 429 on rate-limit; 402 when out of credits; queue position via
  `preceding_tasks`. Our script polls every 10s with a 1200s per-stage timeout.

## Tripo API (retrieved 2026-08-14, developers.tripo3d.ai / platform.tripo3d.ai)

- **Base (current):** `https://openapi.tripo3d.ai/v3` — note: older integrations used
  `https://api.tripo3d.ai/v2/openapi`; v3 is what the current quick-start documents.
  `TRIPO_API_BASE` env var overrides the base if the host moves again.
- **Auth header (name):** `Authorization`, shape `Bearer <key>`
- **Create task:** `POST /generation/text-to-model` — fields: `prompt` (required),
  `model` (version id, e.g. a dated v3.1 tag; server default when omitted), optional
  `texture`, `face_limit`.
  - Response envelope: `{ "code": 0, "data": { "task_id": "<task-id>" } }`
- **Poll:** `GET /tasks/<task-id>` — `data.status`: in-progress states (`queued`,
  `running`) then `success` | `failed` | `cancelled` | `banned`; `data.progress` 0–100.
  Vendor guidance: poll ~every 2s, stay under 1 request/second overall. Our script
  polls every 5s with a 900s timeout.
- **Download:** `data.output.model_url` (GLB; other formats exist) — **signed URLs
  expire within minutes** (docs say ~5), so the script downloads immediately after
  `success`. Typical generation 10–120s.

## License terms (feed the still-owed asset ADR — nothing ships in the Play binary before that ADR lands)

- **Meshy** (help.meshy.ai asset-ownership articles, retrieved 2026-08-14):
  - **Paid plans:** the subscriber owns generated assets outright — commercial use with
    no attribution required, resale on third-party marketplaces allowed — provided the
    asset is not published to the public Meshy community and inputs didn't infringe
    third-party rights.
  - **Free plan:** outputs are CC BY 4.0 — commercial use allowed but attribution to
    Meshy is required. Free-plan output is therefore NOT acceptable for the Play binary
    without an attribution decision; generate on the paid account.
- **Tripo** (Terms of User Agreement, last updated 2025-07-11, platform.tripo3d.ai/terms,
  retrieved 2026-08-14):
  - **Paid tiers (Pro and up):** broad rights in inputs and outputs — use, reproduce,
    modify, create derivatives, distribute, license, and derive revenue — subject to the
    agreement and non-infringement responsibility. Tripo states paid users' data is not
    used for model training.
  - **Free tier:** marketed as non-commercial / CC BY 4.0-style attribution terms —
    same rule as Meshy: paid account only for anything that can ship.
  - Assets generated during a paid subscription keep their commercial rights after
    cancellation; post-cancellation generations fall back to free-tier terms.
- **ADR note:** purely AI-generated geometry may be uncopyrightable in the US (license
  ≠ copyright); the owed asset ADR must record per-asset provenance (service, account
  tier at generation time, task id — the sidecar JSON carries this) before release.

## Flow

```
prompt (CAT-MANIFEST.json)
  -> create task (Meshy: preview -> refine | Tripo: text-to-model)
  -> poll until terminal status (timeouts: Meshy 1200s/stage, Tripo 900s)
  -> download GLB immediately (signed URLs expire)
  -> unity/Assets/Art/Generated/incoming/<id>.glb  + <id>.glb.json sidecar
       (prompt, service, task id, UTC timestamp, sha256)
```

`incoming/` is **gitignored**: generated candidates stay LOCAL until a human curates
them (silhouette-at-64px + palette checks per product_spec §7) and the provenance
sidecar accompanies the promoted asset — the Polyfork custody pattern. Promotion into
the tracked tree is a separate, reviewed change.

## Verification without keys

`--dry-run` prints every request the script WOULD make (method, URL, headers with the
auth value redacted, JSON body, poll/download plan) and requires no keys. Agents test
with dry-run + `tests/assets/gen-assets-custody.test.sh`; live generation is
human-armed only, via the one-liner above.
