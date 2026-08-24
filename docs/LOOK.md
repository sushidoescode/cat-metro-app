# The look

This is the whole point of the project. Everything else serves it.

| | |
|---|---|
| **Target** | `reference/target-01-tabletop.png`, `reference/target-02-diorama.png` |
| **Where we are** | `reference/actual-01-home.jpeg`, `reference/actual-02-board.jpeg` |

**Open all four side by side before you touch the game.** The gap is the work.

## What the target is

A wooden toy train set sitting on a warm desk, photographed from a low isometric angle.
Chunky low-poly pieces with soft rounded edges, the way a good children's toy is made. A coffee
cup and a pencil sit just outside the board, so it reads as a real object in a real room rather
than a UI.

The specifics that make it work:

- **Real track.** Navy rails on cream sleepers, curving in smooth arcs across a wooden board.
  Track is a physical object with thickness, not a line.
- **Cats ride trains.** Round chibi cat heads sit *in* the carriages of a little steam engine.
  They are passengers, not markers.
- **Stations are places.** Raised wooden platforms with coloured roofs and a big shape badge —
  blue square, red circle — so you read the destination instantly.
- **The world is furnished.** A depot building, pine and round trees, fences, bushes, a clock,
  a lamp post, signposts. The board is full without being busy.
- **Switches are objects you want to touch.** A chunky orange lever on a teal base, tilted,
  with an arrow showing where it will send the train.
- **Warm light.** Late-afternoon lamp light, soft shadows under everything, gentle depth of
  field on the desk beyond the board. Nothing is flat-lit.
- **Palette.** Navy, cream, warm wood, teal, tomato red, marigold. Muted and cosy, never neon.

## Where we actually are

The board draws itself out of Unity primitives: stretched white quads for track, a sphere on a
stick for a signal, flat coloured squares labelled `R` and `B` for stations, against the default
sunset skybox. The Home screen shows three untextured grey-blue cat silhouettes floating in a
cream void. A development console overlays the game with collider errors, and the fail message
is clipped off both edges of the screen.

The 3D cats are real and good — you can see the shapes are right in the silhouettes. They are
just grey, and standing in nothing.

## The order to fix it

Each step should end with a render you look at. Do not batch them.

1. **Colour the cats.** They are textured — the maps are embedded in the FBX and extracted
   alongside them. URP is not binding base colour. Fix the material and the cats stop being
   ghosts. This is the cheapest, biggest single win available.
2. **Give the board a body.** A wooden tabletop under the level, warm background instead of the
   stock skybox, isometric camera pitched down, one warm key light with soft shadows.
   The same abstract layout will read ten times better before a single new model lands.
3. **Make track look like track.** Rails and sleepers with real thickness along the level graph,
   replacing the stretched quads.
4. **Turn stations into platforms.** Raised wooden bases with a coloured roof and the shape
   badge. `prop-station-kiosk` and `prop-depot-shed` are already generated and sitting unused.
5. **Furnish it.** `prop-trees`, `prop-desk-clutter`, `prop-toy-engine` are generated and unused.
   The Polyfork library (FOUNDERS account, MCP at `polyfork.dev/mcp`) has low-poly track,
   scenery and buildings that match this style, and it is already paid for.
6. **Put the cats on trains.** Carriages that follow the track, with a cat seated in each.
   This is what the concept art is actually selling.
7. **Fix the UI.** Kill the dev-console overlay in release builds, fit the text to the screen,
   and give Home something better than three cats floating in a void.

## Rules of thumb

- Look at it on the phone, not just in the editor. It is a phone game.
- Silhouette and colour first, detail last. A flat-lit detailed model looks worse than a
  well-lit simple one.
- If a change does not move a screenshot closer to the target, it is not this project's work
  right now.
- The generated assets are inconsistent by nature — different scales, different forward axes,
  some with plinths. Correct that in the presentation layer, not by editing the model files;
  their bytes are pinned by the licensing record.
