#if UNITY_EDITOR
using System.Linq;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    // The vertical half of the consist scale, measured rather than assumed.
    //
    // Every horizontal clearance in the suite projects onto a board-plane direction, so none of
    // them can see a rider sinking through the carriage floor. Scaling the seat sink directly
    // with the consist did exactly that: at ConsistScale 1.30 the licensed rig's deepest vertex
    // measured .187013 against a tub floor at .124500. Both numbers here come out of the real
    // imported meshes and the real baked skin, so the assertions hold at any scale and do not
    // restate the production arithmetic.
    public sealed class SeatFloorClearanceTests
    {
        // Measured once from the imported tub at ConsistScale 1.0: the recessed floor plateau
        // sits at .150 and the seated licensed rig's deepest vertex at .143856.
        private const float FloorAtUnitScale = .150f;
        private const float SeatedClearanceAtUnitScale = .006144f;

        [Test]
        public void SeatedRider_StaysAboveTheImportedTubFloorAtTheShippedConsistScale()
        {
            GameObject rigPrefab = Resources.Load<GameObject>(CatModelCatalog.ResourcePath);
            if (rigPrefab == null)
                Assert.Ignore("The licensed local rig is absent; run this in the combined asset workspace.");
            var catalog = new CatModelCatalog(rigPrefab);
            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1), catalog.RejectionReason);

            var host = new GameObject("seat-floor-host");
            var baked = new Mesh { name = "seat-floor-bake" };
            try
            {
                ToyTrainView view = ToyTrainView.Create(host.transform, "train:seat-floor",
                    new[] { 0 }, new[] { 1 }, catalog);
                view.SyncSlot(41L, CatColor.Red);
                Assert.That(view.OriginalCarriageAdmitted, Is.True, view.CarriageFallbackReason);
                Transform carriage = view.transform.Find("Carriage");
                Transform cat = carriage.Find("Cat");
                Transform model = carriage.Find("OriginalCarriage");
                float scale = CatModelCatalog.ConsistScale;

                // 1. The wheels stay on the rails. The imported vehicle is mounted at the rail
                //    crown and bottoms out there; that contact is board geometry and must not
                //    move with the consist scale.
                Vector3[] tub = model.GetComponentsInChildren<MeshFilter>()
                    .SelectMany(filter => filter.sharedMesh.vertices.Select(vertex =>
                        carriage.InverseTransformPoint(filter.transform.TransformPoint(vertex))))
                    .ToArray();
                Assert.That(tub.Max(point => point.z),
                    Is.EqualTo(ToyTrainView.RailCrownDepth).Within(.00001f),
                    "the imported vehicle must still bottom out on the rail crown at scale "
                    + scale);

                // 2. The recessed floor is the largest coplanar plateau of interior vertices.
                //    Locate it in the mesh, then check where it landed: it belongs to the
                //    vehicle, so it rises from the rail crown as the vehicle grows.
                MeshFilter shell = model.Find("OpenShell").GetComponent<MeshFilter>();
                Vector3[] interior = shell.sharedMesh.vertices
                    .Select(vertex => carriage.InverseTransformPoint(
                        shell.transform.TransformPoint(vertex)))
                    .Where(point => Mathf.Abs(point.x) < .24f * scale
                        && Mathf.Abs(point.y) < .22f * scale)
                    .ToArray();
                Assert.That(interior, Is.Not.Empty, "the open shell must expose interior vertices");
                var plateau = interior.GroupBy(point => Mathf.Round(point.z * 10000f) / 10000f)
                    .OrderByDescending(group => group.Count()).First();
                Assert.That(plateau.Count(), Is.GreaterThanOrEqualTo(24),
                    "the floor plateau must be a real coplanar face, not a stray vertex cluster");
                float floor = plateau.Key;
                Assert.That(floor, Is.EqualTo(ToyTrainView.RailCrownDepth
                        - (ToyTrainView.RailCrownDepth - FloorAtUnitScale) * scale).Within(.0005f),
                    "the tub floor rises from the rail crown as the vehicle grows");

                // 3. The seated rider's deepest vertex, across the whole seated clip, must stay
                //    above that floor by the clearance measured at ConsistScale 1.0, scaled with
                //    the vehicle. Both bodies grew, so the gap grows with them.
                Animator animator = cat.GetComponentInChildren<Animator>(true);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                SkinnedMeshRenderer[] skins =
                    animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(skins, Has.Length.EqualTo(1),
                    "the pinned licensed artifact must expose its one measured skin");
                SkinnedMeshRenderer skin = skins[0];
                AnimationClip ride = animator.runtimeAnimatorController.animationClips
                    .Single(clip => clip.name == CatRigPresentation.RideClip);
                int samples = Mathf.Max(1, Mathf.CeilToInt(ride.length * ride.frameRate * 2f));
                float deepest = float.NegativeInfinity;
                for (int sample = 0; sample <= samples; sample++)
                {
                    animator.Play("Base Layer." + CatRigPresentation.RideClip, 0,
                        sample / (float)samples);
                    animator.Update(0f);
                    view.ApplyPresentation(CatPresentationState.RideIdle, 1f, false,
                        sample * .017f, false, 1f);
                    skin.BakeMesh(baked, true);
                    foreach (Vector3 vertex in baked.vertices)
                        deepest = Mathf.Max(deepest, carriage.InverseTransformPoint(
                            skin.transform.TransformPoint(vertex)).z);
                }
                TestContext.Out.WriteLine("SEAT_FLOOR_READBACK consistScale=" + scale.ToString("F4")
                    + " railCrown=" + tub.Max(point => point.z).ToString("F6")
                    + " floor=" + floor.ToString("F6")
                    + " riderDeepest=" + deepest.ToString("F6")
                    + " clearance=" + (floor - deepest).ToString("F6")
                    + " required=" + (SeatedClearanceAtUnitScale * scale).ToString("F6")
                    + " rideSamples=" + samples);
                Assert.That(floor - deepest,
                    Is.GreaterThanOrEqualTo(SeatedClearanceAtUnitScale * scale - .00002f),
                    "the seated licensed rig must keep its feet above the imported tub floor; "
                    + "the sink is measured from the rail crown, not from the anchor origin");
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(host);
            }
        }
    }
}
#endif
