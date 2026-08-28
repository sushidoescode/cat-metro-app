using System.Collections;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Hud.WavePreview;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    // The face is presentation-only: these tests deliberately feed visual time directly rather
    // than advancing a session or waiting for a random cadence phase.
    public sealed class CatFaceMotionTests
    {
        private GameObject _parent;
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            if (_parent != null) Object.DestroyImmediate(_parent);
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        [Test]
        public void LayoutAtAndSameVisualTime_ApplyFromBaselinesWithoutDrift()
        {
            var face = CreateLaidOutFace();
            var centre = new Vector2(137f, -42f);
            const float size = 91f;

            face.LayoutAt(centre, size);
            face.ApplyVisualTime(0.73f);
            Vector2 firstPosition = face.FaceRect.anchoredPosition;
            Vector3 firstEyeScale = Eye(face, "eyeL").localScale;
            Quaternion firstEarRotation = Ear(face, "earL").localRotation;

            face.ApplyVisualTime(0.73f);
            Assert.That(face.FaceRect.anchoredPosition, Is.EqualTo(firstPosition));
            Assert.That(Eye(face, "eyeL").localScale, Is.EqualTo(firstEyeScale));
            Assert.That(Ear(face, "earL").localRotation, Is.EqualTo(firstEarRotation));

            face.LayoutAt(centre, size);
            AssertNeutral(face, centre);
            face.ApplyVisualTime(0.73f);
            Assert.That(face.FaceRect.anchoredPosition, Is.EqualTo(firstPosition),
                "the same layout and visual input has the same pose, not a second offset");
        }

        [Test]
        public void ExplicitVisualTimes_KeepBlinkBobAndEarTwitchWithinHudBounds()
        {
            var face = CreateLaidOutFace();
            const float faceSize = 91f;
            const float maxBobPx = faceSize * 0.03f;
            bool sawBlink = false;
            bool sawBob = false;

            for (int i = 0; i <= 1000; i++)
            {
                face.ApplyVisualTime(i * 0.01f);
                float eyeY = Eye(face, "eyeL").localScale.y;
                float bob = face.FaceRect.anchoredPosition.y - 18f;
                float leftOffset = Mathf.DeltaAngle(18f, Ear(face, "earL").localEulerAngles.z);
                float rightOffset = Mathf.DeltaAngle(-18f, Ear(face, "earR").localEulerAngles.z);

                Assert.That(eyeY, Is.InRange(0.08f, 1f));
                Assert.That(Mathf.Abs(bob), Is.LessThanOrEqualTo(maxBobPx + 0.001f));
                Assert.That(Mathf.Abs(leftOffset), Is.LessThanOrEqualTo(14.001f));
                Assert.That(Mathf.Abs(rightOffset), Is.LessThanOrEqualTo(14.001f));
                sawBlink |= eyeY <= 0.081f;
                sawBob |= Mathf.Abs(bob) > 0.001f;
            }

            Assert.That(sawBlink, Is.True, "the sampled explicit visual times include a blink");
            Assert.That(sawBob, Is.True, "the sampled explicit visual times include a face bob");
        }

        [Test]
        public void MotionOffAndDisabledReuse_ResetToExactNeutralImmediately()
        {
            var face = CreateLaidOutFace();
            var centre = new Vector2(-12f, 18f);
            face.LayoutAt(centre, 91f);

            bool motionOff = false;
            face.BindMotionOff(() => motionOff);
            face.ApplyVisualTime(0.73f);
            motionOff = true;
            face.ApplyVisualTime(0.73f);
            AssertNeutral(face, centre);

            motionOff = false;
            face.ApplyVisualTime(0.73f);
            face.gameObject.SetActive(false);
            AssertNeutral(face, centre);
            face.gameObject.SetActive(true);
            AssertNeutral(face, centre);
        }

        [Test]
        public void DisableBeforeLayout_ResetsIdentityGeometry()
        {
            _parent = new GameObject("parent", typeof(RectTransform));
            var face = CatFaceView.Create(_parent.transform, "unlaid-face");
            face.FaceRect.anchoredPosition = new Vector2(9f, 11f);
            face.FaceRect.localScale = new Vector3(2f, 3f, 1f);
            Eye(face, "eyeL").localScale = new Vector3(1f, 0.08f, 1f);
            Ear(face, "earL").localRotation = Quaternion.Euler(0f, 0f, 32f);

            face.gameObject.SetActive(false);

            Assert.That(face.FaceRect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(face.FaceRect.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Eye(face, "eyeL").localScale, Is.EqualTo(Vector3.one));
            Assert.That(Ear(face, "earL").localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void FaceTree_IsRenderOnlyAndHasNoInteractiveComponents()
        {
            var face = CreateLaidOutFace();

            Assert.That(face.GetComponentsInChildren<Renderer>(true), Is.Empty);
            Assert.That(face.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(face.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(face.GetComponentsInChildren<Selectable>(true), Is.Empty);
            Assert.That(face.GetComponentsInChildren<BaseRaycaster>(true), Is.Empty);
            foreach (var graphic in face.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name + " must be decorative");
        }

        [UnityTest]
        public IEnumerator GameRoot_BindsMotionOffToFacesAfterWireAndPreviewRebuild()
        {
            _root = GameRoot.Launch();
            yield return null;

            _root.MotionOffToggle = true;
            AssertPreviewFacesAreNeutral(_root.Preview);

            _root.Retry();
            yield return null;
            AssertPreviewFacesAreNeutral(_root.Preview);
        }

        [UnityTest]
        public IEnumerator SessionTickLayout_DoesNotLeaveAnActiveFaceNeutralAtEndOfFrame()
        {
            _root = GameRoot.Launch();
            yield return new WaitForEndOfFrame();
            var face = FirstActiveFace(_root.Preview);

            _root.MotionOffToggle = true;
            face.ApplyVisualTime(Time.unscaledTime);
            Vector2 neutralPosition = face.FaceRect.anchoredPosition;
            _root.MotionOffToggle = false;

            // A real session change makes WavePreviewStrip.Refresh/Layout run in LateUpdate.
            // The assertion resumes after rendering, when a pre-render callback has had its
            // final chance to reapply the visual pose.
            _root.Session.AdvanceMs(CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return new WaitForEndOfFrame();

            Assert.That(_root.Session.State.Tick, Is.GreaterThan(0));
            Assert.That(IsAnimated(face, neutralPosition), Is.True,
                "a LateUpdate layout must not win over the face's final render pose");
        }

        private CatFaceView CreateLaidOutFace()
        {
            _parent = new GameObject("parent", typeof(RectTransform));
            var face = CatFaceView.Create(_parent.transform, "hud-face");
            face.Bind("red");
            face.LayoutAt(new Vector2(0f, 18f), 91f);
            return face;
        }

        private static void AssertPreviewFacesAreNeutral(WavePreviewStrip preview)
        {
            Assert.That(preview, Is.Not.Null);
            var faces = preview.GetComponentsInChildren<CatFaceView>(true);
            Assert.That(faces, Is.Not.Empty);
            int laidOutFaceCount = 0;
            foreach (var face in faces)
            {
                if (!face.gameObject.activeSelf) continue;
                laidOutFaceCount++;
                // willRenderCanvases may already have sampled an animated pose before this
                // helper runs. Apply the caller's motion-off source first, then capture the
                // actual layout centre that every following motion-off sample must preserve.
                face.ApplyVisualTime(Time.unscaledTime);
                Vector2 centre = face.FaceRect.anchoredPosition;
                foreach (float time in new[] { 0.17f, 0.93f, 1.71f })
                {
                    face.ApplyVisualTime(time);
                    AssertNeutral(face, centre);
                }
            }
            Assert.That(laidOutFaceCount, Is.GreaterThan(0),
                "the real L001 composition exposes at least one laid-out preview face");
        }

        private static CatFaceView FirstActiveFace(WavePreviewStrip preview)
        {
            foreach (var face in preview.GetComponentsInChildren<CatFaceView>(true))
                if (face.gameObject.activeSelf) return face;
            Assert.Fail("the real preview must expose an active face for this render-order test");
            return null;
        }

        private static bool IsAnimated(CatFaceView face, Vector2 neutralPosition) =>
            face.FaceRect.anchoredPosition != neutralPosition
            || Eye(face, "eyeL").localScale != Vector3.one
            || Eye(face, "eyeR").localScale != Vector3.one
            || Ear(face, "earL").localRotation != Quaternion.Euler(0f, 0f, 18f)
            || Ear(face, "earR").localRotation != Quaternion.Euler(0f, 0f, -18f);

        private static void AssertNeutral(CatFaceView face, Vector2 centre)
        {
            Assert.That(face.FaceRect.anchoredPosition, Is.EqualTo(centre));
            Assert.That(face.FaceRect.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Eye(face, "eyeL").localScale, Is.EqualTo(Vector3.one));
            Assert.That(Eye(face, "eyeR").localScale, Is.EqualTo(Vector3.one));
            Assert.That(Ear(face, "earL").localRotation, Is.EqualTo(Quaternion.Euler(0f, 0f, 18f)));
            Assert.That(Ear(face, "earR").localRotation, Is.EqualTo(Quaternion.Euler(0f, 0f, -18f)));
        }

        private static RectTransform Eye(CatFaceView face, string name) =>
            face.transform.Find(name) as RectTransform;

        private static RectTransform Ear(CatFaceView face, string name) =>
            face.transform.Find(name) as RectTransform;
    }
}
