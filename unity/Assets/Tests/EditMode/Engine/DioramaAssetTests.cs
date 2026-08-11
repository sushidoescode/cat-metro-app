using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
        public void PolyforkManifest_ReferencesExactlyNineImportedModelsAndColliderFreePrefabs()
        {
            const string manifestPath = "Assets/Art/Polyfork/PROVENANCE.md";
            string fullManifest = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", manifestPath));
            Assert.That(File.Exists(fullManifest), Is.True, "asset provenance must ship with the art");
            string manifest = File.ReadAllText(fullManifest);

            string[] models = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Polyfork/Models" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
            Assert.That(models.Length, Is.EqualTo(9),
                "the licensed-local profile must import exactly the nine receipt models");

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

                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                Assert.That(importer, Is.Not.Null, modelPath + " has no ModelImporter");
                Assert.That(importer.addCollider, Is.False, modelPath);
                Assert.That(importer.importAnimation, Is.False, modelPath);
                Assert.That(importer.importBlendShapes, Is.False, modelPath);
                Assert.That(importer.importCameras, Is.False, modelPath);
                Assert.That(importer.importLights, Is.False, modelPath);
                Assert.That(importer.importVisibility, Is.False, modelPath);
                Assert.That(importer.isReadable, Is.False, modelPath);
                Assert.That(importer.materialImportMode,
                    Is.EqualTo(ModelImporterMaterialImportMode.None), modelPath);
            }

            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Diorama" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
            Assert.That(prefabs.Length, Is.EqualTo(9));
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
        public void PolyforkManifest_CryptographicallyPinsEveryDerivativeAndTriangleCount()
        {
            const string manifestPath = "Assets/Art/Polyfork/PROVENANCE.md";
            string fullManifest = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", manifestPath));
            string manifest = File.ReadAllText(fullManifest);
            Assert.That(manifest, Does.Contain("blender --background --python"),
                "the provenance receipt must include the exact offline conversion shape");
            Assert.That(manifest, Does.Contain("GET https://polyfork.dev/dl/<asset-id>.glb"),
                "the authenticated source acquisition receipt must be explicit");

            var rows = manifest.Split('\n')
                .Where(x => x.StartsWith("| [") && x.Contains(".fbx`"))
                .ToArray();
            Assert.That(rows.Length, Is.EqualTo(9));
            string[] models = AssetDatabase.FindAssets("t:Model",
                    new[] { "Assets/Art/Polyfork/Models" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
            Assert.That(rows.Length, Is.EqualTo(models.Length));
            foreach (string row in rows)
            {
                string[] columns = row.Split('|');
                int triangles = int.Parse(columns[2].Trim().Replace(",", ""));
                string modelName = BetweenBackticks(columns[4]);
                string expectedHash = BetweenBackticks(columns[5]);
                string modelPath = "Assets/Art/Polyfork/Models/" + modelName;
                Assert.That(models, Does.Contain(modelPath));

                string absolute = Path.GetFullPath(Path.Combine(
                    UnityEngine.Application.dataPath, "..", modelPath));
                using (var stream = File.OpenRead(absolute))
                using (var sha = SHA256.Create())
                    Assert.That(ToHex(sha.ComputeHash(stream)), Is.EqualTo(expectedHash), modelName);

                int importedTriangles = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                    .OfType<Mesh>().Sum(x => x.triangles.Length / 3);
                Assert.That(importedTriangles, Is.EqualTo(triangles), modelName);
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
            Assert.That(models.Length, Is.EqualTo(9));
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
            Assert.That(greybox.HasProperty("_RampThresholds"), Is.True,
                "the shared shader must expose its three-step toon ramp");
            Assert.That(greybox.HasProperty("_RimStrength"), Is.True,
                "the shared shader must expose its view-dependent rim");

            string[] materials = AssetDatabase.FindAssets("t:Material",
                    new[] { "Assets/Art/Materials" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
            Assert.That(materials.Length, Is.GreaterThanOrEqualTo(6));
            foreach (string path in materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material.shader, Is.EqualTo(greybox.shader), path);
                if (path.EndsWith("ContactShadow.mat"))
                {
                    Assert.That(material.GetFloat("_VertexColorWeight"), Is.Zero, path);
                    Assert.That(material.GetFloat("_VertexAlphaWeight"), Is.EqualTo(1f), path);
                    Assert.That(material.color.a, Is.InRange(0.08f, 0.35f), path);
                    Assert.That(material.HasProperty("_ZWrite"), Is.True, path);
                    Assert.That(material.GetFloat("_ZWrite"), Is.Zero,
                        "a blended contact disc must never punch an opaque depth hole");
                    Assert.That(material.renderQueue,
                        Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                    Assert.That(material.GetTag("RenderType", false), Is.EqualTo("Transparent"));
                }
                else
                {
                    Assert.That(material.GetFloat("_VertexColorWeight"), Is.EqualTo(1f), path);
                }
            }
        }

        [Test]
        public void RendererStaysFeatureFreeWhileProfileKeepsSubtleVignette()
        {
            Object[] rendererAssets = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Settings/CatMetro_Renderer.asset");
            Object ssao = rendererAssets.SingleOrDefault(x => x != null
                && x.GetType().Name == "ScreenSpaceAmbientOcclusion");
            Assert.That(ssao, Is.Null,
                "the mobile renderer stays feature-free; depth comes from authored blobs/AO");
            Object rendererData = rendererAssets.Single(x => x != null
                && x.name == "CatMetro_Renderer");
            var rendererSerialized = new SerializedObject(rendererData);
            var features = rendererSerialized.FindProperty("m_RendererFeatures");
            var featureMap = rendererSerialized.FindProperty("m_RendererFeatureMap");
            Assert.That(features.arraySize, Is.Zero,
                "criterion 2 forbids hidden renderer passes on the mobile baseline");
            Assert.That(featureMap.arraySize, Is.Zero);

            string authoring = File.ReadAllText(
                "Assets/Art/Polyfork/Editor/CatMetroDioramaAuthoring.cs");
            Assert.That(authoring, Does.Not.Contain("ScreenSpaceAmbientOcclusion"),
                "re-authoring the scene must never silently restore SSAO");
            Assert.That(authoring, Does.Not.Contain("rendererFeatures.Add"));

            Object[] profileAssets = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Art/Settings/CatMetro_TabletopPost.asset");
            Object vignette = profileAssets.SingleOrDefault(x => x != null
                && x.GetType().Name == "Vignette");
            Assert.That(vignette, Is.Not.Null, "the tabletop profile must contain Vignette");
            var vignetteSerialized = new SerializedObject(vignette);
            var intensity = vignetteSerialized.FindProperty("intensity.m_Value");
            Assert.That(intensity, Is.Not.Null);
            Assert.That(intensity.floatValue, Is.InRange(0.08f, 0.2f),
                "the vignette stays subtle rather than crushing the board edges");
        }

        [Test]
        public void AndroidPlayerSettings_PinPortraitOnly()
        {
            string settingsPath = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "ProjectSettings/ProjectSettings.asset"));
            string settings = File.ReadAllText(settingsPath);

            Assert.That(PlayerSettings.defaultInterfaceOrientation,
                Is.EqualTo(UIOrientation.Portrait));
            Assert.That(settings, Does.Contain("allowedAutorotateToPortrait: 1"));
            Assert.That(settings, Does.Contain("allowedAutorotateToPortraitUpsideDown: 0"));
            Assert.That(settings, Does.Contain("allowedAutorotateToLandscapeRight: 0"));
            Assert.That(settings, Does.Contain("allowedAutorotateToLandscapeLeft: 0"));
            Assert.That(settings, Does.Contain("useOSAutorotation: 0"));
        }

        [Test]
        public void PolyforkOrientationSheet_UsesUniqueCreateNewTempOutput()
        {
            string authoringPath = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "Art/Polyfork/Editor/CatMetroDioramaAuthoring.cs"));
            string authoring = File.ReadAllText(authoringPath);

            Assert.That(authoring,
                Does.Not.Contain("/tmp/catmetro-polyfork-orientations.png"));
            Assert.That(authoring, Does.Contain("Guid.NewGuid().ToString(\"N\")"));
            Assert.That(authoring, Does.Contain("FileAttributes.ReparsePoint"));
            Assert.That(authoring, Does.Contain("FileMode.CreateNew"));
            Assert.That(authoring, Does.Contain("FileShare.None"));
        }

        [Test]
        public void PolyforkLocalCustodyVerifier_AcceptsExactPackAndRejectsMissingOrTamperedPack()
        {
            System.Type verifierType = System.Type.GetType(
                "CatMetro.Editor.PolyforkLocalCustody, Assembly-CSharp-Editor");
            Assert.That(verifierType, Is.Not.Null,
                "the editor-only cryptographic custody verifier must compile");
            MethodInfo verify = verifierType.GetMethod("RequireExactAt",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(verify, Is.Not.Null);

            string actualRoot = Path.Combine(UnityEngine.Application.dataPath,
                "Art", "Polyfork", "Models");
            Assert.That(() => verify.Invoke(null, new object[] { actualRoot }), Throws.Nothing,
                "the exact owner-local receipt pack must pass");

            string scratch = Path.Combine(Path.GetTempPath(),
                "catmetro-polyfork-custody-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            try
            {
                AssertVerifierRejects(verify, scratch);

                string manifest = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                    "Art", "Polyfork", "PROVENANCE.md"));
                foreach (string row in manifest.Split('\n')
                    .Where(x => x.StartsWith("| [") && x.Contains(".fbx`")))
                {
                    string modelName = BetweenBackticks(row.Split('|')[4]);
                    File.WriteAllText(Path.Combine(scratch, modelName), "not licensed model bytes\n");
                    File.WriteAllText(Path.Combine(scratch, modelName + ".meta"),
                        "fileFormatVersion: 2\nguid: 00000000000000000000000000000000\n");
                }
                AssertVerifierRejects(verify, scratch);
            }
            finally
            {
                Directory.Delete(scratch, true);
            }
        }

        [Test]
        public void AndroidBuildFlowToken_RejectsMissing_ValidatesWithoutMutation_ConsumesOnce()
        {
            System.Type guardType = System.Type.GetType(
                "CatMetro.Editor.PolyforkCustodyBuildPreprocessor, Assembly-CSharp-Editor");
            Assert.That(guardType, Is.Not.Null);
            MethodInfo validate = guardType.GetMethod("RequireCanonicalBuildFlowTokenPresent",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo consume = guardType.GetMethod("ConsumeCanonicalBuildFlowToken",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(validate, Is.Not.Null,
                "the CLI entry must validate flow sequencing before output mutation");
            Assert.That(consume, Is.Not.Null,
                "the Android preprocessor must consume its one-use flow token");

            const string pathKey = "CM_POLYFORK_BUILD_FLOW_TOKEN";
            const string nonceKey = "CM_POLYFORK_BUILD_FLOW_NONCE";
            string previousPath = System.Environment.GetEnvironmentVariable(pathKey);
            string previousNonce = System.Environment.GetEnvironmentVariable(nonceKey);
            string scratch = Path.Combine(Path.GetTempPath(),
                "catmetro-build-flow-token-test-" + System.Guid.NewGuid().ToString("N"));
            string token = Path.Combine(scratch, "token");
            Directory.CreateDirectory(scratch);
            try
            {
                System.Environment.SetEnvironmentVariable(pathKey, null);
                System.Environment.SetEnvironmentVariable(nonceKey, null);
                var missing = Assert.Throws<TargetInvocationException>(() =>
                    validate.Invoke(null, null));
                Assert.That(missing.InnerException.GetType().Name,
                    Is.EqualTo("BuildFailedException"));

                const string nonce = "one-use-test-nonce";
                File.WriteAllText(token, nonce + "\n");
                System.Environment.SetEnvironmentVariable(pathKey, token);
                System.Environment.SetEnvironmentVariable(nonceKey, nonce);
                Assert.That(() => validate.Invoke(null, null), Throws.Nothing);
                Assert.That(File.Exists(token), Is.True,
                    "pre-output validation must not consume the token");
                Assert.That(() => consume.Invoke(null, null), Throws.Nothing);
                Assert.That(File.Exists(token), Is.False,
                    "the accepted flow token must be consumed before build work");
                var replay = Assert.Throws<TargetInvocationException>(() =>
                    consume.Invoke(null, null));
                Assert.That(replay.InnerException.GetType().Name,
                    Is.EqualTo("BuildFailedException"));
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(pathKey, previousPath);
                System.Environment.SetEnvironmentVariable(nonceKey, previousNonce);
                if (File.Exists(token)) File.Delete(token);
                Directory.Delete(scratch, true);
            }
        }

        private static string Html(Color color) => ColorUtility.ToHtmlStringRGB(color);
        private static string Html(Color32 color) => ColorUtility.ToHtmlStringRGB(color);

        private static string BetweenBackticks(string value)
        {
            int first = value.IndexOf('`');
            int last = value.LastIndexOf('`');
            Assert.That(first, Is.GreaterThanOrEqualTo(0), value);
            Assert.That(last, Is.GreaterThan(first), value);
            return value.Substring(first + 1, last - first - 1);
        }

        private static void AssertVerifierRejects(MethodInfo verify, string path)
        {
            var exception = Assert.Throws<TargetInvocationException>(() =>
                verify.Invoke(null, new object[] { path }));
            Assert.That(exception.InnerException,
                Is.TypeOf<System.InvalidOperationException>());
        }

        private static string ToHex(byte[] bytes) =>
            string.Concat(bytes.Select(x => x.ToString("x2")));
    }
}
