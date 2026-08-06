using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-03 criteria 1-6 under the inherited live-wiring/anti-vacuity rule: every absence
    // assert carries a positive control in the same fixture. The affordance marks the verb
    // surface (contract A-UX3-1); nothing here derives or asserts route correctness.
    public sealed class TeachAffordanceTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            Time.timeScale = 1f;
        }

        private static ImportedLevel Import(string json)
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(r.Ok, Is.True, "fixture must import: " + r.Error);
            return r.Value;
        }

        private static int RingCount(GameObject root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("teachring")) n++;
            return n;
        }

        // --- criterion 1: band gate, both directions ---

        [UnityTest]
        public IEnumerator OnboardingBand_RendersTheAffordance()
        {
            _root = GameRoot.LaunchWith(Import(TwoSwitchJson("onboarding")));
            yield return null;
            Assert.That(_root.View.TeachAffordancePresent(0), Is.True);
            Assert.That(_root.View.TeachAffordancePresent(1), Is.True);
            Assert.That(RingCount(_root.View.gameObject), Is.EqualTo(2),
                "one static ring per switch");
        }

        [UnityTest]
        public IEnumerator AlternationBand_RendersNothing_PositiveControlInFile()
        {
            _root = GameRoot.LaunchWith(Import(TwoSwitchJson("alternation")));
            yield return null;
            Assert.That(_root.View.TeachAffordancePresent(0), Is.False);
            Assert.That(RingCount(_root.View.gameObject), Is.EqualTo(0));
            // R1-F3 of #36: "component-identical" asserted as counts, not just ring names —
            // a non-onboarding board carries exactly as many transforms and renderers as the
            // onboarding board MINUS its two rings.
            int altTransforms = _root.View.gameObject
                .GetComponentsInChildren<Transform>(true).Length;
            int altRenderers = _root.View.gameObject
                .GetComponentsInChildren<Renderer>(true).Length;
            // positive control: the SAME probes on the onboarding fixture
            Object.Destroy(_root.gameObject);
            _root = GameRoot.LaunchWith(Import(TwoSwitchJson("onboarding")));
            yield return null;
            Assert.That(_root.View.TeachAffordancePresent(0), Is.True);
            Assert.That(_root.View.gameObject.GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(altTransforms + 2), "the ONLY structural delta is the two rings");
            Assert.That(_root.View.gameObject.GetComponentsInChildren<Renderer>(true).Length,
                Is.EqualTo(altRenderers + 2));
        }

        // --- criterion 2: per-switch clear on first toggle; toggle-back stays cleared ---

        [UnityTest]
        public IEnumerator TogglingSwitchZero_ClearsOnlyItsAffordance_AndStaysCleared()
        {
            _root = GameRoot.LaunchWith(Import(TwoSwitchJson("onboarding")));
            yield return null;
            Assert.That(_root.View.TeachAffordancePresent(0), Is.True,
                "pre-toggle positive control");

            Vector2 s0 = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Input.HandleTapAtScreen(s0), Is.EqualTo(0));
            yield return null;
            Assert.That(_root.View.TeachAffordancePresent(0), Is.False,
                "the taught switch clears within one pumped frame");
            Assert.That(_root.View.TeachAffordancePresent(1), Is.True,
                "the untaught switch keeps its affordance");

            Assert.That(_root.Input.HandleTapAtScreen(s0), Is.EqualTo(0)); // toggle back
            yield return null;
            Assert.That(_root.View.TeachAffordancePresent(0), Is.False,
                "the clear keys on the command log, not the current route");
        }

        // --- criterion 3: Retry restores the teach state ---

        [UnityTest]
        public IEnumerator Retry_RestoresTheTeachState()
        {
            _root = GameRoot.LaunchWith(Import(TwoSwitchJson("onboarding")));
            yield return null;
            Vector2 s0 = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            _root.Input.HandleTapAtScreen(s0);
            yield return null;
            Assert.That(_root.View.TeachAffordancePresent(0), Is.False, "cleared pre-Retry");

            _root.Retry();
            yield return null;
            Assert.That(_root.View.TeachAffordancePresent(0), Is.True,
                "tick-0 re-simulation restores the Read phase, teach included");
            Assert.That(_root.View.TeachAffordancePresent(1), Is.True);
        }

        // --- criterion 4: motion-off removes the pulse only; the ring is always static ---

        [UnityTest]
        public IEnumerator MotionOff_PulseStops_RingAlwaysStatic()
        {
            _root = GameRoot.LaunchWith(Import(TwoSwitchJson("onboarding")));
            yield return null;

            var disc = FindByName(_root.View.gameObject, "switch:S1");
            var ring = FindByName(_root.View.gameObject, "teachring:S1");
            Assert.That(disc, Is.Not.Null);
            Assert.That(ring, Is.Not.Null);

            // motion ON (source unset): the pulse demonstrably oscillates — the positive
            // control proving there is motion to remove
            var scales = new List<Vector3>();
            var ringScales = new List<Vector3>();
            for (int i = 0; i < 30; i++)
            {
                scales.Add(disc.localScale);
                ringScales.Add(ring.localScale);
                yield return null;
            }
            Assert.That(Distinct(scales), Is.GreaterThan(1), "the pulse animates when motion is on");
            Assert.That(Distinct(ringScales), Is.EqualTo(1), "the ring never animates");

            // motion OFF: the disc scale is constant while the ring remains present
            _root.View.MotionOffSource = () => true;
            yield return null; // let one frame settle to the base scale
            scales.Clear();
            var ringPos = new List<Vector3>();
            var ringScalesOff = new List<Vector3>();
            for (int i = 0; i < 10; i++)
            {
                scales.Add(disc.localScale);
                ringPos.Add(ring.localPosition);
                ringScalesOff.Add(ring.localScale);
                yield return null;
            }
            Assert.That(Distinct(scales), Is.EqualTo(1), "motion-off removes the oscillation");
            // R1-F6 of #36: the ring's FULL transform is static in BOTH states — position
            // sampled here (off) and scale in both windows; a bobbing ring would carry motion.
            Assert.That(Distinct(ringPos), Is.EqualTo(1), "ring position never animates");
            Assert.That(Distinct(ringScalesOff), Is.EqualTo(1));
            Assert.That(ring.gameObject.activeSelf, Is.True,
                "the ring carries the information in both motion states");
            Assert.That(ring.GetComponent<Animator>(), Is.Null);
            Assert.That(ring.GetComponent<Animation>(), Is.Null, "legacy animation too");
            // Human ruling 2026-08-06: the ring reads as a ring — its material is the tinted
            // instance, never the disc's white (same shader; distinct color).
            var ringMat = ring.GetComponent<Renderer>().sharedMaterial;
            var discMat = disc.GetComponent<Renderer>().sharedMaterial;
            Assert.That(ringMat, Is.Not.EqualTo(discMat), "ring is visually differentiated");
            Assert.That(ringMat.shader, Is.EqualTo(discMat.shader),
                "same pipeline shader — no stripping surface added");
        }

        // --- criterion 5: the zero-instructional-text law, inventoried ---

        // The complete legal rendered-text set during Playing on a tutorial board. The
        // CM-R13.5 hint chip (ui.csv key hint.tutorial) is PRE-REGISTERED here so CM-UX-05
        // cannot turn this law red (the #27 R1-F8 obligation): when it arrives, its csv value
        // joins this set by amending THIS list, never by weakening the assert.
        private static readonly Regex[] LegalTexts =
        {
            new Regex("^[A-Z?]$"),   // station symbol glyphs
            new Regex(@"^x\d+$"),    // wave-preview count badges
        };

        private static bool IsLegal(string text)
        {
            foreach (var rx in LegalTexts)
                if (rx.IsMatch(text)) return true;
            return false;
        }

        // Scene-global by design (the law covers board + HUD together); R1-F7 of #36: the
        // former host parameter was unused and is dropped — if another suite ever leaks an
        // active prose TextMesh into the scene, this test fails loudly and the leak is the
        // defect to fix, not this scan.
        private static List<string> IllegalTexts()
        {
            var offenders = new List<string>();
            foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                if (tm.text.Length > 0 && !IsLegal(tm.text)) offenders.Add(tm.text);
            foreach (var tmp in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
                if (tmp.text.Length > 0 && tmp.gameObject.activeInHierarchy && !IsLegal(tmp.text))
                    offenders.Add(tmp.text);
            return offenders;
        }

        [UnityTest]
        public IEnumerator TutorialBoard_RendersZeroInstructionalText()
        {
            _root = GameRoot.LaunchWith(Import(TwoSwitchJson("onboarding")));
            yield return null;
            Assert.That(IllegalTexts(), Is.Empty,
                "CM-R13.1: no tutorial text, no icons-as-text, no modals during Playing");

            // anti-vacuity decoy: a stray instruction is detected by the same inventory
            var decoy = new GameObject("decoy-text");
            try
            {
                var tm = decoy.AddComponent<TextMesh>();
                tm.text = "tap the switch now";
                Assert.That(IllegalTexts(), Is.Not.Empty,
                    "the inventory can detect an instructional string");
            }
            finally { Object.Destroy(decoy); }
        }

        // --- criterion 6: no per-frame object churn while Playing ---

        [UnityTest]
        public IEnumerator NoNewObjectsAcrossFrames_WhilePlaying()
        {
            _root = GameRoot.LaunchWith(Import(TwoSwitchJson("onboarding")));
            yield return null;
            yield return null;
            int before = _root.View.gameObject.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < 20; i++) yield return null;
            int after = _root.View.gameObject.GetComponentsInChildren<Transform>(true).Length;
            Assert.That(after, Is.EqualTo(before),
                "the pulse mutates cached transforms — it never allocates objects per frame");
        }

        // The #33 evidence rig (disarmed path PASSES explicitly — the CM-UX-02 wrapper lesson).
        [UnityTest]
        public IEnumerator CaptureEvidence_TeachAffordanceFrame_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_UX03_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_UX03_CAPTURE_DIR to emit the frame");
                yield break;
            }
            _root = GameRoot.LaunchWith(Import(TwoSwitchJson("onboarding")));
            yield return null;
            yield return null;
            var rt = new RenderTexture(Screen.width, Screen.height, 24); // Screen-matched (M1)
            _root.Cam.targetTexture = rt;
            _root.Cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            _root.Cam.targetTexture = null;
            RenderTexture.active = null;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(dir, "cm-ux-03-teach.png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
        }

        private static Transform FindByName(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static int Distinct(List<Vector3> values)
        {
            var seen = new HashSet<Vector3>();
            foreach (var v in values) seen.Add(v);
            return seen.Count;
        }

        // Two-switch chain: SRC -> J1 {J2 | YEL}; J2 {RED | BLU}. Quiet waves; Playing
        // persists for the whole test (the merged fixture grammar).
        private static string TwoSwitchJson(string band)
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T907"", ""name"": ""Teach Fixture"", ""seed"": 907,
  ""meta"": { ""band"": """ + band + @""", ""difficultyTarget"": 0.1, ""mechanics"": [""switch""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9 },
      { ""id"": ""J1"", ""x"": 3, ""y"": 6 },
      { ""id"": ""J2"", ""x"": 2, ""y"": 4 },
      { ""id"": ""YEL"", ""x"": 5, ""y"": 4 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 }, { ""id"": ""BLU"", ""x"": 3, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 10 },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""J2"", ""travelTicks"": 10 },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""YEL"", ""travelTicks"": 10 },
      { ""id"": ""E4"", ""from"": ""J2"", ""to"": ""RED"", ""travelTicks"": 10 },
      { ""id"": ""E5"", ""from"": ""J2"", ""to"": ""BLU"", ""travelTicks"": 10 } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] } ],
  ""stations"": [
    { ""nodeId"": ""YEL"", ""accepts"": [""yellow""], ""capacity"": 6 },
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 6 },
    { ""nodeId"": ""BLU"", ""accepts"": [""blue""], ""capacity"": 6 } ],
  ""switches"": [
    { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 },
    { ""id"": ""S2"", ""nodeId"": ""J2"", ""routes"": [""E4"", ""E5""], ""initialRoute"": 0 } ],
  ""waves"": [ { ""tick"": 3999, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 99, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }
    }
}
