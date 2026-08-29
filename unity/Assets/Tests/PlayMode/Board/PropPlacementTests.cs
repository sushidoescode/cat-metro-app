using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
            foreach (string id in AuthoredLevelIds())
            {
                var level = ImportLevel(id);
                var view = BuildBoard(level);
                var root = BoardPropDecorator.Decorate(level, view.transform, FurnishedCatalog());
                var props = root.GetComponentsInChildren<BoardPropInstance>(true);

                // A RELATION, not a constant that happens to match. Sources.Length is a real
                // term — the decorator spawns a depot per source — but on THIS branch it is
                // unexercised, and not merely because no level uses two: LevelImporter throws
                // PinnedMechanic on a second source, so a two-source board cannot even import
                // here. feat/level-variety's CM-C14a harvest lifts that pin (it modifies both
                // LevelImporter and LevelGraph), and its L018/L019 are the corpus's first
                // two-source boards. This assertion is written to be correct across THAT
                // merge, which is where the source term finally gets tested.
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
            // Reference distinctness, NOT vertex-count distinctness. The first cut counted
            // vertices as a cheap proxy for "different silhouette", and the manifest
            // correction retired that proxy: a diamond is a square on its point, so green and
            // blue plates now have an identical 24 vertices and genuinely different outlines.
            // Which is also the open question flagged in the PR — a rotated square is the
            // weakest separation in the set, and blue-versus-green is exactly the pair a
            // colourblind player leans on shape for.
            Assert.That(plates.Values
                    .Select(x => x.GetComponent<MeshFilter>().sharedMesh).Distinct().Count(),
                Is.EqualTo(CatLine.Names.Count),
                "four berths on one board render four different meshes");
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
        public void NoColourTableSurvivesAnywhereInPresentation_OnEitherKeyType()
        {
            // Named for what it actually proves. Its first cut reflected only into BoardView
            // and was called ColourHasOneDecisionSite_OnBothKeyTypes, which was a lie by
            // scope — and the lie had a live victim: WavePreviewStrip held a THIRD colour
            // table, the only one built from raw literals instead of Palette tokens, and it
            // had already drifted in the shipped build (red off SignalRed by 0.153). The wave
            // strip announced an arriving red cat in a different red from the badge that
            // accepts it, and a BoardView-only test passed the whole time.
            //
            // So this DISCOVERS its targets across the Presentation assembly rather than
            // naming them: any private static shaped like a line-colour lookup must agree with
            // the vocabulary. A fourth table added later is caught the day it appears, without
            // anyone remembering to extend this test.
            // Matched on SIGNATURE, not on being named "ColorFor" — a table named something
            // unanticipated is exactly what this needs to catch, and at the time of writing
            // the shape has no innocent occurrences (the only private static Color(string) or
            // Color(byte) members in the whole assembly are the three line-colour doors).
            // CatLine's own ColorOf is public, so NonPublic correctly leaves the vocabulary
            // out of a sweep for things that duplicate it. If this ever fires on a genuinely
            // unrelated helper, narrow it by name then — do not delete it.
            const BindingFlags all = BindingFlags.NonPublic | BindingFlags.Static;
            var byName = new List<MethodInfo>();
            var byCode = new List<MethodInfo>();
            foreach (var type in typeof(BoardView).Assembly.GetTypes())
                foreach (var method in type.GetMethods(all))
                {
                    if (method.ReturnType != typeof(Color)) continue;
                    var args = method.GetParameters();
                    if (args.Length != 1) continue;
                    if (args[0].ParameterType == typeof(string)) byName.Add(method);
                    else if (args[0].ParameterType == typeof(byte)) byCode.Add(method);
                }

            // Anti-vacuity: a sweep that finds nothing must not report success. These two are
            // the ones known to exist as this is written.
            Assert.That(byName.Any(x => x.DeclaringType == typeof(BoardView)), Is.True,
                "positive control: BoardView's name-keyed door is in scope of the sweep");
            Assert.That(byCode.Any(x => x.DeclaringType == typeof(BoardView)), Is.True,
                "positive control: BoardView's byte-keyed door is in scope of the sweep");

            foreach (var method in byName)
                foreach (string line in CatLine.Names)
                    Assert.That((Color)method.Invoke(null, new object[] { line }),
                        Is.EqualTo(CatLine.ColorOf(line)),
                        method.DeclaringType.Name + "." + method.Name + " holds its own colour"
                        + " for " + line + " instead of asking CatLine");

            // 0..6 spans None, every line, the reserved Wild, and one code past the end — so
            // a line added to CatLine without a byte door following fails right here.
            foreach (var method in byCode)
                for (int code = 0; code <= 6; code++)
                    Assert.That((Color)method.Invoke(null, new object[] { (byte)code }),
                        Is.EqualTo(CatLine.ColorOf((byte)code)),
                        method.DeclaringType.Name + "." + method.Name + " holds its own colour"
                        + " for code " + code + " instead of asking CatLine");

            // The alignment that lets the byte door be an index instead of a second table.
            Assert.That(CatLine.NameOfCode(CatMetro.Domain.CatColor.Red), Is.EqualTo("red"));
            Assert.That(CatLine.NameOfCode(CatMetro.Domain.CatColor.Blue), Is.EqualTo("blue"));
            Assert.That(CatLine.NameOfCode(CatMetro.Domain.CatColor.Yellow),
                Is.EqualTo("yellow"));
            Assert.That(CatLine.NameOfCode(CatMetro.Domain.CatColor.Green), Is.EqualTo("green"));
            Assert.That(CatLine.Names.Count, Is.EqualTo(4),
                "Names is the DESTINATION list and wild is not a destination, so this stays 4"
                + " even though CodeNames has five entries. If a fifth real LINE is authored,"
                + " re-read Domain.CatColor and both arrays together before touching this —"
                + " NameOfCode indexes CodeNames directly and a new line cannot simply be"
                + " appended after wild without breaking the 1-based alignment");

            // Wild is a real cat colour, so it is NOT the unknown sentinel — but it must never
            // be mistakable for a line either, or a player hunts for the berth it matches.
            Assert.That(CatLine.ColorOf(CatMetro.Domain.CatColor.Wild),
                Is.EqualTo(Palette.CatnipViolet),
                "conformance to CAT-MANIFEST.json: cat-wild-alley was generated in catnip"
                + " violet, and those bytes are pinned by the licensing record");
            foreach (string line in CatLine.Names)
                Assert.That(CatLine.ColorOf(CatMetro.Domain.CatColor.Wild),
                    Is.Not.EqualTo(CatLine.ColorOf(line)),
                    "wild must not wear " + line + "'s colour — it has no destination, so a"
                    + " line colour on it would send the player looking for a berth");
            Assert.That(CatLine.ShapeOf("wild"), Is.EqualTo(DestinationShape.Star));
            foreach (string line in CatLine.Names)
                Assert.That(CatLine.ShapeOf("wild"), Is.Not.EqualTo(CatLine.ShapeOf(line)),
                    "and the star is what actually says 'goes anywhere', so it cannot collide"
                    + " with " + line + "'s shape");

            // What REMAINS the genuine unknown sentinel after wild stopped being magenta.
            Assert.That(CatLine.ColorOf(CatMetro.Domain.CatColor.None), Is.EqualTo(Color.magenta),
                "code 0 is still 'no colour', not a cat");
            Assert.That(CatLine.ColorOf((byte)6), Is.EqualTo(Color.magenta),
                "and anything past wild is still unmapped");
            Assert.That(CatLine.ColorOf(""), Is.EqualTo(Color.magenta));
        }

        [Test]
        public void RedAndBlue_RenderTheSameBadgeFaceAndDepth()
        {
            // Circle is still the builtin cylinder laid on its face and square still the
            // builtin cube — the mesh choice is what the captures pin, and it is untouched.
            //
            // What these assert is the rendered SIZE, and they assert it because the previous
            // version of this test could not. It pinned localScale == (0.9, 0.05, 0.9) for the
            // red plate and passed for the whole life of the badge while the red plate rendered
            // at TWICE the blue one's width — visible in the 2026-08-25 r6 board capture, where
            // the two station roofs (same cube, same size) project to within 0.05% of each
            // other under the orthographic camera while the red disc measures 1.99x the blue
            // square. PlateScale halved only the cylinder's Y, on the belief that the builtin
            // cylinder is 2 units tall but 1 across; it is 2 across too.
            //
            // A pinned scale FACTOR says nothing about how big a thing renders unless the mesh
            // is unit-sized, which is precisely the assumption that keeps failing here (see
            // Sphere.fbx at ~3.33). So these multiply localScale back through the mesh's own
            // bounds and check the badge face and depth in the shared standing-sign frame.
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());
            var plates = StationPlates(view);

            // STATION-PLATFORM moved the two rotation lines, and moved them rather than
            // relaxing them. The plates now STAND on posts, so a plate's localRotation is
            // StationSignRotation composed with its shape rotation and neither of the shipped
            // euler literals can hold. AssertShapeRotation factors the standing turn back out
            // and demands the remainder be EXACTLY DestinationShapeMesh.PlateRotation for that
            // shape — which is strictly stronger than the checks it replaces, because it also
            // catches a plate that picked up a rotation of its own on top (the euler-x check
            // could not: 90 degrees of x with an arbitrary z still passed it).
            var red = plates["RED"];
            AssertBuiltinMesh(red, "Cylinder.fbx", "the red plate is still a cylinder");
            AssertShapeRotation(red, "red");
            AssertSignFrameSize(red, 0.9f, 0.9f, 0.1f,
                "the red plate is a 0.9 disc standing 0.1 off the board, whatever the builtin"
                + " cylinder's intrinsic size turns out to be");

            var blue = plates["BLU"];
            AssertBuiltinMesh(blue, "Cube.fbx", "the blue plate is still a cube");
            AssertShapeRotation(blue, "blue");
            AssertSignFrameSize(blue, 0.9f, 0.9f, 0.1f,
                "and the blue plate is that same 0.9 by 0.1 — unchanged, as its captures pin");

            // The property that was actually violated, stated as a property rather than as two
            // literals that happen to agree. Nothing was making this claim, which is exactly
            // how a double-width red disc survived: each shape was pinned against its own
            // number, so no assertion ever compared one plate to the other.
            var redSize = PlateSizeInSignFrame(red);
            var blueSize = PlateSizeInSignFrame(blue);
            Assert.That(redSize.x, Is.EqualTo(blueSize.x).Within(0.001f),
                "circle and square are one badge in two shapes: SHAPE is the channel, size is"
                + " not, or the board reads one destination as louder than the other");
            Assert.That(redSize.y, Is.EqualTo(blueSize.y).Within(0.001f), "same plate height");
            Assert.That(redSize.z, Is.EqualTo(blueSize.z).Within(0.001f), "same standoff depth");

            foreach (var plate in plates.Values)
            {
                Assert.That(plate.GetComponent<Collider>(), Is.Null,
                    "badge geometry is decoration — the builtin-mesh idiom adds no collider");
                Assert.That(plate.GetComponent<BoardElementId>(), Is.Null,
                    "decoration never claims a board element id");
            }
        }

        // --- STATION-PLATFORM: LOOK step 4, the berth as a raised platform under a canopy ---

        // The shipped diorama projection, so a dimension can be asserted in PIXELS rather than
        // in board units nobody can picture. The camera is orthographic and identity-rotated,
        // so at 917x2048 a board unit is 1024 / orthographicSize pixels. BoardSceneLook's fit
        // gives 95.9 px on L001 and 82.1 on L008, the widest authored board — 82 is used
        // throughout because a dimension that reads at the WORST zoom reads everywhere, and
        // the cat lane independently measured ~93 for the median board, which lands between.
        //
        // The ~4 px floor is not invented here either: the r6 orchestrator render is what
        // found that board detail below roughly that size stops reading at all.
        private const float WorstZoomPixelsPerBoardUnit = 82f;
        private const float ReadableFloorPixels = 4f;

        // Derived from the REAL tilt rather than restated, so a re-authored diorama angle
        // changes what these tests demand instead of silently invalidating them.
        private static float ScreenYPerBoardZ() =>
            Mathf.Abs((RealDioramaTilt() * Vector3.forward).y);

        private static float ScreenXPerBoardXY() =>
            Mathf.Abs((RealDioramaTilt() * Vector3.right).x)
            + Mathf.Abs((RealDioramaTilt() * Vector3.up).x);

        [Test]
        public void StationPlatform_IsARaisedDeckOnAPlinth_NotASlabPaintedOnTheWood()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());

            foreach (var kiosk in Kiosks(view).Values)
            {
                var plinth = kiosk.transform.Find("station:wood-base");
                var deck = kiosk.transform.Find("station:wood-deck");
                Assert.That(plinth, Is.Not.Null, "the platform still meets the wood on a plinth");
                Assert.That(deck, Is.Not.Null,
                    "and is capped by a deck — one box cannot show a raised edge to itself");

                // -Z is toward the camera, so "on top of" means a SMALLER z. The two courses
                // must meet EXACTLY: a gap floats the deck, an overlap z-fights along the seam,
                // and the seam is the whole point of splitting the slab in two.
                float plinthTop = plinth.localPosition.z - plinth.localScale.z * 0.5f;
                float deckBottom = deck.localPosition.z + deck.localScale.z * 0.5f;
                Assert.That(deckBottom, Is.EqualTo(plinthTop).Within(0.0001f),
                    "the deck sits ON the plinth");
                Assert.That(plinth.localPosition.z + plinth.localScale.z * 0.5f,
                    Is.EqualTo(0f).Within(0.0001f),
                    "and the plinth's own foot is the tabletop the kiosk was placed on");

                Assert.That(deck.localScale.x, Is.GreaterThan(plinth.localScale.x),
                    "the deck overhangs the plinth: that lip's shadow is what the eye reads as"
                    + " raised, not the height by itself");
                Assert.That(deck.localScale.y, Is.GreaterThan(plinth.localScale.y));

                float rise = -(deck.localPosition.z - deck.localScale.z * 0.5f);
                Assert.That(rise * ScreenYPerBoardZ() * WorstZoomPixelsPerBoardUnit,
                    Is.GreaterThan(12f),
                    "the platform must stand at least 12 px off the board at the worst authored"
                    + " zoom. The single 0.11 slab this replaces came to 5.6 px and read as a"
                    + " colour painted on the wood in the r6 render");
                foreach (var course in new[] { plinth, deck })
                {
                    Color wood = PropertyColor(course.GetComponent<Renderer>());
                    Assert.That(wood.r, Is.GreaterThan(wood.g), course.name + " is toy wood");
                    Assert.That(wood.g, Is.GreaterThan(wood.b), course.name + " is toy wood");
                }
                Assert.That(PropertyColor(deck.GetComponent<Renderer>()).r,
                    Is.GreaterThan(PropertyColor(plinth.GetComponent<Renderer>()).r),
                    "the deck is the PALER course — a deck darker than the plinth under it"
                    + " reads as a hole rather than as a lit top surface");
            }
        }

        [Test]
        public void StationCanopy_IsHeldUpOnFourPosts_WithDaylightUnderIt()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());

            foreach (var entry in Kiosks(view))
            {
                var kiosk = entry.Value;
                var deck = kiosk.transform.Find("station:wood-deck");
                var roof = kiosk.transform.Find("station:line-roof");
                var posts = kiosk.GetComponentsInChildren<Transform>(true)
                    .Where(x => x.name.StartsWith("station:roof-post-"))
                    .OrderBy(x => x.name, System.StringComparer.Ordinal).ToArray();
                Assert.That(posts.Length, Is.EqualTo(4),
                    "a canopy stands on a leg at each corner; the reference boards show four");

                float deckTop = deck.localPosition.z - deck.localScale.z * 0.5f;
                float roofUnder = roof.localPosition.z + roof.localScale.z * 0.5f;
                Assert.That((deckTop - roofUnder) * ScreenYPerBoardZ()
                        * WorstZoomPixelsPerBoardUnit, Is.GreaterThan(18f),
                    "there must be real daylight between deck and roof — that gap is the whole"
                    + " difference between a shelter a cat waits under and a coloured plate"
                    + " lying on the kiosk's roofline, which is what shipped");

                foreach (var post in posts)
                {
                    Assert.That(post.localPosition.z + post.localScale.z * 0.5f,
                        Is.EqualTo(deckTop).Within(0.0001f), post.name + " foot is on the deck");
                    Assert.That(post.localPosition.z - post.localScale.z * 0.5f,
                        Is.EqualTo(roofUnder).Within(0.0001f),
                        post.name + " head is under the roof — a post derived independently of"
                        + " the roof it holds either floats it or is buried in the deck");
                    Assert.That(Mathf.Abs(post.localPosition.x - roof.localPosition.x),
                        Is.LessThan(roof.localScale.x * 0.5f), post.name + " stands under the roof");
                    Assert.That(Mathf.Abs(post.localPosition.y - roof.localPosition.y),
                        Is.LessThan(roof.localScale.y * 0.5f), post.name + " stands under the roof");
                    Assert.That(post.localScale.x * ScreenXPerBoardXY()
                            * WorstZoomPixelsPerBoardUnit,
                        Is.GreaterThan(ReadableFloorPixels), post.name + " must be wide enough"
                        + " to survive the worst authored zoom");
                }
                Assert.That(posts.Select(x => Mathf.Round(x.localPosition.x * 1000f) + ","
                        + Mathf.Round(x.localPosition.y * 1000f)).Distinct().Count(),
                    Is.EqualTo(4), "four DISTINCT corners, not one leg authored four times");

                // The canopy is still the line channel, and still takes its colour from the
                // vocabulary by way of the station's own material — never from a table here.
                // Channel-wise with a tolerance, like the chip assertions: this colour has been
                // round-tripped through a real Material, and how URP rounds a float colour back
                // out is not what this pin is about.
                Color roofColor = PropertyColor(roof.GetComponent<Renderer>());
                Color line = CatLine.ColorOf(LineOf(level, entry.Key));
                Assert.That(roofColor.r, Is.EqualTo(line.r).Within(0.01f), entry.Key + " roof r");
                Assert.That(roofColor.g, Is.EqualTo(line.g).Within(0.01f), entry.Key + " roof g");
                Assert.That(roofColor.b, Is.EqualTo(line.b).Within(0.01f), entry.Key + " roof b");
            }
        }

        [Test]
        public void StationBadge_StandsOnAPostFromTheTabletop_InsteadOfLyingOnTheBoard()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());
            float contactZ = BoardPropDecorator.ResolveContactPlaneLocalZ(view.transform);

            foreach (var station in Stations(view))
            {
                var plate = station.transform.Find("station:plate-generated");
                var mast = station.transform.Find("station:signmast-generated");
                Assert.That(mast, Is.Not.Null,
                    "the badge is signage on a post now, not paint on the board");
                Assert.That(mast.GetComponent<Renderer>().enabled, Is.True,
                    "and the kiosk suppression sweep must not hide the post out from under it"
                    + " — a disabled renderer passes every mesh, colour and name check there is");

                // Converted back into BOARD space rather than compared against a literal: the
                // station anchor is scaled 0.6 and the scene lane is free to nest it, so a
                // constant here would be measuring the wrong space and still look right.
                float footBoardZ = view.transform.InverseTransformPoint(
                    mast.TransformPoint(new Vector3(0f, 0f, 0.5f))).z;
                Assert.That(footBoardZ, Is.EqualTo(contactZ).Within(0.01f),
                    "a post that stops short of the tabletop is a floating sign, which is the"
                    + " thing this whole change exists to stop being");

                // Measure the rendered face, not a raw scale factor: the builtin cylinder is
                // two units across, so its correct localScale.x is half the requested size.
                float half = PlateSizeInSignFrame(plate).y * 0.5f;
                Assert.That(mast.localPosition.z - mast.localScale.z * 0.5f,
                    Is.EqualTo(plate.localPosition.z + half).Within(0.0001f),
                    "and it stops exactly at the plate's bottom edge — short of that the sign"
                    + " floats off its own pole, past it the pole punches through the badge");

                Assert.That(mast.localScale.z * 0.6f * ScreenYPerBoardZ()
                        * WorstZoomPixelsPerBoardUnit, Is.GreaterThan(20f),
                    "the pole has to be tall enough to read as a pole (0.6 is the station"
                    + " anchor's scale, which is the space these numbers are in)");
                Assert.That(mast.localScale.x * 0.6f * ScreenXPerBoardXY()
                        * WorstZoomPixelsPerBoardUnit, Is.GreaterThan(ReadableFloorPixels),
                    "and thick enough not to disappear at the worst authored zoom");

                Assert.That(station.GetComponentsInChildren<Transform>(true)
                        .Count(x => x.name.StartsWith("station:signmast")), Is.EqualTo(1),
                    "one post per sign, and a single-accept berth posts exactly one sign");
            }
        }

        [Test]
        public void StationSigns_StandPerpendicularToTheBoard_AndNoOtherYawFacesTheCameraBetter()
        {
            var level = Import(FourLineJson());
            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());
            // Without the production tilt every claim below is being made in a space the
            // shipped game never renders. BoardSceneLook applies this after BoardView.Build.
            view.transform.localRotation = RealDioramaTilt();

            // A sign stands OUT of the tabletop. Asserted on the rotation itself because a
            // plate's own local up is not the sign's: the circle plate is a cylinder laid on
            // its face, so its local +Y is the disc's axis, not the direction the glyph reads.
            Assert.That(Vector3.Distance(
                    BoardPropDecorator.StationSignRotation * Vector3.up, Vector3.back),
                Is.LessThan(0.001f),
                "the sign's up is the board's out-of-table axis: perpendicular to the wood like"
                + " a real signpost, not tipped back to cheat a better angle at the camera");

            // The best a board-perpendicular sign can do, swept rather than asserted as a
            // number. This is the assertion that would have caught a sign yawed the wrong way,
            // which is not a slightly-worse angle but a NEGATIVE one — a face pointed into the
            // board, backface-culled, rendering nothing at all.
            Quaternion board = view.transform.rotation;
            float best = float.MinValue;
            for (int deg = 0; deg < 360; deg++)
            {
                float radians = deg * Mathf.Deg2Rad;
                Vector3 candidate = board * new Vector3(
                    Mathf.Cos(radians), Mathf.Sin(radians), 0f);
                best = Mathf.Max(best, Vector3.Dot(candidate, Vector3.back));
            }
            Assert.That(best, Is.EqualTo(0.744f).Within(0.01f),
                "positive control on the diorama's geometry, not on this lane's code. The"
                + " board's NORMAL is 48.07 degrees off the view axis (cos 0.669, which is what"
                + " a plaque lying flat presents); the closest any direction IN the board plane"
                + " gets is 41.93 degrees, cos 0.744. Standing the badge up therefore shows"
                + " about 11% MORE face than lying it down, which is the opposite of the"
                + " trade-off it looks like it should be");

            // Asserted on the SIGN's facing axis, deliberately not on any plate's local -Z. That
            // shortcut is true for the cube and the two extruded prisms and FALSE for the
            // circle — PlateRotation lays the builtin cylinder on its face, so the red badge's
            // visible surface is a cap along its local -Y. A test that assumed -Z would have
            // passed for three lines out of four and reported the red one as pointing out of
            // the tabletop. Which face each shape presents is the vocabulary's business; that
            // it ends up on this axis is this lane's, and the normals test below proves the
            // meshes really are on it.
            Vector3 signFacing = view.transform.TransformDirection(
                BoardPropDecorator.StationSignRotation * Vector3.back);
            Assert.That(Vector3.Dot(signFacing, Vector3.back), Is.GreaterThan(best - 0.002f),
                "the badge must take the best camera-facing yaw available, not merely a"
                + " positive one — the wrong sign here gives -0.744 and renders nothing");
            Assert.That(StationPlates(view).Count, Is.EqualTo(CatLine.Names.Count),
                "positive control: all four lines really did grow a badge to be turned");
        }

        [Test]
        public void EveryStationPlate_ShowsCameraFacingNormals_AndStaysAClosedSolid()
        {
            // The composed-transform twin of GeneratedPlateShapes_FaceTheCamera. That test
            // checks the MESHES, which STATION-PLATFORM did not touch; this one checks what
            // survives the standing rotation those meshes are now placed under, which is where
            // this change could break them. Both matter: this codebase has lost days twice to
            // camera-facing triangles that were culled, once in a builder and once in a
            // transform, and only the first kind is visible in mesh data.
            var level = Import(FourLineJson());
            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());
            view.transform.localRotation = RealDioramaTilt();

            foreach (var entry in StationPlates(view))
            {
                var plate = entry.Value;
                var mesh = plate.GetComponent<MeshFilter>().sharedMesh;
                int towardCamera = 0;
                int awayFromCamera = 0;
                // TransformDirection, not the inverse-transpose: every face normal in these
                // four meshes is axis-aligned in the plate's own space, and an axis-aligned
                // normal survives a per-axis scale unrotated. A skewed normal would not, and
                // if one ever appears here this is the line to revisit.
                foreach (var normal in mesh.normals)
                {
                    float d = Vector3.Dot(
                        plate.TransformDirection(normal).normalized, Vector3.back);
                    if (d > 0.6f) towardCamera++;
                    if (d < -0.6f) awayFromCamera++;
                }
                Assert.That(towardCamera, Is.GreaterThan(0),
                    entry.Key + "'s badge presents no camera-facing facet once it is stood up —"
                    + " it renders as nothing, and no test that counts vertices can see it");
                Assert.That(awayFromCamera, Is.GreaterThan(0),
                    entry.Key + "'s badge is still a closed solid, so no winding or mirrored"
                    + " transform can turn it inside out");
            }
        }

        [Test]
        public void StationSignYaw_TracksTheRealDioramaTilt_RatherThanACopyThatCanDrift()
        {
            // The props lane now reads the scene lane's public tilt directly. Re-authoring the
            // diorama therefore changes the signs through one canonical value instead of a
            // mirror that could silently drift.
            Assert.That(Quaternion.Angle(BoardPropDecorator.StationSignRotation,
                    BoardPropDecorator.StandingSignRotation(BoardSceneLook.BoardTilt)),
                Is.LessThan(0.01f),
                "the station signs must derive directly from BoardSceneLook.BoardTilt");

            // The derivation itself, against the number the cat lane states for the same tilt
            // from its own independent implementation. Two lanes agreeing on -131.4 is what
            // makes this a property of the diorama rather than of either file.
            Assert.That(BoardPropDecorator.CameraFacingYawDegrees(RealDioramaTilt()),
                Is.EqualTo(-131.4f).Within(0.3f),
                "the board-local yaw that faces the camera under Euler(38, -32, -4)");
            Assert.That(Quaternion.Angle(BoardPropDecorator.StationSignRotation,
                    BoardPropDecorator.StandingSignRotation(RealDioramaTilt())),
                Is.LessThan(0.01f),
                "and the rotation the signs actually wear is that derivation, not a literal");
        }

        [Test]
        public void EveryStationPart_IsDecorationOnly_AndBindsItsOwnMaterial()
        {
            // L009 because its COOL berth accepts two lines, so it grows the largest part set
            // any authored level produces — the one most likely to have a straggler.
            var level = ImportLevel("L009");
            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());

            var parts = view.GetComponentsInChildren<Transform>(true)
                .Where(IsStationDecoration).ToArray();
            // Anti-vacuity, and matched to the relation rather than to a count: a platform is a
            // plinth, a deck, four posts and a canopy before any badge parts at all.
            Assert.That(parts.Length,
                Is.GreaterThanOrEqualTo(7 * level.Dto.Stations.Length),
                "the sweep must actually be finding the platform parts");

            foreach (var part in parts)
            {
                Assert.That(part.GetComponent<Collider>(), Is.Null,
                    part.name + " is decoration — the builtin-mesh idiom builds no collider,"
                    + " which is why there is none to remember to destroy");
                Assert.That(part.GetComponent<BoardElementId>(), Is.Null,
                    part.name + " must never enter the authored gameplay inventory");
                Assert.That(part.localScale.x, Is.GreaterThan(0f), part.name + " scale x");
                Assert.That(part.localScale.y, Is.GreaterThan(0f), part.name + " scale y");
                Assert.That(part.localScale.z, Is.GreaterThan(0f),
                    part.name + " scale z — a NEGATIVE scale mirrors the mesh through that axis"
                    + " and flips its winding, which is exactly how this codebase has twice"
                    + " ended up rendering camera-facing geometry as nothing");
                if (part.GetComponent<TextMesh>() != null) continue;
                var renderer = part.GetComponent<Renderer>();
                Assert.That(renderer, Is.Not.Null, part.name + " draws something");
                Assert.That(renderer.sharedMaterial, Is.Not.Null,
                    part.name + " must bind a material explicitly: AddComponent<MeshRenderer>()"
                    + " binds NOTHING and renders the magenta error shader");
            }
        }

        // The decoration prefixes, listed rather than matched on "station:" — BoardView names
        // the gameplay ANCHOR itself "station:RED", and that one legitimately carries both a
        // collider and a BoardElementId. A sweep that caught it would fail for the one reason
        // that is not a bug.
        private static readonly string[] StationDecorationPrefixes =
        {
            "station:wood-base", "station:wood-deck", "station:roof-post-", "station:line-roof",
            "station:plate-", "station:keyline-", "station:symbol-", "station:signmast-",
        };

        private static bool IsStationDecoration(Transform part) =>
            StationDecorationPrefixes.Any(prefix => part.name.StartsWith(prefix));

        private static Quaternion RealDioramaTilt()
        {
            var field = typeof(BoardSceneLook).GetField("BoardTilt",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null,
                "BoardSceneLook must still publish its diorama tilt as a static BoardTilt for"
                + " the props lane to be able to track it at all");
            return (Quaternion)field.GetValue(null);
        }

        private static Dictionary<string, BoardPropInstance> Kiosks(BoardView view) =>
            view.GetComponentsInChildren<BoardPropInstance>(true)
                .Where(x => x.AssetId == PropModelCatalog.StationKioskId)
                .ToDictionary(x => x.AnchorId);

        private static BoardElementId[] Stations(BoardView view) =>
            view.GetComponentsInChildren<BoardElementId>(true)
                .Where(x => x.Kind == "station")
                .OrderBy(x => x.Id, System.StringComparer.Ordinal).ToArray();

        private static string LineOf(ImportedLevel level, string nodeId) =>
            level.Dto.Stations.ToArray().Single(x => x.NodeId == nodeId).Accepts.Span[0];

        [Test]
        public void EveryPlateShape_RendersTheSizeItWasAskedFor()
        {
            // The general form of the bug above, and the assertion whose absence let it ship.
            // Every shape was pinned against its own literal scale, so the suite could not see
            // that one of them rendered at a different size from the rest — the one thing a
            // shape channel must never do, since a plate twice its neighbour's size reads as a
            // louder destination and there is no such thing in the game.
            //
            // Both sides come from the mesh's own bounds, so this holds for whatever geometry
            // Unity hands back rather than for the sizes this repo currently believes in. That
            // matters more than it sounds: the belief has now been wrong three times
            // (Sphere.fbx ~3.33 across, Cylinder.fbx 2 tall, Cylinder.fbx 2 across).
            const float size = 0.9f;
            const float depth = 0.1f;
            int checked_ = 0;
            foreach (string line in CatLine.Names)
            {
                var shape = CatLine.ShapeOf(line);
                var mesh = DestinationShapeMesh.ForShape(shape);
                var turned = DestinationShapeMesh.PlateRotation(shape) * Vector3.Scale(
                    mesh.bounds.size, DestinationShapeMesh.PlateScale(shape, size, depth));
                Assert.That(Mathf.Abs(turned.x), Is.EqualTo(size).Within(0.001f),
                    line + "'s " + shape + " plate must render " + size + " wide");
                Assert.That(Mathf.Abs(turned.y), Is.EqualTo(size).Within(0.001f),
                    line + "'s " + shape + " plate must render " + size + " tall");
                Assert.That(Mathf.Abs(turned.z), Is.EqualTo(depth).Within(0.001f),
                    line + "'s " + shape + " plate must stand " + depth + " off the board");
                checked_++;
            }
            // Fails closed: a vocabulary that went empty would otherwise pass this vacuously.
            Assert.That(checked_, Is.GreaterThan(1),
                "at least two lines must exist for 'the same size as each other' to mean"
                + " anything at all");
        }

        [Test]
        public void GeneratedPlateShapes_FaceTheCamera_AndAreClosedSolids()
        {
            // Two earlier lanes lost days to backface culling here. The camera looks from -Z,
            // so a plate whose front facets point away renders as nothing at all — and no test
            // that only counts vertices can see it.
            foreach (var shape in new[] { DestinationShape.Triangle, DestinationShape.Diamond })
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
        public void WildHasNoStationPlate_AndAskingForOneThrowsInsteadOfReturningGarbage()
        {
            // A star is CONCAVE and Extrude fans from vertex 0, so a fallthrough here would
            // hand back a self-overlapping tangle that renders as something plausible but
            // wrong. Silent geometry garbage is the failure this repo has already eaten twice,
            // so the unbuildable case is loud by construction.
            Assert.Throws<System.ArgumentException>(
                () => DestinationShapeMesh.ForShape(DestinationShape.Star),
                "wild is a cat colour, not a destination — no station wears a star");

            // Positive control: every shape a real LINE maps to is buildable, so the throw
            // above is about wild specifically and not a hole in the builder.
            foreach (string line in CatLine.Names)
                Assert.That(DestinationShapeMesh.ForShape(CatLine.ShapeOf(line)), Is.Not.Null,
                    line + " is a destination and must have a plate");
        }

        [Test]
        public void VocabularyMatchesTheCatManifest()
        {
            // The systemic fix, and the one worth more than any correction it forced. Every
            // cat model was GENERATED wearing a badge — a shape on its chest and a matching
            // tag on its collar — and those bytes are paid for and pinned by the licensing
            // record. The code table is therefore downstream of the manifest, and nothing was
            // checking that. Green had already drifted: the manifest says diamond, the code
            // said hexagon, and no green level had shipped to reveal it.
            //
            // Fails closed on a missing file. A conformance gate that reports OK after reading
            // nothing is the fail-open pattern this repo removed once already.
            string path = Path.Combine(UnityEngine.Application.dataPath,
                "..", "..", "docs", "design", "assets", "CAT-MANIFEST.json");
            Assert.That(File.Exists(path), Is.True,
                "the art record must be readable to be conformed to: " + path);
            string manifest = File.ReadAllText(path);

            // Art speaks English ("a white diamond badge on its chest"); code speaks enum.
            // Translating between them is this test's whole job, so the map lives here.
            var badges = new Dictionary<string, DestinationShape>
            {
                { "circle", DestinationShape.Circle },
                { "square", DestinationShape.Square },
                { "triangle", DestinationShape.Triangle },
                { "diamond", DestinationShape.Diamond },
                { "star", DestinationShape.Star },
            };

            // Every cat prompt pins its coat as a hex and its badge as a word. Pull both from
            // each prompt that carries them; poses of the same cat repeat, and must agree.
            var byHex = new Dictionary<string, string>();
            foreach (Match prompt in Regex.Matches(manifest, "\"prompt\"\\s*:\\s*\"([^\"]*)\""))
            {
                string text = prompt.Groups[1].Value;
                var hex = Regex.Match(text, @"hex ([0-9A-Fa-f]{6})");
                var badge = Regex.Match(text, @"white (\w+) badge");
                if (!hex.Success || !badge.Success) continue; // e.g. the conductor, unbadged
                string key = hex.Groups[1].Value.ToUpperInvariant();
                string shape = badge.Groups[1].Value.ToLowerInvariant();
                if (byHex.TryGetValue(key, out var seen))
                    Assert.That(shape, Is.EqualTo(seen),
                        "the manifest disagrees with itself for " + key
                        + ": poses of one cat must wear one badge");
                else byHex[key] = shape;
            }
            Assert.That(byHex.Count, Is.GreaterThanOrEqualTo(5),
                "anti-vacuity: the four lines plus wild must all be found in the manifest,"
                + " otherwise the parse has silently stopped matching and this passes on air");

            // The conformance itself, stated per line so a failure names the culprit.
            // Matched on CHANNEL VALUES within a tolerance rather than on a formatted hex
            // string: the palette is authored as n/255 floats and how Unity rounds those back
            // to bytes is not what this test is about — a pin that failed on a rounding mode
            // would be reporting the wrong thing entirely.
            foreach (string line in CatLine.Names.Concat(new[] { "wild" }))
            {
                Color code = CatLine.ColorOf(line);
                string matched = byHex.Keys.FirstOrDefault(x => HexMatches(x, code));
                Assert.That(matched, Is.Not.Null,
                    "code paints " + line + " as #" + ColorUtility.ToHtmlStringRGB(code)
                    + ", which no cat model wears — either Palette drifted from the art or"
                    + " the models were regenerated without the palette following");
                // TryGetValue, not an indexer. This fires in precisely the scenario the gate
                // exists for — art regenerated wearing a badge the enum has no member for, a
                // pentagon or a heart or a shield — and a bare KeyNotFoundException would name
                // neither the line, the hex, nor the unrecognised word.
                Assert.That(badges.TryGetValue(byHex[matched], out var art), Is.True,
                    "UNKNOWN BADGE for " + line + ": the manifest badges #" + matched + " as '"
                    + byHex[matched] + "', which DestinationShape has no member for. The art"
                    + " was regenerated with a new shape — add it to the enum, to"
                    + " DestinationShapeMesh (or throw, if it is not a destination), and to"
                    + " HudShapeSprites, then map it here.");
                Assert.That(CatLine.ShapeOf(line), Is.EqualTo(art),
                    "SHAPE MISMATCH for " + line + " (#" + matched + "): the cat model wears a "
                    + byHex[matched] + " badge (" + art + ") but CatLine.ShapeOf says "
                    + CatLine.ShapeOf(line) + ". Model bytes are pinned by the licensing"
                    + " record, so the code moves, not the art.");
            }
        }

        private static bool HexMatches(string hex, Color color)
        {
            int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
            return Mathf.Abs(color.r * 255f - r) <= 1.5f
                && Mathf.Abs(color.g * 255f - g) <= 1.5f
                && Mathf.Abs(color.b * 255f - b) <= 1.5f;
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
            // Physical face width, not localScale: COOL's primary is a square today, so a
            // scale factor happens to equal a width here — but a circle-primary berth with
            // chips would have compared 1.08 against a keyline that really spanned 2.16 and
            // reported clearance it did not have. Measure what is drawn; the neighbouring
            // plate bug was exactly this arithmetic trusted one file over.
            float clearance =
                (PlateSizeInSignFrame(primaryKeyline).x
                    + PlateSizeInSignFrame(chipKeyline).x) * 0.5f;
            Assert.That(Mathf.Abs(chipKeyline.localPosition.x - primaryKeyline.localPosition.x),
                Is.GreaterThan(clearance),
                "the accept chip's keyline must not overlap the primary keyline beside it");
            Assert.That(chipKeyline.localPosition.y,
                Is.EqualTo(primaryKeyline.localPosition.y).Within(0.0001f),
                "and it shares the plate's row, so the badge reads as one strip of signage");
            // STATION-PLATFORM: this used to pin the primary KEYLINE at x = 0. It cannot any
            // more, and the reason is worth having in the file rather than in a diff. The three
            // badge layers are stacked along the SIGN's facing axis now instead of along board
            // -Z, and that axis has a board-x component, so the halo sits a hair off centre in
            // x by construction. The thing the old line was really protecting — "a second
            // accepted line never shoves the primary sideways" — belongs on the PLATE, which is
            // the part the player reads and which genuinely has not moved at all.
            Assert.That(primary.localPosition.x, Is.EqualTo(0f).Within(0.0001f),
                "the primary never moves to make room — that is what keeps a single-accept"
                + " station identical to what it rendered before chips existed");
            Assert.That(primary.localPosition.y, Is.EqualTo(-1f).Within(0.0001f),
                "and it holds the shipped row height too");

            // The keyline is BEHIND the plate and nowhere else. Decomposed against the sign's
            // own axes, because "behind" stopped being a board direction when the badge stood
            // up: a halo displaced sideways or vertically instead would peek out of one edge of
            // its own plate, and a halo at zero offset would z-fight with it. Neither is
            // visible to any assertion above, which all check meshes, colours, names and sizes.
            var sign = BoardPropDecorator.StationSignRotation;
            Vector3 offset = primaryKeyline.localPosition - primary.localPosition;
            Assert.That(Vector3.Dot(offset, sign * Vector3.right), Is.EqualTo(0f).Within(0.0001f),
                "the keyline is not displaced across the sign's face");
            Assert.That(Vector3.Dot(offset, sign * Vector3.up), Is.EqualTo(0f).Within(0.0001f),
                "nor up or down it");
            Assert.That(Vector3.Dot(offset, sign * Vector3.back), Is.LessThan(-0.001f),
                "it sits BEHIND the plate, which is what makes it read as a halo around the"
                + " silhouette rather than a second plate in front of the first");

            // The chips grow sideways toward the board edge, and BoardSurface.Margin is only
            // 1.05 — so "does it still fit on the wood" is a real question and this is the
            // only test that can ask it. RuntimeSceneRigTests owns the equivalent framing
            // check but runs L008, which has no multi-accept berth.
            var wood = view.transform.Find("BoardBody/WoodTop");
            Assert.That(wood, Is.Not.Null, "precondition: the tabletop exists to fit onto");
            Bounds surface = wood.GetComponent<Renderer>().bounds;
            // The masts are in this sweep for a reason that is not decoration: a post runs from
            // the badge all the way DOWN to the wood, so it reaches further toward the board's
            // near rim than anything the chip lane had to fit, and it is the one new part that
            // could hang off the edge.
            var masts = station.GetComponentsInChildren<Transform>(true)
                .Where(x => x.name.StartsWith("station:signmast")).ToArray();
            Assert.That(masts.Length, Is.EqualTo(2),
                "a two-line berth posts two signs, so it stands two masts");
            foreach (var chip in chips.Concat(new[] { chipKeyline }).Concat(masts))
            {
                Bounds b = chip.GetComponent<Renderer>().bounds;
                Assert.That(b.min.x, Is.GreaterThan(surface.min.x), chip.name + " off left");
                Assert.That(b.max.x, Is.LessThan(surface.max.x), chip.name + " off right");
                Assert.That(b.min.y, Is.GreaterThan(surface.min.y), chip.name + " off bottom");
                Assert.That(b.max.y, Is.LessThan(surface.max.y), chip.name + " off top");
            }

            // AddComponent<MeshRenderer>() binds NOTHING, where CreatePrimitive used to hand
            // back Unity's default material for free. Every badge renderer must therefore be
            // bound explicitly — a miss renders the magenta error shader, and DeviceConfigTests'
            // scene-wide material walk only ever runs L001.
            foreach (var part in station.GetComponentsInChildren<Renderer>(true))
                if (part.gameObject.name.StartsWith("station:")
                    && part.GetComponent<TextMesh>() == null)
                    Assert.That(part.sharedMaterial, Is.Not.Null,
                        part.gameObject.name + " must bind a material explicitly");

            // Positive/negative control in the same fixture: the single-accept berth beside it
            // grows no chips at all, so the row is evidence of a second line and not decoration.
            Assert.That(AcceptChips(Station(view, "RED")).Length, Is.Zero);
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
        }

        [Test]
        public void EmptyAcceptsBerth_BadgesTheBugLoudly_ThroughTheRealMaterialPath()
        {
            // The only unknown line that can actually REACH the fallback is "" from a station
            // with an empty accepts list: LevelImporter rejects any other unknown colour
            // outright, and ReqArr caps that array's size without requiring a minimum.
            //
            // This is the end-to-end pin, and it exists because the test above is not enough
            // on its own. The primary plate never calls CatLine.ColorOf — it inherits the
            // station anchor's material, which BoardView tinted. BoardView used to hold its
            // own private copy of the colour switch, so the two could have drifted and the
            // badge could have gone red while CatLine still said magenta. BoardView.ColorFor
            // now delegates, and this asserts the result at the surface a player sees.
            var result = LevelImporter.Import(Encoding.UTF8.GetBytes(EmptyAcceptsJson()));
            if (!result.Ok)
                Assert.Ignore("content refuses an empty accepts list outright (" + result.Error
                    + "), which makes the unknown-line fallback unreachable — a better answer"
                    + " than a loud badge, and worth recording rather than asserting past");

            var level = result.Value;
            var view = BuildBoard(level);
            BoardPropDecorator.Decorate(level, view.transform, KioskCatalog());
            var station = Station(view, "NUL");

            var plate = station.transform.Find("station:plate-generated");
            Assert.That(plate, Is.Not.Null, "an unmapped berth still gets a badge");
            Color worn = plate.GetComponent<Renderer>().sharedMaterial.color;
            Assert.That(worn.r, Is.EqualTo(1f).Within(0.01f), "bug colour r");
            Assert.That(worn.g, Is.EqualTo(0f).Within(0.01f), "bug colour g");
            Assert.That(worn.b, Is.EqualTo(1f).Within(0.01f), "bug colour b");
            Assert.That(plate.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(DestinationShapeMesh.ForShape(DestinationShape.Circle)),
                "the shape channel has no fifth shape and falls back to red's circle — which"
                + " is only safe because the colour above is magenta, not SignalRed");
            Assert.That(station.GetComponentsInChildren<TextMesh>(true).Any(x => x.text == "?"),
                Is.True, "and the letter channel says unknown too");

            // The whole point: a magenta circle and a red circle must not be the same badge.
            var red = Station(view, "RED").transform.Find("station:plate-generated");
            Assert.That(red.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(plate.GetComponent<MeshFilter>().sharedMesh),
                "positive control: they really do share a shape, so colour is the separator");
            Assert.That(red.GetComponent<Renderer>().sharedMaterial.color,
                Is.Not.EqualTo(worn), "and it separates them");
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

        // Keyed by LINE, not by a DestinationShape written out here: the expected rotation has
        // to come from the same vocabulary the board asked, or this test becomes the second
        // shape decision site the whole branch exists to prevent.
        private static void AssertShapeRotation(Transform plate, string line)
        {
            var expected = DestinationShapeMesh.PlateRotation(CatLine.ShapeOf(line));
            var standingRemoved = Quaternion.Inverse(BoardPropDecorator.StationSignRotation)
                * plate.localRotation;
            Assert.That(Quaternion.Angle(standingRemoved, expected), Is.LessThan(0.01f),
                line + "'s plate must be exactly the vocabulary's shape rotation with the ONE"
                + " shared standing turn composed on its left — no per-plate correction, and"
                + " not the operands the other way round (which for a circle looks identical"
                + " and for a triangle silently moves the apex)");
        }

        // The size a plate actually OCCUPIES in the shared sign frame: the mesh's own bounds,
        // scaled by localScale, with the standing-sign rotation factored back out so a
        // laid-flat cylinder and an upright cube are described in the same face/depth axes.
        // Deliberately NOT localScale — a scale factor is only a size for a unit mesh.
        private static Vector3 PlateSizeInSignFrame(Transform plate)
        {
            Vector3 intrinsic = plate.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            Quaternion shapeRotation =
                Quaternion.Inverse(BoardPropDecorator.StationSignRotation)
                * plate.localRotation;
            Vector3 turned = shapeRotation * Vector3.Scale(intrinsic, plate.localScale);
            return new Vector3(
                Mathf.Abs(turned.x), Mathf.Abs(turned.y), Mathf.Abs(turned.z));
        }

        private static void AssertSignFrameSize(
            Transform plate, float x, float y, float z, string because)
        {
            var actual = PlateSizeInSignFrame(plate);
            Assert.That(actual.x, Is.EqualTo(x).Within(0.001f), because);
            Assert.That(actual.y, Is.EqualTo(y).Within(0.001f), because);
            Assert.That(actual.z, Is.EqualTo(z).Within(0.001f), because);
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

        // A berth that accepts nothing. Not a level anyone would author — it is the one shape
        // of content bug that reaches CatLine's unknown-line fallback, because ReqArr caps the
        // accepts array without requiring a minimum.
        private static string EmptyAcceptsJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T941"", ""name"": ""Empty Accepts Fixture"", ""seed"": 941,
  ""meta"": { ""band"": ""alternation"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9 },
      { ""id"": ""J1"", ""x"": 3, ""y"": 6 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 }, { ""id"": ""NUL"", ""x"": 5, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 10 },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""RED"", ""travelTicks"": 10 },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""NUL"", ""travelTicks"": 10 } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] } ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 6 },
    { ""nodeId"": ""NUL"", ""accepts"": [], ""capacity"": 6 } ],
  ""switches"": [
    { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 } ],
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
            foreach (string id in AuthoredLevelIds())
            {
                var level = ImportLevel(id);
                var view = BuildBoard(level);
                var root = BoardPropDecorator.Decorate(level, view.transform, CompleteCatalog());
                var props = root.GetComponentsInChildren<BoardPropInstance>(true);

                // Relational for the same reason as the furnished sweep: one depot per source,
                // so a two-source board must produce one more prop than a one-source board of
                // the same station count. Also unexercised until feat/level-variety lifts the
                // second-source pin — see that sweep for the detail.
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
                // Prefix, not exact name: a cream keyline is decoration by this helper's own
                // logic, and a multi-accept berth grows "station:keyline-accept-N" beside the
                // primary. Matching only the exact name would quietly fold that halo into the
                // signature as if it were line coding the first time this meets such a level.
                //
                // station:signmast is decoration by exactly the same argument — it is the post
                // the badge stands on, made of toy wood, and it carries none of the three
                // channels (colour, shape, letter) this signature exists to compare. Note it
                // would not even report the wood: a mast wears GreyboxMaterial.Shared plus a
                // property block, so renderer.material.color is Greybox's white. Folding a
                // constant white into every station's signature would not make this helper
                // WRONG, only vacuous in one more place — which is worse, because it still
                // reads as coverage.
                if (renderer.gameObject.name.StartsWith("station:keyline")
                    || renderer.gameObject.name.StartsWith("station:signmast")) continue;
                var material = renderer.material;
                parts.Add(renderer.GetType().Name + ":" + material.color);
            }
            foreach (var text in station.GetComponentsInChildren<TextMesh>(true))
                if (text.GetComponent<Renderer>().enabled) parts.Add("text:" + text.text);
            parts.Sort();
            return string.Join("|", parts);
        }

        // TWO read-backs live in this file and they are NOT interchangeable — swapping them
        // fails silently, with a plausible-looking colour, which is the worst way to fail.
        //
        //   PropertyColor(r)        — for anything wearing GreyboxMaterial.Shared plus a
        //                             MaterialPropertyBlock: the accept chips, the kiosk wood
        //                             base, the line roof. Their sharedMaterial.color is
        //                             Greybox's own white and says nothing about the line.
        //   r.sharedMaterial.color  — for the PRIMARY station plate only. It inherits the
        //                             station anchor's real per-station material (BoardView
        //                             tinted it) and carries no property block at all, so
        //                             PropertyColor returns an unset black.
        //
        // On URP/Lit, `.color` IS _BaseColor — the shader tags it [MainColor]. The stale
        // _Color line in Greybox.mat is an unread leftover; do not reach for it.
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

        // Derived from the corpus, never a literal bound. Both sweeps below are named
        // AllAuthoredLevels and used to loop `levelNumber <= 17`, which was true when it was
        // written and quietly stopped being true: feat/level-variety adds L018 and L019, so a
        // hardcoded bound would skip the two newest boards while still reading as full
        // coverage. That is the same class of measurement bug this branch has spent its time
        // fixing, so it is not worth re-committing here in a smaller form.
        private static string[] AuthoredLevelIds()
        {
            string staged = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "content", "levels");
            Assert.That(Directory.Exists(staged), Is.True,
                "the staged corpus must be readable to be swept: " + staged);
            var ids = LevelIdsIn(staged);

            // Fail closed with a floor. A sweep that finds nothing — or finds a handful
            // because it is pointed at the wrong directory — must not report full coverage.
            Assert.That(ids.Length, Is.GreaterThanOrEqualTo(17),
                "anti-vacuity: the corpus held 17 authored levels when this was written, so"
                + " finding fewer means this is reading the wrong place rather than that"
                + " levels were deleted");

            // StreamingAssets holds tracked COPIES that scripts/stage-content.sh syncs from
            // content/levels. Deriving from the staged copy alone would still rot: a level
            // authored but never staged gets swept straight past, which is the identical
            // silent under-coverage wearing a different hat.
            //
            // These level JSONs are the only mirror in the repo that CAN drift like this. The
            // dotnet leg compiles the Unity C# in place — `<Compile Include=
            // "../../unity/Assets/Scripts/**/*.cs" />` — rather than copying it, so there is
            // no second copy of the sources to fall behind.
            string authored = Path.Combine(UnityEngine.Application.dataPath,
                "..", "..", "content", "levels");
            Assert.That(Directory.Exists(authored), Is.True,
                "the authored corpus must be readable to compare against: " + authored);
            Assert.That(ids, Is.EqualTo(LevelIdsIn(authored)),
                "the staged corpus has drifted from the authored one — run"
                + " scripts/stage-content.sh. Sweeping the stale copy would silently be"
                + " testing yesterday's levels while claiming to cover them all.");
            return ids;
        }

        private static string[] LevelIdsIn(string directory) =>
            Directory.GetFiles(directory, "L*.json")
                .Where(x => Path.GetExtension(x) == ".json") // never the .meta siblings
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(x => x, System.StringComparer.Ordinal)
                .ToArray();

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
