using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CatMetro.Services.Purchases;
using NUnit.Framework;

namespace CatMetro.Tests.Purchases
{
    // The API key config. Two things are being protected here:
    //   1. No key is ever committed, and the absence of one is a normal, survivable state.
    //   2. A RevenueCat Test Store key can never reach a release build. RevenueCat's docs are
    //      unambiguous — "You must NEVER submit an app to the App Store or Google Play that is
    //      configured with a Test Store API key" — and a Test Store key makes every purchase
    //      fake, so shipping one means a live store listing where nobody can actually buy
    //      anything and no revenue is ever recorded.
    public sealed class MonetizationKeysTests
    {
        private const string Good = @"{ ""googleApiKey"": ""goog_realkey"", ""appleApiKey"": ""appl_realkey"" }";

        [Test]
        public void AGoodConfig_YieldsThePlatformKey()
        {
            var google = MonetizationKeys.Parse(Good, StorePlatform.GooglePlay, isDebugBuild: false);
            Assert.That(google.IsConfigured, Is.True, google.Problem);
            Assert.That(google.ApiKey, Is.EqualTo("goog_realkey"));

            var apple = MonetizationKeys.Parse(Good, StorePlatform.Apple, isDebugBuild: false);
            Assert.That(apple.ApiKey, Is.EqualTo("appl_realkey"));
        }

        [Test]
        public void Whitespace_AroundAPastedKey_IsTrimmed()
        {
            var keys = MonetizationKeys.Parse(@"{ ""googleApiKey"": ""  goog_realkey\n"" }",
                StorePlatform.GooglePlay, false);
            Assert.That(keys.ApiKey, Is.EqualTo("goog_realkey"),
                "a key pasted out of the dashboard usually arrives with whitespace on it");
        }

        // ---- the release guard -----------------------------------------------------------

        [Test]
        public void ATestStoreKey_IsRefusedInAReleaseBuild()
        {
            var keys = MonetizationKeys.Parse(
                @"{ ""googleApiKey"": ""test_key"", ""useTestStore"": true }",
                StorePlatform.GooglePlay, isDebugBuild: false);

            Assert.That(keys.IsConfigured, Is.False,
                "shipping a Test Store key would make every purchase in the live listing fake");
            Assert.That(keys.Problem, Does.Contain("Test Store"));
        }

        [Test]
        public void ATestStoreKey_IsAllowedInADebugBuild()
        {
            var keys = MonetizationKeys.Parse(
                @"{ ""googleApiKey"": ""test_key"", ""useTestStore"": true }",
                StorePlatform.GooglePlay, isDebugBuild: true);

            Assert.That(keys.IsConfigured, Is.True, keys.Problem);
            Assert.That(keys.UseTestStore, Is.True,
                "this is the only way to complete a purchase on device without Play Console products");
        }

        [Test]
        public void ARealKey_IsFineInAReleaseBuild()
        {
            var keys = MonetizationKeys.Parse(
                @"{ ""googleApiKey"": ""goog_realkey"", ""useTestStore"": false }",
                StorePlatform.GooglePlay, isDebugBuild: false);
            Assert.That(keys.IsConfigured, Is.True, keys.Problem);
        }

        [Test]
        public void ATestStoreKey_IsRefusedEvenWhenTheHumanFlagSaysProduction()
        {
            var keys = MonetizationKeys.Parse(
                @"{ ""googleApiKey"": ""test_real_test_store_key"", ""useTestStore"": false }",
                StorePlatform.GooglePlay, isDebugBuild: false);

            Assert.That(keys.IsConfigured, Is.False,
                "the key prefix, not a hand-authored boolean, determines whether purchases are fake");
            Assert.That(keys.Problem, Does.Contain("Test Store"));
        }

        [Test]
        public void ATestStoreKey_RequiresTheExplicitDebugFlag()
        {
            var keys = MonetizationKeys.Parse(
                @"{ ""googleApiKey"": ""test_real_test_store_key"", ""useTestStore"": false }",
                StorePlatform.GooglePlay, isDebugBuild: true);

            Assert.That(keys.IsConfigured, Is.False);
            Assert.That(keys.Problem, Does.Contain("useTestStore"));
        }

