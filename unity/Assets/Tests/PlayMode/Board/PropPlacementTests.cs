using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Props;
using CatMetro.Presentation.Theme;

namespace CatMetro.Tests.PlayMode
{
    public sealed class PropPlacementTests
    {
        private readonly List<GameObject> _owned = new List<GameObject>();
        private readonly List<Material> _ownedMaterials = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _owned)
                if (go != null) Object.DestroyImmediate(go);
            _owned.Clear();
            foreach (var material in _ownedMaterials)
                if (material != null) Object.DestroyImmediate(material);
            _ownedMaterials.Clear();
        }

        [Test]
        public void Catalog_AdmitsOnlyKnownRenderOnlyPrefabs()
        {
            var safe = RenderPrefab("safe");
            var unsafePrefab = RenderPrefab("unsafe");
            unsafePrefab.AddComponent<BoxCollider>();

            var catalog = new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.DepotShedId, safe),
                Entry(PropModelCatalog.StationKioskId, safe),
                Entry(PropModelCatalog.TreesId, safe),
                Entry(PropModelCatalog.DeskClutterId, safe),
                Entry(PropModelCatalog.ToyEngineId, safe),
                Entry("prop-not-authored", safe),
            });

            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(5));
            Assert.That(catalog.RejectedEntryCount, Is.EqualTo(1));
            Assert.That(catalog.TryGet(PropModelCatalog.DeskClutterId, out var clutter), Is.True);
            Assert.That(clutter.Prefab, Is.SameAs(safe));
            Assert.That(catalog.TryGet("prop-not-authored", out _), Is.False);

            var unsafeCatalog = new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.DepotShedId, unsafePrefab),
            });
            Assert.That(unsafeCatalog.AdmittedEntryCount, Is.Zero,
                "component admission—not duplicate ordering—rejects colliders");
            Assert.That(unsafeCatalog.RejectedEntryCount, Is.EqualTo(1));
        }

        [Test]
        public void Catalog_RejectsAtlaslessMaterialBeforeItCanHideTheFallback()
        {
            var atlasless = RenderPrefab("atlasless", bindAtlas: false);
            var catalog = new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.StationKioskId, atlasless),
            });

            Assert.That(catalog.AdmittedEntryCount, Is.Zero);
            Assert.That(catalog.RejectedEntryCount, Is.EqualTo(1));
            Assert.That(catalog.TryGet(PropModelCatalog.StationKioskId, out _), Is.False,
                "a grey/atlasless kiosk must leave the project-owned station visible");
        }

        [Test]
        public void Decorate_L001_AddsBuildingsAndScenery_WithoutChangingAuthoredInventory()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            var catalog = CompleteCatalog();
            var authoredBefore = view.GetComponentsInChildren<BoardElementId>(true)
                .Select(x => x.Kind + ":" + x.Id).OrderBy(x => x).ToArray();

            var root = BoardPropDecorator.Decorate(level, view.transform, catalog);

            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.parent, Is.SameAs(view.transform));
            var props = root.GetComponentsInChildren<BoardPropInstance>(true);
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.DepotShedId), Is.EqualTo(1));
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.StationKioskId), Is.EqualTo(2));
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.TreesId), Is.EqualTo(1));
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.DeskClutterId), Is.EqualTo(1));
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.ToyEngineId), Is.EqualTo(1));
            Assert.That(props.Single(x => x.AssetId == PropModelCatalog.DepotShedId).AnchorId,
                Is.EqualTo("SRC"));
            Assert.That(props.Where(x => x.AssetId == PropModelCatalog.StationKioskId)
                .Select(x => x.AnchorId).OrderBy(x => x).ToArray(),
                Is.EqualTo(new[] { "BLU", "RED" }));
            Assert.That(props.All(x => x.GetComponent<BoardElementId>() == null), Is.True,
                "decorative art must never enter the authored gameplay inventory");
            Assert.That(view.GetComponentsInChildren<BoardElementId>(true)
                .Select(x => x.Kind + ":" + x.Id).OrderBy(x => x).ToArray(),
                Is.EqualTo(authoredBefore));

            foreach (var prop in props)
            {
                var model = prop.transform.GetChild(0);
                Assert.That(Vector3.Distance(model.localRotation * Vector3.up, Vector3.back),
                    Is.LessThan(0.001f), "FBX Y-up must stand out of the board's XY plane");
            }
        }

        [Test]
        public void FurnishedCatalog_AdmitsAllTenKnownIds()
        {
            var catalog = FurnishedCatalog();

            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(10));
            Assert.That(catalog.RejectedEntryCount, Is.Zero);
            foreach (var id in FurnishIds)
                Assert.That(catalog.TryGet(id, out _), Is.True, id);
        }

        [Test]
        public void Decorate_WithFurnishedCatalog_AddsFencesBushesAndStationFurniture()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            var authoredBefore = view.GetComponentsInChildren<BoardElementId>(true)
                .Select(x => x.Kind + ":" + x.Id).OrderBy(x => x).ToArray();

            var root = BoardPropDecorator.Decorate(level, view.transform, FurnishedCatalog());

            Assert.That(root, Is.Not.Null);
            var props = root.GetComponentsInChildren<BoardPropInstance>(true);
            Assert.That(props.Length, Is.EqualTo(16),
                "L001: 2 kiosks + 2 lamps + 1 depot + trees/clutter/engine + 1 depot signpost"
                + " + 3 fences + 3 bushes + 1 trail signpost");
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.FenceId), Is.EqualTo(3));
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.BushId), Is.EqualTo(3));
            Assert.That(props.Where(x => x.AssetId == PropModelCatalog.LampPostId)
                .Select(x => x.AnchorId).OrderBy(x => x).ToArray(),
                Is.EqualTo(new[] { "BLU", "RED" }),
                "every station platform gets its own lantern");
            Assert.That(props.Single(x => x.AssetId == PropModelCatalog.SignpostId).AnchorId,
                Is.EqualTo("SRC"), "the signpost stands by the depot");
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.TrailSignpostId),
                Is.EqualTo(1));
            Assert.That(props.Where(x => x.AssetId == PropModelCatalog.FenceId)
                .All(x => x.Role == "fence-line"), Is.True);
            Assert.That(props.Where(x => x.AssetId == PropModelCatalog.BushId)
                .All(x => x.Role == "rim-bush"), Is.True);

            float contactZ = BoardPropDecorator.ResolveContactPlaneLocalZ(view.transform);
            Assert.That(props.Where(x => x.AssetId != PropModelCatalog.DeskClutterId)
                .All(x => Mathf.Abs(x.transform.localPosition.z - contactZ) < 0.001f), Is.True,
                "furnish props stand on the board contact plane like every other prop");

            Assert.That(props.All(x => x.GetComponent<BoardElementId>() == null), Is.True,
                "furnish art must never enter the authored gameplay inventory");
            Assert.That(view.GetComponentsInChildren<BoardElementId>(true)
                .Select(x => x.Kind + ":" + x.Id).OrderBy(x => x).ToArray(),
                Is.EqualTo(authoredBefore));

            var fenceYs = props.Where(x => x.AssetId == PropModelCatalog.FenceId)
                .Select(x => x.transform.localPosition.y).Distinct().ToArray();
            Assert.That(fenceYs.Length, Is.EqualTo(1),
                "the fence run is one straight line along the south apron");
            var stationYs = props.Where(x => x.AssetId == PropModelCatalog.StationKioskId)
                .Select(x => x.transform.localPosition.y);
            Assert.That(fenceYs[0], Is.LessThan(stationYs.Min()),
                "fences dress the apron below the play area, never the platforms");
        }

        [Test]
        public void CoreOnlyCatalog_SpawnsNoFurnishRoles()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);

            var root = BoardPropDecorator.Decorate(level, view.transform, CompleteCatalog());

            var props = root.GetComponentsInChildren<BoardPropInstance>(true);
            Assert.That(props.Length, Is.EqualTo(6),
                "a checkout with only the original five props renders exactly as before");
            foreach (var id in FurnishIds)
                Assert.That(props.Any(x => x.AssetId == id), Is.False, id);
        }

        [Test]
        public void AllAuthoredLevels_ProduceOneStableFurnishedSet()
        {
            for (int levelNumber = 1; levelNumber <= 17; levelNumber++)
            {
                var level = ImportLevel("L" + levelNumber.ToString("000"));
                var view = BuildBoard(level);
                var root = BoardPropDecorator.Decorate(level, view.transform, FurnishedCatalog());
                var props = root.GetComponentsInChildren<BoardPropInstance>(true);

                Assert.That(props.Length,
                    Is.EqualTo(level.Dto.Stations.Length * 2 + level.Dto.Sources.Length + 11),
                    level.Dto.Id + " should add a lamp per station, a depot signpost, three"
                    + " fences, three bushes, and one trail signpost to the core scenery");
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void PartialCatalog_IsAQuietFallback_AndStationCodingRemainsProjectOwned()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            var stations = view.GetComponentsInChildren<BoardElementId>(true)
                .Where(x => x.Kind == "station").OrderBy(x => x.Id).ToArray();
            var codingBefore = stations.Select(StationVisualSignature).ToArray();
            Assert.That(codingBefore[0], Is.Not.EqualTo(codingBefore[1]),
                "precondition: blue and red stations have distinct project-owned coding");
            var sourceVisuals = view.GetComponentsInChildren<BoardElementId>(true)
                .Single(x => x.Kind == "source").GetComponentsInChildren<Renderer>(true);
            Assert.That(sourceVisuals.Any(x => x.enabled), Is.True,
                "precondition: the fallback source visual is visible");
            var catalog = new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.DepotShedId, RenderPrefab("depot")),
                Entry(PropModelCatalog.StationKioskId, RenderPrefab("kiosk")),
            });

            Assert.DoesNotThrow(() => BoardPropDecorator.Decorate(level, view.transform, catalog));
            Assert.That(view.GetComponentsInChildren<BoardPropInstance>(true).Length, Is.EqualTo(3));
            Assert.That(stations.Select(StationVisualSignature).ToArray(), Is.EqualTo(codingBefore),
                "neutral kiosks cannot mutate the project's line colors or symbols");
            foreach (var station in stations)
            {
                Assert.That(station.GetComponent<Renderer>().enabled, Is.False,
                    "the fallback cube must not stack through the admitted kiosk");
                var badge = station.transform.Find("station:plate-generated");
                Assert.That(badge, Is.Not.Null,
                    "the project supplies a colored badge; the baked blue sign is never semantic");
                Assert.That(badge.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(badge.localPosition.y, Is.LessThan(-0.5f),
                    "the shape badge sits in front of the kiosk instead of merging into its roof");
                Assert.That(station.transform.Find("station:keyline-generated"), Is.Not.Null,
                    "a cream keyline keeps the badge silhouette distinct from the colored roof");
                Assert.That(station.GetComponentsInChildren<TextMesh>(true)
                        .All(x => x.GetComponent<Renderer>().enabled), Is.True,
                    "the project-owned line symbols stay visible over neutral kiosks");
            }
            Assert.That(stations.Select(x => x.transform.Find("station:plate-generated")
                        .GetComponent<MeshFilter>().sharedMesh.vertexCount).Distinct().Count(),
                Is.EqualTo(2), "red circle and blue square must remain distinct without color");

            var kiosks = view.GetComponentsInChildren<BoardPropInstance>(true)
                .Where(x => x.AssetId == PropModelCatalog.StationKioskId)
                .ToDictionary(x => x.AnchorId);
            foreach (var kiosk in kiosks.Values)
            {
                var woodBase = kiosk.transform.Find("station:wood-base");
                var lineRoof = kiosk.transform.Find("station:line-roof");
                Assert.That(woodBase, Is.Not.Null,
                    "each kiosk stands on a raised project-owned wooden platform");
                Assert.That(lineRoof, Is.Not.Null,
                    "each kiosk has a project-owned line-coloured roof cap");
                Color wood = PropertyColor(woodBase.GetComponent<Renderer>());
                Assert.That(wood.r, Is.GreaterThan(wood.g));
                Assert.That(wood.g, Is.GreaterThan(wood.b));
            }
            Color redRoof = PropertyColor(
                kiosks["RED"].transform.Find("station:line-roof").GetComponent<Renderer>());
            Color blueRoof = PropertyColor(
                kiosks["BLU"].transform.Find("station:line-roof").GetComponent<Renderer>());
            Assert.That(redRoof.r, Is.GreaterThan(redRoof.b), "RED roof carries the red line");
            Assert.That(blueRoof.b, Is.GreaterThan(blueRoof.r), "BLU roof carries the blue line");
            Assert.That(sourceVisuals.All(x => !x.enabled), Is.True,
                "an admitted shed replaces the source placeholder without deleting its gameplay root");
        }

        // --- STATION-BADGE: the destination vocabulary on the board plate ---

        [Test]
        public void EveryLine_GetsItsOwnPlateShape_DrivenByCatLineAlone()
        {
            var level = Import(FourLineJson());
            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());
            var plates = StationPlates(view);

            // Driven off CatLine.Names, never a list written out here. A fifth line added to
            // the vocabulary makes this test demand a fifth plate shape from the board with no
            // edit to the test, which is the property that keeps the two from drifting.
            foreach (string line in CatLine.Names)
            {
                string node = line.Substring(0, 3).ToUpperInvariant();
                Assert.That(plates.ContainsKey(node), Is.True, "fixture must berth " + line);
                Assert.That(plates[node], Is.Not.Null,
                    line + " must grow a project-owned plate under the neutral kiosk");
                Assert.That(plates[node].GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(DestinationShapeMesh.ForShape(CatLine.ShapeOf(line))),
                    line + " must take its plate straight from the shared vocabulary");
            }

            Assert.That(CatLine.Names.Select(CatLine.ShapeOf).Distinct().Count(),
                Is.EqualTo(CatLine.Names.Count),
                "every line owns a DISTINCT shape — a shared one puts identity back on colour");
            Assert.That(CatLine.Names
                    .Select(x => DestinationShapeMesh.ForShape(CatLine.ShapeOf(x)))
                    .Distinct().Count(),
                Is.EqualTo(CatLine.Names.Count),
                "and each distinct shape is a distinct mesh, so the board really renders four");
            Assert.That(plates.Values
                    .Select(x => x.GetComponent<MeshFilter>().sharedMesh.vertexCount)
                    .Distinct().Count(),
                Is.EqualTo(CatLine.Names.Count),
                "four berths on one board are separable by silhouette with colour discarded");
        }

        [Test]
        public void PlateShape_HasExactlyOneDecisionSite_AndItIsNotTheDecorator()
        {
            // The bug this lane closed was a SECOND shape switch living in the props layer
            // (`label.text == "R" ? Cylinder : Cube`) that could — and did — disagree with the
            // vocabulary. Nothing in the board or props layer may hold a shape decision again:
            // the only member anywhere allowed to yield a DestinationShape is CatLine.ShapeOf.
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (var type in new[]
                     { typeof(BoardPropDecorator), typeof(DestinationShapeMesh) })
            {
                Assert.That(type.GetMethods(all)
                        .Any(x => x.ReturnType == typeof(DestinationShape)), Is.False,
                    type.Name + " must not decide a shape; CatLine.ShapeOf is the only source");
                Assert.That(type.GetProperties(all)
                        .Any(x => x.PropertyType == typeof(DestinationShape)), Is.False,
                    type.Name + " must not cache a shape decision either");
            }
            Assert.That(typeof(CatLine).GetMethod("ShapeOf"), Is.Not.Null,
                "positive control: the one permitted decision site exists and is public");
        }

        [Test]
        public void RedAndBlue_RenderExactlyWhatTheyRenderedBeforeTheVocabulary()
        {
            // Existing captures and pins hold these two. Circle stayed the builtin cylinder
            // laid on its face; square stayed the builtin cube. Both scales are the shipped
            // literals, and the cylinder's 0.05 is the cube's 0.1 halved because the builtin
            // cylinder is two units tall — the two plates are the same 0.1 thick.
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());
            var plates = StationPlates(view);

            var red = plates["RED"];
            AssertBuiltinMesh(red, "Cylinder.fbx", "the red plate is still a laid-flat cylinder");
            Assert.That(red.localRotation.eulerAngles.x, Is.EqualTo(90f).Within(0.01f));
            AssertScale(red, 0.9f, 0.05f, 0.9f);

            var blue = plates["BLU"];
            AssertBuiltinMesh(blue, "Cube.fbx", "the blue plate is still a cube");
            Assert.That(Quaternion.Angle(blue.localRotation, Quaternion.identity),
                Is.LessThan(0.01f));
            AssertScale(blue, 0.9f, 0.9f, 0.1f);

            foreach (var plate in plates.Values)
            {
                Assert.That(plate.GetComponent<Collider>(), Is.Null,
                    "badge geometry is decoration — the builtin-mesh idiom adds no collider");
                Assert.That(plate.GetComponent<BoardElementId>(), Is.Null,
                    "decoration never claims a board element id");
            }
        }

        [Test]
        public void GeneratedPlateShapes_FaceTheCamera_AndAreClosedSolids()
        {
            // Two earlier lanes lost days to backface culling here. The camera looks from -Z,
            // so a plate whose front facets point away renders as nothing at all — and no test
            // that only counts vertices can see it.
            foreach (var shape in new[] { DestinationShape.Triangle, DestinationShape.Hexagon })
            {
                var mesh = DestinationShapeMesh.ForShape(shape);
                var normals = mesh.normals;
                var vertices = mesh.vertices;
                int facingCamera = 0;
                int facingBoard = 0;
                for (int i = 0; i < normals.Length; i++)
                {
                    if (vertices[i].z < -0.49f && normals[i].z < -0.9f) facingCamera++;
                    if (vertices[i].z > 0.49f && normals[i].z > 0.9f) facingBoard++;
                }
                Assert.That(facingCamera, Is.GreaterThan(0),
                    shape + " must present a camera-facing front face, not a culled one");
                Assert.That(facingBoard, Is.GreaterThan(0),
                    shape + " is a closed solid: no winding or mirrored transform can hide it");
                Assert.That(mesh.bounds.size.x, Is.EqualTo(1f).Within(0.001f),
                    shape + " is normalised to the builtin cube's box so one scale drives all");
                Assert.That(mesh.bounds.size.z, Is.EqualTo(1f).Within(0.001f));
            }
        }

        [Test]
        public void MultiAcceptBerth_AdvertisesEveryColourItAccepts_NotJustItsFirst()
        {
            // L009's COOL berth takes blue AND yellow and used to badge a bare "B": the yellow
            // half was unlearnable from the board. Real shipped level, not a fixture.
            var level = ImportLevel("L009");
            var cool = level.Dto.Stations.ToArray().Single(x => x.NodeId == "COOL");
            Assert.That(cool.Accepts.ToArray(), Is.EqualTo(new[] { "blue", "yellow" }),
                "precondition: L009 still berths two lines at COOL");

            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());
            var station = Station(view, "COOL");

            var primary = station.transform.Find("station:plate-generated");
            Assert.That(primary.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(DestinationShapeMesh.ForShape(CatLine.ShapeOf("blue"))));

            var chips = AcceptChips(station);
            Assert.That(chips.Length, Is.EqualTo(1),
                "one chip per FURTHER accepted line; the first is the plate itself");
            Assert.That(chips[0].GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(DestinationShapeMesh.ForShape(CatLine.ShapeOf("yellow"))),
                "the second line carries its own shape, from the same vocabulary");
            Color chipColor = PropertyColor(chips[0].GetComponent<Renderer>());
            Color yellow = CatLine.ColorOf("yellow");
            Assert.That(chipColor.r, Is.EqualTo(yellow.r).Within(0.001f), "chip line colour r");
            Assert.That(chipColor.g, Is.EqualTo(yellow.g).Within(0.001f), "chip line colour g");
            Assert.That(chipColor.b, Is.EqualTo(yellow.b).Within(0.001f), "chip line colour b");
            Assert.That(chipColor, Is.Not.EqualTo(CatLine.ColorOf("blue")),
                "the chip is not a second copy of the berth's primary line");
            Assert.That(station.GetComponentsInChildren<TextMesh>(true)
                    .Any(x => x.text == CatLine.GlyphOf("yellow")), Is.True,
                "and its own letter — a chip carries all three channels the plate does");
            Assert.That(station.transform.Find("station:keyline-accept-0"), Is.Not.Null,
                "a chip gets the same cream keyline that keeps the plate off the board");

            Assert.That(chips.All(x => x.GetComponent<Renderer>().enabled), Is.True,
                "chips are line-owned overlays, so kiosk suppression must not hide them");

            // Review finding: the first cut hung this row BELOW the plate, where the two cream
            // keylines overlapped by 0.16 at an identical Z and z-fought — invisible to every
            // assertion above, all of which check meshes, colours and names but never a
            // position. The chip must clear the primary keyline's footprint outright.
            var primaryKeyline = station.transform.Find("station:keyline-generated");
            var chipKeyline = station.transform.Find("station:keyline-accept-0");
            float clearance = (primaryKeyline.localScale.x + chipKeyline.localScale.x) * 0.5f;
            Assert.That(Mathf.Abs(chipKeyline.localPosition.x - primaryKeyline.localPosition.x),
                Is.GreaterThan(clearance),
                "the accept chip's keyline must not overlap the primary keyline beside it");
            Assert.That(chipKeyline.localPosition.y,
                Is.EqualTo(primaryKeyline.localPosition.y).Within(0.0001f),
                "and it shares the plate's row, so the badge reads as one strip of signage");
            Assert.That(primaryKeyline.localPosition.x, Is.EqualTo(0f).Within(0.0001f),
                "the primary never moves to make room — that is what keeps a single-accept"
                + " station identical to what it rendered before chips existed");
        }

        [Test]
        public void UnknownLine_IsLoudOnTheChannelsThatCanBeLoud()
        {
            // Review finding: ShapeOf's fallback is Circle, which is also red's shape. That is
            // only safe because the other two channels shout, so pin the PAIR — otherwise
            // someone tidies the magenta away and an unknown line quietly starts rendering as
            // a plausible red station.
            Assert.That(CatLine.ShapeOf(""), Is.EqualTo(CatLine.ShapeOf("red")),
                "documented: there is no fifth shape for the shape channel to fall back to");
            Assert.That(CatLine.ColorOf(""), Is.EqualTo(Color.magenta),
                "so the colour channel carries the whole signal and must stay unmistakable");
            Assert.That(CatLine.ColorOf(""), Is.Not.EqualTo(CatLine.ColorOf("red")),
                "a magenta circle is a bug; a red circle is a destination");
            Assert.That(CatLine.GlyphOf(""), Is.EqualTo("?"));

            foreach (string line in CatLine.Names)
                Assert.That(CatLine.ColorOf(line), Is.Not.EqualTo(Color.magenta),
                    "positive control: a real line never wears the bug colour (" + line + ")");

            // Positive/negative control in the same fixture: the single-accept berth beside it
            // grows no chips at all, so the row is evidence of a second line and not decoration.
            Assert.That(AcceptChips(Station(view, "RED")).Length, Is.Zero);
        }

        // Compared by name and vertex count against a freshly fetched builtin rather than by
        // reference: whether Unity hands back a cached instance is not what this pin is about,
        // and a pin that failed on that would be reporting the wrong thing entirely.
        private static void AssertBuiltinMesh(Transform plate, string builtin, string because)
        {
            var expected = Resources.GetBuiltinResource<Mesh>(builtin);
            var actual = plate.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(actual, Is.Not.Null, because);
            Assert.That(actual.name, Is.EqualTo(expected.name), because);
            Assert.That(actual.vertexCount, Is.EqualTo(expected.vertexCount), because);
        }

        private static void AssertScale(Transform plate, float x, float y, float z)
        {
            Assert.That(plate.localScale.x, Is.EqualTo(x).Within(0.0001f));
            Assert.That(plate.localScale.y, Is.EqualTo(y).Within(0.0001f));
            Assert.That(plate.localScale.z, Is.EqualTo(z).Within(0.0001f));
        }

        private static Dictionary<string, Transform> StationPlates(BoardView view) =>
            view.GetComponentsInChildren<BoardElementId>(true)
                .Where(x => x.Kind == "station")
                .ToDictionary(x => x.Id, x => x.transform.Find("station:plate-generated"));

        private static BoardElementId Station(BoardView view, string id) =>
            view.GetComponentsInChildren<BoardElementId>(true)
                .Single(x => x.Kind == "station" && x.Id == id);

        private static Transform[] AcceptChips(BoardElementId station) =>
            station.GetComponentsInChildren<Transform>(true)
                .Where(x => x.name.StartsWith("station:plate-accept-"))
                .OrderBy(x => x.name).ToArray();

        private PropModelCatalog KioskCatalog() => new PropModelCatalog(new[]
        {
            Entry(PropModelCatalog.DepotShedId, RenderPrefab("depot")),
            Entry(PropModelCatalog.StationKioskId, RenderPrefab("kiosk")),
        });

        private static ImportedLevel Import(string json)
        {
            var result = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(result.Ok, Is.True, "fixture must import: " + result.Error);
            return result.Value;
        }

        // One berth per line in CatLine.Names, so the vocabulary can be exercised whole. No
        // shipped level has a green destination yet — feat/level-variety is authoring the
        // first — and this lane does not touch level JSON.
        private static string FourLineJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T940"", ""name"": ""Four Line Fixture"", ""seed"": 940,
  ""meta"": { ""band"": ""alternation"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 4, ""y"": 10 },
      { ""id"": ""J1"", ""x"": 4, ""y"": 8 },
      { ""id"": ""J2"", ""x"": 2, ""y"": 5 }, { ""id"": ""J3"", ""x"": 6, ""y"": 5 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 }, { ""id"": ""BLU"", ""x"": 3, ""y"": 2 },
      { ""id"": ""YEL"", ""x"": 5, ""y"": 2 }, { ""id"": ""GRE"", ""x"": 7, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 10 },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""J2"", ""travelTicks"": 10 },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""J3"", ""travelTicks"": 10 },
      { ""id"": ""E4"", ""from"": ""J2"", ""to"": ""RED"", ""travelTicks"": 10 },
      { ""id"": ""E5"", ""from"": ""J2"", ""to"": ""BLU"", ""travelTicks"": 10 },
      { ""id"": ""E6"", ""from"": ""J3"", ""to"": ""YEL"", ""travelTicks"": 10 },
      { ""id"": ""E7"", ""from"": ""J3"", ""to"": ""GRE"", ""travelTicks"": 10 } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] } ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 6 },
    { ""nodeId"": ""BLU"", ""accepts"": [""blue""], ""capacity"": 6 },
    { ""nodeId"": ""YEL"", ""accepts"": [""yellow""], ""capacity"": 6 },
    { ""nodeId"": ""GRE"", ""accepts"": [""green""], ""capacity"": 6 } ],
  ""switches"": [
    { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 },
    { ""id"": ""S2"", ""nodeId"": ""J2"", ""routes"": [""E4"", ""E5""], ""initialRoute"": 0 },
    { ""id"": ""S3"", ""nodeId"": ""J3"", ""routes"": [""E6"", ""E7""], ""initialRoute"": 0 } ],
  ""waves"": [ { ""tick"": 3999, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1,
    ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 99, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }

        [Test]
        public void Decorate_PreservesEveryChildOfMultipartScenery()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            var sevenPartClutter = MultipartRenderPrefab("seven-part-clutter", 7);
            var catalog = new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.DeskClutterId, sevenPartClutter),
            });

            var root = BoardPropDecorator.Decorate(level, view.transform, catalog);
            var clutter = root.GetComponentsInChildren<BoardPropInstance>(true).Single();

            Assert.That(clutter.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(7),
                "props keep all authored parts; there is no largest-component cleanup");
        }

        [Test]
        public void Decorate_UsesFinalAnchorSpace_WhenSceneLaneNestsNodeVisuals()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            var red = view.GetComponentsInChildren<BoardElementId>(true)
                .Single(x => x.Id == "RED" && x.Kind == "station");
            var visualLayout = new GameObject("Final node layout").transform;
            visualLayout.SetParent(view.transform, false);
            visualLayout.localPosition = new Vector3(1.2f, -0.7f, 0f);
            visualLayout.localRotation = Quaternion.Euler(0f, 0f, 8f);
            red.transform.SetParent(visualLayout, true);
            var finalAnchor = view.transform.InverseTransformPoint(red.transform.position);
            finalAnchor.z = BoardPropDecorator.ResolveContactPlaneLocalZ(view.transform);

            var root = BoardPropDecorator.Decorate(level, view.transform, CompleteCatalog());
            var redKiosk = root.GetComponentsInChildren<BoardPropInstance>(true)
                .Single(x => x.AssetId == PropModelCatalog.StationKioskId && x.AnchorId == "RED");

            Assert.That(Vector3.Distance(redKiosk.transform.localPosition,
                    finalAnchor + new Vector3(0f, 0.42f, 0f)),
                Is.LessThan(0.001f),
                "placement follows the scene lane's final visual anchor, not a nested localPosition");
        }

        [Test]
        public void Decorate_UsesTheBoardViewsPresentationPositionSeam()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            Vector3 PresentationPosition(string id)
            {
                var node = level.Dto.Nodes.ToArray().Single(x => x.Id == id);
                return new Vector3(node.X, 5f + (node.Y - 5f) * 1.4f, 0.245f);
            }

            var root = BoardPropDecorator.Decorate(level, view.transform, CompleteCatalog(),
                PresentationPosition);
            var sourceDepot = root.GetComponentsInChildren<BoardPropInstance>(true)
                .Single(x => x.AssetId == PropModelCatalog.DepotShedId);

            Assert.That(Vector3.Distance(sourceDepot.transform.localPosition,
                    PresentationPosition("SRC") + new Vector3(0f, 0.62f, 0f)),
                Is.LessThan(0.001f));
            var trees = root.GetComponentsInChildren<BoardPropInstance>(true)
                .Single(x => x.AssetId == PropModelCatalog.TreesId);
            Assert.That(trees.transform.localPosition.y, Is.GreaterThan(7f),
                "scenery bounds use presentation positions too, never raw DTO coordinates");
            float presentedMinX = level.Dto.Nodes.ToArray()
                .Min(x => PresentationPosition(x.Id).x);
            Assert.That(trees.transform.localPosition.x,
                Is.GreaterThanOrEqualTo(presentedMinX - 0.2f),
                "tree plinths stay supported inside the raised board rim");
            var clutter = root.GetComponentsInChildren<BoardPropInstance>(true)
                .Single(x => x.AssetId == PropModelCatalog.DeskClutterId);
            Assert.That(root.GetComponentsInChildren<BoardPropInstance>(true)
                    .Where(x => x != clutter)
                    .All(x => Mathf.Abs(x.transform.localPosition.z - 0.245f) < 0.001f),
                Is.True, "board props land on the scene lane's board contact plane");
            Assert.That(clutter.transform.localPosition.z,
                Is.EqualTo(BoardPropDecorator.ResolveDeskContactPlaneLocalZ(view.transform))
                    .Within(0.001f),
                "desk clutter uses its supporting desk plane, not the raised board plane");
            float presentedMinY = level.Dto.Nodes.ToArray()
                .Min(x => PresentationPosition(x.Id).y);
            Assert.That(clutter.transform.localPosition.y,
                Is.LessThanOrEqualTo(presentedMinY - 1.35f),
                "desk clutter stays wholly outside the playable board footprint");
        }

        [Test]
        public void ContactPlane_UsesExplicitMarkerThenDioramaSurfaceFace()
        {
            var board = Own(new GameObject("contact-plane-board"));
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _owned.Add(surface);
            surface.name = BoardPropDecorator.DioramaSurfaceName;
            surface.transform.SetParent(board.transform, false);
            surface.transform.localPosition = new Vector3(3f, 6.665f, 0.43f);
            surface.transform.localScale = new Vector3(6.55f, 20.93f, 0.34f);

            Assert.That(BoardPropDecorator.ResolveContactPlaneLocalZ(board.transform),
                Is.EqualTo(0.26f).Within(0.001f),
                "without a marker, use the camera-facing mesh face of the diorama surface");

            var marker = new GameObject(BoardPropDecorator.ContactPlaneMarkerName);
            _owned.Add(marker);
            marker.transform.SetParent(board.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0f, 0.245f);

            Assert.That(BoardPropDecorator.ResolveContactPlaneLocalZ(board.transform),
                Is.EqualTo(0.245f).Within(0.001f),
                "the scene lane can publish its exact contact plane explicitly");
        }

        [Test]
        public void BoardViewBuild_WithInjectedCatalog_AlwaysRunsTheIntegrationHook()
        {
            var level = ImportLevel("L001");
            var host = Own(new GameObject("hook-board-host"));
            var view = BoardView.Build(level, host.transform, new GameSession(level),
                CompleteCatalog());

            var props = view.GetComponentsInChildren<BoardPropInstance>(true);
            Assert.That(props.Length, Is.EqualTo(6));
            var marker = view.transform.Find(BoardPropDecorator.ContactPlaneMarkerName);
            Assert.That(marker, Is.Not.Null,
                "the scene lane publishes its tabletop contact plane before decoration");
            var deskMarker = view.transform.Find(BoardPropDecorator.DeskContactPlaneMarkerName);
            Assert.That(deskMarker, Is.Not.Null,
                "the desk dressing has a support plane outside the raised board");
            Assert.That(deskMarker.localPosition.z, Is.GreaterThan(marker.localPosition.z));
            Assert.That(props.Where(x => x.AssetId != PropModelCatalog.DeskClutterId)
                    .All(x => Mathf.Abs(x.transform.localPosition.z - marker.localPosition.z)
                        < 0.001f), Is.True,
                "board prop feet sit on BoardSurface's camera-facing face");
            Assert.That(props.Single(x => x.AssetId == PropModelCatalog.DeskClutterId)
                    .transform.localPosition.z,
                Is.EqualTo(deskMarker.localPosition.z).Within(0.001f));
        }

        [Test]
        public void BoardAndDeskSurfaces_ReuseTheCommittedMaterial()
        {
            var view = BuildBoard(ImportLevel("L001"));
            var boardBody = view.transform.Find("BoardBody");
            var desk = view.transform.Find("DeskSurface");
            Assert.That(boardBody, Is.Not.Null);
            Assert.That(desk, Is.Not.Null);

            var renderers = boardBody.GetComponentsInChildren<Renderer>(true)
                .Concat(desk.GetComponentsInChildren<Renderer>(true)).ToArray();
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(7));
            Assert.That(renderers.All(x => x.sharedMaterial == GreyboxMaterial.Shared), Is.True,
                "Retry/LoadNext must not leak one native Material per slab part");
        }

        [Test]
        public void AllAuthoredLevels_ProduceOneStableScenerySet()
        {
            for (int levelNumber = 1; levelNumber <= 17; levelNumber++)
            {
                var level = ImportLevel("L" + levelNumber.ToString("000"));
                var view = BuildBoard(level);
                var root = BoardPropDecorator.Decorate(level, view.transform, CompleteCatalog());
                var props = root.GetComponentsInChildren<BoardPropInstance>(true);

                Assert.That(props.Length,
                    Is.EqualTo(level.Dto.Stations.Length + level.Dto.Sources.Length + 3),
                    level.Dto.Id + " should have one kiosk per station, one depot per source, "
                    + "and exactly one each of trees, clutter, and parked engine");
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void LocalResources_WhenPresent_AreCompleteUrpPrefabsAndWireThroughBoardView()
        {
            var catalog = PropModelCatalog.LoadResources();
            if (catalog.AdmittedEntryCount == 0)
                Assert.Pass("optional paid prop bytes are absent in this licence-neutral checkout");

            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(5).Or.EqualTo(10),
                "local installs are atomic per batch: the core five, optionally plus the"
                + " five-piece Polyfork furnish set — never a partial batch");
            Assert.That(catalog.RejectedEntryCount, Is.EqualTo(0));
            var expectedIds = new List<string>
            {
                PropModelCatalog.DepotShedId,
                PropModelCatalog.StationKioskId,
                PropModelCatalog.TreesId,
                PropModelCatalog.DeskClutterId,
                PropModelCatalog.ToyEngineId,
            };
            if (catalog.AdmittedEntryCount == 10) expectedIds.AddRange(FurnishIds);
            foreach (var id in expectedIds)
            {
                Assert.That(catalog.TryGet(id, out var entry), Is.True, id);
                foreach (var renderer in entry.Prefab.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, id + " material");
                    Assert.That(renderer.sharedMaterial.shader.name,
                        Is.EqualTo("Universal Render Pipeline/Lit"), id + " shader");
                    Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null,
                        id + " must bind the external baked atlas explicitly");
                }
            }

            var level = ImportLevel("L001");
            var host = Own(new GameObject("local-resource-board-host"));
            var view = BoardView.Build(level, host.transform, new GameSession(level));
            Assert.That(view.GetComponentsInChildren<BoardPropInstance>(true).Length,
                Is.EqualTo(catalog.AdmittedEntryCount == 10 ? 16 : 6));
        }

        private BoardView BuildBoard(ImportedLevel level)
        {
            var host = Own(new GameObject("board-host"));
            return BoardView.Build(level, host.transform, new GameSession(level),
                PropModelCatalog.Empty);
        }

        private static readonly string[] FurnishIds =
        {
            PropModelCatalog.FenceId,
            PropModelCatalog.BushId,
            PropModelCatalog.LampPostId,
            PropModelCatalog.SignpostId,
            PropModelCatalog.TrailSignpostId,
        };

        private PropModelCatalog CompleteCatalog()
        {
            var prefab = RenderPrefab("complete-catalog-model");
            return new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.DepotShedId, prefab),
                Entry(PropModelCatalog.StationKioskId, prefab),
                Entry(PropModelCatalog.TreesId, prefab),
                Entry(PropModelCatalog.DeskClutterId, prefab),
                Entry(PropModelCatalog.ToyEngineId, prefab),
            });
        }

        private PropModelCatalog FurnishedCatalog()
        {
            var prefab = RenderPrefab("furnished-catalog-model");
            var entries = new List<PropModelCatalog.Entry>
            {
                Entry(PropModelCatalog.DepotShedId, prefab),
                Entry(PropModelCatalog.StationKioskId, prefab),
                Entry(PropModelCatalog.TreesId, prefab),
                Entry(PropModelCatalog.DeskClutterId, prefab),
                Entry(PropModelCatalog.ToyEngineId, prefab),
            };
            foreach (var id in FurnishIds) entries.Add(Entry(id, prefab));
            return new PropModelCatalog(entries);
        }

        private static PropModelCatalog.Entry Entry(string id, GameObject prefab) =>
            new PropModelCatalog.Entry(id, prefab, 1f, 0f, Vector3.zero);

        private static string StationVisualSignature(BoardElementId station)
        {
            var parts = new List<string>();
            foreach (var renderer in station.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (renderer.gameObject.name == "station:keyline-generated") continue;
                var material = renderer.material;
                parts.Add(renderer.GetType().Name + ":" + material.color);
            }
            foreach (var text in station.GetComponentsInChildren<TextMesh>(true))
                if (text.GetComponent<Renderer>().enabled) parts.Add("text:" + text.text);
            parts.Sort();
            return string.Join("|", parts);
        }

        private static Color PropertyColor(Renderer renderer)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            return properties.GetColor("_BaseColor");
        }

        private GameObject RenderPrefab(string name, bool bindAtlas = true)
        {
            var go = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = TestPropMaterial(bindAtlas);
            return go;
        }

        private GameObject MultipartRenderPrefab(string name, int parts)
        {
            var root = Own(new GameObject(name));
            var material = TestPropMaterial(bindAtlas: true);
            for (int i = 0; i < parts; i++)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = "part-" + i;
                Object.DestroyImmediate(part.GetComponent<Collider>());
                part.GetComponent<Renderer>().sharedMaterial = material;
                part.transform.SetParent(root.transform, false);
            }
            return root;
        }

        private Material TestPropMaterial(bool bindAtlas)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "URP/Lit test precondition");
            var material = new Material(shader);
            material.SetTexture("_BaseMap", bindAtlas ? Texture2D.whiteTexture : null);
            _ownedMaterials.Add(material);
            return material;
        }

        private GameObject Own(GameObject go)
        {
            _owned.Add(go);
            return go;
        }

        private static ImportedLevel ImportLevel(string id)
        {
            var path = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "content", "levels", id + ".json");
            var result = LevelImporter.Import(File.ReadAllBytes(path));
            Assert.That(result.Ok, Is.True, id + " fixture must import: " + result.Error);
            return result.Value;
        }
    }
}
