using System;
using System.Collections;
using System.Linq;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class CaptureRigTests
    {
        [Test]
        public void ParseSize_EmptyValueKeepsCallerPinnedDefault()
        {
            CaptureRig.Size size = CaptureRig.ParseSize(null, 917, 2048);

            Assert.That(size.Width, Is.EqualTo(917));
            Assert.That(size.Height, Is.EqualTo(2048));
        }

        [TestCase("1179x2556", 1179, 2556)]
        [TestCase("1080x1920", 1080, 1920)]
        public void ParseSize_ExactContractReturnsRequestedPixels(string value,
            int expectedWidth, int expectedHeight)
        {
            CaptureRig.Size size = CaptureRig.ParseSize(value, 917, 2048);

            Assert.That(size.Width, Is.EqualTo(expectedWidth));
            Assert.That(size.Height, Is.EqualTo(expectedHeight));
        }

        [TestCase("1179X2556")]
        [TestCase("0x1920")]
        [TestCase("1080x")]
        [TestCase("1080x1920x24")]
        public void ParseSize_InvalidContractFailsLoudly(string value)
        {
            Assert.That(() => CaptureRig.ParseSize(value, 917, 2048),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void ResolveLevel_UsesTheRuntimeBandWithoutBakingItsLength()
        {
            string[] futureBand = { "L058", "L059", "L060" };

            Assert.That(CaptureRig.ResolveLevel(null, futureBand), Is.EqualTo("L058"));
            Assert.That(CaptureRig.ResolveLevel("L060", futureBand), Is.EqualTo("L060"));
            Assert.That(() => CaptureRig.ResolveLevel("L019", futureBand),
                Throws.TypeOf<ArgumentException>());
        }

        [TestCase(null, 14)]
        [TestCase("0", 0)]
        [TestCase("96", 96)]
        public void ParseTick_ReturnsAnExactNonNegativeBoundary(string value, int expected)
        {
            Assert.That(CaptureRig.ParseTick(value, 14), Is.EqualTo(expected));
        }

        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase(" 12")]
        public void ParseTick_InvalidContractFailsLoudly(string value)
        {
            Assert.That(() => CaptureRig.ParseTick(value, 14),
                Throws.TypeOf<FormatException>());
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("on", true)]
        [TestCase("off", false)]
        public void ParseHud_UsesTheExactOnOffContract(string value, bool expected)
        {
            Assert.That(CaptureRig.ParseHud(value), Is.EqualTo(expected));
        }

        [TestCase("ON")]
        [TestCase("true")]
        [TestCase("0")]
        [TestCase(" off")]
        public void ParseHud_RejectsValuesOutsideTheExactContract(string value)
        {
            Assert.That(() => CaptureRig.ParseHud(value), Throws.TypeOf<FormatException>());
        }

        [TestCase(0, 5)]
        [TestCase(1, 0)]
        [TestCase(0, 0)]
        public void RequireStoreCaptureArt_RefusesWhenEitherCatalogIsEmpty(
            int admittedCatEntries, int admittedPropEntries)
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                CaptureRig.RequireStoreCaptureArt(null,
                    admittedCatEntries, admittedPropEntries));

            StringAssert.Contains(
                $"CatModelCatalog.AdmittedEntryCount={admittedCatEntries}",
                exception.Message);
            StringAssert.Contains(
                $"PropModelCatalog.AdmittedEntryCount={admittedPropEntries}",
                exception.Message);
            StringAssert.Contains("main checkout", exception.Message);
            StringAssert.Contains("CM_CAPTURE_ALLOW_PLACEHOLDER=1", exception.Message);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0")]
        [TestCase("true")]
        [TestCase(" 1")]
        public void RequireStoreCaptureArt_OnlyExactOneBypassesMissingArt(string value)
        {
            Assert.That(() => CaptureRig.RequireStoreCaptureArt(value, 0, 0),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void RequireStoreCaptureArt_ExplicitPlaceholderOverrideAllowsDiagnostics()
        {
            Assert.DoesNotThrow(() => CaptureRig.RequireStoreCaptureArt("1", 0, 0));
        }

        [Test]
        public void RequireStoreCaptureArt_InstalledCatalogsNeedNoOverride()
        {
            Assert.DoesNotThrow(() => CaptureRig.RequireStoreCaptureArt(null, 1, 5));
        }

        [Test]
        public void ParseSwitchReceipts_ResolvesAuthoredIdsAndPreservesReceiptOrder()
        {
            var switches = new ContentIdMap(new[] { "S1", "S2" });

            CaptureRig.SwitchReceipt[] receipts = CaptureRig.ParseSwitchReceipts(
                "S2@12,S1@12,S2@42", switches, captureTick: 42).ToArray();

            Assert.That(receipts.Select(receipt => receipt.SwitchId),
                Is.EqualTo(new[] { "S2", "S1", "S2" }));
            Assert.That(receipts.Select(receipt => receipt.SwitchIndex),
                Is.EqualTo(new[] { 1, 0, 1 }));
            Assert.That(receipts.Select(receipt => receipt.Tick),
                Is.EqualTo(new[] { 12, 12, 42 }));
        }

        [TestCase("S3@12")]
        [TestCase("S1")]
        [TestCase("S1@-1")]
        [TestCase("S1@13")]
        [TestCase("S1@12,")]
        public void ParseSwitchReceipts_InvalidOrUnreachableReceiptFailsLoudly(string value)
        {
            var switches = new ContentIdMap(new[] { "S1", "S2" });

            Assert.That(() => CaptureRig.ParseSwitchReceipts(value, switches, captureTick: 12),
                Throws.Exception);
        }

        [UnityTest]
        public IEnumerator Replay_UsesShippedSessionForTheReachableL032HeroBoundary()
        {
            GameRoot root = null;
            try
            {
                GameRoot.DevSkipShippedHome = true;
                root = GameRoot.Launch(GameRoot.LevelPath("L032"));
                root.enabled = false;
                var receipts = CaptureRig.ParseSwitchReceipts("S1@1",
                    root.Session.Level.IdMaps.Switches, captureTick: 5);

                CaptureRig.Replay(root, captureTick: 5, receipts);

                Assert.That(root.CurrentLevelId, Is.EqualTo("L032"));
                Assert.That(root.Session.State.Tick, Is.EqualTo(5));
                Assert.That(root.Session.Alpha, Is.Zero.Within(0.000000001d));
                Assert.That(root.Session.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Running));
                Assert.That(root.Session.State.Deliveries, Is.Zero);
                Assert.That(root.Session.State.SwitchesUsed, Is.EqualTo(1));
                Assert.That(root.Session.Log.Entries.Count, Is.EqualTo(1));
                Assert.That(root.Session.Log.Entries[0].SwitchId, Is.EqualTo(0));
                Assert.That(root.Session.Log.Entries[0].Tick, Is.EqualTo(1));
                Assert.That(SwitchState.Route(root.Session.State.SwitchRoutes[0]), Is.EqualTo(1));

                TrainSlot[] live = root.Session.State.Trains
                    .Where(train => train.Id != 0 && train.State != TrainState.None).ToArray();
                Assert.That(live, Has.Length.EqualTo(1));
                Assert.That(CatToken.Color(live[0].Color), Is.EqualTo(CatColor.Blue));
                Assert.That(CatToken.Shape(live[0].Color), Is.EqualTo(CatShape.Round));
                Assert.That(live[0].EdgeId,
                    Is.EqualTo(root.Session.Level.IdMaps.Edges.IndexOf("E_FEED")));
                Assert.That(live[0].ProgressTicks, Is.EqualTo(2));
                Assert.That(root.Session.TrainOccupantGeneration(0), Is.GreaterThan(0),
                    "the passenger must have spawned through GameSession, not hand placement");
                Assert.That(root.Session.State.NodeQueueCounts, Is.All.Zero);
                yield return null;
            }
            finally
            {
                GameRoot.DevSkipShippedHome = false;
                if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Replay_ReceiptAtTargetBoundaryIsPendingAndAlreadyVisibleToTheView()
        {
            GameRoot root = null;
            try
            {
                GameRoot.DevSkipShippedHome = true;
                root = GameRoot.Launch(GameRoot.LevelPath("L030"));
                root.enabled = false;
                int initialRoute = SwitchState.Route(root.Session.State.SwitchRoutes[0]);
                var receipts = CaptureRig.ParseSwitchReceipts("S1@1",
                    root.Session.Level.IdMaps.Switches, captureTick: 1);

                CaptureRig.Replay(root, captureTick: 1, receipts);

                Assert.That(root.Session.State.Tick, Is.EqualTo(1));
                Assert.That(root.Session.Log.Entries.Single().Tick, Is.EqualTo(1));
                Assert.That(SwitchState.Route(root.Session.State.SwitchRoutes[0]),
                    Is.EqualTo(initialRoute),
                    "a receipt stamped at T applies to authoritative state after boundary T");
                Assert.That(root.Session.PendingToggleCount(0), Is.EqualTo(1),
                    "the shipped view uses the pending receipt as the committed lever route");
                Assert.That(root.View.CommittedRoute(0),
                    Is.EqualTo((initialRoute + 1) % root.Session.Level.Graph.SwitchRoutes[0].Length));
                yield return null;
            }
            finally
            {
                GameRoot.DevSkipShippedHome = false;
                if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Replay_RejectsATerminalTargetThatGameRootCannotPresentWhileFrozen()
        {
            GameRoot root = null;
            try
            {
                GameRoot.DevSkipShippedHome = true;
                root = GameRoot.Launch(GameRoot.LevelPath("L001"));
                root.enabled = false;
                var receipts = CaptureRig.ParseSwitchReceipts("S1@0",
                    root.Session.Level.IdMaps.Switches, captureTick: 29);

                Assert.That(() => CaptureRig.Replay(root, captureTick: 29, receipts),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("terminal state"));
                yield return null;
            }
            finally
            {
                GameRoot.DevSkipShippedHome = false;
                if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void ScaleSafeArea_PreservesReferenceFractions()
        {
            var reference = new Rect(0f, 64f, 917f, 1920f);
            var target = new CaptureRig.Size(1080, 1920);

            Rect scaled = CaptureRig.ScaleSafeArea(reference, 917, 2048, target);

            Assert.That(scaled.x, Is.Zero.Within(0.001f));
            Assert.That(scaled.y, Is.EqualTo(60f).Within(0.001f));
            Assert.That(scaled.width, Is.EqualTo(1080f).Within(0.001f));
            Assert.That(scaled.height, Is.EqualTo(1800f).Within(0.001f));
        }

        [Test]
        public void CreateTarget_AndReadbackAreSrgbAndOpaqueRgb()
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = null;
            Texture2D texture = null;
            try
            {
                target = CaptureRig.CreateTarget(new CaptureRig.Size(8, 4));
                Assert.That(target.sRGB, Is.True);
                RenderTexture.active = target;
                GL.Clear(clearDepth: true, clearColor: true, backgroundColor: Color.black);
                texture = CaptureRig.ReadRgb24(target);

                Assert.That(texture.format, Is.EqualTo(TextureFormat.RGB24));
                Assert.That(texture.isDataSRGB, Is.True);
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        [Test]
        public void EncodeOpaqueSrgbPng_EmitsRgbPixelsAndAnExplicitSrgbChunk()
        {
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(2, 1, TextureFormat.RGB24,
                    mipChain: false, linear: false);
                texture.SetPixels(new[] { Color.red, Color.blue });
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                byte[] png = CaptureRig.EncodeOpaqueSrgbPng(texture);

                Assert.That(png[25], Is.EqualTo(2),
                    "IHDR colour type 2 is truecolour RGB with no alpha channel");
                int chunk = FindPngChunk(png, "sRGB");
                Assert.That(chunk, Is.GreaterThan(0), "the PNG must explicitly declare sRGB");
                Assert.That(ReadBigEndianInt32(png, chunk), Is.EqualTo(1));
                Assert.That(png[chunk + 8], Is.Zero,
                    "rendering intent 0 is PNG's perceptual sRGB intent");
                Assert.That(png.Skip(chunk + 9).Take(4).ToArray(),
                    Is.EqualTo(new byte[] { 0xae, 0xce, 0x1c, 0xe9 }),
                    "the inserted sRGB chunk must carry its standard CRC");
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static int FindPngChunk(byte[] png, string name)
        {
            int offset = 8;
            while (offset + 12 <= png.Length)
            {
                int length = ReadBigEndianInt32(png, offset);
                string chunkName = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
                if (chunkName == name) return offset;
                offset += 12 + length;
            }
            return -1;
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
            (bytes[offset] << 24) | (bytes[offset + 1] << 16)
            | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }
}
