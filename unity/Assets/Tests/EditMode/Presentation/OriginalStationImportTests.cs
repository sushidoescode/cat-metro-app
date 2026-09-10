using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class OriginalStationImportTests
    {
        private string _assetRoot;
        private string _temporary;

        [SetUp]
        public void SetUp()
        {
            string id = Guid.NewGuid().ToString("N");
            _assetRoot = "Assets/__StationImportTest_" + id;
            _temporary = Path.Combine(Path.GetTempPath(), "cm-station-import-" + id);
            Directory.CreateDirectory(_temporary);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_assetRoot)) AssetDatabase.DeleteAsset(_assetRoot);
            if (!string.IsNullOrEmpty(_temporary) && Directory.Exists(_temporary))
                Directory.Delete(_temporary, true);
        }

        [TestCase("wrong-size-atlas")]
        [TestCase("wrong-station-parts")]
        public void RejectedCandidateLeavesThePreviouslyImportedRuntimeAssetsUnchanged(string defect)
        {
            string recipes = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "..", "..", "docs", "design", "assets"));
            string station = Path.Combine(recipes, "original-station-runtime", "Models", "original-station-runtime.fbx");
            string atlas = Path.Combine(recipes, "original-station-runtime", "Textures", "original-toy-atlas.png");
            Assert.That(File.Exists(station) && File.Exists(atlas), Is.True, "committed original study required");
            Import(station, atlas);
            string[] runtime = { "original-toy-atlas.png", "OriginalStation.mat",
                "Meshes/Body.asset", "Meshes/RoofTint.asset", "Station.prefab" };
            byte[][] before = runtime.Select(p => File.ReadAllBytes(_assetRoot + "/" + p)).ToArray();
            string[] guids = runtime.Select(p => AssetDatabase.AssetPathToGUID(_assetRoot + "/" + p)).ToArray();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(_assetRoot + "/OriginalStation.mat");
            Texture originalAtlas = material.GetTexture("_BaseMap");
            string candidateAtlas = Path.Combine(_temporary, "candidate.png");
            var texture = new Texture2D(defect == "wrong-size-atlas" ? 512 : 1024, 512);
            try
            {
                var pixels = Enumerable.Repeat(Color.magenta, texture.width * texture.height).ToArray();
                texture.SetPixels(pixels); texture.Apply();
                File.WriteAllBytes(candidateAtlas, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
            string candidateFbx = defect == "wrong-station-parts"
                ? Path.Combine(recipes, "original-open-carriage", "Models", "original-open-carriage.fbx")
                : station;
            Assert.That(File.Exists(candidateFbx), Is.True);
            var error = Assert.Throws<TargetInvocationException>(() => Import(candidateFbx, candidateAtlas));
            Assert.That(error.InnerException, Is.TypeOf<InvalidDataException>());
            Assert.That(error.InnerException.Message, Does.Contain(defect == "wrong-size-atlas"
                ? "1024x512" : "mesh named Body"));
            for (int i = 0; i < runtime.Length; i++)
            {
                string path = _assetRoot + "/" + runtime[i];
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before[i]), path + " changed after rejection");
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guids[i]));
            }
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(originalAtlas));
            Assert.That(EditorUtility.IsDirty(material), Is.False);
            // A successful retry must still reuse the existing material/mesh/prefab GUIDs.
            Import(station, atlas);
            for (int i = 0; i < runtime.Length; i++)
                Assert.That(AssetDatabase.AssetPathToGUID(_assetRoot + "/" + runtime[i]), Is.EqualTo(guids[i]));
        }

        private void Import(string fbx, string atlas)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CatMetro.EditorTools.CatMetroOriginalStationImportPipeline"))
                .FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null);
            MethodInfo build = type.GetMethod("BuildFromSources", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null);
            build.Invoke(null, new object[] { fbx, atlas, _assetRoot, _assetRoot + "/Station.prefab" });
        }
    }
}
