using System;
using System.Reflection;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using NUnit.Framework;

namespace CatMetro.Tests.Presentation
{
    // CM-CATS-WIRE AC1/AC2, phase-1 RED. Reflection is deliberate: current main still
    // compiles, then fails with a named missing-seam assertion instead of failing because
    // the gitignored GLBs are absent from a clean checkout.
    public sealed class CatModelManifestMapTests
    {
        private const string MapTypeName =
            "CatMetro.Presentation.Cats.CatModelManifestMap";

        private static Type RequireMapType()
        {
            var type = typeof(BoardView).Assembly.GetType(MapTypeName, false);
            Assert.That(type, Is.Not.Null,
                "CM-CATS-WIRE expected RED: CatModelManifestMap seam is missing");
            return type;
        }

        private static MethodInfo RequireStaticMethod(Type type, string name)
        {
            var method = type.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null,
                MapTypeName + "." + name + " must be the closed mapping seam");
            return method;
        }

        private static string BoardId(Type type, byte color)
        {
            return (string)RequireStaticMethod(type, "BoardManifestId")
                .Invoke(null, new object[] { color });
        }

        private static string HomeId(Type type, string slot)
        {
            return (string)RequireStaticMethod(type, "HomeManifestId")
                .Invoke(null, new object[] { slot });
        }

        private static string Filename(Type type, string manifestId)
        {
            return (string)RequireStaticMethod(type, "DerivativeFilename")
                .Invoke(null, new object[] { manifestId });
        }

        private static long StaticNumber(Type type, string name)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var field = type.GetField(name, flags);
            if (field != null) return Convert.ToInt64(field.GetValue(null));
            var property = type.GetProperty(name, flags);
            Assert.That(property, Is.Not.Null,
                MapTypeName + "." + name + " budget is missing");
            return Convert.ToInt64(property.GetValue(null, null));
        }

        private static void AssertBoardMap(Func<byte, string> lookup)
        {
            Assert.That(lookup(CatColor.Red), Is.EqualTo("cat-red-tabby"));
            Assert.That(lookup(CatColor.Blue), Is.EqualTo("cat-blue-siamese"));
            Assert.That(lookup(CatColor.Yellow), Is.EqualTo("cat-yellow-longhair"));
            Assert.That(lookup(CatColor.Green), Is.EqualTo("cat-green-shorthair"));
            Assert.That(lookup(CatColor.Wild), Is.EqualTo("cat-wild-alley"));
        }

        private static string FixtureBoardId(byte color)
        {
            if (color == CatColor.Red) return "cat-red-tabby";
            if (color == CatColor.Blue) return "cat-blue-siamese";
            if (color == CatColor.Yellow) return "cat-yellow-longhair";
            if (color == CatColor.Green) return "cat-green-shorthair";
            if (color == CatColor.Wild) return "cat-wild-alley";
            return null;
        }

        [Test]
        public void OracleControl_SwappedBoardMappingIsRejected()
        {
            Assert.DoesNotThrow(() => AssertBoardMap(FixtureBoardId),
                "the independent compliant fixture proves the oracle can pass");
            Assert.Throws<AssertionException>(() => AssertBoardMap(color =>
                color == CatColor.Red ? "cat-blue-siamese" : FixtureBoardId(color)),
                "the same oracle must reject the red/blue swap mutation");
        }

        [Test]
        public void ClosedMap_ResolvesTheEightFrozenManifestRows()
        {
            var type = RequireMapType();
            AssertBoardMap(color => BoardId(type, color));

            Assert.That(HomeId(type, "ParkedDistrictA"),
                Is.EqualTo("cat-red-tabby-sitting"));
            Assert.That(HomeId(type, "ParkedDistrictB"),
                Is.EqualTo("cat-blue-siamese-loaf"));
            Assert.That(HomeId(type, "ParkedDistrictC"),
                Is.EqualTo("cat-conductor"));

            Assert.That(Filename(type, "cat-red-tabby"),
                Is.EqualTo("cat-red-tabby.glb"));
            Assert.That(Filename(type, "cat-blue-siamese"),
                Is.EqualTo("cat-blue-siamese.glb"));
            Assert.That(Filename(type, "cat-yellow-longhair"),
                Is.EqualTo("cat-yellow-longhair.glb"));
            Assert.That(Filename(type, "cat-green-shorthair"),
                Is.EqualTo("cat-green-shorthair.glb"));
            Assert.That(Filename(type, "cat-wild-alley"),
                Is.EqualTo("cat-wild-alley.glb"));
            Assert.That(Filename(type, "cat-red-tabby-sitting"),
                Is.EqualTo("cat-red-tabby-sitting.glb"));
            Assert.That(Filename(type, "cat-blue-siamese-loaf"),
                Is.EqualTo("cat-blue-siamese-loaf.glb"));
            Assert.That(Filename(type, "cat-conductor"),
                Is.EqualTo("cat-conductor.glb"));

            // Live-seam mutation control: once the map exists, a swapped red/blue result is
            // observed through the production lookup, not only through the fixture above.
            Assert.Throws<AssertionException>(() => AssertBoardMap(color =>
                color == CatColor.Red ? "cat-blue-siamese" : BoardId(type, color)));
        }

        [Test]
        public void ClosedMap_UnknownKeysReturnNull_NeverAnArbitraryCat()
        {
            var type = RequireMapType();
            Assert.That(BoardId(type, CatColor.None), Is.Null);
            Assert.That(BoardId(type, byte.MaxValue), Is.Null);
            Assert.That(HomeId(type, "ParkedDistrictD"), Is.Null);
            Assert.That(HomeId(type, null), Is.Null);
            Assert.That(Filename(type, "cat-yellow-longhair-wave"), Is.Null,
                "the known-blemish wave asset is deliberately outside this closed slice");
            Assert.That(Filename(type, "CAT-RED-TABBY"), Is.Null,
                "manifest ids are case-sensitive");
        }

        [Test]
        public void Budgets_PinBothSurfaceCaps_CombinedTriangles_AndSourceBytes()
        {
            var type = RequireMapType();
            Assert.That(StaticNumber(type, "BoardInstanceLimit"), Is.EqualTo(9));
            Assert.That(StaticNumber(type, "HomeInstanceLimit"), Is.EqualTo(3));
            Assert.That(StaticNumber(type, "CombinedInstanceLimit"), Is.EqualTo(12));
            Assert.That(StaticNumber(type, "CombinedTriangleLimit"), Is.EqualTo(180000));
            Assert.That(StaticNumber(type, "SelectedSourceByteLimit"),
                Is.EqualTo(20L * 1024L * 1024L));

            // Evidence fact, not a production exact-byte pin: the eight current PR-#94
            // derivatives total 17,434,232 bytes and fit below the frozen ceiling.
            const long measuredSelectedBytes = 17434232L;
            Assert.That(measuredSelectedBytes,
                Is.LessThanOrEqualTo(StaticNumber(type, "SelectedSourceByteLimit")));

            // Boundary controls: equality is accepted; one byte/triangle above is not.
            Assert.That(12L * 15000L,
                Is.EqualTo(StaticNumber(type, "CombinedTriangleLimit")));
            Assert.That(12L * 15000L + 1L,
                Is.GreaterThan(StaticNumber(type, "CombinedTriangleLimit")));
            Assert.That(20L * 1024L * 1024L + 1L,
                Is.GreaterThan(StaticNumber(type, "SelectedSourceByteLimit")));
        }
    }
}