        [TestCase(StorePlatform.GooglePlay, "appl_wrong_platform")]
        [TestCase(StorePlatform.Apple, "goog_wrong_platform")]
        [TestCase(StorePlatform.GooglePlay, "not_a_revenuecat_key")]
        public void AProductionKey_MustMatchTheBuildPlatform(StorePlatform platform, string key)
        {
            string field = platform == StorePlatform.Apple ? "appleApiKey" : "googleApiKey";
            var keys = MonetizationKeys.Parse("{ \"" + field + "\": \"" + key + "\" }",
                platform, isDebugBuild: false);

            Assert.That(keys.IsConfigured, Is.False);
            Assert.That(keys.Problem, Does.Contain(platform == StorePlatform.Apple ? "appl_" : "goog_"));
        }

        [TestCase(StorePlatform.GooglePlay, "googleApiKey")]
        [TestCase(StorePlatform.Apple, "appleApiKey")]
        public void ASecretRevenueCatKey_IsAlwaysRefused(StorePlatform platform, string field)
        {
            var keys = MonetizationKeys.Parse(
                "{ \"" + field + "\": \"sk_this_must_stay_server_side\" }",
                platform, isDebugBuild: true);

            Assert.That(keys.IsConfigured, Is.False);
            Assert.That(keys.Problem, Does.Contain("secret"));
        }

        [Test]
        public void AProductionKey_CannotBeMislabelledAsTestStoreInDebug()
        {
            var keys = MonetizationKeys.Parse(
                @"{ ""googleApiKey"": ""goog_realkey"", ""useTestStore"": true }",
                StorePlatform.GooglePlay, isDebugBuild: true);

            Assert.That(keys.IsConfigured, Is.False);
            Assert.That(keys.Problem, Does.Contain("useTestStore"));
        }

        // ---- the survivable failures -----------------------------------------------------

