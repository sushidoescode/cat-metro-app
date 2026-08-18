# GLB-CURATION rendered evidence — 2026-08-17

## Boundary

- Base: `origin/task/GLB-DECIMATION` at `16e20e3fe9793ecaeb0f7865e66a55521eebf7db`.
- Renderer: tracked `scripts/glb-silhouette.py`, SHA-256
  `e3e82e5e2323e4a5f60a4fb9be2602486986161a764cd0b0ccd65941938ed7b5`.
- Render settings: yaw 25°, 520×520 RGB, splat radius 2, 1% coverage gate.
- Contact sheets: ImageMagick 7.1.2-25; `+append` five-image rows and
  `-append` grids, without resampling source frames.
- Ignored artifact root:
  `/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming`.
- Recoverable pre-curation files remain under the ignored local
  `curation-backups/GLB-CURATION-2026-08-17-16e20e3/` directory.
- The wave correction preserves its first-pass source and derivative pairs in
  `curation-backups/GLB-CURATION-WAVE-CORRECTION-2026-08-18-841d4a3/`.
- The original two-asset derivative rerun uses tracked
  `docs/design/assets/GLB-CURATION-MANIFEST.json`, whose inventory is exactly
  the loaf and wave assets. At the first-pass head, an isolated rerun produced
  exactly two GLBs and two sidecars byte-identical to that head's derivatives.
- The correction rerun uses tracked
  `docs/design/assets/GLB-CURATION-WAVE-MANIFEST.json`, whose inventory is
  exactly the wave asset. An isolated rerun produced one GLB byte-identical to
  the retained derivative and one sidecar differing only by its fresh tool
  timestamp.
- The source publisher locks the input directory, revalidates both frozen
  GLB/sidecar hashes after Blender, and uses a durable recovery journal around
  the two sequential replacements.

## Inventory and order

`source-comparison.png`, left to right:

1. loaf source before;
2. loaf source after;
3. wave source before; and
4. wave source after.

`changed-derivative-comparison.png` uses the same order for derivatives.

Each derivative grid is three five-image rows in manifest order:

1. `cat-red-tabby`, `cat-blue-siamese`, `cat-yellow-longhair`,
   `cat-green-shorthair`, `cat-wild-alley`;
2. `cat-red-tabby-sitting`, `cat-blue-siamese-loaf`,
   `cat-yellow-longhair-wave`, `cat-green-shorthair-sit`, `cat-conductor`;
3. `prop-depot-shed`, `prop-toy-engine`, `prop-station-kiosk`, `prop-trees`,
   `prop-desk-clutter`.

`derivative-comparison-grid.png` places the complete before grid above the
complete after grid. `wave-correction-comparison.png` places the first-pass
source before/after on its top row and the first-pass derivative before/after
on its bottom row. `SHA256SUMS` covers all 44 committed PNGs.

## Looked-at record

Lane C viewed `source-comparison.png`, `changed-derivative-comparison.png`, and
the full 2600×3120 `derivative-comparison-grid.png` at original detail on
2026-08-17, then viewed all four dedicated correction frames and
`wave-correction-comparison.png` at original detail on 2026-08-18.

- **Loaf — PASS for the ruled change.** The wide display disc is absent from
  both curated source and regenerated derivative. The paws, body, head, ears,
  whisker silhouette, and tail remain legible. No replacement plinth or new
  detached silhouette appeared.
- **Wave — PASS for the corrected ruling.** The first pass removed the dark
  min-Y debris but retained a torso-height floating blob under the original
  wording. The correction keeps the unique largest cat component and removes
  every other component from source and derivative. The blob is gone; ears,
  face, raised paw, torso, feet, and curled tail remain intact with no visible
  seam or amputation.
- **Other 13 — PASS, unchanged.** Every before/after derivative PNG pair is
  byte-identical, matching the literal derivative hash-pin test.

The renderer normalizes each file to its own retained bounds, so removing the
loaf plinth makes the cat occupy more of its frame. That is expected framing,
not scale mutation in the GLB. This is silhouette evidence for the two ruled
curations, not a new material/color review, generated-asset licence approval,
or shipping approval.

## Local artifact result

| Asset | Source SHA before → after | Source triangles before → after | Derivative SHA before → after | Derivative triangles |
|---|---|---:|---|---:|
| `cat-blue-siamese-loaf` | `e3015351ec9bda2aebeafcc0ff23f5aa35512af4234c168d79cac750118070e3` → `257e59ebac613e3260bfd1161b228ec2be4aa7024969b4b1a3fec2366ffe0097` | 1,427,775 → 773,061 | `cc1ff113257d48994a94cfdff52554236034e3e6455d402de195461b8c8fc236` → `9a7b2ef923f923a78466f18d8bf0cfb82140aebbd30ba3e7cddd3f814fd2953c` | 14,999 |
| `cat-yellow-longhair-wave` | `8d7190fd24f552f874bf1d733f2870c44a24c27d6b50cfe1e32095f625fcc57c` → `bf4626c2a41214444a483bde1920c7fd95a06069feca202df860861edb540d64` | 1,494,090 → 1,383,894 | `4e20de09cee1dcfa383bb708608f03b5f8c1aa78ca4a510a3064f435f5f87a27` → `a3c4a363b06064ecc5dc03509c36ddd5ab91200a41314a3c674cd91ef4386696` | 15,000 |

The correction input hashes were source
`f91ccb7ff9b527ecef168d4285488ff647023fb70875f5403c31db8e2349d99d`
and derivative
`2eee06883d024631263485b48da067dd8042f66ef81fc669016731fa5fdaa1ef`.
