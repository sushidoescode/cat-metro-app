using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Board;

namespace CatMetro.Tests.PlayMode
{
    public sealed class BoardLookTests
    {
        private GameRoot _root;

        [SetUp]
        public void SetUp()
        {
            GameRoot.DevSkipShippedHome = true;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.DevSkipShippedHome = false;
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            _root = null;
        }

        [UnityTest]
        public IEnumerator BoardBody_FramesL001WithAVisibleLayeredSlab()
        {
            _root = GameRoot.Launch();
            yield return null;

            var body = _root.View.transform.Find("BoardBody");
            Assert.That(body, Is.Not.Null, "the level needs a finite tabletop silhouette");
            Assert.That(body.Find("WoodTop"), Is.Not.Null, "wood is the playable surface");
            Assert.That(body.Find("CreamRim"), Is.Not.Null, "the pale rim separates top from edge");
            Assert.That(body.Find("NavyBase"), Is.Not.Null, "the dark base makes thickness readable");

            var nodes = _root.Session.Level.Dto.Nodes.ToArray();
            float minX = nodes.Min(n => n.X);
            float maxX = nodes.Max(n => n.X);
            float minY = nodes.Min(n => n.Y);
            float maxY = nodes.Max(n => n.Y);
            var top = body.Find("WoodTop");
            Assert.That(top.localPosition.x - top.localScale.x * 0.5f,
                Is.LessThanOrEqualTo(minX - 0.75f));
            Assert.That(top.localPosition.x + top.localScale.x * 0.5f,
                Is.GreaterThanOrEqualTo(maxX + 0.75f));
            Assert.That(top.localPosition.y - top.localScale.y * 0.5f,
                Is.LessThanOrEqualTo(minY - 0.75f));
            Assert.That(top.localPosition.y + top.localScale.y * 0.5f,
                Is.GreaterThanOrEqualTo(maxY + 0.75f));
        }

        [UnityTest]
        public IEnumerator BoardBody_IsDecorationOnlyAndSitsBehindGameplay()
        {
            _root = GameRoot.Launch();
            yield return null;

            var body = _root.View.transform.Find("BoardBody");
            Assert.That(body, Is.Not.Null);
            Assert.That(body.GetComponentsInChildren<Collider>(true), Is.Empty,
                "collider-free mesh parts must not intercept input or trip stripped Android builds");
            Assert.That(body.GetComponentsInChildren<MeshFilter>(true)
                    .All(filter => filter.sharedMesh != null), Is.True,
                "every collider-free board part still needs the built-in cube mesh");
            Assert.That(body.GetComponentsInChildren<BoardElementId>(true), Is.Empty,
                "decoration must stay out of the authored gameplay inventory");
            Assert.That(body.Find("WoodTop").localPosition.z, Is.GreaterThan(0.2f),
                "positive Z is behind the furthest track geometry in this board convention");
        }

        [UnityTest]
        public IEnumerator DeskSurface_ExtendsWellPastTheRaisedBoard()
        {
            _root = GameRoot.Launch();
            yield return null;

            var wood = _root.View.transform.Find("BoardBody/WoodTop");
            var desk = _root.View.transform.Find("DeskSurface/DeskTop");
            Assert.That(desk, Is.Not.Null, "the room-scale tabletop sits behind the toy board");
            Assert.That(desk.localScale.x, Is.GreaterThan(wood.localScale.x + 12f));
            Assert.That(desk.localScale.y, Is.GreaterThan(wood.localScale.y + 12f),
                "the warm desk should continue beyond the portrait frame, not end like a second board");
            var deskBounds = desk.GetComponent<Renderer>().bounds;
            var properties = new MaterialPropertyBlock();
            desk.GetComponent<Renderer>().GetPropertyBlock(properties);
            Assert.That(properties.GetTexture("_BaseMap"), Is.Not.Null,
                "the oversized desk needs subtle continuous grain without geometry seams");
            Assert.That(_root.Cam.transform.position.z + _root.Cam.nearClipPlane,
                Is.LessThan(deskBounds.min.z),
                "the tilted overscan desk must remain wholly in front of the camera near plane");
            float gameplayDepth = Vector3.Dot(
                _root.View.transform.TransformPoint(_root.View.PresentationCenterLocal)
                    - _root.Cam.transform.position,
                _root.Cam.transform.forward);
            Assert.That(gameplayDepth, Is.LessThan(24f),
                "the board must stay inside the URP asset's 25-unit main-light shadow distance");
        }

