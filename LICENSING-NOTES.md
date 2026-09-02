# Polyfork furnish set — licensing notes (for the human to fold into the custody record)

This branch adds five board-furnish prop roles sourced from the Polyfork library
(polyfork.dev). **No licensing ADR or custody record was modified** — this file is the
raw material for extending that record deliberately.

## What was acquired

All five assets are Polyfork **FREE-plan** assets, downloaded 2026-08-24 from the
public no-auth `/cdn` endpoints. The MCP connection was **anonymous**: the FOUNDERS
key is not wired into this machine's Polyfork MCP config (`who_am_i` reports
`authenticated: false`), so no paid/Pro asset was downloadable this session. Nothing
in this batch depended on the paid account. If the FOUNDERS key is added to the MCP
server config (Authorization: Bearer header), Pro assets and the structured GLB /
`.mjs` module downloads become available.

Licence for every asset below, exactly as reported by the Polyfork MCP per asset:

> Use in commercial projects, no attribution. No redistribution of the files
> themselves. https://polyfork.dev/licensing

Note for any future web/platform use: Polyfork's help states that a product serving
these assets to *its own users* is redistribution and needs a separate platform
licence. Shipping them baked into a game binary is the normal "commercial project"
use. Play-Store shipping remains the human's deliberate call, as with the Meshy and
Tripo assets.

## The assets

| local id | Polyfork asset id | kit | plan | triangles | size (m) |
|---|---|---|---|---|---|
| prop-fence | wooden-fence-section-d72055 | cozy-farm-cc979f (Cozy Farm) | free | 248 | 2.00 x 1.10 x 0.23 |
| prop-bush | round-bush-cd2ac0 | nature-forest-kit-f29d6a (Nature & Forest) | free | 512 | 0.94 x 0.74 x 0.82 |
| prop-lamp-post | lantern-post-f47665 | nature-forest-kit-f29d6a (Nature & Forest) | free | 482 | 0.47 x 2.40 x 0.93 |
| prop-signpost | signpost-0bf523 | cozy-farm-cc979f (Cozy Farm) | free | 540 | 1.00 x 1.83 x 0.82 |
| prop-trail-signpost | trail-signpost-bd29a7 | nature-forest-kit-f29d6a (Nature & Forest) | free | 420 | 1.15 x 2.00 x 1.16 |

Asset pages: https://polyfork.dev/asset/{polyfork-asset-id}

## Where the bytes live

Only in the gitignored machine-local staging area, alongside SHA-256 hashes and the
full acquisition record:

    unity/Assets/Art/Generated/incoming/polyfork-furnish/
      ACQUISITION.md      — full record: hashes, kit contracts, conversion evidence
      glb/                — pristine provider-delivered GLBs (the pinned originals)
      Models/ Textures/   — converted FBX + baked palette atlas (derived, reproducible)
      proofs/             — colour-proof renders of every converted asset
      Editor/             — machine-local prefab installer (PolyforkFurnishInstaller)

Derived FBX/PNG files are reproducible from the pinned GLBs via
`scripts/blender_polyfork_bake.py` on this branch; the GLBs in `glb/` are the
provider-delivered copies to preserve.

## Catalogue gap

The target art's **clock** (wooden clock tower) does not exist in the Polyfork
catalogue at all. Registered upstream via the MCP's `report_need`. Options if it
stays missing: generate one via the Meshy/Tripo pipeline, or commission it
(polyfork.dev/steer kits start at $75).
