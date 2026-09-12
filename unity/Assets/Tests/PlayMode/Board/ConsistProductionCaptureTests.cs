using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Screens;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    // Production evidence for the consist scale, opt-in via CM_CONSIST_PRODUCTION_DIR.
    //
    // Deliberately NOT a probe that scales instances after the fact: it runs the shipped
    // GameRoot loop with the shipped ConsistScale, so every derived constant -- the seat sink,
    // the platform offset, the badge step, the delivered-queue pitch, the framing half-width --
    // is exercised the way the player gets them. The UI is left ON, because the composition
    // question is what the phone actually shows. Time.captureDeltaTime pins presentation, the
    // simulation and the Animator to one clock, so frame N is the same moment of the same
    // animation at any consist scale and the two runs can be compared frame for frame.
    //
    // Alongside the frames it records, per frame, the seated rider's depth against the imported
    // tub floor and the stride of its baked feet in MODEL units -- which are scale-free, so a
    // rider whose legs stopped moving or whose seat went rigid shows up as a number, not as a
    // claim that the animation is "preserved by construction".
    public sealed class ConsistProductionCaptureTests
    {
        private static readonly Rect PhoneSafe = new Rect(0, 64, 917, 1920);
        // Measured from this same capture at ConsistScale 1.0 on L001/L008/L009.
        private const float LivePlaybackFloorDipAtUnitScale = -.004484f;
        private GameRoot _root;
        private bool _reportedChrome;
        private bool _previousDevSkip;
        private float _previousCaptureDelta;
        private readonly List<Object> _owned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _previousDevSkip = GameRoot.DevSkipShippedHome;
            _previousCaptureDelta = Time.captureDeltaTime;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = _previousCaptureDelta;
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            foreach (Object value in _owned) if (value != null) Object.DestroyImmediate(value);
            _owned.Clear();
            _reportedChrome = false;
            GameRoot.DevSkipShippedHome = _previousDevSkip;
        }

        [UnityTest]
        // L001 sparse, L009 crowded, L008 successive arrivals to one station, then the measured
        // extremes of the corpus: L048 is the largest board (12.0 x 24.99 units, 11 nodes) so the
        // camera pulls furthest out and the rider is smallest in frame; L036 is the narrowest and
        // tallest (3.2 x 11.76, aspect .27) so the rider is widest relative to frame; L052 carries
        // the most cats (6) for five deliveries across two stations; L016 has the most stations
        // (4); L043 is a large board that is also crowded. Every level's stations already sit on a
        // board extremum, so "edge station" does not discriminate between them.
        public IEnumerator CaptureProductionConsist_WhenRequested(
            [Values("L001", "L009", "L008", "L048", "L036", "L052", "L016", "L043")] string levelId)
        {
            string directory = Environment.GetEnvironmentVariable("CM_CONSIST_PRODUCTION_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                Assert.Pass("consist production capture disarmed");
                yield break;
            }
            Assert.That(CatModelCatalog.LoadResources().AdmittedEntryCount, Is.EqualTo(1),
                "armed production captures require the main checkout's admitted licensed rig");
            string stem = "k" + CatModelCatalog.ConsistScale.ToString("0.00", CultureInfo.InvariantCulture)
                .Replace(".", "");
            string levelDirectory = Path.Combine(directory, stem + "-" + levelId);
            Directory.CreateDirectory(levelDirectory);

            GameRoot.DevSkipShippedHome = true;
            _root = GameRoot.Launch(GameRoot.LevelPath(levelId));
            _root.MotionOffToggle = false;   // real motion: the feet have to actually move
            yield return null;
            Time.captureDeltaTime = 1f / 60f;
            var target = Own(new RenderTexture(917, 2048, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB));
            target.Create();
            Camera camera = _root.Cam;
            camera.targetTexture = target;
            camera.aspect = 917f / 2048f;
            yield return null;
            BoardSceneLook.FitCamera(camera, _root.View);
            if (_root.Intro != null && _root.Intro.IsVisible)
            {
                _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
                yield return null;
            }
            Assert.That(_root.Session.EnqueueToggle(0), Is.True,
                levelId + "'s authored entry lever must be accepted");

            float floor = float.NaN;   // read off the real tub once a consist exists
            var rows = new List<RiderSample>();
            var captured = new HashSet<string>();
            int deliveredSeen = 0;

            for (int frame = 0; frame < 1200; frame++)
            {
                yield return null;   // the shipped GameRoot.Update drives everything
                Transform live = LiveRider();
                string phase = "none";
                if (live != null)
                {
                    if (float.IsNaN(floor)) floor = MeasureTubFloor();
                    var view = live.GetComponentInParent<ToyTrainView>();
                    phase = view.PresentationState.ToString();
                    rows.Add(SampleRider(frame, view, live, floor));
                }
                int delivered = _root.View.transform.Cast<Transform>()
                    .Count(child => child.name.StartsWith("delivered-cat:", StringComparison.Ordinal));

                // One frame per named moment, plus a slow cadence, plus the moment a second cat
                // joins an already-occupied station. Frame indices are the matching key between
                // the two consist scales.
                bool wanted = false;
                if (phase != "none" && captured.Add("phase-" + phase)) wanted = true;
                if (delivered > deliveredSeen)
                {
                    deliveredSeen = delivered;
                    if (captured.Add("delivered-" + delivered)) wanted = true;
                }
                if (frame % 150 == 0 && captured.Add("cadence-" + frame)) wanted = true;
                if (wanted)
                    yield return Capture(levelDirectory, string.Format(CultureInfo.InvariantCulture,
                        "{0}-{1}-f{2:D4}-{3}-d{4}.png", stem, levelId, frame, phase, delivered));
            }
            // The celebration, held long enough for the beat and the win chrome to be up.
            yield return Capture(levelDirectory, stem + "-" + levelId + "-final-" + _root.ScreenState + ".png");

            float[] walk = rows.Where(row => row.state == "Walk")
                .Select(row => row.footStrideModelUnits).ToArray();
            RiderSample[] seated = rows.Where(row => row.seatWeightDepth > 0f).ToArray();
            var summary = new
            {
                level = levelId,
                consistScale = CatModelCatalog.ConsistScale,
                presenterScale = CatModelCatalog.PresenterScale,
                platformSideOffset = ToyTrainView.PlatformSideOffset,
                platformBadgeClearance = ToyTrainView.PlatformBadgeClearance,
                deliveredQueueSpacing = ToyTrainView.PlatformDeliveredQueueSpacing,
                seatDepth = ToyTrainView.OpenCarriageSeatDepth,
                tubFloor = floor,
                floorClearanceAtUnitScale = .006144f,
                livePlaybackDipAtUnitScale = LivePlaybackFloorDipAtUnitScale,
                frames = rows.Count,
                deliveries = deliveredSeen,
                screenState = _root.ScreenState,
                walkSamples = walk.Length,
                footStrideMin = walk.Length == 0 ? 0f : walk.Min(),
                footStrideMax = walk.Length == 0 ? 0f : walk.Max(),
                seatedSamples = seated.Length,
                minimumFloorClearance = seated.Length == 0
                    ? 0f : seated.Select(row => row.floorClearance).Min(),
            };
            File.WriteAllText(Path.Combine(levelDirectory, "consist-production.json"),
                JsonConvert.SerializeObject(new { summary, rows }, Formatting.Indented));
            TestContext.Out.WriteLine("CONSIST_PRODUCTION "
                + JsonConvert.SerializeObject(summary));

            Assert.That(rows.Count, Is.GreaterThan(60), levelId + " must render a live rider");
            Assert.That(walk.Length, Is.GreaterThanOrEqualTo(4),
                levelId + " must actually walk a cat, or the stride number means nothing");
            Assert.That(walk.Max() - walk.Min(), Is.GreaterThan(.05f),
                levelId + " walk stride never changed: the feet are not moving");
            // Live playback goes marginally DEEPER than the bare Ride clip does: the micro-motion
            // breathing pose puts the licensed rig's lowest vertex a hair under the modelled tub
            // floor plane, hidden behind the tub walls. That is shipped behaviour, not something
            // the consist scale introduced -- the same capture at ConsistScale 1.0 measures
            // -0.004483 and at 1.30 measures -0.005826, a ratio of 1.2996. So the law is that it
            // must not get proportionally WORSE, which a mis-anchored seat sink immediately would:
            // scaling .0983 directly puts this at -0.0625 instead.
            if (seated.Length > 0)
                Assert.That(summary.minimumFloorClearance, Is.GreaterThanOrEqualTo(
                        LivePlaybackFloorDipAtUnitScale * CatModelCatalog.ConsistScale - .0001f),
                    levelId + " seated rider sank further through the imported tub floor than the "
                    + "measured ConsistScale 1.0 playback does, scaled");
        }

        // The recessed floor of the imported tub, in Carriage-local board units, read off the
        // real mesh rather than assumed. Returns NaN when the fallback primitive carriage is up.
        private float MeasureTubFloor()
        {
            foreach (Transform child in _root.View.transform)
            {
                Transform carriage = child.Find("Carriage");
                Transform model = carriage == null ? null : carriage.Find("OriginalCarriage");
                MeshFilter shell = model == null ? null : model.Find("OpenShell")?.GetComponent<MeshFilter>();
                if (shell == null) continue;
                float scale = CatModelCatalog.ConsistScale;
                Vector3[] interior = shell.sharedMesh.vertices
                    .Select(vertex => carriage.InverseTransformPoint(
                        shell.transform.TransformPoint(vertex)))
                    .Where(point => Mathf.Abs(point.x) < .24f * scale
                        && Mathf.Abs(point.y) < .22f * scale)
                    .ToArray();
                if (interior.Length == 0) continue;
                return interior.GroupBy(point => Mathf.Round(point.z * 10000f) / 10000f)
                    .OrderByDescending(group => group.Count()).First().Key;
            }
            return float.NaN;
        }

        // Follow the most INTERESTING rider on the board, not simply the first one. On a large
        // level the first consist can sit parked at a source for the whole capture, which both
        // starves the stride measurement and means the frames never catch a boarding or an
        // arrival. Ranking by presentation state puts the probe on the cat the player is
        // actually watching.
        private static int Interest(CatPresentationState state) => state switch
        {
            CatPresentationState.Celebrate => 5,
            CatPresentationState.Alight => 4,
            CatPresentationState.Board => 3,
            CatPresentationState.Walk => 2,
            CatPresentationState.RideIdle => 1,
            _ => 0,
        };

        private Transform LiveRider()
        {
            Transform best = null;
            int bestInterest = -1;
            foreach (Transform child in _root.View.transform)
            {
                if (!child.name.StartsWith("train:", StringComparison.Ordinal)) continue;
                var view = child.GetComponent<ToyTrainView>();
                if (view == null || !view.RigAdmitted) continue;
                Transform cat = child.Find("Carriage/Cat");
                if (cat == null || !cat.gameObject.activeInHierarchy) continue;
                int interest = Interest(view.PresentationState);
                if (interest <= bestInterest) continue;
                bestInterest = interest;
                best = cat;
            }
            return best;
        }

        private sealed class RiderSample
        {
            public int frame;
            public string state;
            public float seatWeightDepth;
            public float riderDeepestZ;
            public float floorClearance;
            public float footStrideModelUnits;
            public float catBoardX;
            public float catBoardY;
        }

        private RiderSample SampleRider(int frame, ToyTrainView view, Transform cat, float floor)
        {
            Transform carriage = cat.parent;
            Animator animator = cat.GetComponentInChildren<Animator>(true);
            SkinnedMeshRenderer skin = animator == null
                ? null : animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
            Transform rig = skin == null ? null : skin.transform;
            while (rig != null && rig.parent != null && rig.parent != cat) rig = rig.parent;
            float deepest = float.NaN, stride = 0f;
            if (skin != null)
            {
                var baked = new Mesh();
                try
                {
                    skin.BakeMesh(baked, true);
                    Vector3[] vertices = baked.vertices;
                    float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
                    var model = new Vector3[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 world = skin.transform.TransformPoint(vertices[i]);
                        model[i] = rig.InverseTransformPoint(world);
                        if (model[i].y < lowest) lowest = model[i].y;
                        if (model[i].y > highest) highest = model[i].y;
                        float depth = carriage.InverseTransformPoint(world).z;
                        if (float.IsNaN(deepest) || depth > deepest) deepest = depth;
                    }
                    // The feet: the lowest eighth of the bind-space body. Their fore-aft spread
                    // is the stride, in MODEL units, so it does not move with the consist scale.
                    float band = lowest + (highest - lowest) * .125f;
                    float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
                    foreach (Vector3 point in model)
                    {
                        if (point.y > band) continue;
                        if (point.z < minZ) minZ = point.z;
                        if (point.z > maxZ) maxZ = point.z;
                    }
                    if (maxZ > minZ) stride = maxZ - minZ;
                }
                finally { Object.DestroyImmediate(baked); }
            }
            Vector3 board = _root.View.transform.InverseTransformPoint(cat.position);
            return new RiderSample
            {
                frame = frame,
                state = view.PresentationState.ToString(),
                seatWeightDepth = rig == null ? 0f : rig.localPosition.z,
                riderDeepestZ = deepest,
                floorClearance = float.IsNaN(floor) || float.IsNaN(deepest)
                    ? float.NaN : floor - deepest,
                footStrideModelUnits = stride,
                catBoardX = board.x,
                catBoardY = board.y,
            };
        }

        private IEnumerator Capture(string directory, string name)
        {
            Camera camera = _root.Cam;
            RenderTexture target = camera.targetTexture;
            // Settle the frame FIRST, then lay the chrome out, then render without yielding
            // again. Order matters: WavePreviewStrip re-lays itself out in LateUpdate from
            // Screen.dpi * (canvasHeight / Screen.height), and in batchmode those are the
            // headless Game view's numbers rather than this 917x2048 render target's -- so the
            // counter text ends up sized against one ruler and the capsule against another, and
            // the two counters collide. Yielding after the injection hands the frame back to
            // that LateUpdate and the injection is lost. BoardLookTests renders in the same
            // frame it lays out in, for exactly this reason.
            yield return null;
            _root.Preview?.LayoutForViewport(PhoneSafe, 408f);
            _root.Intro?.LayoutForViewport(PhoneSafe, 408f);
            _root.Banner?.LayoutForViewport(PhoneSafe, 408f);
            _root.GetComponent<ResultsPanel>()?.LayoutForViewport(PhoneSafe, 408f);
            _root.GetComponent<ScreenChromeController>()?.Cta?.LayoutForViewport(PhoneSafe, 408f);
            Canvas.ForceUpdateCanvases();
            if (!_reportedChrome && _root.Preview != null)
            {
                _reportedChrome = true;
                Rect capsule = WavePreviewStrip.CapsuleRect(PhoneSafe, 408f);
                TestContext.Out.WriteLine("CONSIST_CHROME injectedSafeArea=" + PhoneSafe
                    + " injectedDpi=408 capsule=" + capsule
                    + " screen=" + Screen.width + "x" + Screen.height
                    + " screenDpi=" + Screen.dpi
                    + " target=" + target.width + "x" + target.height);
            }
            RenderTexture active = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = active; Object.DestroyImmediate(pixels); }
        }

        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
    }
}
