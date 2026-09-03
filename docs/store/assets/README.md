# Store asset production receipt

## Status

**Icon: BLOCKED — BRIEF ONLY.** The requested raster set exists and its mechanical checks pass,
but the human taste/legal review and the independent G-13PLUS review remain open. Nothing in this
directory is an upload receipt or permission to upload.

No S1–S6 or D0 screenshot raster was produced. Their deterministic selectors are recorded in
`screenshot-state-plan.md` for the exact-capture lane.

## Source identity

- Store-assets base/source revision: `02871d644af1f5a7b632578c73e6ba3e6a899787`.
- Admitted local cat-rig worktree revision: `8a0b6340762e2886ed57087cbbbe4d72633aa4cd`.
- Tripo rig task: `40e83aa2-b6e7-4f57-a5e3-ab1212dc77bc`.
- Pinned rigged GLB SHA-256:
  `e9bcbb70f8fbc803b926b505c5ab4eb57fdad5bc3173498adf0b732080516a39`.
- Icon master SHA-256:
  `01a47f4694fc7c43c590681ab86337ed2b1dce19152ac377b06037a2a814598a`.
- Renderer: Blender `5.1.2`, headless and outside the sandbox; ImageMagick `7.1.2-25`.

The pinned provider GLB remains ignored and unmodified. `source/render_icon.py` checks its SHA before
import, assigns a temporary Cream Card material in memory, and adds separate modeled cap, badge,
eyes, nose, and mouth objects. A non-destructive presentation mask crops the fresh render below the
chin so only the face remains; it does not change the mesh. The script neither resculpts nor exports
the provider mesh. No image model was used. The orange field is one plain circle with no bar,
lettering, logo, or transit-authority geometry.

Rebuild from this checkout with the admitted local source path:

```sh
docs/store/assets/source/build_store_assets.sh /absolute/path/to/the/pinned/model.glb
```

The build was run twice consecutively and all 17 raster hashes were byte-identical. Every derivative
is rendered at 1024 px or downscaled directly from a 1024 px source; no raster is upscaled.

## Mechanical evidence

- The centered 512 px safe-crop overlay places the eyes, muzzle, cap crown, and badge inside its
  magenta box. Both ears remain inside the teal central-80% box.
- The modeled badge's projected bounding square is at most 75 × 75 px, a conservative
  `0.54%` of the 1024-square canvas. A pixel hue mask measures `0.406647%` teal-like pixels. Both are
  below the `2.00%` ceiling.
- The Play icon is 512 × 512, 8-bit RGBA, explicitly tagged sRGB, fully opaque, and 108,451 bytes.
- The master and Devpost icon are 1024 × 1024, 8-bit RGB, explicitly tagged sRGB, and opaque.
- The feature graphic is 1024 × 500, 8-bit RGB, explicitly tagged sRGB, and has no alpha.
- `tests/store/store-assets.test.sh` checks the dimensions, formats, channels, alpha policy, explicit
  sRGB declaration, Play byte ceiling, teal ceiling, review derivatives, and Unity icon layers.
- `raster-sha256.txt` records every committed raster, including the Unity launcher sources.

The safe-crop boxes and color mask are mechanical evidence, not aesthetic or legal approval.
