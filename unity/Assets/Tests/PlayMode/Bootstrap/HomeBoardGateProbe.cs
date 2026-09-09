using CatMetro.Bootstrap;
using CatMetro.Presentation.Input;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.PlayMode
{
    internal static class HomeBoardGateProbe
    {
        public static Vector2 PlaceDiscOutsidePins(GameRoot root)
        {
            // These fixtures isolate the board-input gate, not the board's composition.
            // Put a real switch at the aperture centre so none of Home's current pins
            // can claim the tap before it reaches that gate.
            var corners = new Vector3[4];
            root.Home.DioramaWindowTransform.GetWorldCorners(corners);
            var target = (Vector2)root.Cam.WorldToScreenPoint((corners[0] + corners[2]) * .5f);
            Vector3 original = root.View.SwitchWorldPos(0);
            float depth = root.Cam.WorldToScreenPoint(original).z;
            root.View.transform.position += root.Cam.ScreenToWorldPoint(
                new Vector3(target.x, target.y, depth)) - original;
            var disc = (Vector2)root.Cam.WorldToScreenPoint(root.View.SwitchWorldPos(0));
            Assert.That(root.Home.PinPaintedRectPx.Contains(disc), Is.False);
            Assert.That(root.Home.DailyPinPaintedRectPx.Contains(disc), Is.False);
            Assert.That(root.Wardrobe.EntryRectPx.Contains(disc), Is.False);
            Assert.That(root.Input.Regions.TryResolve(disc, out _), Is.False,
                "every registered Home control must miss the board-gate probe");
            Assert.That(TapInput.ResolveNearestDisc(target, new[] { disc }, 1f), Is.Zero,
                "positive control: the relocated switch projects at the intended gate probe");
            return disc;
        }
    }
}
