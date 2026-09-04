using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Props;

namespace CatMetro.Tests.PlayMode
{
    // CM-C2b-DEVFIX criteria 1-5: URP restored and provably active with pinned mobile-sane
    // config, boot frame-rate policy, committed URP/Lit material bound to every project
    // primitive, admitted URP/Lit props bound to their atlas, and text surviving the pipeline
    // switch. RED on the pre-fix main by
    // construction: currentRenderPipeline is null, the target frame rate is the engine default,
    // and Resources holds no material. Device-only facts (stripping, label pixels, frame times)
    // are NEVER asserted here — criterion 6 hands those to the human (A-DEVFIX-6).
    public sealed class DeviceConfigTests
    {
        private GameRoot _root;

        [SetUp]
        public void SetUp()
        {
            // criterion 3: a target leaked by any earlier test would make the boot-policy
            // assertion vacuous — prove Wire() sets it, not history
            UnityEngine.Application.targetFrameRate = -1;
            // CM-BOOT-HOME: both UnityTests below boot through GameRoot.Launch() — Home now
            // composes there by default (criterion 1), which would gate the switch tap
            // (FramePolicy test is unaffected, but AllRuntimeRenderers_BindGreybox_TextMesh
            // Survives walks every renderer under the root and would trip on Home's own
            // Image/Text components, none of which bind the Greybox material). Opt OUT via the
            // dev skip-Home hatch.
            GameRoot.DevSkipShippedHome = true;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.DevSkipShippedHome = false; // hygiene: restore the true global default
            Time.timeScale = 1f;
            QualitySettings.vSyncCount = 0; // undo the F-3 de-vacuum poke (runtime-only)
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        // --- criterion 1: URP active, single reference, no level override ---
        [Test]
        public void Urp_Restored_Active_SingleReference_NoLevelOverride()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            Assert.That(rp, Is.Not.Null,
                "no render pipeline is active — the tree ships no pipeline asset (A-DEVFIX-1)");
            Assert.That(rp, Is.InstanceOf<UniversalRenderPipelineAsset>(),
                "ADR-0004:37 pins URP");
            Assert.That(rp.name, Is.EqualTo("CatMetro_URP"),
                "the one committed asset under Assets/Settings/");
            Assert.That(QualitySettings.renderPipeline, Is.Null,
                "no quality level may override the project pipeline (cleared, not repointed)");
        }

        // --- criterion 2: the configuration is pinned, not inherited ---
        [Test]
        public void PipelineConfig_PinnedMobileSane()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(urp, Is.Not.Null, "criterion 1 first");
            Assert.That(urp.supportsHDR, Is.False, "HDR off: tile bandwidth for nothing");
            Assert.That(urp.msaaSampleCount, Is.EqualTo(1), "MSAA disabled");
            Assert.That(urp.renderScale, Is.EqualTo(1f).Within(0.001f), "no hidden scaling");
            Assert.That(urp.supportsCameraDepthTexture, Is.False, "no depth pass");
            Assert.That(urp.supportsCameraOpaqueTexture, Is.False, "no opaque copy");
            Assert.That(urp.supportsMainLightShadows, Is.True,
                "the single diorama key casts the board's contact shadow");
            Assert.That(urp.supportsSoftShadows, Is.True,
                "the contact shadow stays cosy instead of reading as a hard debug silhouette");
            Assert.That(urp.additionalLightsRenderingMode,
                Is.EqualTo(LightRenderingMode.Disabled), "no additional lights exist");
            Assert.That(urp.useSRPBatcher, Is.True, "the one free win");
            Assert.That(urp.supportsDynamicBatching, Is.False, "superseded by SRP Batcher");
            Assert.That(urp.useAdaptivePerformance, Is.False,
                "F-DEV-1 interlock: no runtime governor may defeat the frame policy");
            Assert.That(urp.mainLightRenderingMode,
                Is.EqualTo(LightRenderingMode.PerPixel), "pinned, not the inherited default");
            Assert.That(urp.colorGradingMode,
                Is.EqualTo(ColorGradingMode.LowDynamicRange), "LDR matches HDR-off");
        }

        // --- criterion 3: boot frame-rate policy on every boot path ---
        [UnityTest]
        public IEnumerator FramePolicy_AppliedAtWire_TargetGoverns()
        {
            Assert.That(UnityEngine.Application.targetFrameRate, Is.EqualTo(-1),
                "SetUp reset holds before boot");
            // review F-3: de-vacuum the vsync half — set it non-zero so the assertion below
            // proves the POLICY drove it to 0, not the project default (runtime-only state;
            // never serialized back to the committed asset)
            QualitySettings.vSyncCount = 1;
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(UnityEngine.Application.targetFrameRate, Is.EqualTo(FramePolicy.TARGET_FPS),
                "Wire() applies the policy on every boot path");
            Assert.That(FramePolicy.TARGET_FPS, Is.EqualTo(60), "criterion 8's own budget");
            Assert.That(QualitySettings.vSyncCount, Is.Zero,
                "vsync 0 so the target governs (vsync 1 on a 120 Hz panel presents 120 fps)");
        }

