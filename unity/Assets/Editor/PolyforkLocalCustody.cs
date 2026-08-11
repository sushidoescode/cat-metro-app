using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace CatMetro.Editor
{
    public static class PolyforkLocalCustody
    {
        private readonly struct Receipt
        {
            public readonly string Name;
            public readonly string DerivativeHash;
            public readonly string Guid;
            public readonly string MetaHash;

            public Receipt(string name, string derivativeHash, string guid, string metaHash)
            {
                Name = name;
                DerivativeHash = derivativeHash;
                Guid = guid;
                MetaHash = metaHash;
            }
        }

        private static readonly Receipt[] Receipts =
        {
            new Receipt("polyfork_tram_track_tile_f3c69a.fbx",
                "7c97c3d0b170aa940edce47c2f3c9dbcf14f67da6f9174515ee857aab541d987",
                "73b5135d9d81848829c189a03d9a2305",
                "716e8f49b2cc12c25585954bfab8475f018d87bf60754cedc8e971616fbedec3"),
            new Receipt("polyfork_train_engine_180979.fbx",
                "e505020cd12effebdfd4f0d632bf7d46b2ed8c976e9847defdc12e3ce256e418",
                "f7048b3033b864284a1da1dcd80590a4",
                "a6ce1c33660b67cf243e1b36b8824146328b1624fe94be3f7cf478b10af9ae21"),
            new Receipt("polyfork_log_cabin_4fac3b.fbx",
                "1339fabc925e6832d0617d25631ca95315e4906baada5554e0ef90378691a7fc",
                "e144f368bd353443ab2321bc1b1a4b37",
                "ae824800de97cf73fe7257cc9245a7dc2a8fe1583b809bf3f82793065fe0e98d"),
            new Receipt("polyfork_young_pine_0d7695.fbx",
                "e7887354371ecbce519e81e2dce68a05aa1e6b9f573d381dffb17db231735fde",
                "43a97ff877c5549b298ae17d717488da",
                "7ebbf6b259999f88a5bde8a94738e30befc3bd17e15f0442d365d897f4bdb084"),
            new Receipt("polyfork_wooden_fence_section_5f04b7.fbx",
                "a0dd008200317da8dbd46cb37cf4043d558e64be2983e78bd50eaec5cf4aba88",
                "05fa41426451a4430b827a8f218bfd6a",
                "ed744ecd688fed06a48ad09f87c63af565b7651d95e415dd665ab6af17bcb24f"),
            new Receipt("polyfork_wooden_bench_661da4.fbx",
                "8629dabcafac68d8a610bd5eb60e515dbda0dcb1980ae56fca1bd908f22eb7f9",
                "6c49e285287f34668883553f0db8fb93",
                "5c299d41198dd287f592e8ac4312d9a6f20d8cf018844ac600770a0572c9c418"),
            new Receipt("polyfork_sandwich_board_sign_cb5e7c.fbx",
                "498223ca9062bba616ff83df73a17954e8ec2c34dc2153bbe2687cc38183eb3a",
                "1cafd8f1a15ad493b86c7f51d13ec219",
                "0dd52af7605de286d337b33d1f2c12787d0f49ef1663f99bb63f23f5f2a94804"),
            new Receipt("polyfork_street_lamp_29f365.fbx",
                "1ec680dd882c9df00b45b9d7526d09157b2a3513e9c578591c0409eb7b7ba5e6",
                "065d26fb6b23f4b3799508e87b9cd07f",
                "0dda6bb01953c4e79f7eef4dffa4e70cdb85d525b04ac25562574a0d8ec1a6c2"),
            new Receipt("polyfork_coffee_cup_90be67.fbx",
                "df64b866c0a2e116b3308f08467004eed599f956c4bf65cf34cccdb6abe664e2",
                "936e185b29f664f84a96fde34e134207",
                "ed1b4d4ee99fa52b61a6233534c5c5d255bbb244507e1d93cb8101a532582f33"),
        };

        public static void RequireExact()
        {
            RequireExactAt(Path.Combine(
                UnityEngine.Application.dataPath, "Art", "Polyfork", "Models"));
        }

        public static void RequireExactAt(string modelRoot)
        {
            if (string.IsNullOrWhiteSpace(modelRoot))
                throw new InvalidOperationException("Polyfork local custody path is missing");
            if (!Directory.Exists(modelRoot))
                throw new InvalidOperationException("Polyfork local custody directory is missing");
            if ((File.GetAttributes(modelRoot) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Polyfork local custody directory is a link");
            if (Directory.GetDirectories(modelRoot, "*", SearchOption.TopDirectoryOnly).Length != 0)
                throw new InvalidOperationException("Polyfork local custody contains a subdirectory");

            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (Receipt receipt in Receipts)
            {
                expected.Add(receipt.Name);
                expected.Add(receipt.Name + ".meta");
            }
            string[] actual = Directory.GetFiles(modelRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).ToArray();
            if (actual.Length != expected.Count
                || !expected.SetEquals(actual))
                throw new InvalidOperationException("Polyfork local custody inventory mismatch");

            foreach (Receipt receipt in Receipts)
            {
                string fbx = Path.Combine(modelRoot, receipt.Name);
                string meta = fbx + ".meta";
                RequireRegularFile(fbx);
                RequireRegularFile(meta);
                if (!string.Equals(Hash(fbx), receipt.DerivativeHash,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Polyfork derivative hash mismatch: " + receipt.Name);
                if (!string.Equals(Hash(meta), receipt.MetaHash, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Polyfork metadata hash mismatch: " + receipt.Name);
                if (!string.Equals(ReadGuid(meta), receipt.Guid, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Polyfork metadata GUID mismatch: " + receipt.Name);
            }
        }

        private static void RequireRegularFile(string path)
        {
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "Polyfork local custody file is missing: " + Path.GetFileName(path));
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new InvalidOperationException(
                    "Polyfork local custody file is not regular: " + Path.GetFileName(path));
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(stream).Select(x => x.ToString("x2")));
        }

        private static string ReadGuid(string metaPath)
        {
            foreach (string line in File.ReadLines(metaPath))
                if (line.StartsWith("guid: ", StringComparison.Ordinal))
                    return line.Substring("guid: ".Length).Trim();
            return string.Empty;
        }
    }
}
