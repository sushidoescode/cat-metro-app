#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Application.Session;
using CatMetro.Bootstrap;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    // Opt-in owner diagnostic: Editor read-only mesh access, disposable scene clones only.
    // It records contact failures; a passing run is NOT a seat-fit/visual acceptance test.
    public sealed class RidingSeatAnatomyProbeTests
    {
        private const string CarriageResource = "CatMetroOriginal/OpenCarriage";
        private readonly List<Object> _owned = new List<Object>();
        private GameRoot _root;
        private float _timeScale;
        private bool _previousDevSkip, _previousForceMatrices;
        private SkinnedMeshRenderer _sampledSkin;
        private string _directory;
        private readonly List<object> _evidence = new List<object>();
        private static readonly string[] Regions =
            { "unclassified", "rear-minus-Z", "rear-plus-Z", "front-minus-Z", "front-plus-Z", "lower-torso-patch" };
        private static readonly Color[] RegionColors =
            { new Color(.16f,.19f,.21f), Color.cyan, Color.magenta, Color.yellow,
                new Color(.2f,1f,.2f), new Color(1f,.4f,.1f) };

        [TearDown]
        public void TearDown()
        {
            if (_directory != null) File.WriteAllText(Path.Combine(_directory, "seat-anatomy.json"),
                JsonConvert.SerializeObject(_evidence, Formatting.Indented));
            if (_sampledSkin != null) _sampledSkin.forceMatrixRecalculationPerRender = _previousForceMatrices;
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            foreach (Object value in _owned) if (value != null) Object.DestroyImmediate(value);
            _owned.Clear();
            GameRoot.DevSkipShippedHome = _previousDevSkip;
            Time.timeScale = _timeScale;
        }

        [UnityTest]
        public IEnumerator CaptureActualPawsAndOpenCarriage_WhenRequested()
        {
            _timeScale = Time.timeScale;
            _previousDevSkip = GameRoot.DevSkipShippedHome;
            _directory = Environment.GetEnvironmentVariable("CM_RIDING_SEAT_PROBE_DIR");
            if (string.IsNullOrEmpty(_directory)) { _directory = null; yield break; }
            Directory.CreateDirectory(_directory);
            Time.timeScale = 0f;
            GameObject original = Resources.Load<GameObject>(CarriageResource);
            Assert.That(original, Is.Not.Null, "Import the actual original carriage; no synthetic floor substitution.");
            MeshFilter sourceShell = original.transform.Find("OpenShell")?.GetComponent<MeshFilter>();
            Assert.That(sourceShell, Is.Not.Null);
            Assert.That(sourceShell.sharedMesh.isReadable, Is.True, "original owned geometry, required for real triangle queries");
            Assert.That(sourceShell.sharedMesh.triangles.Length, Is.EqualTo(700 * 3));
            var renderers = original.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(2));
            Assert.That(renderers.All(r => r.enabled && r.sharedMaterial != null
                && r.sharedMaterial.shader.name == "Universal Render Pipeline/Lit"
                && r.sharedMaterial.GetTexture("_BaseMap") != null), Is.True, "actual textured original asset");

            GameRoot.DevSkipShippedHome = true;
            _root = GameRoot.Launch(GameRoot.LevelPath("L001"));
            _root.enabled = false;
            yield return null;
            _root.View.MotionOffSource = () => true;
            double alpha = _root.Session.Alpha;
            if (alpha > 0d) _root.Session.AdvanceMs((1d - alpha) * TickInterpolator.TICK_MS);
            foreach (var particles in _root.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Place(0);
            var train = _root.View.transform.Find("train:0").GetComponent<ToyTrainView>();
            Assert.That(train.RigAdmitted, Is.True, train.RigFallbackReason);
            train.enabled = false;
            var carriage = train.transform.Find("Carriage");
            var cat = carriage.Find("Cat");
            var animator = cat.GetComponentInChildren<Animator>(true);
            var rigPresentation = animator.GetComponent<CatRigPresentation>();
            Assert.That(rigPresentation, Is.Not.Null);
            Assert.That(rigPresentation.AuthoredMotionInstalled, Is.True);
            animator.Rebind(); animator.Update(0f); animator.enabled = false;
            var skin = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            _sampledSkin = skin;
            _previousForceMatrices = skin.forceMatrixRecalculationPerRender;
            // Synchronous SampleAnimation/Camera.Render can otherwise reuse stale GPU
            // matrices while CPU/Bake report the new pose. Diagnostic only; restored below.
            skin.forceMatrixRecalculationPerRender = true;
            Assert.That(skin.sharedMesh.vertexCount, Is.EqualTo(7841));
            Transform rigRoot = animator.transform;
            while (rigRoot.parent != cat) rigRoot = rigRoot.parent;
            Vector3 baselineOffset = rigRoot.localPosition;
            Mesh sourceMesh = skin.sharedMesh;
            Vector3[] vertices = SourceVertices(sourceMesh);
            BoneWeight[] weights = sourceMesh.boneWeights;
            Assert.That(sourceMesh.GetBonesPerVertex().ToArray().All(count => count <= 4), Is.True,
                "this source uses the complete legacy four-weight representation; reject a changed source with more influences");
            Matrix4x4[] bind = sourceMesh.bindposes;
            int[] triangles = SourceTriangles(sourceMesh);
            var sourcePrefab = Resources.Load<GameObject>(CatModelCatalog.ResourcePath);
            var sourceAnimator = sourcePrefab.GetComponentInChildren<Animator>(true);
            var sourceSkin = sourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(sourceSkin.sharedMesh, Is.SameAs(sourceMesh));
            Matrix4x4 toAnimator = sourceAnimator.transform.worldToLocalMatrix * sourceSkin.transform.localToWorldMatrix;
            Vector3[] rest = vertices.Select(toAnimator.MultiplyPoint3x4).ToArray();
            int[] regions = Classify(rest, weights, skin.bones);
            int[][] soles = Enumerable.Range(0, Regions.Length).Select(region =>
            {
                int[] indices = Enumerable.Range(0, regions.Length).Where(i => regions[i] == region).ToArray();
                if (region == 0) return Array.Empty<int>();
                Assert.That(indices.Length, Is.GreaterThan(20), "actual source region: " + Regions[region]);
                float cutoff = Percentile(indices.Select(i => rest[i].y), .25f);
                return indices.Where(i => rest[i].y <= cutoff).ToArray();
            }).ToArray();
            _evidence.Add(new { kind = "source", mesh = sourceMesh.name, sourceMesh.isReadable,
                vertexCount = vertices.Length, weightCount = weights.Length,
                rendererQuality = skin.quality.ToString(), globalSkinWeights = QualitySettings.skinWeights.ToString(),
                effectiveInfluenceLimit = EffectiveInfluenceLimit(skin),
                forceMatricesForSynchronousRender = skin.forceMatrixRecalculationPerRender,
                sourceFbx = UnityEditor.AssetDatabase.GetAssetPath(sourceMesh),
                anatomy = Enumerable.Range(1, Regions.Length - 1).Select(r => new {
                    region = Regions[r], count = regions.Count(value => value == r),
                    soleCount = soles[r].Length, soleRestBounds = BoundsRecord(soles[r].Select(i => rest[i])),
                    directBoneWeights = skin.bones.Select((bone, b) => new { bone = bone.name,
                        mass = Enumerable.Range(0, regions.Length).Where(i => regions[i] == r).Sum(i => Weight(weights[i], b)) })
                        .Where(pair => pair.mass > 1f).OrderByDescending(pair => pair.mass).ToArray() }).ToArray() });

            var target = Own(new RenderTexture(917, 2048, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB) { antiAliasing = 4 });
            target.Create();
            Camera camera = _root.Cam;
            camera.targetTexture = target; camera.aspect = 917f / 2048f;
            yield return null;
            BoardSceneLook.FitCamera(camera, _root.View);
            Canvas.ForceUpdateCanvases();
            var canvases = _root.GetComponentsInChildren<Canvas>(true);
            bool[] canvasStates = canvases.Select(c => c.enabled).ToArray();
            AnimationClip ride = animator.runtimeAnimatorController.animationClips.Single(c => c.name == "Cat_Ride");
            AnimationClip celebrate = animator.runtimeAnimatorController.animationClips.Single(c => c.name == "Cat_Celebrate");
            Sample(ride, .4f, animator, rigPresentation);
            Vector3[] current = CpuWorld(skin, vertices, weights, bind);
            CheckCpuAgainstBake(skin, current);
            var phone = CameraPose.Capture(camera);
            var close = Frames(camera, animator, carriage, current, .13f);
            // Existing live carriage first. If runtime integration is installed this is labeled honestly.
            bool productionOriginal = carriage.Find("OriginalCarriage") != null;
            SaveViews("baseline-" + (productionOriginal ? "production-original" : "production-solid"),
                camera, target, canvases, canvasStates, phone, close);

            Transform wrapper = carriage.Find("OriginalCarriage");
            if (wrapper == null)
            {
                wrapper = Object.Instantiate(original, carriage, false).transform;
                wrapper.name = "SeatProbeOriginalCarriage";
                wrapper.localPosition = new Vector3(0f, 0f, .235f);
                wrapper.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                wrapper.localScale = Vector3.one;
                foreach (string name in new[] { "Body", "Chassis" })
                {
                    Transform fallback = carriage.Find(name);
                    Assert.That(fallback, Is.Not.Null, "expected existing fallback; do not hide arbitrary geometry");
                    fallback.gameObject.SetActive(false);
                }
            }
            MeshFilter shell = wrapper.Find("OpenShell").GetComponent<MeshFilter>();
            Assert.That(shell.sharedMesh, Is.SameAs(sourceShell.sharedMesh));
            Vector3[] shellVertices = shell.sharedMesh.vertices.Select(v => carriage.InverseTransformPoint(
                shell.transform.TransformPoint(v))).ToArray();
            int[] shellTriangles = shell.sharedMesh.triangles;
            ValidateActualCavity(shellVertices, shellTriangles);
            _evidence.Add(new { kind = "carriage", productionOriginal,
                mode = productionOriginal ? "production runtime wrapper" : "diagnostic wrapper using actual imported Resources prefab",
                resourcePath = UnityEditor.AssetDatabase.GetAssetPath(original),
                wrapperPosition = Vec(wrapper.localPosition), wrapperScale = Vec(wrapper.localScale),
                sourceParts = original.GetComponentsInChildren<MeshFilter>().Select(f => new { f.name,
                    triangles = f.sharedMesh.triangles.Length / 3, path = UnityEditor.AssetDatabase.GetAssetPath(f.sharedMesh) }).ToArray(),
                shellCarriageBounds = BoundsRecord(shellVertices) });

            // Empty actual carriage views expose the floor and low wall. No proxy cap/floor is created.
            bool catWasActive = cat.gameObject.activeSelf;
            cat.gameObject.SetActive(false);
            SaveViews("actual-empty-cavity", camera, target, canvases, canvasStates, phone, close);
            Transform engine = train.transform.Find("Engine");
            Transform pin = carriage.Find("Pin");
            bool engineActive = engine.gameObject.activeSelf, pinActive = pin.gameObject.activeSelf;
            engine.gameObject.SetActive(false); pin.gameObject.SetActive(false);
            var top = new CameraPose { position = carriage.TransformPoint(new Vector3(0f,0f,-3f)),
                rotation = Quaternion.LookRotation(carriage.TransformDirection(Vector3.forward),carriage.up), size = .36f };
            SaveViews("actual-empty-cavity-top-control", camera,target,canvases,canvasStates,phone,new[] {top,top});
            engine.gameObject.SetActive(engineActive); pin.gameObject.SetActive(pinActive);
            cat.gameObject.SetActive(catWasActive);
            // Also show the exact unposed source mesh selection: this refutes anatomically
            // wrong names independently of the ride's deformation and carriage occlusion.
            bool wrapperActive = wrapper.gameObject.activeSelf;
            wrapper.gameObject.SetActive(false);
            SaveAnatomy("source-rest-unoccluded", skin, carriage,
                vertices.Select(skin.transform.TransformPoint).ToArray(),
                triangles, regions, camera, target, canvases, canvasStates, phone, close);
            wrapper.gameObject.SetActive(wrapperActive);
            Sample(ride, .4f, animator, rigPresentation);
            SaveAnatomy("ride", skin, carriage, CpuWorld(skin, vertices, weights, bind),
                triangles, regions, camera, target, canvases, canvasStates, phone, close);

            var measurements = new List<Measurement>();
            // L001's three authored edges give distinct real spline headings. No manual train rotation.
            for (int edge = 0; edge < 3; edge++)
            {
                Place(edge); train.enabled = false; animator.enabled = false;
                Sample(ride, .4f, animator, rigPresentation);
                rigRoot.localPosition = baselineOffset;
                current = CpuWorld(skin, vertices, weights, bind);
                close = Frames(camera, animator, carriage, current, .13f);
                float heading = Mathf.Atan2(carriage.right.y, carriage.right.x) * Mathf.Rad2Deg;
                _evidence.Add(new { kind = "heading", edge, boardLocalRotation = Vec(carriage.localEulerAngles), worldHeading = heading });
                foreach (var spec in new[] { (ride, new[] { 0f, .4f, 1.2f }, .4f),
                    (celebrate, new[] { 0f, .22f, .48f }, .22f) })
                {
                    bool requiredSeatPose = spec.Item1 == ride;
                    string poseContext = requiredSeatPose ? "required carriage Ride"
                        : "exploratory: platform Celebrate sampled on carriage, outside production state";
                    // Preserve the old Celebrate comparison as exploration. Production
                    // Celebrate has PlatformBlend=1 and cannot constrain a carriage seat fit.
                    foreach (float seconds in spec.Item2)
                    {
                        Sample(spec.Item1, seconds, animator, rigPresentation);
                        rigRoot.localPosition = baselineOffset;
                        current = CpuWorld(skin, vertices, weights, bind);
                        CheckCpuAgainstBake(skin, current);
                        for (int region = 1; region < Regions.Length; region++)
                        {
                            var m = Measure(edge, spec.Item1.name, seconds, region, soles[region], current,
                                carriage, shellVertices, shellTriangles);
                            m.poseContext = poseContext;
                            measurements.Add(m); _evidence.Add(m);
                        }
                    }
                    var sheets = new[] { Own(new Texture2D(917 * 3, 2048, TextureFormat.RGB24, false)),
                        Own(new Texture2D(768 * 3, 768, TextureFormat.RGB24, false)),
                        Own(new Texture2D(768 * 3, 768, TextureFormat.RGB24, false)) };
                    int column = 0;
                    foreach (float down in new[] { 0f, .10f, .13f })
                    {
                        Sample(spec.Item1, spec.Item3, animator, rigPresentation);
                        // Only disposable rig wrapper translation changes. Cat/pin anchors, scale,
                        // tint, pose curves and carriage geometry remain unchanged.
                        rigRoot.localPosition = baselineOffset + Vector3.forward * down;
                        yield return null;
                        for (int view = 0; view < 3; view++)
                        {
                            ConfigureView(view, camera, target, canvases, canvasStates, phone, close);
                            var pixels = Read(camera, target);
                            sheets[view].SetPixels32(column * target.width, 0, target.width, target.height, pixels);
                        }
                        column++;
                    }
                    for (int view = 0; view < 3; view++)
                    {
                        sheets[view].Apply();
                        string name = "edge-" + edge + "-" + spec.Item1.name + "-" + new[] { "phone", "side", "front" }[view];
                        if (!requiredSeatPose) name = "exploratory-out-of-production-state-" + name;
                        string file = name + "-offsets-0-100-130mm.png";
                        File.WriteAllBytes(Path.Combine(_directory, file),
                            CaptureRig.EncodeOpaqueSrgbPng(sheets[view]));
                        _evidence.Add(new { kind = "beauty-comparison", file, clip = spec.Item1.name, poseContext });
                        Object.DestroyImmediate(sheets[view]);
                    }
                }
            }
            rigRoot.localPosition = baselineOffset;
            // Compute, but do not force, the intersection needed to bring BOTH rear soles
            // within 10 mm of a real floor without >5 mm penetration over required Ride poses.
            // The raw out-of-production-state Celebrate samples remain in the evidence above.
            var rear = measurements.Where(m => m.clip == ride.name
                && m.region.StartsWith("rear-", StringComparison.Ordinal)).ToArray();
            foreach (string side in new[] { "rear-minus-Z", "rear-plus-Z" })
                Assert.That(rear.Where(m => m.region == side).Select(m => m.edge).Distinct(),
                    Is.EquivalentTo(Enumerable.Range(0, 3)), "required seat evidence includes both rear soles at every heading");
            Assert.That(measurements.Any(m => m.clip == celebrate.name), Is.True,
                "exploratory Celebrate measurements remain available for review");
            bool allHaveFloor = rear.All(m => m.floorSamples > 0);
            float lower = allHaveFloor ? rear.Max(m => m.minimumFloorGap - .010f) : float.NaN;
            float upper = allHaveFloor ? rear.Min(m => m.minimumFloorGap + .005f) : float.NaN;
            _evidence.Add(new { kind = "candidate-offset-interval", requiredClip = ride.name,
                requiredRearSamples = rear.Length, excludedExploratoryClip = celebrate.name,
                allHaveFloor, lower, upper,
                hasCommonVerticalOffset = allHaveFloor && lower <= upper,
                warning = "Required carriage Ride only; platform Celebrate sampled on the carriage is exploratory and excluded. Anatomy mask and actual beauty frames require owner review. This interval uses minimum rear-sole gap only; rim collisions/footprint coverage and lower torso/tail remain separate constraints." });
            TestContext.Out.WriteLine("RIDING_SEAT_PROBE " + JsonConvert.SerializeObject(_evidence.Last()));
            Assert.That(skin.sharedMesh, Is.SameAs(sourceMesh));
            // Do not assert that one trial offset fits: the purpose is to expose counterexamples.
        }

        private void Place(int edge)
        {
            _root.Session.State.Trains[0] = new TrainSlot { Id = 1, Color = CatColor.Red,
                EdgeId = (short)edge, NodeId = (short)(edge == 0 ? 0 : 1), ProgressTicks = 6, State = TrainState.OnEdge };
            _root.View.UpdateFrom(_root.Session, 0f);
            _root.View.UpdateFrom(_root.Session, 2f);
            var train = _root.View.transform.Find("train:0").GetComponent<ToyTrainView>();
            train.ApplyPresentation(CatPresentationState.RideIdle, 0f, true);
        }
        private static void Sample(AnimationClip clip, float seconds, Animator animator, CatRigPresentation presentation)
        { clip.SampleAnimation(animator.gameObject, seconds); presentation.ApplyHeadShape(); }
        private static Vector3[] SourceVertices(Mesh mesh)
        {
            using (var data = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh))
            using (var values = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
            { data[0].GetVertices(values); return values.ToArray(); }
        }
        private static int[] SourceTriangles(Mesh mesh)
        {
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            using (var data = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh))
            using (var values = new NativeArray<int>((int)mesh.GetIndexCount(0), Allocator.Temp))
            { data[0].GetIndices(values, 0); return values.ToArray(); }
        }
        private static int[] Classify(Vector3[] rest, BoneWeight[] weights, Transform[] bones)
        {
            int[] indices(string[] names) => names.Select(n => Array.FindIndex(bones, b => b.name == n)).ToArray();
            int[] rearA = indices(new[] { "tripo::1_Left_Limb_0", "tripo::1_Left_Limb_1", "tripo::1_Right_Limb_0" });
            int[] rearB = indices(new[] { "bone_28", "bone_29" });
            int[] front = indices(new[] { "tripo::0_Left_Limb_0", "tripo::0_Left_Limb_1", "tripo::0_Left_Limb_2", "tripo::0_Left_Limb_3" });
            int torso = Array.FindIndex(bones, b => b.name == "tripo::Head_0");
            Assert.That(rearA.Concat(rearB).Concat(front).Append(torso).All(i => i >= 0), Is.True);
            var result = new int[rest.Length];
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 p = rest[i];
                if (p.y < .11f && p.x < -.22f && front.Sum(b => Weight(weights[i], b)) >= .25f)
                    result[i] = p.z < 0f ? 3 : 4;
                else if (p.y < .11f && p.x >= -.22f && p.x < .12f)
                {
                    if (p.z < 0f && rearA.Sum(b => Weight(weights[i], b)) >= .25f) result[i] = 1;
                    if (p.z >= 0f && rearB.Sum(b => Weight(weights[i], b)) >= .25f) result[i] = 2;
                }
                else if (p.y >= .11f && p.y < .27f && p.x > -.18f && p.x < .12f
                    && Weight(weights[i], torso) >= .5f) result[i] = 5;
            }
            return result;
        }
        private static Vector3[] CpuWorld(SkinnedMeshRenderer skin, Vector3[] vertices, BoneWeight[] weights, Matrix4x4[] bind)
        {
            Matrix4x4[] matrices = skin.bones.Select((bone, b) => bone.localToWorldMatrix * bind[b]).ToArray();
            int limit = EffectiveInfluenceLimit(skin);
            // Native Auto/TwoBones bakes the strongest two influences after renormalizing.
            // Membership above still uses all source weights; only posed world positions
            // follow the renderer's actual policy. No quality or source data is changed.
            return vertices.Select((v, i) =>
            {
                BoneWeight w = weights[i];
                var selected = new[] { (bone: w.boneIndex0, weight: w.weight0),
                    (bone: w.boneIndex1, weight: w.weight1), (bone: w.boneIndex2, weight: w.weight2),
                    (bone: w.boneIndex3, weight: w.weight3) }
                    .Where(value => value.weight > 0f).OrderByDescending(value => value.weight).Take(limit).ToArray();
                float mass = selected.Sum(value => value.weight);
                if (!(mass > 0f)) throw new InvalidOperationException("Source vertex has no retained skin influence: " + i);
                Vector3 world = Vector3.zero;
                foreach (var influence in selected)
                    world += matrices[influence.bone].MultiplyPoint3x4(v) * influence.weight;
                return world / mass;
            }).ToArray();
        }
        private static int EffectiveInfluenceLimit(SkinnedMeshRenderer skin)
        {
            switch (skin.quality)
            {
                case SkinQuality.Bone1: return 1;
                case SkinQuality.Bone2: return 2;
                case SkinQuality.Bone4: return 4;
                case SkinQuality.Auto:
                    switch (QualitySettings.skinWeights)
                    {
                        case SkinWeights.OneBone: return 1;
                        case SkinWeights.TwoBones: return 2;
                        case SkinWeights.FourBones:
                        case SkinWeights.Unlimited: return 4; // Complete source representation asserted above.
                        default: throw new InvalidOperationException("Unknown global skin influence policy.");
                    }
                default: throw new InvalidOperationException("Unknown renderer skin influence policy.");
            }
        }
        private static void CheckCpuAgainstBake(SkinnedMeshRenderer skin, Vector3[] cpu)
        {
            var baked = new Mesh();
            try { skin.BakeMesh(baked, true); float error = baked.vertices.Select((v, i) =>
                    Vector3.Distance(skin.transform.TransformPoint(v), cpu[i])).Max();
                TestContext.Out.WriteLine("SEAT_EFFECTIVE_CPU_BAKE " + JsonConvert.SerializeObject(new {
                    limit = EffectiveInfluenceLimit(skin), maximumWorldError = error, tolerance = .0001f }));
                Assert.That(error, Is.LessThan(.0001f), "independent CPU source vertices * bone * bindpose versus actual baked skin"); }
            finally { Object.DestroyImmediate(baked); }
        }
        private static float Weight(BoneWeight w, int b) => (w.boneIndex0 == b ? w.weight0 : 0f)
            + (w.boneIndex1 == b ? w.weight1 : 0f) + (w.boneIndex2 == b ? w.weight2 : 0f)
            + (w.boneIndex3 == b ? w.weight3 : 0f);
        private sealed class Measurement
        {
            public string kind = "sole-contact", clip, region, poseContext;
            public int edge, soleSamples, floorSamples, rimOrWallSamples, outsideSamples;
            public float seconds, minimumFloorGap, medianFloorGap, minimumRimGap;
            public object carriageLocalBounds;
        }
        private static Measurement Measure(int edge, string clip, float seconds, int region, int[] indices,
            Vector3[] world, Transform carriage, Vector3[] shell, int[] triangles)
        {
            Vector3[] points = indices.Select(i => carriage.InverseTransformPoint(world[i])).ToArray();
            var floor = new List<float>(); var rim = new List<float>(); int outside = 0;
            foreach (Vector3 p in points)
            {
                if (!Ray(shell, triangles, new Vector3(p.x, p.y, -1f), Vector3.forward, out Vector3 hit)) { outside++; continue; }
                if (Mathf.Abs(hit.z - .150f) < .001f) floor.Add(hit.z - p.z);
                else rim.Add(hit.z - p.z);
            }
            return new Measurement { edge = edge, clip = clip, seconds = seconds, region = Regions[region],
                soleSamples = points.Length, floorSamples = floor.Count, rimOrWallSamples = rim.Count, outsideSamples = outside,
                minimumFloorGap = floor.Count == 0 ? float.NaN : floor.Min(), medianFloorGap = Percentile(floor, .5f),
                minimumRimGap = rim.Count == 0 ? float.NaN : rim.Min(), carriageLocalBounds = BoundsRecord(points) };
        }
        private void ValidateActualCavity(Vector3[] vertices, int[] triangles)
        {
            foreach (float x in new[] { -.15f, 0f, .15f }) foreach (float y in new[] { -.13f, 0f, .13f })
            {
                Assert.That(Ray(vertices, triangles, new Vector3(x, y, -1f), Vector3.forward, out Vector3 hit), Is.True);
                Assert.That(hit.z, Is.EqualTo(.150f).Within(.0002f), "actual recessed floor: a solid slab at rim height fails");
            }
            foreach (Vector2 p in new[] { new Vector2(.245f,0f), new Vector2(-.245f,0f), new Vector2(0f,.225f), new Vector2(0f,-.225f) })
            {
                Assert.That(Ray(vertices, triangles, new Vector3(p.x,p.y,-1f), Vector3.forward, out Vector3 hit), Is.True);
                Assert.That(hit.z, Is.EqualTo(.070f).Within(.0002f), "actual rim, distinct from floor");
            }
            Assert.That(Ray(vertices, triangles, new Vector3(0f,0f,.10f), Vector3.right, out Vector3 wall), Is.True);
            Assert.That(wall.x, Is.InRange(.20f,.261f), "real inner wall at cavity depth");
            Assert.That(Ray(vertices, triangles, new Vector3(0f,0f,.05f), Vector3.right, out _), Is.False,
                "negative control above actual open rim must miss; no artificial lid");
            _evidence.Add(new { kind = "actual-cavity-controls", floorRays = 9, rimRays = 4,
                innerWall = Vec(wall), openAboveRim = true });
        }
        private static bool Ray(Vector3[] vertices, int[] triangles, Vector3 origin, Vector3 direction, out Vector3 hit)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], e1 = vertices[triangles[i+1]] - a, e2 = vertices[triangles[i+2]] - a;
                Vector3 p = Vector3.Cross(direction,e2); float d = Vector3.Dot(e1,p);
                if (Mathf.Abs(d) < 1e-8f) continue;
                Vector3 t = origin - a; float u = Vector3.Dot(t,p)/d;
                if (u < -1e-6f || u > 1.000001f) continue;
                Vector3 q = Vector3.Cross(t,e1); float v = Vector3.Dot(direction,q)/d;
                if (v < -1e-6f || u+v > 1.000001f) continue;
                float distance = Vector3.Dot(e2,q)/d;
                if (distance >= 0f) nearest = Mathf.Min(nearest,distance);
            }
            hit = origin + direction * nearest;
            return float.IsFinite(nearest);
        }
        private void SaveAnatomy(string name, SkinnedMeshRenderer skin, Transform carriage, Vector3[] world,
            int[] triangles, int[] regions, Camera camera, RenderTexture target, Canvas[] canvases, bool[] states,
            CameraPose phone, CameraPose[] close)
        {
            var host = new GameObject("Seat probe anatomy surface"); host.transform.SetParent(carriage, false);
            Mesh mesh = Own(new Mesh()); mesh.vertices = world.Select(carriage.InverseTransformPoint).ToArray();
            mesh.subMeshCount = Regions.Length;
            var buckets = Enumerable.Range(0, Regions.Length).Select(_ => new List<int>()).ToArray();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = regions[triangles[i]], b = regions[triangles[i+1]], c = regions[triangles[i+2]];
                int region = a == b || a == c ? a : b == c ? b : 0;
                buckets[region].AddRange(new[] { triangles[i],triangles[i+1],triangles[i+2] });
            }
            for (int i = 0; i < buckets.Length; i++) mesh.SetTriangles(buckets[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            host.AddComponent<MeshRenderer>().sharedMaterials = RegionColors.Select(color => {
                var m = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit"))); m.SetColor("_BaseColor",color); return m; }).ToArray();
            skin.enabled = false;
            try { SaveViews("anatomy-" + name, camera,target,canvases,states,phone,close); }
            finally { skin.enabled = true; Object.DestroyImmediate(host); }
        }
        private struct CameraPose
        {
            public Vector3 position; public Quaternion rotation; public float size;
            public static CameraPose Capture(Camera camera) => new CameraPose { position = camera.transform.position, rotation = camera.transform.rotation, size = camera.orthographicSize };
            public void Apply(Camera camera) { camera.transform.SetPositionAndRotation(position,rotation); camera.orthographicSize = size; }
        }
        private static CameraPose[] Frames(Camera camera, Animator animator, Transform carriage, Vector3[] world, float maxDown)
        {
            Vector3 up = carriage.TransformDirection(Vector3.back);
            Vector3 front = animator.transform.TransformDirection(Vector3.left).normalized;
            Vector3 side = Vector3.Cross(up,front).normalized;
            var all = world.Concat(world.Select(v => v - up * maxDown)).Concat(carriage.GetComponentsInChildren<MeshFilter>()
                .Where(f => f.GetComponent<MeshRenderer>()?.enabled == true && !f.transform.IsChildOf(carriage.Find("Cat")))
                .SelectMany(f => f.sharedMesh.vertices.Select(f.transform.TransformPoint))).ToArray();
            Bounds bounds = new Bounds(all[0],Vector3.zero); foreach (var p in all) bounds.Encapsulate(p);
            var result = new List<CameraPose>();
            foreach (var direction in new[] { side, front })
            {
                Quaternion rotation = Quaternion.LookRotation(-direction,up);
                var points = all.Select(p => Quaternion.Inverse(rotation) * (p-bounds.center)).ToArray();
                float size = Mathf.Max(points.Max(p => Mathf.Abs(p.x)),points.Max(p => Mathf.Abs(p.y))) * 1.12f;
                result.Add(new CameraPose { position = bounds.center + direction * 3f,rotation = rotation,size = size });
            }
            return result.ToArray();
        }
        private static void ConfigureView(int view, Camera camera, RenderTexture target, Canvas[] canvases, bool[] states,
            CameraPose phone, CameraPose[] close)
        {
            camera.targetTexture = null; target.Release(); target.width = view == 0 ? 917 : 768;
            target.height = view == 0 ? 2048 : 768; target.Create(); camera.targetTexture = target;
            camera.aspect = (float)target.width / target.height;
            for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = view == 0 && states[i];
            if (view == 0) phone.Apply(camera); else close[view-1].Apply(camera);
            camera.nearClipPlane = .01f; camera.farClipPlane = 100f;
            Canvas.ForceUpdateCanvases();
        }
        private void SaveViews(string prefix, Camera camera, RenderTexture target, Canvas[] canvases, bool[] states,
            CameraPose phone, CameraPose[] close)
        {
            for (int view = 0; view < 3; view++)
            {
                ConfigureView(view,camera,target,canvases,states,phone,close);
                Texture2D image = Own(new Texture2D(target.width,target.height,TextureFormat.RGB24,false));
                image.SetPixels32(Read(camera,target)); image.Apply();
                File.WriteAllBytes(Path.Combine(_directory,prefix+"-"+new[] { "phone","side","front" }[view]+".png"),CaptureRig.EncodeOpaqueSrgbPng(image));
                Object.DestroyImmediate(image);
            }
        }
        private static Color32[] Read(Camera camera, RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active; Texture2D image = null;
            try { camera.Render(); RenderTexture.active = target; image = CaptureRig.ReadRgb24(target); return image.GetPixels32(); }
            finally { RenderTexture.active = previous; if (image != null) Object.DestroyImmediate(image); }
        }
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
        private static float Percentile(IEnumerable<float> values, float p)
        { float[] ordered = values.OrderBy(v => v).ToArray(); return ordered.Length == 0 ? float.NaN : ordered[Mathf.RoundToInt((ordered.Length-1)*p)]; }
        private static float[] Vec(Vector3 v) => new[] {v.x,v.y,v.z};
        private static object BoundsRecord(IEnumerable<Vector3> vertices)
        { Vector3[] p = vertices.ToArray(); return new { min = new[] {p.Min(v=>v.x),p.Min(v=>v.y),p.Min(v=>v.z)}, max = new[] {p.Max(v=>v.x),p.Max(v=>v.y),p.Max(v=>v.z)} }; }
    }
}
#endif