        [UnityTest]
        public IEnumerator DeskGrain_IsItsOwnBroadSheet_AndWarmthFallsOffTowardTheEdges()
        {
            _root = GameRoot.Launch();
            yield return null;

            var wood = _root.View.transform.Find("BoardBody/WoodTop").GetComponent<Renderer>();
            var desk = _root.View.transform.Find("DeskSurface/DeskTop").GetComponent<Renderer>();
            var woodProperties = new MaterialPropertyBlock();
            var deskProperties = new MaterialPropertyBlock();
            wood.GetPropertyBlock(woodProperties);
            desk.GetPropertyBlock(deskProperties);

            var deskTexture = deskProperties.GetTexture("_BaseMap") as Texture2D;
            Assert.That(deskTexture, Is.Not.Null);
            Assert.That(deskTexture, Is.Not.SameAs(woodProperties.GetTexture("_BaseMap")),
                "the desk needs its own broader grain, not the board's repeating tile");
            Vector4 st = deskProperties.GetVector("_BaseMap_ST");
            Assert.That(st.x, Is.EqualTo(1f).Within(0.001f),
                "one untiled sheet — a repeating tile cannot carry a radial falloff");
            Assert.That(st.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(deskTexture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp),
                "clamped edges keep the vignette from wrapping back to a bright seam");

            // Region averages, not single texels: the sheet now carries real grain valleys
            // and plank seams, so one texel is a lottery. The law governs the neighbourhood.
            Color centre = AverageDeskSample(deskTexture, 0.5f, 0.5f);
            Color corner = AverageDeskSample(deskTexture, 0.07f, 0.07f);
            Assert.That(centre.maxColorComponent,
                Is.GreaterThan(corner.maxColorComponent + 0.2f),
                "warmth must pool around the board and fall away toward the desk edges");
            Assert.That(centre.r - centre.b, Is.GreaterThan(corner.r - corner.b),
                "the falloff cools as it darkens, like lamp light leaving the desk");
        }

        private static Color AverageDeskSample(Texture2D texture, float u, float v)
        {
            Color sum = Color.clear;
            for (int du = -1; du <= 1; du++)
                for (int dv = -1; dv <= 1; dv++)
                    sum += texture.GetPixelBilinear(u + du * 0.04f, v + dv * 0.04f);
            return sum / 9f;
        }

        [UnityTest]
        public IEnumerator KeyLight_RakesTheTiltedBoardLikeLateAfternoon()
        {
            _root = GameRoot.Launch();
            yield return null;

            var key = _root.GetComponentsInChildren<Light>(true)
                .Single(light => light.name == "Diorama Warm Key");
            Vector3 toSource = -key.transform.forward;
            Assert.That(toSource.y, Is.GreaterThan(0.1f), "the key still hangs above the desk");
            Assert.That(toSource.x, Is.GreaterThan(0.2f),
                "late-afternoon light enters from frame right, matching the target renders");

            Vector3 boardNormal = -_root.View.transform.forward;
            float incidence = Vector3.Angle(toSource, boardNormal);
            Assert.That(incidence, Is.InRange(18f, 55f),
                "the key must rake across the tilted board — square-on light flattens every "
                + "face and collapses shadow length to nothing");

            Assert.That(key.color.r - key.color.b, Is.GreaterThan(0.3f),
                "amber key, not noon white");
            Assert.That(key.shadowStrength, Is.InRange(0.4f, 0.8f),
                "shadows must read on the desk yet stay airy");
            Assert.That(RenderSettings.ambientSkyColor.r - RenderSettings.ambientSkyColor.b,
                Is.GreaterThan(RenderSettings.ambientGroundColor.r
                    - RenderSettings.ambientGroundColor.b),
                "warm sky over cool ground keeps shaded faces from going muddy");
        }

