using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatMetro.Application.Session;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    // The arrival is the payoff moment, and the whole EditMode geometry suite can be green while
    // the delivered cat is invisible: before ToyTrainView.PlatformBadgeClearance the station badge
    // covered 27.7% to 91.5% of it on every measured level and nothing failed. These tests render.
    //
    // They measure the cat's OWN pixels: one pass with nothing drawn establishes the background,
    // one with only the cat gives its true silhouette, one with the real scene says how much of
    // that silhouette survives, and one with the badge suppressed attributes the difference. A
    // step of zero, of the wrong sign, or too short to clear the keyline all fail here.
    public sealed class ArrivalOcclusionTests
    {
        private GameRoot _root;
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
            GameRoot.DevSkipShippedHome = _previousDevSkip;
        }

        // Moment one: the LIVE cat standing on the arrival spot through Alight, the delivery
        // walk and Celebrate. This is the point the station badge covers, and the only point the
        // badge clearance protects — a parked cat is separately protected by the queue pitch, so
        // measuring the parked cat would be insensitive to the clearance. Verified by mutation:
        // with PlatformBadgeClearance set to 0 this test fails and the parked-cat one does not.
        [UnityTest]
        public IEnumerator ArrivingCat_IsNotHiddenByItsOwnStationBadge(
            [Values("L001", "L009", "L011")] string levelId)
        {
            yield return Launch(levelId, motionOff: false);
            Transform cat = null;
            // Motion on, so the real departure track runs. A fixed capture delta keeps the
            // presentation, simulation and Animator clocks in lockstep.
            for (int frame = 0; frame < 900 && cat == null; frame++)
            {
                float dt = Time.captureDeltaTime;
                _root.Session.AdvanceMs(dt * 1000d);
                _root.View.UpdateFrom(_root.Session, (frame + 1) * dt);
                yield return null;
                foreach (Transform child in _root.View.transform)
                {
                    var view = child.GetComponent<ToyTrainView>();
                    if (view == null || view.PresentationState != CatPresentationState.Celebrate) continue;
                    Transform candidate = child.Find("Carriage/Cat");
                    if (candidate != null && candidate.gameObject.activeInHierarchy) cat = candidate;
                    if (cat != null) break;
                }
            }
            Assert.That(cat, Is.Not.Null,
                levelId + " must actually celebrate an arrival before this measures one");
            Measure(cat, levelId, out float visibleFraction, out float badgeShare);

            Assert.That(badgeShare, Is.LessThanOrEqualTo(.02f),
                $"{levelId}: the station badge covers {badgeShare:P1} of the arriving cat's own "
                + "pixels. Before ToyTrainView.PlatformBadgeClearance this was .277 to .915 on "
                + "every measured level and the whole EditMode geometry suite was green.");
            Assert.That(visibleFraction, Is.GreaterThanOrEqualTo(.80f),
                $"{levelId}: only {visibleFraction:P1} of the arriving cat is visible; the badge "
                + $"accounts for {badgeShare:P1} of the loss.");
        }

        // Moment two: successive deliveries to ONE station must read as a queue. Lane 0 used to
        // take a zero offset, so the first parked cat sat exactly where the next arrival walks
        // to and the second cat spent its whole arrival inside the first: 28.2% visible.
        [UnityTest]
        public IEnumerator SecondDeliveryToOneStation_DoesNotStandInsideTheFirst()
        {
            // L008 routes two red cats to RED once its entry lever is thrown.
            yield return Launch("L008", motionOff: true);
            Assert.That(_root.Session.EnqueueToggle(0), Is.True, "L008's entry lever");
            for (int step = 0; step < 240 && _root.Session.CatDeliveryCount < 2; step++)
            {
                _root.Session.AdvanceMs(TickInterpolator.TICK_MS);
                _root.View.UpdateFrom(_root.Session, step * .125f);
            }
            Assert.That(_root.Session.CatDeliveryCount, Is.GreaterThanOrEqualTo(2),
                "L008 must deliver two cats to the same station");
            // Let the parked queue settle into its lanes.
            for (int step = 0; step < 40; step++) _root.View.UpdateFrom(_root.Session, 30f + step * .1f);
            yield return null;

            Transform first = _root.View.transform.Find("delivered-cat:0/Carriage/Cat");
            Transform second = _root.View.transform.Find("delivered-cat:1/Carriage/Cat");
            Assert.That(first, Is.Not.Null); Assert.That(second, Is.Not.Null);
            Assert.That(first.gameObject.activeInHierarchy && second.gameObject.activeInHierarchy,
                Is.True, "both retained passengers must paint");
            Assert.That(Vector3.Distance(first.position, second.position), Is.GreaterThan(.4f),
                "two cats delivered to one station must not occupy one point");

            Measure(second, "L008/second", out float visibleFraction, out float badgeShare);
            Assert.That(visibleFraction, Is.GreaterThanOrEqualTo(.80f),
                $"the second cat delivered to one station is only {visibleFraction:P1} visible; "
                + "it used to stand inside the first at 28.2%.");
        }

        private IEnumerator Launch(string levelId, bool motionOff)
        {
            GameRoot.DevSkipShippedHome = true;
            _root = GameRoot.Launch(GameRoot.LevelPath(levelId));
            _root.MotionOffToggle = motionOff;
            _root.enabled = false;
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
            if (levelId == "L001")
                Assert.That(_root.Session.EnqueueToggle(0), Is.True,
                    "L001's authored lever input must be accepted");
        }

        private void Measure(Transform cat, string label, out float visibleFraction,
            out float badgeShare)
        {
            Camera camera = _root.Cam;
            RenderTexture target = camera.targetTexture;
            foreach (Canvas canvas in _root.GetComponentsInChildren<Canvas>(true))
                canvas.enabled = false;

            Renderer[] catRenderers = cat.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            Assert.That(catRenderers, Is.Not.Empty, label + ": the passenger must draw something");
            Renderer[] others = _root.View.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.transform.IsChildOf(cat))
                .ToArray();
            Renderer[] badge = others.Where(IsStationBadge).ToArray();
            Assert.That(badge, Is.Not.Empty, label + ": the station badge must exist");

            SetEnabled(others, false);
            SetEnabled(catRenderers, false);
            Color32[] empty = Read(camera, target);
            SetEnabled(catRenderers, true);
            Color32[] alone = Read(camera, target);
            SetEnabled(others, true);
            Color32[] scene = Read(camera, target);
            SetEnabled(badge, false);
            Color32[] withoutBadge = Read(camera, target);
            SetEnabled(badge, true);

            int silhouette = 0, visible = 0, visibleWithoutBadge = 0;
            for (int i = 0; i < alone.Length; i++)
            {
                if (Near(alone[i], empty[i])) continue;
                silhouette++;
                if (Near(alone[i], scene[i])) visible++;
                if (Near(alone[i], withoutBadge[i])) visibleWithoutBadge++;
            }
            Assert.That(silhouette, Is.GreaterThan(500),
                label + ": the free silhouette must be large enough for a fraction to mean anything");
            visibleFraction = visible / (float)silhouette;
            badgeShare = (visibleWithoutBadge - visible) / (float)silhouette;
            TestContext.Out.WriteLine($"ARRIVAL_OCCLUSION case={label} silhouette={silhouette} "
                + $"visible={visibleFraction:F4} badge_share={badgeShare:F4}");
        }

        private static bool IsStationBadge(Renderer renderer)
        {
            string name = renderer.gameObject.name;
            return name.StartsWith("station:keyline", System.StringComparison.Ordinal)
                || name.StartsWith("station:plate", System.StringComparison.Ordinal)
                || name.StartsWith("station:symbol", System.StringComparison.Ordinal)
                || name.StartsWith("station:signmast", System.StringComparison.Ordinal);
        }

        private static void SetEnabled(Renderer[] renderers, bool value)
        {
            foreach (Renderer renderer in renderers) renderer.enabled = value;
        }

        private static bool Near(Color32 a, Color32 b) =>
            Mathf.Abs(a.r - b.r) <= 6 && Mathf.Abs(a.g - b.g) <= 6 && Mathf.Abs(a.b - b.b) <= 6;

        private Color32[] Read(Camera camera, RenderTexture target)
        {
            RenderTexture active = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                return pixels.GetPixels32();
            }
            finally
            {
                RenderTexture.active = active;
                Object.DestroyImmediate(pixels);
            }
        }

        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
    }
}
