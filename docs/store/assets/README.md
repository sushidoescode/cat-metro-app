# Store asset production receipt

## Status

**Icon: HUMAN SELECTED — G-13PLUS REVIEW OPEN.** The human selected candidate A over the
superseded Blender render and supplied the source-ownership ruling below. The requested raster set
exists and its mechanical checks pass. Independent G-13PLUS review and any store upload remain
human-only. Nothing in this directory is an upload receipt or permission to upload.

No S1–S6 or D0 screenshot raster was produced. Their deterministic selectors are recorded in
`screenshot-state-plan.md` for the exact-capture lane. The feature graphic and listing copy are
unchanged by the icon replacement.

## Current selected source

- Store-assets base revision: `02871d644af1f5a7b632578c73e6ba3e6a899787`.
- Replacement parent revision: `351a39aef2ce0f5471a403ce619d3e3392add0f6`.
- Generator: ChatGPT image model.
- Generation/selection date: `2026-09-05`.
- Prompt: the orchestrator's **Icon — exact 1024 × 1024 master** brief in
  `docs/store/creative-shot-list.md`.
- Original human-selected file:
  `.catshots/icon-candidates/candidate-A-chatgpt-1254.png`.
- Rejected alternate B remains beside it at
  `.catshots/icon-candidates/candidate-B-gemini-2048.jpeg`; it is not a pipeline input.
- Checked-in byte-identical source:
  `source/cat-metro-icon-source-chatgpt-1254.png`.
- Source SHA-256:
  `aa7166bee38a90aa2b2ba9bf25a1b8c24979248a89cebd25a5eb50a9960454e5`.
- Source format: 1254 × 1254, 8-bit RGB PNG, opaque, no alpha. ImageMagick decodes it as
  sRGB; the original has no explicit ICC or PNG sRGB chunk. Its original bytes, including its
  C2PA/JUMBF provenance record, are preserved unchanged.
- Rights ruling supplied by the human: the ChatGPT image-model output is owned by the human under
  OpenAI's output terms. This receipt records that ruling; it is not an independent legal opinion.
- Icon master SHA-256:
  `33850db45514aa84fe56c4d61135e9e210f8bd4997ff136541b5df202605ea8d`.
- Raster tool: ImageMagick `7.1.2-25`.

The pipeline validates the checked-in source SHA and format before doing any work. It converts to
linear RGB, reduces 1254 px to 1024 px with Lanczos and a pinned filter blur of
`0.9891028367558475`, converts back to sRGB, and uses the system sRGB profile to explicitly tag each
derivative as sRGB. The native reduction is `81.658692%`; no image is upscaled. The master and
Devpost copy are opaque RGB. The Play export is opaque 8-bit RGBA as required.

Candidate A is flattened. To make the adaptive launcher layers without inventing a matte or
redrawing selected pixels, the pipeline clips the complete motif to its orange roundel, scales that
motif to 942 × 942 (92% of the 1024 foreground canvas), and centers it on transparency. The
background layer is solid Cream Card (`#FAF6EC`). The resulting 512 px foreground retains alpha.
The safe-zone proof draws the centered 66% Android circle over the composited adaptive icon.

Rebuild from this checkout:

```sh
docs/store/assets/source/build_store_assets.sh
```

## Superseded Blender source

The first admitted icon remains recorded here and its renderer remains at `source/render_icon.py`;
it was superseded, not erased from provenance.

- Admitted local cat-rig worktree revision: `8a0b6340762e2886ed57087cbbbe4d72633aa4cd`.
- Tripo rig task: `40e83aa2-b6e7-4f57-a5e3-ab1212dc77bc`.
- Pinned rigged GLB SHA-256:
  `e9bcbb70f8fbc803b926b505c5ab4eb57fdad5bc3173498adf0b732080516a39`.
- Superseded icon master SHA-256:
  `01a47f4694fc7c43c590681ab86337ed2b1dce19152ac377b06037a2a814598a`.
- Renderer: Blender `5.1.2`, headless and outside the sandbox; ImageMagick `7.1.2-25`.

That renderer SHA-checks the ignored provider GLB, assigns a temporary Cream Card material in
memory, and adds separate modeled cap, badge, eyes, nose, and mouth objects. It neither resculpts nor
exports the provider mesh. The pinned provider GLB remains ignored and unmodified.

## Mechanical evidence

- The flat 1024 master intentionally lets the cap crown extend beyond the centered 50% square; the
  human explicitly accepted that exception for the flat Play and Devpost icons. The central crop is
  retained for inspection.
- On the 512 adaptive foreground, the navy/teal critical-pixel mask reaches a maximum radius of
  `159.958 px` inside the `168.960 px` 66% safe-circle radius, leaving `9.002 px` of radial margin.
  The magenta circle in `icon/review/cat-metro-icon-safe-crop-overlay-1024.png` is the visual proof.
- The brief's conservative teal-like pixel mask measures `0.472832%` of the master canvas, below
  the `2.00%` ceiling. A looser source badge mask measures approximately `0.50%`.
- The Play icon is 512 × 512, 8-bit RGBA, explicitly tagged sRGB, fully opaque, and 282,161 bytes.
- The master and Devpost icon are 1024 × 1024, 8-bit RGB, explicitly tagged sRGB, and opaque.
- The feature graphic remains the unchanged 1024 × 500 opaque sRGB PNG with SHA-256
  `6662274f013231f7660858063ae072a85e7ff66029e04e5e18f58c4480632509`.
- `tests/store/store-assets.test.sh` checks dimensions, formats, channels, alpha policy, explicit
  sRGB declarations, Play byte ceiling, teal ceiling, review derivatives, and Unity icon layers.
- `tests/store/icon-source-replacement.test.sh` checks the selected source identity, no-upscale
  precondition, exact decoded-pixel master derivation, adaptive 66% circle containment, and proof
  overlay.
- `raster-sha256.txt` records every committed raster, including the byte-identical selected source
  and all Unity launcher sources.
- Two consecutive rebuilds produced the same complete raster manifest byte for byte.

The safe-zone geometry, color masks, and hashes are mechanical evidence. They do not verify
adult-premium taste, legal clearance, similarity to a real transit mark, 48 px human readability,
or the final cross-asset G-13PLUS review.
