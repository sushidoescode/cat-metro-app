# Generated-asset verdict (2026-08-15) — first time anyone LOOKED

Method: no glTF viewer needed. Poly counts parsed from the glTF JSON chunk; base-colour
textures extracted from the BIN chunk; geometry rendered as a depth-shaded point splat by a
pure-stdlib script (scratchpad/glb-silhouette.py). Quick Look timed out on 40 MB files.

## GOOD — the art direction landed
- **cat-red-tabby-sitting**: genuinely cute chibi figurine — round plump body, two ears, seated
  pose, tail curled at the base. Clean, readable silhouette.
- **cat-blue-siamese-loaf**: visibly DIFFERENT — slimmer body, distinct ears, curled tail out to
  the side, a collar band. So the prompts differentiate characters rather than cloning one cat.
- **Textures on-brief**: the tabby atlas is warm red/cream with tabby striping, black eyes with
  white catchlights, and an orange ring that reads as the circle badge. Matches the manifest
  prompt's colour + symbol identity.

## BLOCKERS before any of this ships
1. **~1.43 M triangles and ~730 k vertices EACH** (measured across all 9; range 1.426–1.494 M).
   The manifest asked for `target_polycount: 30000`. That is ~48× over budget, ~13 M triangles
   for the set. Mobile budget is a few hundred thousand on screen TOTAL. **Decimation to roughly
   5–20 k tris per asset is mandatory** before anything goes near the APK.
2. **~40 MB per asset, 358 MB for 9.** Ships nowhere as-is.
3. **Inconsistent display plinths**: the siamese sits on a circular base disc; the tabby does not.
   Either strip them all or keep them all — a mixed set will read as a bug on the board. (A small
   base may actually suit the "toy figurines on a wooden desk" diorama — that is a taste call.)

## Not yet judged
Whether the colour+symbol badges are legible at game scale (needs decimated assets placed on the
real board), and per-cat licence provenance beyond `plan_tier=paid` in each sidecar.
