# Actual riding-seat anatomy probe

This opt-in PlayMode fixture compares the installed rig and actual imported original carriage.
It changes only disposable scene clones. Its passing result means the diagnostic controls ran;
it does not mean a seat offset is accepted. Open the images and read the contact rows first.

Run the owner Unity PlayMode test filter
`CatMetro.Tests.PlayMode.RidingSeatAnatomyProbeTests` with
`CM_RIDING_SEAT_PROBE_DIR=/absolute/capture/directory`.
The imported `Resources/CatMetroOriginal/OpenCarriage` prefab and the admitted paid rig/original
motion controller must exist. The original carriage importer on `art/original-station-track`
is the input; no invented box or floor replaces a missing asset. No `.meta` is hand-authored.

The fixture runs on base `53f60131` without requiring carriage production changes. If the
production `Carriage/OriginalCarriage` wrapper exists, it uses that actual wrapper. Otherwise
it mounts the real imported prefab at the runtime candidate's `(0,0,.235)`, X rotation -90°,
unit scale, and hides only the old `Carriage/Body` and `Carriage/Chassis`. JSON labels which path
ran. Cat/pin/train anchors, rig scale, tint, motion curves, source mesh, and materials are retained.
Trial offsets apply only to the disposable rig's local Z (board down).

## Anatomical basis

Source coordinates are taken from the actual Unity mesh using
`UnityEditor.MeshUtility.AcquireReadOnlyMeshData`; this leaves the paid Read/Write setting
unchanged. Direct source bone weights remain available. Source vertices are transformed into
the source Animator's coordinates, facing -X with +Y up; they are not classified from the
already-posed minimum height. The 2026-09-10 read-only Blender weighted-region evidence independently
matches those coordinates as `(-Blender X, Blender Z, -Blender Y)`.

- Rear minus-Z paw: direct combined weights of `1_Left_Limb_0/1` and `1_Right_Limb_0`,
  rest height <.11, -.22 <= X <.12, Z <0.
- Rear plus-Z paw: `bone_28/29`, same rear/height bounds, Z >=0.
- Both front paws: combined `0_Left_Limb_0/1/2/3` weight, rest X <-.22 and height <.11,
  split by Z. The cross-paw influence of `0_Left_Limb_2` is intentional.
- A separately labeled **lower-torso patch**, not a claim of a named anatomical bone:
  direct `Head_0` weight >=.5, rest Y .11–.27 and X -.18–.12.

Paw membership requires combined direct influence >=.25. Each region's bottom quarter in
source rest height provides the sole sample. Both membership and sole counts/bounds/weight
masses are reported. Colored real-mesh triangle views expose source-rest and riding selections:
cyan/magenta rear, yellow/green front, orange lower torso, dark unclassified. These masks still
need visual confirmation; provider names alone are explicitly insufficient.

Independent CPU skinning uses each source vertex and `bone.localToWorldMatrix * bindpose`,
then checks every sampled position against the baked skin within .0001 world units.
This catches a scaling/projection error before using it to claim contact.

## Actual carriage and captures

Nine rays must hit the imported recessed floor at carriage Z .150. Four rim rays must hit
Z .070. A horizontal ray at Z .10 must hit a real inner wall; one above the rim at Z .05 must
miss. An old slab at rim height fails the floor control. Empty actual carriage side/front/top
views show the cavity separately. No synthetic floor has a path to a green result.

All three L001 authored spline edges are used at progress 6/10. Each has ride phases
0/.4/1.2 seconds and celebrate phases 0/.22/.48 seconds measured. Beauty comparisons use
ride .4 and celebrate .22. Every comparison image has three fixed-camera columns:
**current offset, +.100, +.130 down**. Phone columns are 917×2048; side/front columns are 768².
There are 18 comparison sheets plus the baseline/cavity/anatomy control views. Colors/tints and
nearby board geometry stay real; canvas visibility changes only for close views.

For each sole/phase/heading the JSON reports actual floor, rim/wall, or outside-footprint hits;
minimum/median floor gap; minimum rim/wall gap; and carriage-space bounds. Positive gap means
floating, negative means penetration. A final interval intersects the translations needed to
put **both** rear soles within .010 of a real floor while allowing at most .005 penetration.
No shared interval is an explicit result, not grounds to loosen the metric. Coverage, wall
intersections, lower torso, and tail remain independent constraints even if an interval exists.

The +.130 trial has a geometric basis, not acceptance: earlier Blender seated rear-paw minima
were -.0360/-.0343, and the actual wrapper scale is .52×1.0301806. The imported floor at .150
then predicts downward offsets about .1307/.1317. Three Unity paw joints differed from Blender
bind rest, so this is a bracket to refute in actual Unity. The old 1175-vertex .0896 gap would
suggest only ~.1046 after the floor change; the .100 column directly contrasts that heuristic.
Do not ship either number before the anatomical Unity rows and actual renders agree.

Reviewed inputs: `gen-ref-v2-moments.png`, actual `Cat_Ride-side.png`, original carriage
`01-open-front.png`, and the earlier `weights-0_Left_Limb_2`, `weights-bone_28`, and
`weights-Spine_0` images. The latter refute name-based limb identification.

Validation performed here: complete Presentation and PlayMode C# assemblies compiled with
installed Unity 6000.3.16f1 Roslyn flags/references, all outputs in
`/private/tmp/catmetro-seat-anatomy-compile`; final exits 0. No Unity was launched.

Unverified: native Unity read-only mesh snapshot behavior in this new fixture, actual anatomy
mask images, contact rows and interval, final offset, all candidate beauty frames, full suites,
player/Android behavior, and device rendering. The probe does not implement production seating.
