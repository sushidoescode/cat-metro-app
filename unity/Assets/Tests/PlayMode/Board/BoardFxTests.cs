using System.Collections;
using CatMetro.Presentation.Fx;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatMetro.Tests.PlayMode
{
    public sealed class BoardFxTests
    {
        private GameObject _owner;
        private BoardFx _fx;
        private Transform _target;
        private bool _motionOff;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("BoardFx test");
            _target = new GameObject("toy", typeof(RectTransform)).transform;
            _target.SetParent(_owner.transform, false);
            _motionOff = false;
            _fx = BoardFx.GetOrCreate(_owner.transform, () => _motionOff);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(_owner);
        }

        [Test]
        public void Flash_DoesNotOverwriteAStateChangeDuringThePress()
        {
            var face = _target.gameObject.AddComponent<Image>();
            face.color = Color.white;
            _fx.Flash(face, Color.gray);
            face.color = Color.green;
            _fx.Advance(0.01f, Time.frameCount + 1);
            Assert.That(face.color, Is.EqualTo(Color.green), "selection owns the new paint");
            _fx.Advance(0.2f, Time.frameCount + 2);
            Assert.That(face.color, Is.EqualTo(Color.green), "completion cannot restore stale paint");
        }

        [Test]
        public void Press_KeepsThePaintedCenterWithACornerPivot()
        {
            var rect = (RectTransform)_target;
            rect.sizeDelta = new Vector2(400, 100);
            rect.pivot = Vector2.zero;
            rect.localPosition = new Vector3(80, 120, 0);
            Vector3 center = rect.TransformPoint(rect.rect.center);
            _fx.Press(rect);
            _fx.Advance(0.042f);
            Assert.That(Vector3.Distance(rect.TransformPoint(rect.rect.center), center), Is.LessThan(0.001f));
            _fx.Advance(0.1f);
            Assert.That(rect.localPosition, Is.EqualTo(new Vector3(80, 120, 0)));
        }

        [Test]
        public void Punch_RisesAndSettles_WithoutCompoundingOnRepeatedTaps()
        {
            _target.localScale = new Vector3(2f, 3f, 1f);
            _fx.Punch(_target, 1.12f, 0.14f);
            _fx.Advance(0.045f);
            Assert.That(_target.localScale.x, Is.GreaterThan(2.1f));
            _fx.Punch(_target, 1.12f, 0.14f);
            _fx.Advance(0.14f);
            Assert.That(_target.localScale, Is.EqualTo(new Vector3(2f, 3f, 1f)));
            Assert.That(_fx.ActiveTweenCount, Is.Zero);
        }

        [Test]
        public void Press_CompressesRecoversAndRestoresPaint_AfterTwoFrames()
        {
            var face = _target.gameObject.AddComponent<Image>();
            face.color = new Color(1f, 0.8f, 0.5f, 0.6f);
            var baseline = face.color;
            int frame = Time.frameCount;
            _fx.Press((RectTransform)_target, face);
            Assert.That(face.color.r, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(face.color.a, Is.EqualTo(0.6f));
            _fx.Advance(0.035f, frame);
            Assert.That(_target.localScale.x, Is.LessThan(0.98f));
            Assert.That(face.color.r, Is.EqualTo(0.9f).Within(0.001f));
            _fx.Advance(0.035f, frame + 1);
            Assert.That(face.color.r, Is.EqualTo(0.9f).Within(0.001f));
            _fx.Advance(0.035f, frame + 2);
            Assert.That(face.color, Is.EqualTo(baseline));
            Assert.That(_target.localScale.x, Is.GreaterThan(1f));
            _fx.Advance(0.035f);
            Assert.That(_target.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void CompletionCanDisableHelper_AndSettleTheOtherTween()
        {
            float other = 0f;
            _fx.Tween(_owner, 1f, p => other = p);
            _fx.Tween(_target, 0.1f, p => { if (p >= 1f) _fx.enabled = false; });
            Assert.DoesNotThrow(() => _fx.Advance(0.1f));
            Assert.That(other, Is.EqualTo(1f));
            Assert.That(_fx.ActiveTweenCount, Is.Zero);
        }

        [Test]
        public void CompletionCanReplaceOtherTween_WithoutSamplingTheCancelledOneAgain()
        {
            float oldProgress = 0f, newProgress = 0f;
            _fx.Tween(_owner, 1f, p => oldProgress = p);
            _fx.Tween(_target, 0.1f, p =>
            {
                if (p >= 1f) _fx.Tween(_owner, 0.3f, q => newProgress = q);
            });
            _fx.Advance(0.1f);
            Assert.That(oldProgress, Is.EqualTo(1f));
            Assert.That(newProgress, Is.Zero, "new work starts on the following update");
            _fx.Advance(0.3f);
            Assert.That(newProgress, Is.EqualTo(1f));
        }

        [Test]
        public void ReentrantReplacement_HasOneOwner_AndTheLatestRequestWins()
        {
            float value = 0f;
            _fx.Tween(_target, 1f, p =>
            {
                if (p >= 1f) _fx.Tween(_target, 0.2f, q => value = 10f + q);
            });
            _fx.Tween(_target, 0.2f, p => value = 20f + p);
            Assert.That(_fx.ActiveTweenCount, Is.EqualTo(1));
            _fx.Advance(0.2f);
            Assert.That(value, Is.EqualTo(11f), "the callback requested its follow-up last");
        }

        [Test]
        public void ExternalFactoryParticles_StopWithMotionOffAndHelperDisable_AndDieWithOwner()
        {
            var external = new GameObject("External funnel");
            try
            {
                var ps = _fx.CreateParticles(external.transform, "Smoke", BoardFxSprite.Puff);
                ps.Emit(2);
                _motionOff = true;
                _fx.Advance(0f);
                Assert.That(ps.particleCount, Is.Zero);
                _motionOff = false;
                ps.Emit(2);
                _fx.enabled = false;
                Assert.That(ps.particleCount, Is.Zero);
                Object.DestroyImmediate(_owner);
                Assert.That(ps == null, Is.True);
            }
            finally { Object.DestroyImmediate(external); }
        }

        [Test]
        public void FlashStartedAfterUpdate_StillSpansTwoRenderedFrames()
        {
            var face = _target.gameObject.AddComponent<Image>();
            int frame = Time.frameCount;
            _fx.Advance(0.01f, frame);
            _fx.Flash(face, Color.red);
            Assert.That(face.color, Is.EqualTo(Color.red));
            _fx.Advance(0.01f, frame + 1);
            Assert.That(face.color, Is.EqualTo(Color.red));
            _fx.Advance(0.01f, frame + 2);
            Assert.That(face.color, Is.EqualTo(Color.white));
        }

        [Test]
        public void MotionOff_SettlesActiveMotionAndSuppressesNewParticles()
        {
            _fx.Shake(_target, 5f, 0.3f);
            _fx.Advance(0.025f);
            Assert.That(Quaternion.Angle(_target.localRotation, Quaternion.identity),
                Is.GreaterThan(0.5f));
            _motionOff = true;
            _fx.Advance(0.01f);
            _fx.Emit(BoardFxSprite.Heart, Vector3.zero, Color.white, 12);
            Assert.That(_target.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(_fx.ActiveTweenCount, Is.Zero);
            foreach (var ps in _owner.GetComponentsInChildren<ParticleSystem>())
                Assert.That(ps.particleCount, Is.Zero);
            float progress = 0f;
            _fx.Tween(_target, 0.3f, p => progress = p);
            Assert.That(progress, Is.EqualTo(1f), "reduced motion still applies the final state");
        }

        [Test]
        public void DestroyedTargets_AreCancelledWithoutInvokingTheirCallbacks()
        {
            int samples = 0;
            _fx.Tween(_target, 0.2f, p => samples++);
            int beforeDestroy = samples;
            Object.DestroyImmediate(_target.gameObject);
            Assert.DoesNotThrow(() => _fx.Advance(1f));
            Assert.That(samples, Is.EqualTo(beforeDestroy));
            Assert.That(_fx.ActiveTweenCount, Is.Zero);
        }

        [Test]
        public void BurstPool_IsBoundedAndUsesTheRetainedParticleShader()
        {
            for (int i = 0; i < 12; i++)
                _fx.Emit((BoardFxSprite)(i % 3), Vector3.zero, Color.white, 6);
            var systems = _owner.GetComponentsInChildren<ParticleSystem>();
            Assert.That(systems, Has.Length.EqualTo(4));
            int count = 0;
            foreach (var ps in systems)
            {
                count += ps.particleCount;
                var material = ps.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Assert.That(material.shader.name,
                    Is.EqualTo("Universal Render Pipeline/Particles/Unlit"));
                Assert.That(material.mainTexture.width, Is.EqualTo(64));
                Assert.That(material.mainTexture.height, Is.EqualTo(64));
                Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            }
            Assert.That(count, Is.GreaterThan(0));
            _motionOff = true;
            _fx.Advance(0.01f);
            foreach (var ps in systems) Assert.That(ps.particleCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RuntimeTween_ProgressesWhileGameTimeIsPaused()
        {
            Time.timeScale = 0f;
            float progress = 0f;
            _fx.Tween(_target, 0.1f, p => progress = p);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(progress, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_Fx_WhenRequested()
        {
            string dir = System.Environment.GetEnvironmentVariable("CM_BOARD_FX_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            var camera = new GameObject("Fx camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(_owner.transform, false);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 0.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CatMetro.Presentation.Theme.Palette.InkNavy;
            var rt = new RenderTexture(960, 480, 24);
            camera.targetTexture = rt;
            yield return null;
            for (int i = 0; i < 3; i++)
            {
                var ps = _fx.CreateParticles(_owner.transform, "Glyph " + i, (BoardFxSprite)i);
                var main = ps.main;
                main.startSize = 0.9f;
                main.startSpeed = 0f;
                main.startLifetime = 2f;
                main.startColor = CatMetro.Presentation.Theme.Palette.WarmPaper;
                var shape = ps.shape;
                shape.enabled = false;
                ps.transform.position = new Vector3((i - 1) * 0.7f, 0f, 0f);
                ps.Emit(1);
                ps.Simulate(0.75f, true, false);
            }
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            image.Apply();
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "fx-puff-heart-star.png"),
                image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(rt);
        }
    }
}
