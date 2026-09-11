using System.Collections;
using System.Linq;
using System.Threading;
using CatMetro.Application.Session;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class BoardReadabilityTests
    {
        private GameRoot _root;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 0f;
            GameRoot.DevSkipShippedHome = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            if (_host != null) Object.DestroyImmediate(_host);
            GameRoot.DevSkipShippedHome = false;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator L009_SquareRider_IsRedAndCarriesASquareMesh_NoFloatingLetters()
        {
            _root = GameRoot.LaunchWith(ReadLevel("L009"));
            yield return null;
            _root.View.MotionOffSource = () => true;
            for (int tick = 0; tick < 34; tick++)
            {
                if (tick == 30) Assert.That(_root.Session.EnqueueToggle(0), Is.True);
                _root.Session.AdvanceMs(TickInterpolator.TICK_MS);
                _root.View.UpdateFrom(_root.Session, tick * 0.1f);
            }
            int slot = System.Array.FindIndex(_root.Session.State.Trains, t =>
                t.State == TrainState.OnEdge && CatToken.Shape(t.Color) == CatShape.Square);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "the real L009 square wave must be riding");
            var train = _root.View.transform.Find("train:" + slot).GetComponent<ToyTrainView>();
            foreach (var toy in _root.View.GetComponentsInChildren<ToyTrainView>(true))
            {
                Assert.That(toy.CatTint, Is.Not.EqualTo(Color.magenta));
                var paint = new MaterialPropertyBlock();
                toy.transform.Find("Carriage/Cat/Head").GetComponent<Renderer>().GetPropertyBlock(paint);
                Assert.That(paint.GetColor("_BaseColor"), Is.Not.EqualTo(Color.magenta));
            }
            var symbol = train.transform.Find("Carriage/Pin/Symbol");
            Assert.That(symbol.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(DestinationShapeMesh.ForShape(DestinationShape.Square)));
            Assert.That(train.PinShape, Is.EqualTo(DestinationShape.Square));
            var pinPaint = new MaterialPropertyBlock();
            symbol.GetComponent<Renderer>().GetPropertyBlock(pinPaint);
            Assert.That(Vector4.Distance(pinPaint.GetColor("_BaseColor"), Palette.SignalRed), Is.LessThan(0.001f));
            Assert.That(train.GetComponentsInChildren<TextMesh>(true), Is.Empty);
        }

        [UnityTest]
        public IEnumerator L009_DeliveredPassenger_PreservesTheRidersTintShapeAndExpressMark(
            [Values(CatShape.Square, CatShape.Triangle)] byte shape,
            [Values(false, true)] bool express)
        {
            string authoredShape = shape == CatShape.Square ? "square" : "triangle";
            DestinationShape expectedShape = shape == CatShape.Square
                ? DestinationShape.Square : DestinationShape.Triangle;
            var bytes = new StreamingAssetsContentSource().ReadAsync(
                "content/levels/L009.json", CancellationToken.None).GetAwaiter().GetResult();
            var json = Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes));
            var wave = json["waves"].Single(w => (string)w["shape"] == authoredShape);
            Assert.That((string)wave["color"], Is.EqualTo("red"));
            Assert.That((bool?)wave["stray"] ?? false, Is.False,
                "strays never deliver; a retained stray would be an impossible simulation fixture");
            // Keep the authored routes/timing/shape; the second case adds the independent
            // express flag to prove that the delivered ledger preserves more than colour.
            wave["express"] = express;
            var imported = LevelImporter.Import(System.Text.Encoding.UTF8.GetBytes(json.ToString()));
            Assert.That(imported.Ok, Is.True, imported.Ok ? "" : imported.Error.ToString());
            _root = GameRoot.LaunchWith(imported.Value);
            _root.MotionOffToggle = true; // Use the immediate, still-visible retained handoff.
            yield return null;

            int expectedDeliveries = shape == CatShape.Square ? 2 : 3;
            byte expectedToken = CatToken.Pack(CatColor.Red, shape, false, express);
            bool sawRider = false;
            for (int sample = 0; sample < 100 && _root.Session.CatDeliveryCount < expectedDeliveries; sample++)
            {
                // The first cat has entered ROUND before tick 30; the square has entered
                // SQUARE before tick 50. These are real accepted inputs on authored L009.
                if (_root.Session.State.Tick == 30 || _root.Session.State.Tick == 50)
                    Assert.That(_root.Session.EnqueueToggle(0), Is.True);
                _root.Session.AdvanceMs(TickInterpolator.TICK_MS);
                _root.View.UpdateFrom(_root.Session, sample * .1f);
                for (int slot = 0; slot < _root.Session.State.Trains.Length; slot++)
                {
                    var rider = _root.Session.State.Trains[slot];
                    if (rider.Id == 0 || rider.State == TrainState.None || rider.Color != expectedToken) continue;
                    var toy = _root.View.transform.Find("train:" + slot).GetComponent<ToyTrainView>();
                    AssertPassengerIdentity(toy, expectedShape, express, "live rider positive control");
                    sawRider = true;
                }
            }
            Assert.That(sawRider, Is.True, "the test must observe the actual shaped rider before delivery");
            Assert.That(_root.Session.CatDeliveryCount, Is.EqualTo(expectedDeliveries));
            int deliveredIndex = expectedDeliveries - 1;
            Assert.That(_root.Session.CatDeliveryAt(deliveredIndex).Colour, Is.EqualTo(expectedToken),
                "the real simulation ledger must contain the same packed identity as the live rider");
            var retained = _root.View.transform.Find("delivered-cat:" + deliveredIndex)
                .GetComponent<ToyTrainView>();
            Assert.That(retained.gameObject.activeInHierarchy, Is.True,
                "check the retained cat that actually paints after the motion-off handoff");
            yield return CaptureDeliveredPassengerWhenRequested(retained, authoredShape, express);
            AssertPassengerIdentity(retained, expectedShape, express, "retained delivered passenger");
        }

        private IEnumerator CaptureDeliveredPassengerWhenRequested(ToyTrainView retained,
            string shape, bool express)
        {
            string directory = System.Environment.GetEnvironmentVariable("CM_DELIVERY_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) yield break;
            _root.SendMessage("Update"); // Publish the real outcome/HUD before freezing its render.
            var camera = _root.Cam;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousPosition = camera.transform.position;
            float previousAspect = camera.aspect;
            float previousSize = camera.orthographicSize;
            float previousNear = camera.nearClipPlane, previousFar = camera.farClipPlane;
            bool rootEnabled = _root.enabled;
            var animators = _root.GetComponentsInChildren<Animator>(true);
            var animatorEnabled = animators.Select(animator => animator.enabled).ToArray();
            var canvases = _root.GetComponentsInChildren<Canvas>(true);
            var canvasEnabled = canvases.Select(canvas => canvas.enabled).ToArray();
            var target = new RenderTexture(917, 2048, 24) { antiAliasing = 4 };
            string name = "delivery-" + shape + "-express-" + express.ToString().ToLowerInvariant();
            try
            {
                _root.enabled = false;
                foreach (var animator in animators) animator.enabled = false;
                camera.targetTexture = target;
                camera.aspect = 917f / 2048f;
                yield return null; // Screen-space canvases observe the phone target first.
                BoardSceneLook.FitCamera(camera, _root.View);
                var safe = new Rect(0f, 64f, 917f, 1920f);
                _root.Preview.Refresh();
                _root.Preview.LayoutForViewport(safe, 408f);
                _root.Banner.LayoutForViewport(safe, 408f);
                _root.GetComponent<CatMetro.Presentation.Hud.ScreenChromeController>()
                    .Cta.LayoutForViewport(safe, 408f);
                _root.GetComponent<CatMetro.Presentation.Hud.ResultsPanel>()?.LayoutForViewport(safe, 408f);
                Canvas.ForceUpdateCanvases();
                SaveDeliveredFrame(camera, target, directory, name + "-board-917x2048.png");

                // A second photograph of the same resident, with its actual paint/mesh and
                // surrounding board intact. Only camera framing and HUD visibility change.
                var renderers = retained.GetComponentsInChildren<Renderer>()
                    .Where(renderer => renderer.enabled).ToArray();
                Assert.That(renderers, Is.Not.Empty, "the macro must frame visible retained geometry");
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Vector3 centre = camera.transform.InverseTransformPoint(bounds.center);
                float halfWidth = 0f, halfHeight = 0f;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 world = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1,
                            (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 point = camera.transform.InverseTransformPoint(world) - centre;
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(point.x));
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(point.y));
                }
                camera.transform.position += camera.transform.right * centre.x
                    + camera.transform.up * centre.y;
                camera.orthographicSize = Mathf.Max(.05f,
                    Mathf.Max(halfHeight, halfWidth / camera.aspect) * 1.3f);
                foreach (var canvas in canvases) canvas.enabled = false;
                SaveDeliveredFrame(camera, target, directory, name + "-resident-917x2048.png");
                TestContext.Out.WriteLine($"DELIVERY_CAPTURE shape={shape} express={express} "
                    + $"retained={retained.name} visible={retained.gameObject.activeInHierarchy} "
                    + $"tint={retained.CatTint} badge={retained.PinShape} "
                    + $"rigAdmitted={retained.RigAdmitted} "
                    + $"catalogEntries={CatMetro.Presentation.Cats.CatModelCatalog.LoadResources().AdmittedEntryCount}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                camera.transform.position = previousPosition;
                camera.orthographicSize = previousSize;
                camera.nearClipPlane = previousNear;
                camera.farClipPlane = previousFar;
                RenderTexture.active = previousActive;
                for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = canvasEnabled[i];
                for (int i = 0; i < animators.Length; i++) animators[i].enabled = animatorEnabled[i];
                _root.enabled = rootEnabled;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void SaveDeliveredFrame(Camera camera, RenderTexture target,
            string directory, string name)
        {
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active;
                Object.DestroyImmediate(pixels);
            }
        }

        private static void AssertPassengerIdentity(ToyTrainView toy, DestinationShape shape,
            bool express, string context)
        {
            var symbol = toy.transform.Find("Carriage/Pin/Symbol");
            var pinPaint = new MaterialPropertyBlock();
            symbol.GetComponent<Renderer>().GetPropertyBlock(pinPaint);
            var headPaint = new MaterialPropertyBlock();
            toy.transform.Find("Carriage/Cat/Head").GetComponent<Renderer>().GetPropertyBlock(headPaint);
            var marks = toy.GetComponentInChildren<PassengerStatusMarks>(true);
            TestContext.Out.WriteLine($"PASSENGER_IDENTITY context={context} tint={toy.CatTint} "
                + $"headPaint={headPaint.GetColor("_BaseColor")} pinPaint={pinPaint.GetColor("_BaseColor")} "
                + $"shape={toy.PinShape} expectedShape={shape} stray={marks.StrayVisible} "
                + $"express={marks.ExpressVisible} expectedExpress={express} "
                + $"expressActive={marks.transform.Find("Express").gameObject.activeInHierarchy}");
            Assert.That(Vector4.Distance(toy.CatTint, Palette.SignalRed), Is.LessThan(.001f), context);
            var fur = toy.GetComponentInChildren<BoardFurTint>(true);
            if (fur != null)
            {
                foreach (var skin in fur.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var coatPaint = new MaterialPropertyBlock();
                    skin.GetPropertyBlock(coatPaint);
                    Assert.That(Vector4.Distance(coatPaint.GetColor("_BaseColor"), Color.white),
                        Is.LessThan(.001f), context + ": protected atlas colours");
                    Assert.That(Vector4.Distance(coatPaint.GetColor("_FurColor"), Palette.SignalRed),
                        Is.LessThan(.001f), context + ": actual live/retained fur route colour");
                }
            }
            Assert.That(Vector4.Distance(headPaint.GetColor("_BaseColor"), Palette.SignalRed),
                Is.LessThan(.001f), context + ": actual placeholder paint");
            Assert.That(Vector4.Distance(pinPaint.GetColor("_BaseColor"), Palette.SignalRed),
                Is.LessThan(.001f), context + ": actual rendered destination paint");
            Assert.That(toy.PinShape, Is.EqualTo(shape), context);
            Assert.That(symbol.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(DestinationShapeMesh.ForShape(shape)), context + ": actual destination mesh");
            Assert.That(marks.StrayVisible, Is.False, context);
            Assert.That(marks.ExpressVisible, Is.EqualTo(express), context);
            Assert.That(marks.transform.Find("Express").gameObject.activeInHierarchy,
                Is.EqualTo(express), context + ": visible express strokes survive the handoff");
        }

        [UnityTest]
        public IEnumerator L009_PreviewShowsThreeRedShapes_WithoutTokenLetters()
        {
            _root = GameRoot.LaunchWith(ReadLevel("L009"));
            yield return null;
            _root.Preview.Refresh();
            Assert.That(_root.Preview.Faces.Take(3).Select(f => f.Shape).ToArray(),
                Is.EqualTo(new[] { DestinationShape.Circle, DestinationShape.Square,
                    DestinationShape.Triangle }));
            Assert.That(_root.Preview.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Any(t => t.name == "cat-token"), Is.False);
        }

        [UnityTest]
        public IEnumerator L009_StationsUseThreeShapeSilhouettes_OnCreamDiscs_WithoutText()
        {
            _root = GameRoot.LaunchWith(ReadLevel("L009"));
            yield return null;
            var expected = new[] { DestinationShape.Circle, DestinationShape.Square, DestinationShape.Triangle };
            var stations = _root.View.GetComponentsInChildren<BoardElementId>()
                .Where(e => e.Kind == "station").ToArray();
            Assert.That(stations, Has.Length.EqualTo(3));
            for (int i = 0; i < stations.Length; i++)
            {
                var plate = stations[i].transform.Find("station:plate-generated");
                Assert.That(plate, Is.Not.Null, "project-owned signs survive an unavailable licensed catalog");
                Assert.That(plate.GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(DestinationShapeMesh.ForShape(expected[i])));
                var disc = stations[i].transform.Find("station:keyline-generated");
                Assert.That(disc.GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(DestinationShapeMesh.ForShape(DestinationShape.Circle)));
                Assert.That(stations[i].GetComponentsInChildren<TextMesh>(true), Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator WildAcceptingStation_StillBuildsAndUsesTheStarMesh()
        {
            var level = WildStationLevel();
            _host = new GameObject("wild berth fixture");
            var view = BoardView.Build(level, _host.transform, new GameSession(level));
            yield return null;
            var plate = view.transform.Find("station:BLUE/station:plate-generated");
            Assert.That(plate.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(CatPinMeshBuilder.StarBadge()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CombinedStrayAndExpressMarks_HaveSeparateSpace(bool hud)
        {
            _host = new GameObject("combined status fixture");
            var marks = PassengerStatusMarks.Create(_host.transform, hud);
            marks.Bind(true, true);
            Assert.That(marks.StrayVisible && marks.ExpressVisible, Is.True);
            Assert.That(marks.transform.Find("Express").localPosition.x
                - marks.transform.Find("Stray").localPosition.x, Is.GreaterThan(0.7f));
            marks.Bind(true, false);
            Assert.That(marks.transform.Find("Stray").localPosition, Is.EqualTo(Vector3.zero));
        }

        [UnityTest]
        public IEnumerator DailyQueuedFork_UsesLineShapesWithoutLetterSigns()
        {
            var factory = new CatMetro.Content.Daily.DailyBoardFactory();
            LevelDto daily = null;
            for (uint seed = 1; seed < 100; seed++)
            {
                var candidate = factory.Build(seed, "2026-09-06", 0);
                if (candidate.Nodes.ToArray().Any(n => n.Id == "HOLD")) { daily = candidate; break; }
            }
            Assert.That(daily, Is.Not.Null, "exercise the queued fork family");
            var level = LevelImporter.Import(System.Text.Encoding.UTF8.GetBytes(
                CatMetro.Content.Daily.DailyBoardJson.Serialize(daily)));
            Assert.That(level.Ok, Is.True);
            _root = GameRoot.LaunchWith(level.Value);
            yield return null;
            foreach (var station in daily.Stations.ToArray())
            {
                var anchor = _root.View.transform.Find("station:" + station.NodeId);
                Assert.That(anchor.GetComponentsInChildren<TextMesh>(true), Is.Empty);
                Assert.That(anchor.Find("station:plate-generated").GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(DestinationShapeMesh.ForShape(CatLine.ShapeOf(station.Accepts.Span[0]))));
            }
        }

        [UnityTest]
        public IEnumerator L002_UnauthoredShapes_KeepTheBlueSquareVocabulary()
        {
            _root = GameRoot.LaunchWith(ReadLevel("L002"));
            yield return null;
            _root.Preview.Refresh();
            var blue = _root.Preview.Faces.First(f => f.ColorName == "blue");
            Assert.That(blue.Shape, Is.EqualTo(DestinationShape.Square));
        }

        private static ImportedLevel WildStationLevel()
        {
            var source = new StreamingAssetsContentSource().ReadAsync(
                "content/levels/L001.json", CancellationToken.None).GetAwaiter().GetResult();
            var json = System.Text.Encoding.UTF8.GetString(source)
                .Replace("\"accepts\": [\"blue\"]", "\"accepts\": [\"wild\"]");
            var level = LevelImporter.Import(System.Text.Encoding.UTF8.GetBytes(json));
            Assert.That(level.Ok, Is.True);
            return level.Value;
        }

        private static ImportedLevel ReadLevel(string id)
        {
            var bytes = new StreamingAssetsContentSource().ReadAsync(
                "content/levels/" + id + ".json", CancellationToken.None).GetAwaiter().GetResult();
            var result = LevelImporter.Import(bytes);
            Assert.That(result.Ok, Is.True, result.Ok ? "" : result.Error.ToString());
            return result.Value;
        }

        [UnityTest]
        public IEnumerator CaptureProjectOwnedBadges_WhenRequested()
        {
            string dir = System.Environment.GetEnvironmentVariable("CM_BADGE_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            _root = GameRoot.LaunchWith(ReadLevel("L009"));
            yield return null;
            foreach (var canvas in _root.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            var fixture = new GameObject("Project-owned badge fixture");
            var camera = fixture.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 1.8f;
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.InkNavy;
            var rt = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = rt;
            try
            {
                var stations = _root.View.GetComponentsInChildren<BoardElementId>()
                    .Where(e => e.Kind == "station").ToList();
                var wildLevel = WildStationLevel();
                var wildBoard = BoardView.Build(wildLevel, fixture.transform, new GameSession(wildLevel));
                stations.Add(wildBoard.GetComponentsInChildren<BoardElementId>().Single(e => e.Kind == "station" && e.Id == "BLUE"));
                for (int i = 0; i < 4; i++)
                {
                    float x = (i - 1.5f) * 1.5f;
                    var sign = Object.Instantiate(stations[i].gameObject, fixture.transform).transform;
                    sign.GetComponent<Renderer>().enabled = false;
                    sign.rotation = Quaternion.Inverse(CatMetro.Presentation.Props.BoardPropDecorator.StationSignRotation);
                    var plate = sign.Find("station:plate-generated");
                    sign.position += new Vector3(x, 0.65f, 0f) - plate.position;
                    foreach (var part in sign.GetComponentsInChildren<Transform>()) part.gameObject.layer = 30;

                    var train = ToyTrainView.Create(fixture.transform, "Fixture train", new[] { 0 }, new[] { 1 });
                    train.SyncSlot(i + 1, i == 3 ? CatColor.Wild : CatColor.Red,
                        i == 3 ? DestinationShape.Star : (DestinationShape)i);
                    train.SetTokenFlags(i == 0 || i == 2, i == 1 || i == 2);
                    // Only the project-owned pin is captured. Licensed cat geometry stays out
                    // of this isolated glyph fixture; full board captures use the main checkout.
                    var pin = train.transform.Find("Carriage/Pin");
                    pin.SetParent(fixture.transform, true);
                    pin.rotation = Quaternion.identity;
                    pin.localScale = Vector3.one * 3f;
                    pin.position = new Vector3(x, -0.85f, 0f);
                    foreach (var part in pin.GetComponentsInChildren<Transform>()) part.gameObject.layer = 30;
                    Object.DestroyImmediate(train.gameObject);
                }
                camera.Render();
                RenderTexture.active = rt;
                var capture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                capture.Apply();
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "station-and-pin-shapes.png"), capture.EncodeToPNG());
                Object.DestroyImmediate(capture);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(fixture);
            }
        }
    }
}
