using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CatMetro.Tests.EditMode
{
    public sealed class DioramaAssetTests
    {
        [Test]
        public void Palette_RoundTripsToAllTwelveAuthoritativeHexes()
        {
            var actual = new Dictionary<string, string>
            {
                ["Cream Card"] = Html(DioramaPalette.CreamCard),
                ["Warm Paper"] = Html(DioramaPalette.WarmPaper),
                ["Ink Navy"] = Html(DioramaPalette.InkNavy),
                ["Depot Navy"] = Html(DioramaPalette.DepotNavy),
                ["Metro Teal"] = Html(DioramaPalette.MetroTeal),
                ["Ticket Orange"] = Html(DioramaPalette.TicketOrange),
                ["Signal Red"] = Html(DioramaPalette.SignalRed),
                ["Harbor Blue"] = Html(DioramaPalette.HarborBlue),
                ["Tabby Yellow"] = Html(DioramaPalette.TabbyYellow),
                ["Garden Green"] = Html(DioramaPalette.GardenGreen),
                ["Catnip Violet"] = Html(DioramaPalette.CatnipViolet),
                ["Alarm Coral"] = Html(DioramaPalette.AlarmCoral),
            };

            Assert.That(actual, Is.EqualTo(new Dictionary<string, string>
            {
                ["Cream Card"] = "F2EAD9",
                ["Warm Paper"] = "FAF6EC",
                ["Ink Navy"] = "22304A",
                ["Depot Navy"] = "131C30",
                ["Metro Teal"] = "3BAFA8",
                ["Ticket Orange"] = "F08A3C",
                ["Signal Red"] = "E15A47",
                ["Harbor Blue"] = "3E7CC9",
                ["Tabby Yellow"] = "EFC13D",
                ["Garden Green"] = "4FA36A",
                ["Catnip Violet"] = "A06BD8",
                ["Alarm Coral"] = "D93A2B",
            }));
        }

        [Test]
        public void LineIdentities_PinSymbolColorAndFiveDistinctSilhouettes()
        {
            var identities = new[]
            {
                LineIdentity.For(CatColor.Red),
                LineIdentity.For(CatColor.Blue),
                LineIdentity.For(CatColor.Yellow),
                LineIdentity.For(CatColor.Green),
                LineIdentity.For(CatColor.Wild),
            };

            Assert.That(identities.Select(x => x.SymbolId), Is.EqualTo(new[]
            {
                "circle", "square", "triangle", "diamond", "star",
            }));
            Assert.That(identities.Select(x => x.SilhouetteId), Is.EqualTo(new[]
            {
                "round-tabby", "slim-siamese", "fluffy-longhair", "sleek-shorthair",
                "bent-ear-scruffy",
            }));
            Assert.That(identities.Select(x => x.SilhouetteId).Distinct().Count(), Is.EqualTo(5));
            Assert.That(identities.Select(x => Html(x.Color)), Is.EqualTo(new[]
            {
                "E15A47", "3E7CC9", "EFC13D", "4FA36A", "A06BD8",
            }));
        }

        [Test]
        public void PolyforkManifest_ReferencesSixImportedModelsAndColliderFreePrefabs()
        {
            const string manifestPath = "Assets/Art/Polyfork/PROVENANCE.md";
            string fullManifest = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", manifestPath));
            Assert.That(File.Exists(fullManifest), Is.True, "asset provenance must ship with the art");
            string manifest = File.ReadAllText(fullManifest);

            string[] models = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Polyfork/Models" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
            Assert.That(models.Length, Is.GreaterThanOrEqualTo(6),
                "at least six visible Polyfork GLB derivatives must import as Unity models");

            foreach (string modelPath in models)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                Assert.That(model, Is.Not.Null, modelPath);
                Assert.That(model.GetComponentsInChildren<MeshFilter>(true).Length,
                    Is.GreaterThan(0), modelPath + " must contain mesh geometry");
                Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty,
                    modelPath + " imported an unwanted collider");
                Assert.That(manifest, Does.Contain(Path.GetFileName(modelPath)),
                    modelPath + " is absent from the provenance record");
            }

            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Diorama" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
            Assert.That(prefabs.Length, Is.GreaterThanOrEqualTo(6));
            var greybox = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Resources/Materials/Greybox.mat");
            Assert.That(greybox, Is.Not.Null);
            foreach (string prefabPath in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty, prefabPath);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, prefabPath);
                    Assert.That(renderer.sharedMaterial.shader, Is.EqualTo(greybox.shader), prefabPath);
                }
            }
        }

        [Test]
        public void PolyforkVertexColors_AreRemappedToTheAuthoritativeDecorPalette()
        {
            var allowed = new HashSet<string>
            {
                "F2EAD9", "FAF6EC", "22304A", "131C30", "3BAFA8", "F08A3C",
            };
            string[] models = AssetDatabase.FindAssets("t:Model",
                    new[] { "Assets/Art/Polyfork/Models" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
            Assert.That(models.Length, Is.GreaterThanOrEqualTo(6));
            foreach (string path in models)
            {
                var colors = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>()
                    .SelectMany(x => x.colors32).ToArray();
                Assert.That(colors.Length, Is.GreaterThan(0),
                    path + " must retain its low-poly vertex-color detail");
                var distinct = colors.Select(x => Html(x)).Distinct().ToArray();
                Assert.That(distinct, Is.SubsetOf(allowed),
                    path + " contains a color outside the product palette");
            }
        }

        [Test]
        public void DioramaShader_IsSingleWarmVertexColorPipelineAcrossRuntimeAndPrefabs()
        {
            var greybox = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Resources/Materials/Greybox.mat");
            Assert.That(greybox.shader.name,
                Is.EqualTo("Universal Render Pipeline/Cat Metro Diorama Lit"));
            Assert.That(greybox.HasProperty("_VertexColorWeight"), Is.True);
            Assert.That(greybox.GetFloat("_VertexColorWeight"), Is.Zero);

            string[] materials = AssetDatabase.FindAssets("t:Material",
                    new[] { "Assets/Art/Materials" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
            Assert.That(materials.Length, Is.GreaterThanOrEqualTo(6));
            foreach (string path in materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material.shader, Is.EqualTo(greybox.shader), path);
                Assert.That(material.GetFloat("_VertexColorWeight"), Is.EqualTo(1f), path);
            }
        }

        private static string Html(Color color) => ColorUtility.ToHtmlStringRGB(color);
        private static string Html(Color32 color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
