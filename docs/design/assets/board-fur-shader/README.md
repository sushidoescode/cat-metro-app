# Board fur tint candidate

The board currently multiplies the whole paid cat atlas by its route colour.
That atlas contains orange fur, cream muzzle/chest/paws, navy eyes and pink ears.
The multiplier recolours all of them. This candidate gives a mounted board clone
a separate fur-colour channel while keeping its base multiplier white.

The shader code and selection/recolour operation are original project work.
The paid model, atlas, prefab and source material are unchanged. This record does
not claim ownership of the paid art or of renders containing it.

## Source and ownership

- `unity/Assets/Resources/CatMetroFur/BoardFur.shader` is loaded directly as a
  Resources shader, so inclusion does not depend on a runtime `Shader.Find` call.
- `BoardFurTint` creates one owned material per distinct source material when a
  board rig mounts, reuses its original atlas reference and restores/disposes its
  bindings on destruction. It never generates or reads back textures per frame.
- `ToyTrainView` installs it only on the calibrated rig identified by the existing
  `CatRigPresentation` adapter. Synthetic rigs keep their old rendering path.
  Profile/Home mounting does not install this component. Direct attempts to bind
  the paid Resources prefab itself are rejected.
- Admission requires an opaque, unclipped, white-base URP/Lit atlas material with
  no material keywords. Unsupported materials keep the existing complete path.

Unity must generate the new folder, shader and C# `.meta` files during the owner's
import. None are authored as YAML here.

## Mask and rendering tradeoffs

Selection runs on linear atlas RGB before lighting. A conservative saturation
window of .82–.93 and warm-channel ordering protect the measured cream/navy/pink
swatches. The warm-hue ratio window is .12–.22. These are a candidate for this
specific atlas, not a semantic guarantee about every facial pixel or another cat.

Within the mask, the route hue replaces orange chroma while retaining the source
texel's maximum linear RGB channel. This preserves that value-based stripe
contrast; it does not claim identical perceived luminance across different hues.
`_FurStrength=0` gives the natural-atlas control. `_FurDebug=1` displays the raw
mask on the same visible triangles, bypassing lighting/fog for that debug view.

Normal rendering reuses the installed URP Lit surface/forward implementations,
with an original albedo wrapper. Lighting, main/additional shadows, fog, reflection
probes and SH/probe lighting remain in URP. Shadow/depth passes reuse URP, and a
small normal pass supports forward-only rendering. The current renderer uses
forward mode; no GBuffer, transparent, parallax, detail-map or motion-vector path
is added. The pinned source already disables motion vectors and has no material
keywords. No URP package source is vendored or modified.

The extra properties use the ordinary material binding path, outside URP's fixed
`UnityPerMaterial` buffer. These per-renderer property-block draws already bypass
the SRP Batcher; this is not an SRP-batching optimization. Device cost and shader
variants still require owner validation.

## Owner probe

Run the PlayMode fixture `CatMetro.Tests.PlayMode.BoardFurTintRenderTests` with
`CM_FUR_TINT_CAPTURE_DIR` pointing to a scratch directory. The opt-in real-board
test requires the admitted licensed rig and renders 917 × 2048 files:

- `stock-lit-control-{board,close}.png`: untouched source URP material and atlas.
- `natural-control-{board,close}.png`: candidate with zero tint strength.
- `mask-debug-{board,close}.png`: raw candidate coat selection.
- `{red,blue,yellow,green,wild}-{board,close}.png`: production session-to-view
  colour binding, reusing the same train slot.
- `measurement-*.png`: the same close view at one sample per pixel, including
  natural/stock lighting, source atlas without lighting, silhouette, mask and routes.
- `readback.txt`: stock-material equivalence and protected-region differences.

The full board retains its HUD. Close-ups hide canvases temporarily and keep the
actual cat, pin, carriage, surrounding geometry and camera projection. A separate
temporary white-cat/black-occluder pass identifies visible skin pixels for checks;
it restores every material/property block. Protected regions are classified from
the unlit source atlas independently of the shader's linear mask, at one sample
per pixel. Lighting can make orange fur satisfy a pink/cream photograph predicate;
an MSAA-resolved pixel can mix fur and protected samples with different masks.
Neither is a valid single-region protection measurement. The 4x MSAA beauty
captures remain intact, and all measurement mask/colour assertions still require
zero changed protected pixels. No simulation ticks advance
during the colour comparison. This is a frozen presentation probe, not a complete
live puzzle replay.

The always-running GPU swatch test uses a real sRGB texture and the actual shader.
It requires protected cream/navy/pink pixels to remain unchanged, both fur and
stripe controls to select, all five route hues to render, and stripe contrast to
remain visible. EditMode tests cover material/source ownership, preview reset,
slot reuse and retained passengers. Existing four L009 real-delivery tests also
inspect the admitted fur channel on live and retained cats.

## Verification so far

The owner verified the six EditMode cases and the always-running GPU swatch test
in Unity 6000.3.16f1. Native unlit controls at one sample per pixel selected
50,991 cream, 19,592 navy and 5,725 pink pixels with zero mask leakage, while the
lit-photograph predicates still failed. This established the measurement issue;
the final paired beauty/measurement fixture requires its own native run. Android
rendering and performance remain unverified. The owner must open the actual mask
and route frames before judging this candidate visually.

The real-board probe also selects warm coat from the natural photograph independently of the
shader's linear mask, requires more than 100 of those visible pixels to be strongly positive
in the real GPU mask, and requires every route to change most of that selected coat by more
than eight byte levels with the expected mean route hue. This prevents an all-black paid-atlas
mask or disconnected tint from passing facial protection checks. The readback explicitly says
whether pink pixels were observed in this face view; absent pink uses the named GPU swatch
control and does not claim actual pink-region coverage. All beauty/mask renders precede these
assertions. These new real-atlas controls still require owner Unity execution.