        [UnityTest]
        public IEnumerator WarmBackground_ReplacesTheDefaultSkybox()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Cam.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(RenderSettings.skybox, Is.Null);
            Assert.That(Vector4.Distance(_root.Cam.backgroundColor,
                    new Color(0.85f, 0.81f, 0.73f)),
                Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator WorldSpacePreview_DoesNotCastIntoTheDiorama()
        {
            _root = GameRoot.Launch();
            yield return null;

            foreach (var renderer in _root.Preview.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                Assert.That(renderer.receiveShadows, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator SceneLook_RestoresGlobalEnvironmentWhenRootIsDestroyed()
        {
            Material previousSkybox = RenderSettings.skybox;
            var previousMode = RenderSettings.ambientMode;
            Color previousSky = RenderSettings.ambientSkyColor;
            Color previousEquator = RenderSettings.ambientEquatorColor;
            Color previousGround = RenderSettings.ambientGroundColor;
            float previousIntensity = RenderSettings.ambientIntensity;
            var sentinel = new Material(GreyboxMaterial.Shared.shader);
            var sentinelSky = new Color(0.11f, 0.22f, 0.33f);
            var sentinelEquator = new Color(0.17f, 0.19f, 0.21f);
            var sentinelGround = new Color(0.05f, 0.07f, 0.09f);

            try
            {
                RenderSettings.skybox = sentinel;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientSkyColor = sentinelSky;
                RenderSettings.ambientEquatorColor = sentinelEquator;
                RenderSettings.ambientGroundColor = sentinelGround;
                RenderSettings.ambientIntensity = 0.42f;

                _root = GameRoot.Launch();
                yield return null;
                Assert.That(RenderSettings.skybox, Is.Null);

                var retiringRoot = _root;
                Object.Destroy(retiringRoot.gameObject);
                _root = GameRoot.Launch();
                yield return null;
                Assert.That(retiringRoot == null, Is.True,
                    "the old root is destroyed after the replacement has acquired the look");
                Assert.That(RenderSettings.skybox, Is.Null,
                    "an overlapping live root must retain the diorama environment");
                Assert.That(RenderSettings.ambientMode,
                    Is.EqualTo(UnityEngine.Rendering.AmbientMode.Trilight));

                Object.DestroyImmediate(_root.gameObject);
                _root = null;
                Assert.That(RenderSettings.skybox, Is.SameAs(sentinel));
                Assert.That(RenderSettings.ambientMode,
                    Is.EqualTo(UnityEngine.Rendering.AmbientMode.Flat));
                Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(sentinelSky));
                Assert.That(RenderSettings.ambientEquatorColor, Is.EqualTo(sentinelEquator));
                Assert.That(RenderSettings.ambientGroundColor, Is.EqualTo(sentinelGround));
                Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(0.42f).Within(0.001f));
            }
            finally
            {
                RenderSettings.skybox = previousSkybox;
                RenderSettings.ambientMode = previousMode;
                RenderSettings.ambientSkyColor = previousSky;
                RenderSettings.ambientEquatorColor = previousEquator;
                RenderSettings.ambientGroundColor = previousGround;
                RenderSettings.ambientIntensity = previousIntensity;
                Object.DestroyImmediate(sentinel);
            }
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_BoardLook_917x2048_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_BOARD_LOOK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)
                && System.Environment.GetCommandLineArgs().Contains("-cmBoardLookCapture"))
                dir = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                    "../Library/Captures"));
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_BOARD_LOOK_CAPTURE_DIR to emit a phone frame");
                yield break;
            }

            _root = GameRoot.Launch();
            yield return null;
            yield return null;

            const int width = 917;
            const int height = 2048;
            var rt = new RenderTexture(width, height, 24);
            _root.Cam.targetTexture = rt;
            _root.Preview.Refresh();
            Canvas.ForceUpdateCanvases();
            _root.Cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            tex.Apply();
            _root.Cam.targetTexture = null;
            RenderTexture.active = null;

            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "step-2-board.png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
        }
    }
}
