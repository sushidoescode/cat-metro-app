using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Theme;
using CatMetro.Services.Cosmetics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatMetro.Tests.PlayMode
{
    public sealed class CosmeticPortraitViewTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("PortraitHost", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            _host = null;
        }

        [Test]
        public void Create_BindsCurrentPortrait_AndExposesRealLayerReadbacks()
        {
            var source = PortraitTestSource.WithRealTokens(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "outfit.conductor", "", "frame.brass"));

            var view = CosmeticPortraitView.Create(_host.transform, source, "ProfilePortrait");

            Assert.That(source.SubscriberCount, Is.EqualTo(1));
            Assert.That(view.name, Is.EqualTo("ProfilePortrait"));
            Assert.That(view.RootTransform, Is.SameAs(view.transform));
            Assert.That(view.AppliedCatId, Is.EqualTo("cat.red"));
            Assert.That(view.AppliedOutfitAssetId, Is.EqualTo("outfit.conductor"));
            Assert.That(view.AppliedAccessoryAssetId, Is.Empty);
            Assert.That(view.AppliedFrameAssetId, Is.EqualTo("frame.brass"));
            Assert.That(view.BaseLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(view.OutfitLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(view.AccessoryLayerTransform.gameObject.activeSelf, Is.False);
            Assert.That(view.FrameLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(VisibleImages(view.BaseLayerTransform).Length, Is.GreaterThan(5),
                "the read-back points to painted cat geometry, not an empty marker transform");
        }

        [Test]
        public void Changed_RepaintsTheActualLayerTree()
        {
            var source = PortraitTestSource.WithRealTokens(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "", "", ""));
            var view = CosmeticPortraitView.Create(_host.transform, source);
            var before = VisibleColors(view.BaseLayerTransform);

            source.Set(new CosmeticPortraitSnapshot(
                "cat.blue", "cat.blue_siamese", "outfit.conductor", "", "frame.lantern"));

            var after = VisibleColors(view.BaseLayerTransform);
            Assert.That(after, Is.Not.EqualTo(before), "the source event repaints real child Images");
            Assert.That(after, Does.Contain((Color32)Palette.HarborBlue));
            Assert.That(view.AppliedCatId, Is.EqualTo("cat.blue"));
            Assert.That(view.AppliedOutfitAssetId, Is.EqualTo("outfit.conductor"));
            Assert.That(view.AppliedFrameAssetId, Is.EqualTo("frame.lantern"));
            Assert.That(VisibleImages(view.OutfitLayerTransform).Length, Is.GreaterThan(4));
            Assert.That(VisibleImages(view.FrameLayerTransform).Length, Is.GreaterThan(4));
        }

        [Test]
        public void Bind_DetachesTheOldSource_AndNeverDuplicatesSubscriptions()
        {
            var first = PortraitTestSource.WithRealTokens(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "", "", ""));
            var second = PortraitTestSource.WithRealTokens(new CosmeticPortraitSnapshot(
                "cat.yellow", "cat.yellow_longhair", "", "", "frame.brass"));
            var view = CosmeticPortraitView.Create(_host.transform, first);

            view.Bind(first);
            Assert.That(first.SubscriberCount, Is.EqualTo(1), "rebinding the same source is idempotent");

            view.Bind(second);
            Assert.That(first.SubscriberCount, Is.Zero);
            Assert.That(second.SubscriberCount, Is.EqualTo(1));
            Assert.That(view.AppliedCatId, Is.EqualTo("cat.yellow"));
            Assert.That(VisibleColors(view.BaseLayerTransform),
                Does.Contain((Color32)Palette.TabbyYellow));
        }

        [UnityTest]
        public IEnumerator DisableEnableAndDestroy_BalanceTheSubscription_AndRefreshOnResume()
        {
            var source = PortraitTestSource.WithRealTokens(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "", "", ""));
            var view = CosmeticPortraitView.Create(_host.transform, source);

            view.gameObject.SetActive(false);
            Assert.That(source.SubscriberCount, Is.Zero);
            source.SetSilently(new CosmeticPortraitSnapshot(
                "cat.blue", "cat.blue_siamese", "", "", "frame.lantern"));

            view.gameObject.SetActive(true);
            Assert.That(source.SubscriberCount, Is.EqualTo(1));
            Assert.That(view.AppliedCatId, Is.EqualTo("cat.blue"),
                "resume catches up to authoritative state missed while disabled");
            Assert.That(VisibleColors(view.BaseLayerTransform),
                Does.Contain((Color32)Palette.HarborBlue));

            UnityEngine.Object.Destroy(view.gameObject);
            yield return null;
            Assert.That(source.SubscriberCount, Is.Zero);
            Assert.DoesNotThrow(source.RaiseChanged,
                "destroy cleanup is idempotent and leaves no dead callback");
            _host = null;
        }

        [Test]
        public void EmptyOrUnresolvedAssetIds_ClearAndDeactivateTheirLayers()
        {
            var source = PortraitTestSource.WithRealTokens(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "outfit.conductor", "", "frame.brass"));
            var view = CosmeticPortraitView.Create(_host.transform, source);

            source.Set(new CosmeticPortraitSnapshot(
                "cat.red", "missing.base", "missing.outfit", "missing.accessory", ""));

            Assert.That(view.BaseLayerTransform.gameObject.activeSelf, Is.False);
            Assert.That(view.OutfitLayerTransform.gameObject.activeSelf, Is.False);
            Assert.That(view.AccessoryLayerTransform.gameObject.activeSelf, Is.False);
            Assert.That(view.FrameLayerTransform.gameObject.activeSelf, Is.False);
            Assert.That(view.AppliedOutfitAssetId, Is.Empty);
            Assert.That(view.AppliedAccessoryAssetId, Is.Empty);
            Assert.That(view.AppliedFrameAssetId, Is.Empty);
        }

        private static Image[] VisibleImages(RectTransform layer) => layer
            .GetComponentsInChildren<Image>(false)
            .Where(image => image.gameObject.activeInHierarchy)
            .ToArray();

        private static List<Color32> VisibleColors(RectTransform layer) => VisibleImages(layer)
            .Select(image => (Color32)image.color)
            .ToList();
    }

    internal sealed class PortraitTestSource : ICosmeticPortraitSource
    {
        private readonly Dictionary<string, CosmeticPortraitAssetDefinition> _assets =
            new Dictionary<string, CosmeticPortraitAssetDefinition>(StringComparer.Ordinal);
        private Action _changed;

        public int SubscriberCount { get; private set; }
        public CosmeticPortraitSnapshot CurrentPortrait { get; private set; }

        public event Action Changed
        {
            add
            {
                _changed += value;
                SubscriberCount++;
            }
            remove
            {
                _changed -= value;
                SubscriberCount--;
            }
        }

        private PortraitTestSource(CosmeticPortraitSnapshot current)
        {
            CurrentPortrait = current;
        }

        public static PortraitTestSource WithRealTokens(CosmeticPortraitSnapshot current)
        {
            var source = new PortraitTestSource(current);
            foreach (string token in new[]
            {
                "cat.red_tabby", "cat.blue_siamese", "cat.yellow_longhair",
                "outfit.conductor", "frame.brass", "frame.lantern",
            })
            {
                source._assets.Add(token,
                    new CosmeticPortraitAssetDefinition(token, token, "project." + token));
            }
            return source;
        }

        public bool TryGetPortraitAsset(string assetId, out CosmeticPortraitAssetDefinition asset)
        {
            if (assetId != null) return _assets.TryGetValue(assetId, out asset);
            asset = null;
            return false;
        }

        public void Set(CosmeticPortraitSnapshot portrait)
        {
            CurrentPortrait = portrait;
            RaiseChanged();
        }

        public void SetSilently(CosmeticPortraitSnapshot portrait) => CurrentPortrait = portrait;

        public void RaiseChanged() => _changed?.Invoke();
    }
}
