using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class HomeArtBakeTests
    {
        // Explicit asset-authoring command. Ordinary test runs only read the baked sprite.
        [UnityTest]
        public IEnumerator BakeCatMark_WhenRequested()
        {
            if (System.Environment.GetEnvironmentVariable("CM_BAKE_HOME_ART") != "1") yield break;
            yield return null; // OverrideGeometry is a player-loop API, including in the Editor.
            var baker = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CatMetro.Editor.CatMetroHomeArtBaker"))
                .FirstOrDefault(t => t != null);
            Assert.That(baker, Is.Not.Null);
            baker.GetMethod("Bake").Invoke(null, null);
            var sprite = Resources.Load<Sprite>("Theme/HomeCatMark");
            Assert.That(sprite.vertices.Length, Is.EqualTo(47));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(311, 326)));
        }
    }
}
