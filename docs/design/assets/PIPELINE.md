# Asset generation pipeline — Meshy + Tripo (RICH-ASSETS)

Contract: `state/handoffs/RICH-ASSETS-frozen-contract.md` (H-1 directive 2026-08-14:
richer assets, cuter cats, rich beautiful game). Tooling: `scripts/gen-assets.sh`.
First curated queue: `docs/design/assets/CAT-MANIFEST.json`.

All API facts below were retrieved **2026-08-14** from the vendors' live docs; re-verify
before relying on them after a vendor version bump. Auth headers are recorded by NAME
only — never a value.

## Custody-posture deviation — HUMAN RULING OWED (F7, do not treat as settled)

This design has the **human hold the credential** but **execute agent-authored code**
(`gen-assets.sh`) inside that credentialed session. That is a real deviation from the
repo's standing posture: `docs/security/threat-model.md:238` classes the asset-gen keys
as *Agent-reachable: No — owner-only credential store*, and
`docs/adr/0009-ci-topology-and-secret-custody.md` states agent-reachable contexts never
hold the credential. The mitigation here is that the code never reads/echoes/persists a
key and (post-review) validates every vendor-supplied URL before curl touches it — but
**the deviation itself is a high-severity residual, and the constitution reserves its
acceptance to the human** (principle 5), as does the ordering of the still-owed
asset-license ADR (this pipeline is tooling; nothing ships to the Play binary from it
without that ADR). Two items for the human queue: (1) accept or reject running this
script in the keyed session; (2) sequence the license ADR. Neither is an agent's call.

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
  the file defines; `set +a` turns auto-export back off. **Caveat (F10):** because the
  parts are `&&`-chained, if `source .env` fails (no `.env` yet, wrong directory, typo)
  the chain stops before `set +a` and leaves your shell in auto-export. If that happens,
  run `set +a` yourself, or run the arming in a throwaway subshell:
  `(set -a; source .env; set +a; bash scripts/gen-assets.sh queue docs/design/assets/CAT-MANIFEST.json)`.
- **PROBE BOTH SERVICES BEFORE ANY FULL QUEUE — standing gate, not a historical note.**
  Run one single-asset probe per service and confirm each produces a real `.glb`:
  ```
  ! cd <repo> && set -a && source ./.env && set +a && \
      probe_id="$(date -u +%Y%m%dT%H%M%SZ)-$$" && GEN_ASSETS_ACCOUNT_TIER=paid \
      bash scripts/gen-assets.sh tripo "a test cat" "probe-tripo-${probe_id}.glb"
  ! cd <repo> && set -a && source ./.env && set +a && \
      probe_id="$(date -u +%Y%m%dT%H%M%SZ)-$$" && GEN_ASSETS_ACCOUNT_TIER=paid \
      bash scripts/gen-assets.sh meshy "a test cat" "probe-meshy-${probe_id}.glb"
  ```
  The timestamp-plus-shell-PID suffix makes every probe output new: a fixed filename would
  hit the queue's intentional existing-file `SKIP` path after the first run and falsely
  report success without exercising create, poll, or download. Confirm neither probe logs
  `SKIP`, and inspect the two newly named GLBs and sidecars before starting the queue.
  This gate fires for **any** change that could touch a generation path — ours or the
  vendor's — not only a vendor-version bump. [Review finding, 2026-08-15: an earlier
  revision narrowed this to "for a future vendor-version change". That would NOT have
  caught the two defects actually hit on 2026-08-14: the Tripo one was a vendor-schema
  drift, but the Meshy one was OUR OWN stdout bug in `meshy_poll` — no vendor change
  involved — and it burned 7 assets in a live queue precisely because only Tripo had ever
  been probed. Both services, every time.]
- **Live probe record (F12, 2026-08-14):** the first one-asset Tripo probe returned HTTP
  400/code 1004 because v3 requires `model`; after the script sent `v3.1-20260211`, the
  same probe completed create → poll → signed-URL download and produced a valid GLB plus
  sidecar. Meshy was never probed, and its 7 queue entries all failed — the evidence for
  the both-services rule above.

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
- **Create task:** `POST /generation/text-to-model` — fields: `prompt` and `model` are
  required by the live v3 API as verified 2026-08-14. The reported allowed models were
  `P1-20260311`, `v2.5-20250123`, `v3.0-20250812`, and `v3.1-20260211`; the script defaults
  to `v3.1-20260211` and `TRIPO_MODEL_VERSION` can select another allowed value. Optional
  fields include `texture` and `face_limit`.
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
  tier, task id) before release. The sidecar carries service, task id, timestamp, sha256,
  and a `plan_tier` field — populated from `GEN_ASSETS_ACCOUNT_TIER`, which the human sets
  when arming (the script cannot introspect an account tier). Set it to your real tier
  (e.g. `paid`) so the ADR never has to guess; it defaults to `unknown`.

## Flow

```
prompt (CAT-MANIFEST.json)
  -> create task (Meshy: preview -> refine | Tripo: text-to-model)
  -> poll until terminal status (timeouts: Meshy 1200s/stage, Tripo 900s)
  -> download GLB immediately (signed URLs expire)
  -> host-allowlisted, size-capped (--max-filesize; note: a reliable pre-flight cap
     needs curl >= 8.4.0), glTF-magic-verified, glob-off download (a vendor-supplied URL
     is external DATA — never passed to curl as an option, never allowed to write outside
     the candidate dir; the bearer token only ever reaches the vendor's own host)
  -> unity/Assets/Art/Generated/incoming/<id>.glb  + <id>.glb.json sidecar
       (prompt, service, task id, UTC timestamp, sha256, plan_tier)
```

`incoming/` is **gitignored**: generated candidates stay LOCAL until a human curates
them (silhouette-at-64px + palette checks per product_spec §7) and the provenance
sidecar accompanies the promoted asset — the Polyfork custody pattern. Promotion into
the tracked tree is a separate, reviewed change.

## Verification without keys

`--dry-run` prints every request the script WOULD make (method, URL, headers with the
auth value redacted, JSON body, poll/download plan) and requires no keys. Agents test
with dry-run + `tests/assets/gen-assets-custody.test.sh` and
`tests/assets/tripo-model-contract.test.sh`; live generation is human-armed only, via
the one-liner above.

## Local decimation boundary

Generation custody ends at
`unity/Assets/Art/Generated/incoming/{manifest-out}` and its source provenance
sidecar. The separate offline derivative workflow is documented in
[DECIMATION.md](DECIMATION.md); it writes only beneath the ignored
`incoming/decimated/` tree. Decimation does not contact either generation
service and does not change or supersede the source's service, account tier,
prompt, task, timestamp, provenance, or license record. Promotion into tracked
Unity assets remains subject to the separate generated-asset license ADR,
curation contract, and human approval.