        // --- criterion 4: the committed material loads through the device path ---
        [Test]
        public void GreyboxMaterial_LoadsThroughResources_PairsActivePipeline()
        {
            var mat = Resources.Load<Material>("Materials/Greybox");
            Assert.That(mat, Is.Not.Null, "the committed inclusion route (ADR-0007, no Addressables)");
            Assert.That(mat.shader, Is.Not.Null, "a broken shader reference is silent magenta");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Not.Null);
            Assert.That(mat.shader.name, Does.StartWith("Universal Render Pipeline/"),
                "pairing invariant: a future pipeline change may not silently re-magenta the board");
        }

        // --- criterion 5: every runtime renderer binds it; text survives the switch ---
        [UnityTest]
        public IEnumerator AllRuntimeRenderers_BindGreybox_TextMeshSurvives()
        {
            var greybox = Resources.Load<Material>("Materials/Greybox");
            Assert.That(greybox, Is.Not.Null, "criterion 4 first");

            _root = GameRoot.Launch();
            yield return null;
            // exercise ALL FOUR creation surfaces and prove each before asserting:
            // wave preview chips (inspect before advancing beyond a short authored wave)
            Assert.That(_root.Preview.VisibleChipCount, Is.GreaterThanOrEqualTo(1),
                "the chip surface was exercised");
            // trains (advance until at least one spawns and renders)
            _root.Session.AdvanceMs(12 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            bool trainSeen = false;
            foreach (var el in Object.FindObjectsByType<CatMetro.Presentation.Board.BoardElementId>(
                FindObjectsSortMode.None))
                if (el.Kind == "train") trainSeen = true;
            Assert.That(trainSeen, Is.True, "the train surface was exercised");
            // cause ring (public entry, no failure needed). Review F-2: the ring is DELIBERATELY
            // unparented (CM-C3 review B1 — never rides the camera), so the root walk below
            // cannot see it; it gets its own explicit assertion here. Review N-1: an earlier
            // test's ring may already sit at scene root (ShowRing never destroys it) — clear
            // any stale one first so the assertion inspects the ring THIS test created.
            var stale = GameObject.Find("CauseRing");
            if (stale != null) Object.DestroyImmediate(stale);
            _root.CauseCam.FrameNode("SRC", _root.View.NodeWorldPos(0), motionOff: true);
            yield return null;
            Assert.That(_root.CauseCam.RingVisible, Is.True, "the ring surface was exercised");
            var ringGo = GameObject.Find("CauseRing");
            Assert.That(ringGo, Is.Not.Null, "the ring exists at scene root (unparented)");
            Assert.That(ringGo.GetComponent<Renderer>().sharedMaterial, Is.Not.Null,
                "the ring binds a material");
            Assert.That(ringGo.GetComponent<Renderer>().sharedMaterial.shader,
                Is.EqualTo(greybox.shader),
                "the ring — the failure screen's primitive — binds the committed material");
            int checkedRenderers = 0;
            foreach (var r in _root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TextMesh>() != null)
                {
                    Assert.That(r.sharedMaterial, Is.Not.Null, "text material present");
                    Assert.That(r.sharedMaterial.shader, Is.Not.Null,
                        "text shader compiles (URP drawing is the human screenshot's leg)");
                    continue;
                }
                Assert.That(r.sharedMaterial, Is.Not.Null, r.gameObject.name);
                var prop = r.GetComponentInParent<BoardPropInstance>();
                var generatedModel = prop == null ? null : prop.transform.Find("Model");
                if (generatedModel != null && (r.transform == generatedModel
                    || r.transform.IsChildOf(generatedModel)))
                {
                    Assert.That(r.sharedMaterial.shader.name,
                        Is.EqualTo("Universal Render Pipeline/Lit"),
                        r.gameObject.name + " is an admitted generated prop");
                    Assert.That(r.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null,
                        r.gameObject.name + " must explicitly bind its baked atlas");
                    checkedRenderers++;
                    continue;
                }
                Assert.That(r.sharedMaterial.shader, Is.EqualTo(greybox.shader),
                    r.gameObject.name + " must bind the committed material, not the engine default");
                checkedRenderers++;
            }
            Assert.That(checkedRenderers, Is.GreaterThanOrEqualTo(10),
                "board + trains + chips were actually walked (the unparented ring is asserted " +
                "explicitly above — review F-2)");
        }
    }
}