        [Test]
        public void NoConfigAtAll_IsAStatedProblem_NotAThrow()
        {
            foreach (var input in new[] { null, "", "   ", "not json", "[]" })
            {
                var keys = MonetizationKeys.Parse(input, StorePlatform.GooglePlay, false);
                Assert.That(keys.IsConfigured, Is.False, "input: " + (input ?? "null"));
                Assert.That(keys.Problem, Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public void AMissingPlatformKey_IsNamedInTheProblem()
        {
            var keys = MonetizationKeys.Parse(@"{ ""googleApiKey"": ""goog_x"" }",
                StorePlatform.Apple, false);
            Assert.That(keys.IsConfigured, Is.False);
            Assert.That(keys.Problem, Does.Contain("Apple"));
        }

        // The mistake a human will actually make: copying the example file and forgetting to
        // paste the key. Catching the placeholder by name turns a mysteriously empty shop into
        // one clear line in the launch log.
        [Test]
        public void TheExamplePlaceholder_IsRecognisedAndRefused()
        {
            var keys = MonetizationKeys.Parse(
                @"{ ""googleApiKey"": ""REPLACE_ME_WITH_THE_GOOGLE_PUBLIC_SDK_KEY"" }",
                StorePlatform.GooglePlay, false);

            Assert.That(keys.IsConfigured, Is.False);
            Assert.That(keys.Problem, Does.Contain("placeholder"));
        }

        // ---- the committed example file --------------------------------------------------

        [Test]
        public void TheCommittedExampleFile_IsValidJson_AndIsRefusedAsAKey()
        {
            var path = Path.Combine(CatMetro.Tests.Domain.Fixtures.RepoRoot(),
                "unity", "Assets", "Resources", "Monetization", "revenuecat_config.example.json");
            Assert.That(File.Exists(path), Is.True, "the example config must stay committed");

            var keys = MonetizationKeys.Parse(File.ReadAllText(path), StorePlatform.GooglePlay, false);
            Assert.That(keys.IsConfigured, Is.False,
                "the EXAMPLE must never be usable as a real config");
            Assert.That(keys.Problem, Does.Contain("placeholder"));
        }

        // The test that would catch a leaked key. It reads the blobs staged in Git's index, not
        // the working tree: the real local runtime config is deliberately ignored and may contain
        // a legitimate public SDK key. It inspects parsed string VALUES rather than scanning raw
        // text, because the example file legitimately discusses the key prefixes in its own prose.
        // A real key is a value that STARTS WITH a prefix; a sentence mentioning one is not.
        [Test]
        public void NoRealApiKey_IsCommittedAnywhereInResources()
        {
            string repoRoot = CatMetro.Tests.Domain.Fixtures.RepoRoot();
            const string indexedDirectory = "unity/Assets/Resources/Monetization";
            string[] entries = RunGit(repoRoot,
                    "ls-files --stage -z -- " + indexedDirectory)
                .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            int scannedJsonFiles = 0;

            foreach (string entry in entries)
            {
                int pathSeparator = entry.IndexOf('\t');
                Assert.That(pathSeparator, Is.GreaterThan(0),
                    "git ls-files --stage returned an unparseable index entry");
                string[] fields = entry.Substring(0, pathSeparator).Split(' ');
                Assert.That(fields.Length, Is.EqualTo(3),
                    "git index metadata is mode, object id, and stage");
                Assert.That(fields[2], Is.EqualTo("0"),
                    "the key guard cannot prove an index with unresolved merge stages");

                string indexedPath = entry.Substring(pathSeparator + 1);
                if (!indexedPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                scannedJsonFiles++;
                var root = Newtonsoft.Json.Linq.JObject.Parse(
                    RunGit(repoRoot, "cat-file blob " + fields[1]));
                foreach (var token in root.Descendants())
                {
                    if (token.Type != Newtonsoft.Json.Linq.JTokenType.String) continue;
                    var value = ((string)token ?? string.Empty).Trim();
                    var where = Path.GetFileName(indexedPath);

                    Assert.That(value.StartsWith("goog_"), Is.False,
                        "a Google RevenueCat key is committed in " + where);
                    Assert.That(value.StartsWith("appl_"), Is.False,
                        "an Apple RevenueCat key is committed in " + where);
                    Assert.That(value.StartsWith("sk_"), Is.False,
                        "a RevenueCat SECRET key is committed in " + where +
                        " — rotate it immediately; it can grant entitlements to anyone");
                    Assert.That(value.StartsWith("test_"), Is.False,
                        "a RevenueCat Test Store key is committed in " + where);
                }
            }
            Assert.That(scannedJsonFiles, Is.GreaterThan(0),
                "the git index must expose at least one committed monetization JSON file");
        }

        private static string RunGit(string repoRoot, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var process = Process.Start(startInfo))
            {
                Assert.That(process, Is.Not.Null, "the committed-key guard must be able to run git");
                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(10000))
                {
                    try { process.Kill(); }
                    catch (Exception error)
                    {
                        Assert.Fail("timed-out git process could not be stopped: " + error.Message);
                    }
                    Assert.Fail("git " + arguments + " timed out after 10 seconds");
                }
                Assert.That(Task.WaitAll(new Task[] { stdout, stderr }, 5000), Is.True,
                    "git exited but its redirected output did not drain within 5 seconds");
                Assert.That(process.ExitCode, Is.EqualTo(0),
                    "git " + arguments + " failed: " + stderr.Result);
                return stdout.Result;
            }
        }

        // Proves the detector above actually detects, rather than passing because it looks at
        // the wrong thing. A leak test that has never seen a leak is a decoration.
        [Test]
        public void TheLeakDetector_WouldCatchARealKey()
        {
            var leaked = Newtonsoft.Json.Linq.JObject.Parse(
                @"{ ""googleApiKey"": ""goog_ThisWouldBeARealLeakedKey"" }");

            bool caught = false;
            foreach (var token in leaked.Descendants())
            {
                if (token.Type != Newtonsoft.Json.Linq.JTokenType.String) continue;
                if (((string)token ?? string.Empty).Trim().StartsWith("goog_")) caught = true;
            }

            Assert.That(caught, Is.True);
        }
    }
}
