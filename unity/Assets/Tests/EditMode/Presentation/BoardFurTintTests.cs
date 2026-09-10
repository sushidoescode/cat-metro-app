using System.Linq;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class BoardFurTintTests
    {
        private GameObject _host;
        private Material _source;
        private Texture2D _atlas;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_source != null) Object.DestroyImmediate(_source);
            if (_atlas != null) Object.DestroyImmediate(_atlas);
        }

        [Test]
        public void InstallingAndReusingRouteColour_PreservesSourceAtlasAndIndependentRenderers()
        {
            BuildFixture();
            var a = _host.transform.Find("A").GetComponent<Renderer>();
            var b = _host.transform.Find("B").GetComponent<Renderer>();
            var initial = new MaterialPropertyBlock();
            initial.SetFloat("_BumpScale", .73f);
            a.SetPropertyBlock(initial);
            var fur = BoardFurTint.TryInstall(_host);
            Assert.That(fur, Is.Not.Null, "the shipped shader and bound URP atlas must install");
            Assert.That(BoardFurTint.TryInstall(_host), Is.SameAs(fur), "installation is idempotent");
            Material owned = a.sharedMaterial;
            Assert.That(owned, Is.Not.SameAs(_source));
            Assert.That(b.sharedMaterial, Is.SameAs(owned), "one source material makes one owned clone");
            Assert.That(owned.GetTexture("_BaseMap"), Is.SameAs(_atlas));
            foreach (byte code in new[] { CatColor.Red, CatColor.Blue, CatColor.Green, CatColor.Wild })
            {
                fur.Apply(CatLine.ColorOf(code));
                foreach (Renderer renderer in new[] { a, b })
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    AssertColour(block.GetColor("_BaseColor"), Color.white);
                    AssertColour(block.GetColor("_FurColor"), CatLine.ColorOf(code));
                    Assert.That(block.GetFloat("_FurStrength"), Is.EqualTo(1));
                }
            }
            var readback = new MaterialPropertyBlock();
            a.GetPropertyBlock(readback);
            Assert.That(readback.GetFloat("_BumpScale"), Is.EqualTo(.73f));
            Assert.That(_source.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(_source.GetTexture("_BaseMap"), Is.SameAs(_atlas));
            AssertColour(_source.GetColor("_BaseColor"), Color.white);

            Object.DestroyImmediate(fur);
            Assert.That(a.sharedMaterial, Is.SameAs(_source));
            Assert.That(b.sharedMaterial, Is.SameAs(_source));
            Assert.That(owned == null, Is.True, "owned material must be destroyed with its binding");
            a.GetPropertyBlock(readback);
            Assert.That(readback.GetFloat("_BumpScale"), Is.EqualTo(.73f));
            Assert.That(readback.HasColor(Shader.PropertyToID("_FurColor")), Is.False);
        }

        [Test]
        public void RemovingFurAfterColourAndPreviewChangesRestoresAbsentPropertyBlocks()
        {
            BuildFixture();
            Renderer[] renderers = _host.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.EqualTo(2));
            foreach (Renderer renderer in renderers)
                Assert.That(renderer.HasPropertyBlock(), Is.False, "fixture must begin without any block");
            var fur = BoardFurTint.TryInstall(_host);
            Assert.That(fur, Is.Not.Null);
            fur.Apply(Palette.HarborBlue);
            fur.SetPreview(1f, true);
            foreach (Renderer renderer in renderers)
            {
                Assert.That(renderer.HasPropertyBlock(), Is.True, "positive control: binding really added a block");
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                AssertColour(block.GetColor("_FurColor"), Palette.HarborBlue);
                Assert.That(block.GetFloat("_FurDebug"), Is.EqualTo(1f));
            }
            Object.DestroyImmediate(fur);
            foreach (Renderer renderer in renderers)
            {
                Assert.That(renderer.sharedMaterial, Is.SameAs(_source));
                Assert.That(renderer.HasPropertyBlock(), Is.False,
                    "an allocated empty block is not the original absent block");
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.isEmpty, Is.True);
            }
        }

        [Test]
        public void MissingAtlas_LeavesEverySourceMaterialUntouched()
        {
            BuildFixture();
            _source.SetTexture("_BaseMap", null);
            Assert.That(BoardFurTint.TryInstall(_host), Is.Null);
            Assert.That(_host.GetComponent<BoardFurTint>(), Is.Null);
            Assert.That(_host.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial == _source), Is.True);
        }

        [Test]
        public void NaturalAndMaskPreview_DoNotLoseTheNextRouteColour()
        {
            BuildFixture();
            var fur = BoardFurTint.TryInstall(_host);
            Assert.That(fur, Is.Not.Null);
            fur.Apply(Palette.SignalRed);
            fur.SetPreview(0f, false);
            fur.Apply(Palette.HarborBlue);
            fur.SetPreview(1f, true);
            var block = new MaterialPropertyBlock();
            _host.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
            AssertColour(block.GetColor("_BaseColor"), Color.white);
            AssertColour(block.GetColor("_FurColor"), Palette.HarborBlue);
            Assert.That(block.GetFloat("_FurDebug"), Is.EqualTo(1f));
            fur.SetPreview(1f, false);
            _host.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
            Assert.That(block.GetFloat("_FurDebug"), Is.Zero);
            AssertColour(block.GetColor("_FurColor"), Palette.HarborBlue);
        }

        [Test]
        public void InstalledRig_SlotReuseAndRetainedPassengerKeepRouteAndSourceIdentity()
        {
            GameObject resource = Resources.Load<GameObject>(CatModelCatalog.ResourcePath);
            if (resource == null)
            {
                Assert.That(CatModelCatalog.LoadResources().AdmittedEntryCount, Is.Zero);
                return; // Licensed artifact is optional; synthetic binding and GPU tests still run.
            }
            var catalog = new CatModelCatalog(resource);
            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1), catalog.RejectionReason);
            Assert.That(BoardFurTint.TryInstall(resource), Is.Null, "never bind the paid source prefab");
            _host = new GameObject("fur-route-identity");
            var train = ToyTrainView.Create(_host.transform, "rider", new[] { 0 }, new[] { 1 }, catalog);
            var sourceSkin = resource.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var skin = train.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skin.sharedMesh, Is.SameAs(sourceSkin.sharedMesh));
            Assert.That(train.GetComponentInChildren<BoardFurTint>(true), Is.Not.Null);
            foreach (byte code in new[] { CatColor.Red, CatColor.Blue, CatColor.Yellow, CatColor.Green, CatColor.Wild })
            {
                train.SyncSlot(100L + code, code);
                train.SetTokenFlags(false, true);
                AssertRoute(train, skin, code);
            }
            train.PrepareDeliveredPassenger(new Vector3(2, 3, -.2f));
            AssertRoute(train, skin, CatColor.Wild);
            Assert.That(skin.enabled && skin.gameObject.activeInHierarchy, Is.True);
            Assert.That(train.transform.Find("Engine").gameObject.activeSelf, Is.False);
            Assert.That(train.transform.Find("Carriage/Pin").gameObject.activeSelf, Is.True);
            Assert.That(train.GetComponentInChildren<PassengerStatusMarks>(true).ExpressVisible, Is.True);
            Assert.That(skin.sharedMaterial.GetTexture("_BaseMap"),
                Is.SameAs(sourceSkin.sharedMaterial.GetTexture("_BaseMap")));
            AssertColour(sourceSkin.sharedMaterial.GetColor("_BaseColor"), Color.white);
            Assert.That(sourceSkin.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(resource.GetComponentInChildren<BoardFurTint>(true), Is.Null,
                "Home and all future instances must retain the untouched source material");
        }

        private static void AssertRoute(ToyTrainView train, Renderer skin, byte code)
        {
            var block = new MaterialPropertyBlock();
            skin.GetPropertyBlock(block);
            AssertColour(block.GetColor("_BaseColor"), Color.white);
            AssertColour(block.GetColor("_FurColor"), CatLine.ColorOf(code));
            AssertColour(train.CatTint, CatLine.ColorOf(code));
            train.transform.Find("Carriage/Pin/Symbol").GetComponent<Renderer>().GetPropertyBlock(block);
            AssertColour(block.GetColor("_BaseColor"), CatLine.ColorOf(code));
        }

        private static void AssertColour(Color actual, Color expected) =>
            Assert.That(Vector4.Distance(actual, expected), Is.LessThan(.00001f));

        private void BuildFixture()
        {
            _host = new GameObject("fur-material-fixture");
            _atlas = new Texture2D(2, 2);
            _source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _source.SetColor("_BaseColor", Color.white);
            _source.SetTexture("_BaseMap", _atlas);
            foreach (string name in new[] { "A", "B" })
            {
                var part = new GameObject(name);
                part.transform.SetParent(_host.transform, false);
                part.AddComponent<MeshRenderer>().sharedMaterial = _source;
            }
        }
    }
}
