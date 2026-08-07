using NUnit.Framework;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-02 R1-M6: the hand-authored UiChrome.mat gets the same pairing proof as its
    // GreyboxMaterial precedent — a wrong builtin fileID would load a material with the wrong
    // shader and every other test would stay green while device chrome renders wrong.
    public sealed class UiChromeMaterialTests
    {
        [Test]
        public void SharedMaterial_LoadsAndBindsTheUiDefaultShader()
        {
            var mat = UiChromeMaterial.Shared;
            Assert.That(mat, Is.Not.Null, "Materials/UiChrome must load through Resources");
            Assert.That(mat.shader, Is.Not.Null);
            Assert.That(mat.shader.name, Is.EqualTo("UI/Default"),
                "the hand-authored fileID must bind the intended builtin shader");
        }
    }
}
