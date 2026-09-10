using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Fx;
using CatMetro.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Stopwatch = System.Diagnostics.Stopwatch;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class ProfileRigCostProbeTests
    {
        // Deliberately opt-in: this is CPU attribution, not a frame-rate acceptance test.
        [UnityTest]
        public IEnumerator CaptureProfileAdvanceCpuAndManagedAllocations_WhenRequested()
        {
            string directory = Environment.GetEnvironmentVariable("CM_PROFILE_RIG_COST_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                Assert.Pass("profile cost probe disarmed; set CM_PROFILE_RIG_COST_CAPTURE_DIR");
                yield break;
            }
            Directory.CreateDirectory(directory);
            var storage = new ProbeStorage();
            Func<IStorageRoot> previousStorage = GameRoot.DailyStorageRootOverride;
            bool previousSkip = GameRoot.DevSkipShippedHome;
            GameRoot root = null;
            RenderTexture target = null;
            var csv = new StringBuilder("screen,condition,repeat,sample,cpu_us,managed_bytes\n");
            var summary = new StringBuilder("CPU microbenchmark of synchronous production AdvanceTurntable calls. "
                + "No Unity Update interleaves a synchronous block. Values exclude deferred rendering/layout, "
                + "native allocations and GC pauses outside the call; these are not rendered frame timings.\n");
            try
            {
                GameRoot.DailyStorageRootOverride = () => storage;
                GameRoot.DevSkipShippedHome = false;
                root = GameRoot.Launch();
                target = new RenderTexture(917, 2048, 24);
                target.Create();
                root.Cam.targetTexture = target;
                root.Cam.aspect = 917f / 2048f;
                root.MotionOffToggle = false;
                root.AnimatorDurationScale = 1f;
                yield return new WaitForSecondsRealtime(0.4f);
                for (int screen = 0; screen < 2; screen++)
                {
                    string name = screen == 0 ? "home" : "wardrobe";
                    if (screen == 1)
                    {
                        Assert.That(root.Input.HandleTapAtScreen(root.Wardrobe.EntryRectPx.center), Is.EqualTo(-3));
                        root.GetComponent<BoardFx>().Advance(0.14f);
                        yield return null;
                        Assert.That(root.Stack.Current, Is.EqualTo("wardrobe"));
                        root.Wardrobe.LayoutForViewport(new Rect(0, 64, 917, 1920), 408f);
                    }
                    else root.Home.LayoutForViewport(new Rect(0, 64, 917, 1920), 408f, new Rect(0, 0, 917, 2048));
                    Canvas.ForceUpdateCanvases();
                    ProfileRigMount mount = screen == 0 ? root.Home.ProfileRig : root.Wardrobe.ProfileRig;
                    Assert.That(mount, Is.Not.Null, "armed probe requires the actual licensed profile");
                    Assert.That(mount.Layout(root.Cam), Is.True);
                    Assert.That(mount.AnimatorCount, Is.Zero);
                    CatRigPresentation motion = mount.PrefabRoot.GetComponentInChildren<CatRigPresentation>(true);
                    Assert.That(motion, Is.Not.Null);
                    Assert.That(motion.AuthoredMotionInstalled, Is.True);
                    float amplitude = mount.TurntableAmplitude;
                    summary.AppendLine(name + " mesh_vertices=" + mount.PrefabRoot
                        .GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(s => s.sharedMesh.vertexCount)
                        + " turntable_amplitude=" + amplitude.ToString("R", CultureInfo.InvariantCulture));
                    for (int warmup = 0; warmup < 60; warmup++) yield return null;
                    for (int repeat = 0; repeat < 3; repeat++)
                    {
                        string[] order = repeat % 2 == 0
                            ? new[] { "idle", "turntable", "motion_off", "hidden" }
                            : new[] { "hidden", "motion_off", "turntable", "idle" };
                        foreach (string condition in order)
                        {
                            mount.SetVisible(true);
                            root.MotionOffToggle = condition == "motion_off";
                            mount.TurntableAmplitude = condition == "turntable" ? amplitude : 0f;
                            if (condition == "hidden") mount.SetVisible(false);
                            for (int i = 0; i < 120; i++) mount.AdvanceTurntable(1f / 60f);
                            Transform[] bones = motion.GetComponentsInChildren<Transform>(true);
                            Quaternion[] before = bones.Select(b => b.localRotation).ToArray();
                            Measure(name, condition, repeat, mount, csv, summary);
                            float change = bones.Select((bone, i) => Quaternion.Angle(before[i], bone.localRotation)).Max();
                            if (condition == "hidden" || condition == "motion_off")
                                Assert.That(bones.Select(b => b.localRotation).ToArray(), Is.EqualTo(before),
                                    "the settled hidden/off path must retain the exact sampled pose");
                            else Assert.That(change, Is.GreaterThan(0.001f),
                                "a visible timed path must actually advance the authored pose");
                            Assert.That(mount.AnimatorCount, Is.Zero);
                        }
                    }
                    root.MotionOffToggle = false;
                    mount.TurntableAmplitude = amplitude;
                    mount.SetVisible(true);
                }
                TestContext.Out.WriteLine(summary.ToString());
            }
            finally
            {
                if (root != null)
                {
                    root.Cam.targetTexture = null;
                    Object.DestroyImmediate(root.gameObject);
                }
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                GameRoot.DailyStorageRootOverride = previousStorage;
                GameRoot.DevSkipShippedHome = previousSkip;
                storage.Dispose();
                File.WriteAllText(Path.Combine(directory, "profile-advance-cpu.csv"), csv.ToString());
                File.WriteAllText(Path.Combine(directory, "profile-advance-summary.txt"), summary.ToString());
            }
        }

        private static void Measure(string screen, string condition, int repeat, ProfileRigMount mount,
            StringBuilder csv, StringBuilder summary)
        {
            const int count = 600;
            var elapsed = new long[count];
            var allocated = new long[count];
            int collectionsBefore = GC.CollectionCount(0);
            for (int i = 0; i < count; i++)
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                mount.AdvanceTurntable(1f / 60f);
                elapsed[i] = Stopwatch.GetTimestamp() - start;
                allocated[i] = GC.GetAllocatedBytesForCurrentThread() - bytes;
            }
            int collections = GC.CollectionCount(0) - collectionsBefore;
            double microsPerTick = 1000000d / Stopwatch.Frequency;
            for (int i = 0; i < count; i++)
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:F3},{5}\n",
                    screen, condition, repeat, i, elapsed[i] * microsPerTick, allocated[i]);
            Array.Sort(elapsed);
            summary.AppendFormat(CultureInfo.InvariantCulture,
                "{0}/{1}/{2} p50_us={3:F3} p95_us={4:F3} max_us={5:F3} mean_managed_bytes={6:F3} gc0_collections={7}\n",
                screen, condition, repeat, elapsed[count / 2] * microsPerTick,
                elapsed[(int)(count * 0.95)] * microsPerTick, elapsed[count - 1] * microsPerTick,
                allocated.Average(), collections);
        }

        private sealed class ProbeStorage : IStorageRoot, IDisposable
        {
            public string SaveDirectory { get; } = Path.Combine(Path.GetTempPath(), "cm-profile-cost-" + Guid.NewGuid().ToString("N"));
            public string CacheDirectory => SaveDirectory;
            public ProbeStorage() => Directory.CreateDirectory(SaveDirectory);
            public void Dispose() { if (Directory.Exists(SaveDirectory)) Directory.Delete(SaveDirectory, true); }
        }
    }
}
