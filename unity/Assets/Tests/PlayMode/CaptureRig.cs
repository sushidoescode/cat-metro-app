using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CatMetro.Application.Session;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Domain;
using UnityEngine;

namespace CatMetro.Tests.PlayMode
{
    // Editor-only capture support. This type lives in the UNITY_INCLUDE_TESTS assembly and is
    // never referenced by a player assembly.
    internal static class CaptureRig
    {
        // PNG sRGB chunk: length=1, type=sRGB, perceptual intent=0, CRC=AECE1CE9.
        private static readonly byte[] SrgbPngChunk = {
            0x00, 0x00, 0x00, 0x01, 0x73, 0x52, 0x47, 0x42,
            0x00, 0xae, 0xce, 0x1c, 0xe9,
        };

        internal readonly struct Size
        {
            public Size(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; }
            public int Height { get; }
        }

        internal readonly struct SwitchReceipt
        {
            public SwitchReceipt(string switchId, int switchIndex, int tick)
            {
                SwitchId = switchId;
                SwitchIndex = switchIndex;
                Tick = tick;
            }

            public string SwitchId { get; }
            public int SwitchIndex { get; }
            public int Tick { get; }
        }

        public static Size ParseSize(string value, int defaultWidth, int defaultHeight)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new Size(defaultWidth, defaultHeight);

