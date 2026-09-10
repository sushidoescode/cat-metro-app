using System;
using System.Linq;
using System.Reflection;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Services.Cosmetics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class CosmeticFrameLayoutTests
    {
        [TestCase("frame.brass", 180f, 165f)]
        [TestCase("frame.lantern", 180f, 165f)]
        [TestCase("frame.brass", 753f, 585f)]
        [TestCase("frame.lantern", 753f, 585f)]
        [TestCase("frame.brass", 900f, 300f)]
        [TestCase("frame.lantern", 300f, 900f)]
        public void FittedRailsAndRotatedCorners_ClearOpening_KeepHatAndCoat_AndRestoreExactly(
            string token, float width, float height)
        {
            var host = new GameObject("FrameLayoutHolder", typeof(RectTransform));
            try
            {
                var holder = (RectTransform)host.transform;
                holder.sizeDelta = new Vector2(width, height);
                var portrait = CosmeticPortraitView.Create(holder, new Source(token));
                var original = portrait.FrameLayerTransform.GetComponentsInChildren<RectTransform>(true)
                    .Select(r => new Layout(r)).ToArray();
                Vector3[] outfit = ImageCorners(portrait.OutfitLayerTransform);
                Rect outer = new Rect(-width * .5f, -height * .5f, width, height);
                // Narrow top/bottom margins reproduce the actual Home holder, including
                // rotated diamonds and two-piece lanterns in non-square frame rectangles.
                Rect opening = Rect.MinMaxRect(outer.xMin + width * .12f, outer.yMin + 5f,
                    outer.xMax - width * .15f, outer.yMax - 3f);
                Assert.That((bool)Call(portrait, "FitFrame", outer, opening), Is.True);
                foreach (Image image in portrait.FrameLayerTransform.GetComponentsInChildren<Image>())
                {
                    Rect bounds = LocalBounds(portrait.RootTransform, image.rectTransform);
                    const float tolerance = .001f;
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(outer.xMin - tolerance), image.name);
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(outer.xMax + tolerance), image.name);
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(outer.yMin - tolerance), image.name);
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(outer.yMax + tolerance), image.name);
                    bool intersects = bounds.xMin < opening.xMax - tolerance && bounds.xMax > opening.xMin + tolerance
                        && bounds.yMin < opening.yMax - tolerance && bounds.yMax > opening.yMin + tolerance;
                    Assert.That(intersects, Is.False, image.name + " rotated bounds cross the opening");
                }
                Assert.That(ImageCorners(portrait.OutfitLayerTransform), Is.EqualTo(outfit), "frame fit must not move the coat or hat");
                if (token == "frame.lantern")
                {
                    Transform group = portrait.FrameLayerTransform.Find(token);
                    foreach (RectTransform glow in group.Cast<Transform>().OfType<RectTransform>().Where(r => r.name.EndsWith("Glow")))
                    {
                        var housing = (RectTransform)group.Find(glow.name.Substring(0, glow.name.Length - 4) + "Housing");
                        Assert.That(glow.localScale, Is.EqualTo(housing.localScale), "glow preserves its housing inset");
                        Assert.That(glow.rect.width / housing.rect.width, Is.EqualTo(.058f / .11f).Within(.00001f));
                        Assert.That(glow.position, Is.EqualTo(housing.position));
                    }
                }
                Vector3[] stationary = ImageCorners(portrait.FrameLayerTransform);
                Vector3 delta = new Vector3(9f, -4f, 0f);
                portrait.RootTransform.localPosition += delta;
                Call(portrait, "MoveFrameCenter", outer.center - (Vector2)delta);
                Vector3[] moved = ImageCorners(portrait.FrameLayerTransform);
                for (int i = 0; i < moved.Length; i++)
                    Assert.That(Vector3.Distance(moved[i], stationary[i]), Is.LessThan(.0001f));
                portrait.RootTransform.localPosition -= delta;
                Call(portrait, "ResetFrameLayout");
                foreach (Layout layout in original) layout.AssertRestored();
                Assert.That((bool)Call(portrait, "FitFrame", outer, opening), Is.True, "repeated fit is safe");
                Call(portrait, "ResetFrameLayout");
                foreach (Layout layout in original) layout.AssertRestored();
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void SwitchingOrClearingFrame_RestoresCachedStyleBeforeItIsReused()
        {
            var host = new GameObject("FrameSwitchHolder", typeof(RectTransform));
            try
            {
                ((RectTransform)host.transform).sizeDelta = new Vector2(400f, 400f);
                var portrait = CosmeticPortraitView.Create(host.transform, new Source("frame.brass"));
                var original = portrait.FrameLayerTransform.GetComponentsInChildren<RectTransform>(true)
                    .Select(r => new Layout(r)).ToArray();
                Assert.That((bool)Call(portrait, "FitFrame", new Rect(-200, -200, 400, 400), new Rect(-150, -195, 300, 390)), Is.True);
                portrait.ApplySnapshot(Snapshot("frame.lantern"));
                foreach (Layout layout in original) layout.AssertRestored();
                portrait.ApplySnapshot(Snapshot("frame.brass"));
                foreach (Layout layout in original) layout.AssertRestored();
                portrait.ApplySnapshot(Snapshot(""));
                Assert.That(portrait.FrameLayerTransform.gameObject.activeSelf, Is.False);
                foreach (Layout layout in original) layout.AssertRestored();
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static object Call(CosmeticPortraitView view, string name, params object[] args)
        {
            MethodInfo method = typeof(CosmeticPortraitView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(view, args);
        }
        private static Vector3[] ImageCorners(RectTransform group) => group.GetComponentsInChildren<Image>()
            .SelectMany(i => Corners(i.rectTransform)).ToArray();
        private static Vector3[] Corners(RectTransform rect)
        {
            var points = new Vector3[4]; rect.GetWorldCorners(points); return points;
        }
        private static Rect LocalBounds(RectTransform root, RectTransform part)
        {
            Vector3[] points = Corners(part).Select(root.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        private readonly struct Layout
        {
            private readonly RectTransform _rect;
            private readonly Vector2 _min, _max, _size, _pivot;
            private readonly Vector3 _position, _scale;
            private readonly Quaternion _rotation;
            public Layout(RectTransform rect)
            {
                _rect = rect; _min = rect.anchorMin; _max = rect.anchorMax; _size = rect.sizeDelta;
                _position = rect.anchoredPosition3D; _scale = rect.localScale; _rotation = rect.localRotation; _pivot = rect.pivot;
            }
            public void AssertRestored()
            {
                Assert.That(_rect.anchorMin, Is.EqualTo(_min), _rect.name);
                Assert.That(_rect.anchorMax, Is.EqualTo(_max), _rect.name);
                Assert.That(_rect.sizeDelta, Is.EqualTo(_size), _rect.name);
                Assert.That(_rect.anchoredPosition3D, Is.EqualTo(_position), _rect.name);
                Assert.That(_rect.localScale, Is.EqualTo(_scale), _rect.name);
                Assert.That(_rect.localRotation, Is.EqualTo(_rotation), _rect.name);
                Assert.That(_rect.pivot, Is.EqualTo(_pivot), _rect.name);
            }
        }
        private static CosmeticPortraitSnapshot Snapshot(string frame) =>
            new CosmeticPortraitSnapshot("red_tabby", "cat.red_tabby", "outfit.conductor", "", frame);
        private sealed class Source : ICosmeticPortraitSource
        {
            public CosmeticPortraitSnapshot CurrentPortrait { get; }
            public event Action Changed { add { } remove { } }
            public Source(string frame) => CurrentPortrait = Snapshot(frame);
            public bool TryGetPortraitAsset(string id, out CosmeticPortraitAssetDefinition asset)
            {
                asset = string.IsNullOrEmpty(id) ? null : new CosmeticPortraitAssetDefinition(id, id, "project." + id);
                return asset != null;
            }
        }
    }
}
