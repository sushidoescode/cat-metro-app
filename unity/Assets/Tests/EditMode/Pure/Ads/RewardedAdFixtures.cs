using System;
using System.Collections.Generic;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;

namespace CatMetro.Tests.Ads
{
    public static class RewardedAdFixtures
    {
        public sealed class Provider : IRewardedAdProvider, IRewardedAdPlacementReadiness
        {
            private Action<RewardedAdEvent> _eventReceived;

            public event Action<RewardedAdEvent> EventReceived
            {
                add
                {
                    EventAddCalls++;
                    if (ThrowOnEventAdd)
                        throw new InvalidOperationException("injected provider event-add fault");
                    OnEventAdd?.Invoke();
                    _eventReceived += value;
                    if (ThrowAfterEventAdd)
                        throw new InvalidOperationException(
                            "injected provider post-attach event-add fault");
                }
                remove
                {
                    EventRemoveCalls++;
                    if (ThrowOnEventRemove)
                        throw new InvalidOperationException("injected provider event-remove fault");
                    _eventReceived -= value;
                }
            }

            public bool IsReady { get; set; } = true;
            public bool ShowAccepted { get; set; } = true;
            public bool ThrowOnReady { get; set; }
            public bool ThrowOnInitialize { get; set; }
            public bool ThrowOnLoad { get; set; }
            public bool ThrowOnShow { get; set; }
            public bool ThrowOnEventAdd { get; set; }
            public bool ThrowAfterEventAdd { get; set; }
            public bool ThrowOnEventRemove { get; set; }
            public string CappedPlacement { get; set; }
            public Action OnEventAdd { get; set; }
            public Action OnLoad { get; set; }
            public Action<long, string> OnShow { get; set; }
            public int InitializeCalls { get; private set; }
            public int LoadCalls { get; private set; }
            public int DisposeCalls { get; private set; }
            public int EventAddCalls { get; private set; }
            public int EventRemoveCalls { get; private set; }
            public int EventSubscriberCount => _eventReceived?.GetInvocationList().Length ?? 0;
            public readonly List<(long AttemptId, string PlacementId)> Shows =
                new List<(long, string)>();
            public readonly List<string> PlacementReadinessChecks = new List<string>();

            bool IRewardedAdProvider.IsReady => ThrowOnReady
                ? throw new InvalidOperationException("injected readiness fault")
                : IsReady;

            public bool IsReadyForPlacement(string placementId)
            {
                PlacementReadinessChecks.Add(placementId);
                if (ThrowOnReady)
                    throw new InvalidOperationException("injected placement readiness fault");
                return IsReady && !string.Equals(CappedPlacement, placementId,
                    StringComparison.Ordinal);
            }

            public void Initialize()
            {
                InitializeCalls++;
                if (ThrowOnInitialize) throw new InvalidOperationException("injected init fault");
            }

            public void Load()
            {
                LoadCalls++;
                OnLoad?.Invoke();
                if (ThrowOnLoad) throw new InvalidOperationException("injected load fault");
            }

            public bool TryShow(long attemptId, string placementId)
            {
                Shows.Add((attemptId, placementId));
                OnShow?.Invoke(attemptId, placementId);
                if (ThrowOnShow) throw new InvalidOperationException("injected show fault");
                return ShowAccepted;
            }

            public void Emit(RewardedAdEvent adEvent) => _eventReceived?.Invoke(adEvent);

            public void Dispose() => DisposeCalls++;
        }

        public sealed class Reporter : IAdEventReporter
        {
            private Action _readinessChanged;

            public event Action ReadinessChanged
            {
                add
                {
                    EventAddCalls++;
                    if (ThrowOnEventAdd)
                        throw new InvalidOperationException("injected reporter event-add fault");
                    OnEventAdd?.Invoke();
                    _readinessChanged += value;
                    if (ReadyOnSubscribe) IsReady = true;
                    if (ThrowAfterEventAdd)
                        throw new InvalidOperationException(
                            "injected reporter post-attach event-add fault");
                }
                remove
                {
                    EventRemoveCalls++;
                    if (ThrowOnEventRemove)
                        throw new InvalidOperationException("injected reporter event-remove fault");
                    _readinessChanged -= value;
                }
            }

            public bool IsReady { get; private set; }
            public bool ThrowOnReady { get; set; }
            public bool ThrowOnReport { get; set; }
            public bool ThrowOnEventAdd { get; set; }
            public bool ThrowAfterEventAdd { get; set; }
            public bool ThrowOnEventRemove { get; set; }
            public bool ReadyOnSubscribe { get; set; }
            public Action OnEventAdd { get; set; }
            public Action OnReadyRead { get; set; }
            public int EventAddCalls { get; private set; }
            public int EventRemoveCalls { get; private set; }
            public int ReadyReadCalls { get; private set; }
            public int EventSubscriberCount => _readinessChanged?.GetInvocationList().Length ?? 0;
            public readonly List<RewardedAdEvent> Events = new List<RewardedAdEvent>();

