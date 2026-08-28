using CatMetro.Presentation.Cats;
using NUnit.Framework;
using UnityEditor.Animations;
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

        [Test]
        public void MatchingClipsWithWrongStateNames_RejectTheRigBeforeItCanSilentlyFailPlayback()
        {
            var prefab = new GameObject("rig with wrongly named states");
            var clips = new[]
            {
                Clip(CatModelCatalog.IdleSitClip),
                Clip(CatModelCatalog.WalkClip),
                Clip(CatModelCatalog.BoardClip),
                Clip(CatModelCatalog.AlightClip),
                Clip(CatModelCatalog.CelebrateClip),
            };
            var controller = ScriptableObject.CreateInstance<AnimatorController>();
            controller.AddLayer("Base Layer");
            foreach (var clip in clips)
                controller.layers[0].stateMachine.AddState("wrong_" + clip.name).motion = clip;
            var animator = prefab.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            try
            {
                var catalog = new CatModelCatalog(prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain("state"));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(controller);
                foreach (var clip in clips) Object.DestroyImmediate(clip);
            }
        }

        private static AnimationClip Clip(string name)
        {
            var clip = new AnimationClip { name = name };
            return clip;
        }
    }
}
