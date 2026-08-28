using CatMetro.Presentation.Cats;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class CatModelCatalogTests
    {
        [Test]
        public void MissingRig_StaysUnadmittedWithAReadBackReason()
        {
            var catalog = new CatModelCatalog(null);

            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
            Assert.That(catalog.RejectionReason, Is.Not.Empty);
            Assert.That(catalog.TryInstantiate(null, out var instance), Is.False);
            Assert.That(instance, Is.Null);
        }

        [Test]
        public void ColliderOnRig_RejectsThePrefabRatherThanAdmittingInteractiveDecoration()
        {
            var prefab = new GameObject("invalid cat rig");
            prefab.AddComponent<BoxCollider>();
            try
            {
                var catalog = new CatModelCatalog(prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain("Collider"));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }
    }
}