            bool IAdEventReporter.IsReady
            {
                get
                {
                    ReadyReadCalls++;
                    OnReadyRead?.Invoke();
                    return ThrowOnReady
                        ? throw new InvalidOperationException("injected reporter readiness fault")
                        : IsReady;
                }
            }

            public Reporter(bool ready = true) => IsReady = ready;

            public void SetReady(bool ready)
            {
                IsReady = ready;
                _readinessChanged?.Invoke();
            }

            public void Report(RewardedAdEvent adEvent)
            {
                if (ThrowOnReport) throw new InvalidOperationException("injected report fault");
                Events.Add(adEvent);
            }
        }

        public sealed class CapStore : IRewardedAdCapStore
        {
            private readonly Dictionary<string, int> _counts =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public bool Accept { get; set; } = true;
            public bool ThrowOnRead { get; set; }
            public bool ThrowOnIncrement { get; set; }
            public int IncrementCalls { get; private set; }

            public int ReadLocalDateCount(string placementId, string localDateKey)
            {
                if (ThrowOnRead) throw new InvalidOperationException("injected cap read fault");
                return _counts.TryGetValue(Key(placementId, localDateKey), out var value) ? value : 0;
            }

            public bool TryIncrementLocalDateCount(string placementId, string localDateKey)
            {
                IncrementCalls++;
                if (ThrowOnIncrement) throw new InvalidOperationException("injected cap write fault");
                if (!Accept) return false;
                var key = Key(placementId, localDateKey);
                int current = _counts.TryGetValue(key, out var value) ? value : 0;
                _counts[key] = current == int.MaxValue ? int.MaxValue : current + 1;
                return true;
            }

            public void Seed(string placementId, string localDateKey, int count)
                => _counts[Key(placementId, localDateKey)] = count;

            private static string Key(string placementId, string localDateKey)
                => (placementId ?? "") + "\n" + (localDateKey ?? "");
        }

        public sealed class LeasePersistence : IEntitlementLeasePersistence
        {
            public bool Accept { get; set; } = true;
            public int Calls { get; private set; }
            public Action OnPersist { get; set; }

            public bool TryReplaceRewardedAdLeases(IReadOnlyList<EntitlementGrant> leases)
            {
                Calls++;
                OnPersist?.Invoke();
                return Accept;
            }
        }

        public sealed class Clock
        {
            public long Now { get; set; } = 1_000L;
            public Func<long> Read => () => Now;
            public void Advance(long seconds) => Now += seconds;
        }

        public sealed class LocalDate
        {
            public string Key { get; set; } = "2026-08-29";
            public bool ThrowOnRead { get; set; }
            public Func<string> Read => () => ThrowOnRead
                ? throw new InvalidOperationException("injected local-date fault")
                : Key;
        }

        public static RewardedPlacementCatalog Placements(string caps = "", int count = 2)
        {
            var rows = new List<string>();
            for (int i = 0; i < count; i++)
            {
                rows.Add("{ \"id\": \"p" + i + "\", \"entitlement\": \"outfit_conductor\", " +
                    "\"enabled\": true" + caps + " }");
            }
            rows.Add("{ \"id\": \"disabled\", \"entitlement\": \"outfit_conductor\", " +
                "\"enabled\": false, \"disabledReason\": \"off\" }");
            return RewardedPlacementCatalog.Parse("{ \"placements\": [" +
                string.Join(",", rows) + "] }", Purchases.PFixtures.TinyCatalog());
        }

        public static PurchaseService Service(IEntitlementLeasePersistence persistence = null,
            Func<long> clock = null)
        {
            var service = new PurchaseService(Purchases.PFixtures.TinyCatalog(),
                clock: clock ?? (() => 1_000L));
            service.AttachLeasePersistence(persistence ?? new LeasePersistence());
            return service;
        }

        public static RewardedAdCoordinator Coordinator(Provider provider = null,
            Reporter reporter = null, CapStore caps = null, PurchaseService service = null,
            RewardedPlacementCatalog placements = null)
            => new RewardedAdCoordinator(placements ?? Placements(), service ?? Service(),
                provider ?? new Provider(), reporter ?? new Reporter(), caps ?? new CapStore(),
                () => "2026-08-29");
    }
}
