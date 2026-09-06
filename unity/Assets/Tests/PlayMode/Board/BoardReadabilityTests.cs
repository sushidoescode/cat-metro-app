using System.Collections;
using System.Linq;
using System.Threading;
using CatMetro.Application.Session;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
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
