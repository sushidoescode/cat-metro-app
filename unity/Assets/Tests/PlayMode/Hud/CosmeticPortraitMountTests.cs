using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Hud;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Tests.PlayMode
{
    public sealed class CosmeticPortraitMountTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("HudPortraitHost", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            _host = null;
        }

        [Test]
        public void Factory_MountsExactlyOneTypedViewUnderTheSuppliedHost()
        {
            var source = PortraitTestSource.WithRealTokens(default);

            var view = CosmeticPortraitView.Create(_host.transform, source, "HudPortrait");

            Assert.That(_host.transform.childCount, Is.EqualTo(1));
            Assert.That(_host.transform.GetChild(0), Is.SameAs(view.transform));
            Assert.That(_host.GetComponentsInChildren<CosmeticPortraitView>(true),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void Layers_AreBaseOutfitAccessoryFrame_InExactDrawOrder()
        {
            var view = CosmeticPortraitView.Create(_host.transform,
                PortraitTestSource.WithRealTokens(default));

            Assert.That(view.RootTransform.childCount, Is.EqualTo(4));
            Assert.That(view.BaseLayerTransform.GetSiblingIndex(), Is.EqualTo(0));
            Assert.That(view.OutfitLayerTransform.GetSiblingIndex(), Is.EqualTo(1));
            Assert.That(view.AccessoryLayerTransform.GetSiblingIndex(), Is.EqualTo(2));
            Assert.That(view.FrameLayerTransform.GetSiblingIndex(), Is.EqualTo(3));
            Assert.That(view.RootTransform.GetChild(0), Is.SameAs(view.BaseLayerTransform));
            Assert.That(view.RootTransform.GetChild(1), Is.SameAs(view.OutfitLayerTransform));
            Assert.That(view.RootTransform.GetChild(2), Is.SameAs(view.AccessoryLayerTransform));
            Assert.That(view.RootTransform.GetChild(3), Is.SameAs(view.FrameLayerTransform));
        }

        [Test]
        public void PaintedTree_IsRenderOnlyUGUI_AndUsesSharedChromeResources()
        {
            var source = PortraitTestSource.WithRealTokens(new CatMetro.Services.Cosmetics.CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "outfit.conductor", "", "frame.lantern"));
            var view = CosmeticPortraitView.Create(_host.transform, source);
            var images = view.GetComponentsInChildren<Image>(true);

            Assert.That(images.Length, Is.GreaterThan(15));
            Assert.That(images.All(image => !image.raycastTarget), Is.True);
            Assert.That(images.All(image => image.sprite != null), Is.True,
                "every visible shape comes from HudShapeSprites, never the default UI texture");
            Assert.That(images.All(image => image.material == UiChromeMaterial.Shared), Is.True,
                "all shapes share the project chrome material; no instance is admitted");
            Assert.That(view.GetComponentsInChildren<Selectable>(true), Is.Empty);
            Assert.That(view.GetComponentsInChildren<Button>(true), Is.Empty);
            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(view.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        }

        [Test]
        public void ActualInventoryTokens_SetEqualThePainterVocabulary_AndProjectSourcesExist()
        {
            string inventoryPath = Path.Combine(UnityEngine.Application.dataPath,
                "Resources/Cosmetics/portrait_assets.json");
            var root = JObject.Parse(File.ReadAllText(inventoryPath));
            var actualTokens = new HashSet<string>(root["assets"]
                .Select(row => (string)row["rendererToken"]), StringComparer.Ordinal);

            CollectionAssert.AreEquivalent(actualTokens,
                CosmeticPortraitPainter.SupportedRendererTokens);
            Assert.That(actualTokens.Count, Is.EqualTo(6), "the inventory has the six real renderers");

            string unityRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string repositoryRoot = Directory.GetParent(unityRoot).FullName;
            foreach (var provenance in root["provenance"]
                .Where(row => (string)row["sourceKind"] == "project_authored"))
            {
                string relative = (string)provenance["sourcePath"];
                string absolute = Path.GetFullPath(Path.Combine(repositoryRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
                Assert.That(File.Exists(absolute), Is.True,
                    $"project-authored portrait source must resolve from Application.dataPath: {relative}");
            }
        }
    }
}