            string[] parts = value.Trim().Split('x');
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture,
                    out int width)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture,
                    out int height)
                || width <= 0 || height <= 0)
            {
                throw new FormatException(
                    "CM_CAPTURE_SIZE must be two positive integers formatted as WxH");
            }

            return new Size(width, height);
        }

        public static string ResolveLevel(string value, IReadOnlyList<string> levelBand)
        {
            if (levelBand == null || levelBand.Count == 0)
                throw new ArgumentException("GameRoot.LevelBand must contain at least one level");
            if (string.IsNullOrEmpty(value)) return levelBand[0];

            for (int i = 0; i < levelBand.Count; i++)
                if (string.Equals(levelBand[i], value, StringComparison.Ordinal)) return value;

            throw new ArgumentException(
                "CM_CAPTURE_LEVEL must name a level in the current GameRoot.LevelBand");
        }

        public static int ParseTick(string value, int defaultTick)
        {
            if (string.IsNullOrEmpty(value)) return defaultTick;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                    out int tick)
                || tick < 0)
            {
                throw new FormatException(
                    "CM_CAPTURE_TICK must be a non-negative integer tick boundary");
            }

            return tick;
        }

        public static bool ParseHud(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "on") return true;
            if (value == "off") return false;
            throw new FormatException("CM_CAPTURE_HUD must be exactly on or off");
        }

        public static IReadOnlyList<SwitchReceipt> ParseSwitchReceipts(string value,
            ContentIdMap switchIds, int captureTick)
        {
            if (switchIds == null) throw new ArgumentNullException(nameof(switchIds));
            if (string.IsNullOrEmpty(value)) return Array.Empty<SwitchReceipt>();

            string[] values = value.Split(',');
            var receipts = new SwitchReceipt[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                string[] parts = values[i].Split('@');
                if (parts.Length != 2 || parts[0].Length == 0
                    || !int.TryParse(parts[1], NumberStyles.None,
                        CultureInfo.InvariantCulture, out int tick)
                    || tick < 0)
                {
                    throw new FormatException(
                        "CM_CAPTURE_SWITCHES must be comma-separated switch tap receipts like S1@12,S2@42");
                }
                if (tick > captureTick)
                {
                    throw new ArgumentException(
                        $"switch receipt {values[i]} occurs after CM_CAPTURE_TICK={captureTick}");
                }
                if (!switchIds.TryIndexOf(parts[0], out int switchIndex))
                {
                    throw new ArgumentException(
                        $"switch receipt {values[i]} does not name a switch in the selected level");
                }

                receipts[i] = new SwitchReceipt(parts[0], switchIndex, tick);
            }

            return receipts;
        }

        public static void Replay(GameRoot root, int captureTick,
            IReadOnlyList<SwitchReceipt> receipts)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (receipts == null) throw new ArgumentNullException(nameof(receipts));
            if (captureTick < 0) throw new ArgumentOutOfRangeException(nameof(captureTick));
            if (root.Session.State.Tick != 0 || root.Session.Alpha != 0d
                || root.Session.Log.Entries.Count != 0)
            {
                throw new InvalidOperationException(
                    "capture replay must start from a fresh GameSession at tick boundary 0");
            }

            // Freeze GameRoot's wall-clock Update before the RenderTexture layout yield. The
            // accessibility motion-off setting is a shipped presentation state and removes the
            // only remaining wall-clock micro-motion; neither setting mutates Simulation.
            root.enabled = false;
            root.MotionOffToggle = true;
            root.View.UpdateFrom(root.Session, 0f);

            while (root.Session.State.Tick < captureTick)
            {
                EnqueueReceiptsAtBoundary(root, receipts, root.Session.State.Tick);
                root.View.UpdateFrom(root.Session, VisualTime(root.Session.State.Tick));
                if (root.Session.State.Outcome.Kind != OutcomeKind.Running)
                {
                    throw new InvalidOperationException(
                        $"capture replay ended {root.Session.State.Outcome.Kind} at tick "
                        + $"{root.Session.State.Tick}, before CM_CAPTURE_TICK={captureTick}");
                }

                int before = root.Session.State.Tick;
                root.Session.AdvanceMs(TickInterpolator.TICK_MS);
                if (root.Session.State.Tick != before + 1 || root.Session.Alpha != 0d)
                {
                    throw new InvalidOperationException(
                        "GameSession did not advance to the next exact tick boundary");
                }
            }

            if (root.Session.State.Outcome.Kind != OutcomeKind.Running)
            {
                throw new InvalidOperationException(
                    $"CM_CAPTURE_TICK={captureTick} is a terminal state "
                    + $"({root.Session.State.Outcome.Kind}); the frozen beauty rig can only "
                    + "present reachable Playing boundaries");
            }

            // A tap receipt stamped at T exists at boundary T. GameSession applies it to
            // authoritative Simulation state later, while BoardView immediately shows its
            // committed route through PendingToggleCount — the same law as ordinary input.
            EnqueueReceiptsAtBoundary(root, receipts, captureTick);
            root.View.UpdateFrom(root.Session, VisualTime(captureTick));
            root.Preview.Refresh();
        }

        private static void EnqueueReceiptsAtBoundary(GameRoot root,
            IReadOnlyList<SwitchReceipt> receipts, int boundary)
        {
            for (int i = 0; i < receipts.Count; i++)
                if (receipts[i].Tick == boundary)
                    root.Session.EnqueueToggle(receipts[i].SwitchIndex);
        }

        private static float VisualTime(int tick) =>
            tick * (float)TickInterpolator.TICK_MS / 1000f;

        public static string DescribeState(GameRoot root,
            IReadOnlyList<SwitchReceipt> receipts, bool hudOn)
        {
            var session = root.Session;
            var state = session.State;
            var ids = session.Level.IdMaps;
            var description = new StringBuilder();
            description.Append("format=cm-capture-state-v1\n");
            description.Append("level=").Append(root.CurrentLevelId).Append('\n');
            description.Append("tick=").Append(state.Tick).Append('\n');
            description.Append("switches=");
            for (int i = 0; i < receipts.Count; i++)
            {
                if (i > 0) description.Append(',');
                description.Append(receipts[i].SwitchId).Append('@').Append(receipts[i].Tick);
            }
            description.Append('\n');
            description.Append("hud=").Append(hudOn ? "on" : "off").Append('\n');
            description.Append("outcome=").Append(state.Outcome.Kind).Append('\n');
            description.Append("deliveries=").Append(state.Deliveries).Append('\n');
            description.Append("switches_used=").Append(state.SwitchesUsed).Append('\n');

            for (int s = 0; s < state.SwitchRoutes.Length; s++)
            {
                int committedRoute = root.View.CommittedRoute(s);
                int edgeIndex = state.Graph.SwitchRoutes[s][committedRoute];
                description.Append("switch[").Append(ids.Switches.IdOf(s)).Append("]=")
                    .Append("authoritative_route:").Append(state.SwitchRoutes[s]).Append(',')
                    .Append("committed_route:").Append(committedRoute).Append(',')
                    .Append("edge:").Append(ids.Edges.IdOf(edgeIndex)).Append('\n');
            }

            int liveTrains = 0;
            for (int t = 0; t < state.Trains.Length; t++)
            {
                TrainSlot train = state.Trains[t];
                if (train.Id == 0 || train.State == TrainState.None) continue;
                liveTrains++;
                description.Append("train[").Append(t).Append("]=id:").Append(train.Id)
                    .Append(",color:").Append(train.Color)
                    .Append(",state:").Append(train.State)
                    .Append(",edge:").Append(IdOrNone(ids.Edges, train.EdgeId))
                    .Append(",progress:").Append(train.ProgressTicks)
                    .Append(",node:").Append(IdOrNone(ids.Nodes, train.NodeId))
                    .Append(",generation:").Append(session.TrainOccupantGeneration(t))
                    .Append('\n');
            }
            description.Append("live_trains=").Append(liveTrains).Append('\n');
            return description.ToString();
        }

        private static string IdOrNone(ContentIdMap ids, int index) =>
            index >= 0 && index < ids.Count ? ids.IdOf(index) : "-";

        public static Rect ScaleSafeArea(Rect referenceSafeArea, int referenceWidth,
            int referenceHeight, Size target)
        {
            return new Rect(
                referenceSafeArea.x / referenceWidth * target.Width,
                referenceSafeArea.y / referenceHeight * target.Height,
                referenceSafeArea.width / referenceWidth * target.Width,
                referenceSafeArea.height / referenceHeight * target.Height);
        }

        public static float ScaleDpi(float referenceDpi, int referenceHeight, Size target) =>
            referenceDpi * target.Height / referenceHeight;

        public static RenderTexture CreateTarget(Size size)
        {
            var target = new RenderTexture(size.Width, size.Height, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            return target;
        }

        public static Texture2D ReadRgb24(RenderTexture target)
        {
            var texture = new Texture2D(target.width, target.height,
                TextureFormat.RGB24, mipChain: false, linear: false);
            texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }

        public static byte[] EncodeOpaqueSrgbPng(Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (texture.format != TextureFormat.RGB24 || !texture.isDataSRGB)
            {
                throw new ArgumentException(
                    "capture PNG source must be an sRGB RGB24 texture with no alpha");
            }

            byte[] encoded = texture.EncodeToPNG();
            const int signatureLength = 8;
            const int ihdrChunkLength = 25;
            if (encoded == null || encoded.Length < signatureLength + ihdrChunkLength
                || ReadPngInt32(encoded, signatureLength) != 13
                || !PngChunkNameEquals(encoded, signatureLength, "IHDR")
                || encoded[25] != 2)
            {
                throw new InvalidOperationException(
                    "Unity did not encode the capture as an 8-bit RGB PNG");
            }

            int offset = signatureLength;
            while (offset + 12 <= encoded.Length)
            {
                int payloadLength = ReadPngInt32(encoded, offset);
                if (payloadLength < 0 || offset + 12 + payloadLength > encoded.Length)
                    throw new InvalidOperationException("Unity encoded a malformed PNG chunk");
                if (PngChunkNameEquals(encoded, offset, "sRGB")) return encoded;
                offset += 12 + payloadLength;
            }

            int insertion = signatureLength + ihdrChunkLength;
            var tagged = new byte[encoded.Length + SrgbPngChunk.Length];
            Buffer.BlockCopy(encoded, 0, tagged, 0, insertion);
            Buffer.BlockCopy(SrgbPngChunk, 0, tagged, insertion, SrgbPngChunk.Length);
            Buffer.BlockCopy(encoded, insertion, tagged,
                insertion + SrgbPngChunk.Length, encoded.Length - insertion);
            return tagged;
        }

        private static int ReadPngInt32(byte[] bytes, int offset) =>
            (bytes[offset] << 24) | (bytes[offset + 1] << 16)
            | (bytes[offset + 2] << 8) | bytes[offset + 3];

        private static bool PngChunkNameEquals(byte[] bytes, int chunkOffset, string name) =>
            bytes[chunkOffset + 4] == name[0]
            && bytes[chunkOffset + 5] == name[1]
            && bytes[chunkOffset + 6] == name[2]
            && bytes[chunkOffset + 7] == name[3];
    }
}
