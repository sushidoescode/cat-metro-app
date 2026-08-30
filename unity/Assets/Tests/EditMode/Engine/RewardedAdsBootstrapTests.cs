using System;
using System.Collections.Generic;
using System.IO;
using CatMetro.Application.Save;
using CatMetro.Integrations;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using CatMetro.Tests.Purchases;
using CatMetro.Tests.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests
{
    public sealed class RewardedAdsBootstrapTests
    {
        private const string ConfiguredJson = @"{
          ""iosAppKey"": ""ios-key-do-not-log"",
          ""androidAppKey"": ""android-key-do-not-log"",
          ""iosRewardedAdUnitId"": ""ios-unit-do-not-log"",
          ""androidRewardedAdUnitId"": ""android-unit-do-not-log""
        }";

        private const string PlacementsJson = @"{
          ""placements"": [{
            ""id"": ""wardrobe_conductor_trial"",
            ""entitlement"": ""outfit_conductor"",
            ""enabled"": true,
            ""caps"": { ""session"": 2, ""localDate"": 3 }
          }]
        }";

        private const string ThreeAdEntitlementCatalogJson = @"{
          ""schemaVersion"": 2,
          ""entitlements"": [
            { ""id"": ""outfit_conductor"", ""kind"": ""outfit"",
              ""display"": ""Conductor"", ""adLeaseSeconds"": 3600 },
            { ""id"": ""outfit_bellhop"", ""kind"": ""outfit"",
              ""display"": ""Bellhop"", ""adLeaseSeconds"": 3600 },
            { ""id"": ""outfit_stationmaster"", ""kind"": ""outfit"",
              ""display"": ""Stationmaster"", ""adLeaseSeconds"": 3600 }
          ],
          ""products"": []
        }";

        [SetUp]
        public void SetUp()
        {
            PurchaseBackendFactory.ResetForTests();
            RewardedAdProviderFactory.ResetForTests();
            MonetizationBootstrap.ResetForTests();
            SaveRuntime.ResetForTests();
            PurchaseRuntime.ResetForTests();
            RewardedAdRuntime.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            MonetizationBootstrap.ResetForTests();
            PurchaseBackendFactory.ResetForTests();
            RewardedAdProviderFactory.ResetForTests();
            SaveRuntime.ResetForTests();
            PurchaseRuntime.ResetForTests();
            RewardedAdRuntime.ResetForTests();
        }

        [Test]
        public void Config_MissingInvalidOrBlankSelectedPair_IsUnconfiguredWithoutThrowing()
        {
            RewardedAdsConfig missing = null;
            RewardedAdsConfig invalid = null;
            RewardedAdsConfig blankAndroid = null;
            RewardedAdsConfig blankAndroidUnit = null;

            Assert.DoesNotThrow(() => missing = RewardedAdsConfig.Parse(null,
                RuntimePlatform.Android));
            Assert.DoesNotThrow(() => invalid = RewardedAdsConfig.Parse("not json",
                RuntimePlatform.Android));
            Assert.DoesNotThrow(() => blankAndroid = RewardedAdsConfig.Parse(
                @"{""iosAppKey"":""ios"",""iosRewardedAdUnitId"":""ios-unit"","
                + @"""androidAppKey"":"" "",""androidRewardedAdUnitId"":""""}",
                RuntimePlatform.Android));
            Assert.DoesNotThrow(() => blankAndroidUnit = RewardedAdsConfig.Parse(
                @"{""androidAppKey"":""android"",""androidRewardedAdUnitId"":"" ""}",
                RuntimePlatform.Android));

            Assert.That(missing.IsConfigured, Is.False);
            Assert.That(missing.Problem, Does.Contain("null or empty"));
            Assert.That(invalid.IsConfigured, Is.False);
            Assert.That(invalid.Problem, Does.Contain("valid JSON"));
            Assert.That(blankAndroid.IsConfigured, Is.False);
            Assert.That(blankAndroid.Problem, Does.Contain("Android app key"));
            Assert.That(blankAndroidUnit.IsConfigured, Is.False);
            Assert.That(blankAndroidUnit.Problem, Does.Contain("rewarded ad-unit ID"));
        }

        [Test]
        public void Config_SelectsOnlyTheRequestedPlatformPair()
        {
            var android = RewardedAdsConfig.Parse(ConfiguredJson, RuntimePlatform.Android);
            var ios = RewardedAdsConfig.Parse(ConfiguredJson, RuntimePlatform.IPhonePlayer);
            var unsupported = RewardedAdsConfig.Parse(ConfiguredJson, RuntimePlatform.OSXEditor);

            Assert.That(android.IsConfigured, Is.True);
            Assert.That(android.AppKey, Is.EqualTo("android-key-do-not-log"));
            Assert.That(android.RewardedAdUnitId, Is.EqualTo("android-unit-do-not-log"));
            Assert.That(ios.IsConfigured, Is.True);
            Assert.That(ios.AppKey, Is.EqualTo("ios-key-do-not-log"));
            Assert.That(ios.RewardedAdUnitId, Is.EqualTo("ios-unit-do-not-log"));
            Assert.That(unsupported.IsConfigured, Is.False);
            Assert.That(unsupported.Problem, Does.Contain("unsupported platform"));
        }

        [Test]
        public void Config_MissingResourceAndCommittedExample_AreSafeAndUnconfigured()
        {
            RewardedAdsConfig loaded = null;
            Assert.DoesNotThrow(() => loaded = RewardedAdsConfig.Load());
            Assert.That(loaded.IsConfigured, Is.False);
            Assert.That(loaded.Problem, Does.Contain(RewardedAdsConfig.ResourcePath));

            string repoRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "..", ".."));
            string examplePath = Path.Combine(repoRoot, "config", "rewarded-ads.example.json");
            Assert.That(File.Exists(examplePath), Is.True,
                "the non-secret example is the only committed config contract");
            var example = RewardedAdsConfig.Parse(File.ReadAllText(examplePath),
                RuntimePlatform.Android);
            Assert.That(example.IsConfigured, Is.False);
            Assert.That(example.Problem, Does.Contain("Android app key"));
        }

        [Test]
        public void Factories_CatchConstructionFaults_AndResetAllStaticRegistrations()
        {
            var config = RewardedAdsConfig.Parse(ConfiguredJson, RuntimePlatform.Android);
            RewardedAdProviderFactory.Register(_ =>
                throw new InvalidOperationException("injected provider construction fault"));
            IRewardedAdProvider provider = null;
            Assert.DoesNotThrow(() => provider = RewardedAdProviderFactory.Create(config));
            Assert.That(provider, Is.Null);

            var backend = new BackendReporter();
            PurchaseBackendFactory.Register(_ => backend);
            RewardedAdProviderFactory.Register(_ => new Provider());
            Assert.That(PurchaseBackendFactory.HasFactory, Is.True);
            Assert.That(PurchaseBackendFactory.Create(new PurchaseService(PFixtures.TinyCatalog())),
                Is.SameAs(backend));
            Assert.That(RewardedAdProviderFactory.Create(config), Is.Not.Null);

            PurchaseBackendFactory.ResetForTests();
            RewardedAdProviderFactory.ResetForTests();

            Assert.That(PurchaseBackendFactory.HasFactory, Is.False);
            Assert.That(PurchaseBackendFactory.Create(new PurchaseService(PFixtures.TinyCatalog())),
                Is.Null);
            Assert.That(RewardedAdProviderFactory.Create(config), Is.Null);
        }

        [Test]
        public void Bind_SubscribesBeforeConsumingAnAlreadyInstalledStore_AndRestoresItsLease()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.State.Payload["entitlements"]["localLeases"] = new JArray
            {
                new JObject
                {
                    ["entitlementId"] = "outfit_conductor",
                    ["expiresAtUnixSeconds"] = 5_000L,
                },
            };
            SaveRuntime.Install(store);
            var provider = new Provider();
            var backend = new BackendReporter();
            var service = Service(backend);
            PurchaseRuntime.Install(service);
            using var composition = Composition(service, backend, () => provider);

            composition.Bind();

            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True,
                "Bind must immediately import a store installed before the observer");
            Assert.That(provider.InitializeCalls, Is.EqualTo(1));
            Assert.That(provider.LoadCalls, Is.EqualTo(1));
            Assert.That(backend.ReporterEventAddCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.True);
        }

        [Test]
        public void FactoryBackend_IsTheAttachedReporter_AndAloneGatesProviderStartup()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            SaveRuntime.Install(store);
            var backend = new BackendReporter(isReady: false);
            int backendCreates = 0;
            PurchaseBackendFactory.Register(_ =>
            {
                backendCreates++;
                return backend;
            });
            var service = new PurchaseService(PFixtures.TinyCatalog(), clock: () => 1_000L);
            var exactBackend = PurchaseBackendFactory.Create(service);
            service.AttachBackend(exactBackend);
            var provider = new Provider();
            int providerCreates = 0;
            using var composition = Composition(service,
                exactBackend as IAdEventReporter, () =>
                {
                    providerCreates++;
                    return provider;
                });

            composition.Bind();
            service.RefreshEntitlements();

            Assert.That(exactBackend, Is.SameAs(backend));
            Assert.That(exactBackend as IAdEventReporter, Is.SameAs(backend));
            Assert.That(backendCreates, Is.EqualTo(1));
            Assert.That(providerCreates, Is.EqualTo(1));
            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(1),
                "the factory object must be the PurchaseService backend");
            Assert.That(backend.ReporterEventAddCalls, Is.EqualTo(1),
                "the same factory object must be the coordinator reporter");
            Assert.That(provider.InitializeCalls, Is.Zero);
            Assert.That(provider.LoadCalls, Is.Zero);

            backend.SetReady(true);
            backend.SetReady(true);

            Assert.That(provider.InitializeCalls, Is.EqualTo(1));
            Assert.That(provider.LoadCalls, Is.EqualTo(1));
            Assert.That(backend.ReporterReadinessNotifications, Is.EqualTo(1));
        }

        [Test]
        public void NewStore_AttachesOneAdapter_StartsOneCoordinator_AndInstallsRuntime()
        {
            using var root = new SFixtures.TempRoot();
            var provider = new Provider();
            var backend = new BackendReporter();
            var service = Service(backend);
            PurchaseRuntime.Install(service);
            using var composition = Composition(service, backend, () => provider);
            composition.Bind();

            var store = SFixtures.Store(root);
            store.Load();
            SaveRuntime.Install(store);

            Assert.That(provider.InitializeCalls, Is.EqualTo(1));
            Assert.That(provider.LoadCalls, Is.EqualTo(1));
            Assert.That(provider.EventAddCalls, Is.EqualTo(1));
            Assert.That(backend.ReporterEventAddCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.True);
            Assert.That(RewardedAdRuntime.Current.CanShow("wardrobe_conductor_trial"), Is.True,
                "CanShow proves the same real save adapter is attached for durable grants");
        }

        [Test]
        public void SameStoreAndRepeatedBind_AreIdempotent()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            SaveRuntime.Install(store);
            var provider = new Provider();
            var backend = new BackendReporter();
            var service = Service(backend);
            int providerCreates = 0;
            using var composition = Composition(service, backend, () =>
            {
                providerCreates++;
                return provider;
            });

            composition.Bind();
            composition.Bind();
            SaveRuntime.Install(store);

            Assert.That(providerCreates, Is.EqualTo(1));
            Assert.That(provider.InitializeCalls, Is.EqualTo(1));
            Assert.That(provider.LoadCalls, Is.EqualTo(1));
            Assert.That(provider.EventAddCalls, Is.EqualTo(1));
            Assert.That(backend.ReporterEventAddCalls, Is.EqualTo(1));
        }

        [Test]
        public void GenuinelyNewStore_DisposesOldCoordinatorAndProvider_BeforeReplacement()
        {
            using var firstRoot = new SFixtures.TempRoot();
            using var secondRoot = new SFixtures.TempRoot();
            var providers = new Queue<Provider>(new[] { new Provider(), new Provider() });
            var created = new List<Provider>();
            var backend = new BackendReporter();
            var service = Service(backend);
            using var composition = Composition(service, backend, () =>
            {
                var provider = providers.Dequeue();
                created.Add(provider);
                return provider;
            });
            composition.Bind();

            var first = SFixtures.Store(firstRoot);
            first.Load();
            SaveRuntime.Install(first);
            var firstRuntime = RewardedAdRuntime.Current;
            var second = SFixtures.Store(secondRoot);
            second.Load();
            SaveRuntime.Install(second);

            Assert.That(created, Has.Count.EqualTo(2));
            Assert.That(created[0].DisposeCalls, Is.EqualTo(1));
            Assert.That(created[0].EventRemoveCalls, Is.EqualTo(1));
            Assert.That(created[1].InitializeCalls, Is.EqualTo(1));
            Assert.That(backend.ReporterEventRemoveCalls, Is.EqualTo(1));
            Assert.That(backend.ReporterEventAddCalls, Is.EqualTo(2));
            Assert.That(RewardedAdRuntime.Current, Is.Not.SameAs(firstRuntime));
        }

        [Test]
        public void GenuinelyNewStore_ReplacesOldSaveLeasesBeforeLaterPersistence()
        {
            using var firstRoot = new SFixtures.TempRoot();
            using var secondRoot = new SFixtures.TempRoot();
            var first = SFixtures.Store(firstRoot);
            first.Load();
            SeedLease(first, "outfit_conductor", 5_000L);
            var second = SFixtures.Store(secondRoot);
            second.Load();
            SeedLease(second, "outfit_bellhop", 5_100L);
            var backend = new BackendReporter();
            var catalog = PurchaseCatalog.Parse(ThreeAdEntitlementCatalogJson);
            var service = Service(backend, catalog);
            var providers = new Queue<Provider>(new[] { new Provider(), new Provider() });
            using var composition = Composition(service, backend, () => providers.Dequeue());
            composition.Bind();

            SaveRuntime.Install(first);
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True);
            SaveRuntime.Install(second);

            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False,
                "the new save must remove the prior save's rewarded lease");
            Assert.That(service.IsUnlocked("outfit_bellhop"), Is.True,
                "only the new save's distinct lease is restored");
            Assert.That(service.GrantRewardedAdEntitlement("outfit_stationmaster"),
                Is.EqualTo(AdGrantOutcome.Granted));
            var persisted = new RewardedAdSaveStore(second).ReadLocalLeases();
            Assert.That(persisted, Has.Count.EqualTo(2));
            Assert.That(persisted[0].EntitlementId, Is.EqualTo("outfit_bellhop"),
                "the new save's own lease remains durable");
            Assert.That(persisted[1].EntitlementId, Is.EqualTo("outfit_stationmaster"),
                "a later reward must not write the prior save's lease into the new save");

            SaveRuntime.Install(second);
            Assert.That(providers, Has.Count.EqualTo(0),
                "same-store installation remains reference-idempotent");
        }

        [Test]
        public void RuntimeInstalledCallback_DisposingComposition_CannotLeaveAStartedZombie()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var provider = new Provider();
            var backend = new BackendReporter();
            var service = Service(backend);
            var composition = Composition(service, backend, () => provider);
            RewardedAdRuntime.Installed += composition.Dispose;

            composition.Bind();
            SaveRuntime.Install(store);

            Assert.That(provider.InitializeCalls, Is.Zero,
                "a coordinator disposed during publication must never be started afterwards");
            Assert.That(provider.EventAddCalls, Is.Zero);
            Assert.That(provider.DisposeCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            Assert.That(RewardedAdRuntime.Current.CanShow("wardrobe_conductor_trial"), Is.False);
            composition.Dispose();
            Assert.That(provider.DisposeCalls, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeInstalledCallback_InstallingNewStore_PreservesNewReplacementOwnership()
        {
            using var firstRoot = new SFixtures.TempRoot();
            using var secondRoot = new SFixtures.TempRoot();
            var first = SFixtures.Store(firstRoot);
            first.Load();
            var second = SFixtures.Store(secondRoot);
            second.Load();
            var firstProvider = new Provider();
            var secondProvider = new Provider();
            var providers = new Queue<Provider>(new[] { firstProvider, secondProvider });
            var backend = new BackendReporter();
            var service = Service(backend);
            using var composition = Composition(service, backend, () => providers.Dequeue());
            int publications = 0;
            IRewardedAds replacementRuntime = null;
            RewardedAdRuntime.Installed += () =>
            {
                publications++;
                if (publications == 1)
                    SaveRuntime.Install(second);
                else
                    replacementRuntime = RewardedAdRuntime.Current;
            };
            composition.Bind();

            SaveRuntime.Install(first);

            Assert.That(publications, Is.EqualTo(2));
            Assert.That(firstProvider.InitializeCalls, Is.Zero,
                "the replaced coordinator must not start after its publication returns");
            Assert.That(firstProvider.DisposeCalls, Is.EqualTo(1));
            Assert.That(secondProvider.InitializeCalls, Is.EqualTo(1));
            Assert.That(secondProvider.DisposeCalls, Is.Zero,
                "outer cleanup must not dispose the synchronously installed replacement");
            Assert.That(RewardedAdRuntime.Current, Is.SameAs(replacementRuntime));
            Assert.That(RewardedAdRuntime.Current.CanShow("wardrobe_conductor_trial"), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingReporterOrProvider_StillRestoresAndPersistsWithTheBoundSave(
            bool missingReporter)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            SeedLease(store, "outfit_conductor", 5_000L);
            var backend = new BackendReporter();
            var catalog = PurchaseCatalog.Parse(ThreeAdEntitlementCatalogJson);
            var service = Service(backend, catalog);
            int providerCreates = 0;
            IAdEventReporter reporter = missingReporter ? null : backend;
            Func<IRewardedAdProvider> providerFactory = () =>
            {
                providerCreates++;
                return missingReporter ? new Provider() : null;
            };
            using var composition = Composition(service, reporter, providerFactory);
            composition.Bind();

            SaveRuntime.Install(store);

            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True,
                "degraded ads must still restore the save's rewarded lease");
            Assert.That(service.GrantRewardedAdEntitlement("outfit_bellhop"),
                Is.EqualTo(AdGrantOutcome.Granted));
            var persisted = new RewardedAdSaveStore(store).ReadLocalLeases();
            Assert.That(persisted, Has.Count.EqualTo(2));
            Assert.That(persisted[0].EntitlementId, Is.EqualTo("outfit_bellhop"));
            Assert.That(persisted[1].EntitlementId, Is.EqualTo("outfit_conductor"));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            Assert.That(providerCreates, Is.EqualTo(missingReporter ? 0 : 1));
        }

        [Test]
        public void MissingReporterOrProvider_LeavesNoOpWithoutCreatingOrLeakingAProvider()
        {
            using var firstRoot = new SFixtures.TempRoot();
            using var secondRoot = new SFixtures.TempRoot();
            int forbiddenCreates = 0;
            var service = Service(new BackendReporter());
            using (var noReporter = Composition(service, null, () =>
            {
                forbiddenCreates++;
                return new Provider();
            }))
            {
                noReporter.Bind();
                var first = SFixtures.Store(firstRoot);
                first.Load();
                SaveRuntime.Install(first);
                Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            }
            Assert.That(forbiddenCreates, Is.Zero,
                "a fixed missing reporter must be rejected before provider construction");

            SaveRuntime.ResetForTests();
            RewardedAdRuntime.ResetForTests();
            using var noProvider = Composition(service, new BackendReporter(), () => null);
            noProvider.Bind();
            var second = SFixtures.Store(secondRoot);
            second.Load();
            SaveRuntime.Install(second);
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            Assert.That(RewardedAdRuntime.Current.CanShow("wardrobe_conductor_trial"), Is.False);
        }

        [Test]
        public void Pump_PauseCommitsBoundSave_ResumeRefreshes_AndDestroyUnsubscribes()
        {
            using var firstRoot = new SFixtures.TempRoot();
            using var secondRoot = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(firstRoot, fs);
            store.Load();
            var backend = new BackendReporter();
            var service = Service(backend);
            var firstProvider = new Provider();
            int providerCreates = 0;
            var composition = Composition(service, backend, () =>
            {
                providerCreates++;
                return firstProvider;
            });
            composition.Bind();
            SaveRuntime.Install(store);
            var host = new GameObject("[RewardedAdsBootstrapTests]");
            var pump = host.AddComponent<MonetizationPump>();
            pump.Bind(service, composition);
            store.State.Tickets = 9;
            int refreshesBeforeResume = backend.RefreshEntitlementsCalls;

            pump.OnApplicationPause(true);
            Assert.That(File.Exists(store.SavePath), Is.True,
                "pause must use the bound SaveStore's real atomic commit path");
            Assert.That(fs.Calls.Exists(x => x.StartsWith("Replace:")), Is.True);
            pump.OnApplicationPause(false);
            Assert.That(backend.RefreshEntitlementsCalls,
                Is.EqualTo(refreshesBeforeResume + 1));

            pump.OnDestroy();
            UnityEngine.Object.DestroyImmediate(host);
            Assert.That(firstProvider.DisposeCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            var second = SFixtures.Store(secondRoot);
            second.Load();
            SaveRuntime.Install(second);
            Assert.That(providerCreates, Is.EqualTo(1),
                "destroy must remove the static SaveRuntime subscription");
        }

        private static PurchaseService Service(BackendReporter backend,
            PurchaseCatalog catalog = null)
            => new PurchaseService(catalog ?? PFixtures.TinyCatalog(), backend, () => 1_000L);

        private static RewardedAdsComposition Composition(PurchaseService service,
            IAdEventReporter reporter, Func<IRewardedAdProvider> providerFactory)
        {
            var placements = RewardedPlacementCatalog.Parse(PlacementsJson,
                service.Catalog);
            Assert.That(placements.Problems, Is.Empty);
            return new RewardedAdsComposition(service, placements, providerFactory, reporter,
                () => "2026-08-29");
        }

        private static void SeedLease(SaveStore store, string entitlementId, long expiry)
        {
            store.State.Payload["entitlements"]["localLeases"] = new JArray
            {
                new JObject
                {
                    ["entitlementId"] = entitlementId,
                    ["expiresAtUnixSeconds"] = expiry,
                },
            };
        }

        private sealed class Provider : IRewardedAdProvider
        {
            private Action<RewardedAdEvent> _eventReceived;

            public event Action<RewardedAdEvent> EventReceived
            {
                add { EventAddCalls++; _eventReceived += value; }
                remove { EventRemoveCalls++; _eventReceived -= value; }
            }

            public bool IsReady => true;
            public int EventAddCalls { get; private set; }
            public int EventRemoveCalls { get; private set; }
            public int InitializeCalls { get; private set; }
            public int LoadCalls { get; private set; }
            public int DisposeCalls { get; private set; }

            public void Initialize() => InitializeCalls++;
            public void Load() => LoadCalls++;
            public bool TryShow(long attemptId, string placementId) => true;
            public void Dispose() => DisposeCalls++;
        }

        private sealed class BackendReporter : IPurchaseBackend, IAdEventReporter
        {
            private Action _readinessChanged;
            private bool _isReady;

            public BackendReporter(bool isReady = true)
            {
                _isReady = isReady;
            }

            public BackendAvailability Availability => BackendAvailability.Ready;
            public bool IsReady => _isReady;
            public int RefreshEntitlementsCalls { get; private set; }
            public int ReporterEventAddCalls { get; private set; }
            public int ReporterEventRemoveCalls { get; private set; }
            public int ReporterReadinessNotifications { get; private set; }

            public event Action ReadinessChanged
            {
                add { ReporterEventAddCalls++; _readinessChanged += value; }
                remove { ReporterEventRemoveCalls++; _readinessChanged -= value; }
            }

            public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
                => onDone?.Invoke(Array.Empty<StoreProductView>());

            public void Purchase(string productId, Action<PurchaseResult> onDone)
                => onDone?.Invoke(PurchaseResult.Unavailable(productId, "test"));

            public void Restore(Action<RestoreResult> onDone)
                => onDone?.Invoke(new RestoreResult(RestoreOutcome.Unavailable));

            public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
            {
                RefreshEntitlementsCalls++;
                onDone?.Invoke(EntitlementSnapshot.Unreachable());
            }

            public void SetReady(bool ready)
            {
                if (_isReady == ready) return;
                _isReady = ready;
                ReporterReadinessNotifications++;
                _readinessChanged?.Invoke();
            }

            public void Report(RewardedAdEvent adEvent) { }
        }
    }
}
