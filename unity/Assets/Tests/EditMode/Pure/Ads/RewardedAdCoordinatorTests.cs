using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using NUnit.Framework;

namespace CatMetro.Tests.Ads
{
    public sealed class RewardedAdCoordinatorTests
    {
        [Test]
        public void ExactShow_PrecheckFailuresCompleteOnceWithTheRequestedIdentity()
        {
            var provider = new RewardedAdFixtures.Provider();
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Start();

            AssertImmediateExactFailure(coordinator, "missing", "outfit_conductor",
                RewardedShowOutcome.Unavailable);
            AssertImmediateExactFailure(coordinator, "p0", "wrong_entitlement",
                RewardedShowOutcome.Unavailable);
            provider.IsReady = false;
            AssertImmediateExactFailure(coordinator, "p0", "outfit_conductor",
                RewardedShowOutcome.Unavailable);
            provider.IsReady = true;
            Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Started));
            AssertImmediateExactFailure(coordinator, "p1", "outfit_conductor",
                RewardedShowOutcome.Busy);
        }

        [Test]
        public void ExactShow_SecondStageBusyUnavailableAndOverflowStillCompleteOnce()
        {
            var flipProvider = new RewardedAdFixtures.Provider();
            flipProvider.PlacementReadinessResults.Enqueue(true);
            flipProvider.PlacementReadinessResults.Enqueue(false);
            using var unavailable = RewardedAdFixtures.Coordinator(flipProvider);
            unavailable.Start();
            AssertImmediateExactFailure(unavailable, "p0", "outfit_conductor",
                RewardedShowOutcome.Unavailable);

            var busyProvider = new RewardedAdFixtures.Provider();
            using var busy = RewardedAdFixtures.Coordinator(busyProvider);
            busy.Start();
            int readinessChecks = 0;
            busyProvider.OnPlacementReadinessCheck = _ =>
            {
                readinessChecks++;
                if (readinessChecks != 1) return;
                Assert.That(busy.Show("p1"), Is.EqualTo(RewardedShowOutcome.Started));
            };
            AssertImmediateExactFailure(busy, "p0", "outfit_conductor",
                RewardedShowOutcome.Busy);

            var overflowProvider = new RewardedAdFixtures.Provider();
            using var overflow = RewardedAdFixtures.Coordinator(overflowProvider);
            overflow.Start();
            var nextAttempt = typeof(RewardedAdCoordinator).GetField("_nextAttemptId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(nextAttempt, Is.Not.Null);
            nextAttempt.SetValue(overflow, long.MaxValue);
            AssertImmediateExactFailure(overflow, "p0", "outfit_conductor",
                RewardedShowOutcome.Unavailable);
        }

        [Test]
        public void ExactShow_ProviderRejectOrThrowCompletesOneDisplayFailedWithExactAttempt()
        {
            AssertRejectedExactShow(new RewardedAdFixtures.Provider { ShowAccepted = false });
            AssertRejectedExactShow(new RewardedAdFixtures.Provider { ThrowOnShow = true });
        }

        [Test]
        public void ExactShow_SynchronousDisplayFailureDuringRejectedShowCompletesOnlyOnce()
        {
            var provider = new RewardedAdFixtures.Provider { ShowAccepted = false };
            provider.OnShow = (attemptId, placementId) => provider.Emit(
                new RewardedAdEvent(RewardedAdEventKind.DisplayFailed, attemptId, placementId));
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Start();
            var completions = new List<RewardedAdCompletion>();

            Assert.That(coordinator.Show("p0", "outfit_conductor", completions.Add),
                Is.EqualTo(RewardedShowOutcome.Unavailable));

            AssertExactCompletion(completions, RewardedAdCompletionKind.DisplayFailed,
                attemptMustExist: true);
        }

        [Test]
        public void ExactShow_CloseThenLateRewardKeepsClosedCompletionAndGrantsDurablyOnce()
        {
            var provider = new RewardedAdFixtures.Provider();
            var persistence = new RewardedAdFixtures.LeasePersistence();
            var service = RewardedAdFixtures.Service(persistence);
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service);
            coordinator.Start();
            var completions = new List<RewardedAdCompletion>();
            Assert.That(coordinator.Show("p0", "outfit_conductor", completions.Add),
                Is.EqualTo(RewardedShowOutcome.Started));
            long attempt = provider.Shows.Single().AttemptId;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));
            AssertExactCompletion(completions,
                RewardedAdCompletionKind.ClosedWithoutReward, attemptMustExist: true);
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False);

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));

            AssertExactCompletion(completions,
                RewardedAdCompletionKind.ClosedWithoutReward, attemptMustExist: true);
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(service.Ledger.ExportLeases(), Has.Count.EqualTo(1));
            Assert.That(persistence.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ExactShow_ForeignAttemptOrPlacementTerminalsCannotCompleteOrGrant()
        {
            var provider = new RewardedAdFixtures.Provider();
            var service = RewardedAdFixtures.Service();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service);
            coordinator.Start();
            var completions = new List<RewardedAdCompletion>();
            Assert.That(coordinator.Show("p0", "outfit_conductor", completions.Add),
                Is.EqualTo(RewardedShowOutcome.Started));
            long attempt = provider.Shows.Single().AttemptId;

            foreach (var kind in new[]
                     {
                         RewardedAdEventKind.Rewarded,
                         RewardedAdEventKind.Closed,
                         RewardedAdEventKind.DisplayFailed,
                     })
            {
                provider.Emit(new RewardedAdEvent(kind, attempt + 1L, "p0"));
                provider.Emit(new RewardedAdEvent(kind, attempt, "p1"));
            }

            Assert.That(completions, Is.Empty);
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False);
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            AssertExactCompletion(completions, RewardedAdCompletionKind.Granted,
                attemptMustExist: true);
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True);
        }

        [Test]
        public void ExactShow_DisposeTwiceCompletesOpenOnceButCannotReplacePriorClose()
        {
            var openProvider = new RewardedAdFixtures.Provider();
            var openService = RewardedAdFixtures.Service();
            var open = RewardedAdFixtures.Coordinator(openProvider, service: openService);
            open.Start();
            var cancelled = new List<RewardedAdCompletion>();
            Assert.That(open.Show("p0", "outfit_conductor", cancelled.Add),
                Is.EqualTo(RewardedShowOutcome.Started));
            long openAttempt = openProvider.Shows.Single().AttemptId;
            open.Dispose();
            open.Dispose();
            openProvider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded,
                openAttempt, "p0"));
            AssertExactCompletion(cancelled, RewardedAdCompletionKind.Cancelled,
                attemptMustExist: true);
            Assert.That(openService.IsUnlocked("outfit_conductor"), Is.False);
            Assert.That(openService.Ledger.ExportLeases(), Is.Empty);

            var closedProvider = new RewardedAdFixtures.Provider();
            var closedService = RewardedAdFixtures.Service();
            var closed = RewardedAdFixtures.Coordinator(closedProvider, service: closedService);
            closed.Start();
            var priorClose = new List<RewardedAdCompletion>();
            Assert.That(closed.Show("p0", "outfit_conductor", priorClose.Add),
                Is.EqualTo(RewardedShowOutcome.Started));
            long attempt = closedProvider.Shows.Single().AttemptId;
            closedProvider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));
            closed.Dispose();
            closed.Dispose();
            closedProvider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded,
                attempt, "p0"));
            AssertExactCompletion(priorClose,
                RewardedAdCompletionKind.ClosedWithoutReward, attemptMustExist: true);
            Assert.That(closedService.IsUnlocked("outfit_conductor"), Is.False);
            Assert.That(closedService.Ledger.ExportLeases(), Is.Empty);
        }

        [Test]
        public void Start_SubscribesBeforeInspectingReporterReadiness()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter(ready: false)
            {
                ReadyOnSubscribe = true,
            };
            using var coordinator = RewardedAdFixtures.Coordinator(provider, reporter);

            coordinator.Start();

            Assert.That(reporter.EventAddCalls, Is.EqualTo(1));
            Assert.That(provider.InitializeCalls, Is.EqualTo(1));
            Assert.That(provider.LoadCalls, Is.EqualTo(1));
        }

        [Test]
        public void Start_ThrowingProviderEventAddDoesNotBubbleAndStillSubscribesReporter()
        {
            var provider = new RewardedAdFixtures.Provider { ThrowOnEventAdd = true };
            var reporter = new RewardedAdFixtures.Reporter();
            var coordinator = RewardedAdFixtures.Coordinator(provider, reporter);

            Assert.DoesNotThrow(coordinator.Start);
            Assert.DoesNotThrow(coordinator.Dispose);

            Assert.That(provider.EventAddCalls, Is.EqualTo(1));
            Assert.That(reporter.EventAddCalls, Is.EqualTo(1));
            Assert.That(coordinator.CanShow("p0"), Is.False);
            Assert.That(provider.EventRemoveCalls, Is.EqualTo(1),
                "an add exception conservatively removes in case the accessor attached first");
            Assert.That(provider.EventSubscriberCount, Is.Zero);
            Assert.That(reporter.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(provider.DisposeCalls, Is.EqualTo(1));
        }

        [Test]
        public void Start_ThrowingReporterEventAddDoesNotBubbleAndFailsClosed()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter { ThrowOnEventAdd = true };
            var coordinator = RewardedAdFixtures.Coordinator(provider, reporter);

            Assert.DoesNotThrow(coordinator.Start);
            Assert.DoesNotThrow(coordinator.Dispose);

            Assert.That(provider.EventAddCalls, Is.EqualTo(1));
            Assert.That(reporter.EventAddCalls, Is.EqualTo(1));
            Assert.That(coordinator.CanShow("p0"), Is.False);
            Assert.That(provider.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(reporter.EventRemoveCalls, Is.EqualTo(1),
                "an add exception conservatively removes in case the accessor attached first");
            Assert.That(reporter.EventSubscriberCount, Is.Zero);
            Assert.That(provider.DisposeCalls, Is.EqualTo(1));
        }

        [Test]
        public void Start_ProviderEventAddAttachesThenThrows_CompensatesWithoutAffectingNewerRuntime()
        {
            var provider = new RewardedAdFixtures.Provider { ThrowAfterEventAdd = true };
            var reporter = new RewardedAdFixtures.Reporter();
            var coordinator = RewardedAdFixtures.Coordinator(provider, reporter);
            var newerProvider = new RewardedAdFixtures.Provider();
            var newer = RewardedAdFixtures.Coordinator(newerProvider,
                new RewardedAdFixtures.Reporter());
            newer.Start();
            RewardedAdRuntime.Install(newer);

            try
            {
                Assert.DoesNotThrow(coordinator.Start);
                Assert.DoesNotThrow(coordinator.Dispose);

                Assert.That(provider.EventAddCalls, Is.EqualTo(1));
                Assert.That(provider.EventRemoveCalls, Is.EqualTo(1));
                Assert.That(provider.EventSubscriberCount, Is.Zero,
                    "a handler attached before the add fault must not survive failed startup");
                Assert.That(provider.InitializeCalls, Is.Zero);
                Assert.That(provider.DisposeCalls, Is.EqualTo(1));
                Assert.That(reporter.EventAddCalls, Is.EqualTo(1));
                Assert.That(reporter.EventRemoveCalls, Is.EqualTo(1));
                Assert.That(RewardedAdRuntime.Current, Is.SameAs(newer));
                Assert.That(newerProvider.DisposeCalls, Is.Zero);
            }
            finally
            {
                RewardedAdRuntime.Uninstall(newer);
                coordinator.Dispose();
                newer.Dispose();
                RewardedAdRuntime.ResetForTests();
            }
        }

        [Test]
        public void Start_ReporterEventAddAttachesThenThrows_CompensatesWithoutAffectingNewerRuntime()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter { ThrowAfterEventAdd = true };
            var coordinator = RewardedAdFixtures.Coordinator(provider, reporter);
            var newerProvider = new RewardedAdFixtures.Provider();
            var newer = RewardedAdFixtures.Coordinator(newerProvider,
                new RewardedAdFixtures.Reporter());
            newer.Start();
            RewardedAdRuntime.Install(newer);

            try
            {
                Assert.DoesNotThrow(coordinator.Start);
                Assert.DoesNotThrow(coordinator.Dispose);

                Assert.That(provider.EventAddCalls, Is.EqualTo(1));
                Assert.That(provider.EventRemoveCalls, Is.EqualTo(1));
                Assert.That(provider.EventSubscriberCount, Is.Zero);
                Assert.That(provider.InitializeCalls, Is.Zero);
                Assert.That(provider.DisposeCalls, Is.EqualTo(1));
                Assert.That(reporter.EventAddCalls, Is.EqualTo(1));
                Assert.That(reporter.EventRemoveCalls, Is.EqualTo(1));
                Assert.That(reporter.EventSubscriberCount, Is.Zero,
                    "a handler attached before the add fault must not survive failed startup");
                Assert.That(RewardedAdRuntime.Current, Is.SameAs(newer));
                Assert.That(newerProvider.DisposeCalls, Is.Zero);
            }
            finally
            {
                RewardedAdRuntime.Uninstall(newer);
                coordinator.Dispose();
                newer.Dispose();
                RewardedAdRuntime.ResetForTests();
            }
        }

        [Test]
        public void Start_WaitsForReporterReadinessBeforeInitializingOrLoading()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter(ready: false);
            using var coordinator = RewardedAdFixtures.Coordinator(provider, reporter);

            coordinator.Start();
            Assert.That(provider.InitializeCalls, Is.Zero);
            Assert.That(provider.LoadCalls, Is.Zero);

            reporter.SetReady(true);
            Assert.That(provider.InitializeCalls, Is.EqualTo(1));
            Assert.That(provider.LoadCalls, Is.EqualTo(1));
        }

        [Test]
        public void Start_ProviderEventAddReentrantReplacement_RemovesLateAttachmentAndStopsOldStart()
        {
            var oldProvider = new RewardedAdFixtures.Provider();
            var oldReporter = new RewardedAdFixtures.Reporter();
            var replacementProvider = new RewardedAdFixtures.Provider();
            var replacement = RewardedAdFixtures.Coordinator(replacementProvider,
                new RewardedAdFixtures.Reporter());
            RewardedAdCoordinator old = null;
            oldProvider.OnEventAdd = () =>
            {
                old.Dispose();
                replacement.Start();
                RewardedAdRuntime.Install(replacement);
            };
            old = RewardedAdFixtures.Coordinator(oldProvider, oldReporter);

            try
            {
                old.Start();

                Assert.That(oldProvider.InitializeCalls, Is.Zero,
                    "the disposed provider must never be initialized after event add returns");
                Assert.That(oldProvider.DisposeCalls, Is.EqualTo(1));
                Assert.That(oldProvider.EventAddCalls, Is.EqualTo(1));
                Assert.That(oldProvider.EventRemoveCalls, Is.EqualTo(2),
                    "dispose removes subscription intent; add completion removes a late attach");
                Assert.That(oldProvider.EventSubscriberCount, Is.Zero);
                Assert.That(oldReporter.EventAddCalls, Is.Zero,
                    "old Start must stop at the lost provider-subscription boundary");
                Assert.That(RewardedAdRuntime.Current, Is.SameAs(replacement));
                Assert.That(replacementProvider.InitializeCalls, Is.EqualTo(1));
                Assert.That(replacementProvider.DisposeCalls, Is.Zero);
            }
            finally
            {
                RewardedAdRuntime.Uninstall(replacement);
                old.Dispose();
                replacement.Dispose();
                RewardedAdRuntime.ResetForTests();
            }
        }

        [Test]
        public void Start_ReporterEventAddReentrantReplacement_RemovesLateAttachmentAndStopsOldStart()
        {
            var oldProvider = new RewardedAdFixtures.Provider();
            var oldReporter = new RewardedAdFixtures.Reporter();
            var replacementProvider = new RewardedAdFixtures.Provider();
            var replacement = RewardedAdFixtures.Coordinator(replacementProvider,
                new RewardedAdFixtures.Reporter());
            RewardedAdCoordinator old = null;
            oldReporter.OnEventAdd = () =>
            {
                old.Dispose();
                replacement.Start();
                RewardedAdRuntime.Install(replacement);
            };
            old = RewardedAdFixtures.Coordinator(oldProvider, oldReporter);

            try
            {
                old.Start();

                Assert.That(oldProvider.InitializeCalls, Is.Zero);
                Assert.That(oldProvider.DisposeCalls, Is.EqualTo(1));
                Assert.That(oldProvider.EventAddCalls, Is.EqualTo(1));
                Assert.That(oldProvider.EventRemoveCalls, Is.EqualTo(1));
                Assert.That(oldProvider.EventSubscriberCount, Is.Zero);
                Assert.That(oldReporter.EventAddCalls, Is.EqualTo(1));
                Assert.That(oldReporter.EventRemoveCalls, Is.EqualTo(2),
                    "dispose removes subscription intent; add completion removes a late attach");
                Assert.That(oldReporter.EventSubscriberCount, Is.Zero);
                Assert.That(oldReporter.ReadyReadCalls, Is.Zero,
                    "old Start must stop before observing readiness");
                Assert.That(RewardedAdRuntime.Current, Is.SameAs(replacement));
                Assert.That(replacementProvider.InitializeCalls, Is.EqualTo(1));
                Assert.That(replacementProvider.DisposeCalls, Is.Zero);
            }
            finally
            {
                RewardedAdRuntime.Uninstall(replacement);
                old.Dispose();
                replacement.Dispose();
                RewardedAdRuntime.ResetForTests();
            }
        }

        [Test]
        public void Start_ReporterReadyGetterReentrantReplacement_NeverInitializesDisposedProvider()
        {
            var oldProvider = new RewardedAdFixtures.Provider();
            var oldReporter = new RewardedAdFixtures.Reporter();
            var replacementProvider = new RewardedAdFixtures.Provider();
            var replacement = RewardedAdFixtures.Coordinator(replacementProvider,
                new RewardedAdFixtures.Reporter());
            RewardedAdCoordinator old = null;
            oldReporter.OnReadyRead = () =>
            {
                old.Dispose();
                replacement.Start();
                RewardedAdRuntime.Install(replacement);
            };
            old = RewardedAdFixtures.Coordinator(oldProvider, oldReporter);

            try
            {
                old.Start();

                Assert.That(oldReporter.ReadyReadCalls, Is.EqualTo(1));
                Assert.That(oldProvider.InitializeCalls, Is.Zero,
                    "readiness returned true only after synchronously disposing the old owner");
                Assert.That(oldProvider.DisposeCalls, Is.EqualTo(1));
                Assert.That(oldProvider.EventAddCalls, Is.EqualTo(1));
                Assert.That(oldProvider.EventRemoveCalls, Is.EqualTo(1));
                Assert.That(oldProvider.EventSubscriberCount, Is.Zero);
                Assert.That(oldReporter.EventAddCalls, Is.EqualTo(1));
                Assert.That(oldReporter.EventRemoveCalls, Is.EqualTo(1));
                Assert.That(oldReporter.EventSubscriberCount, Is.Zero);
                Assert.That(RewardedAdRuntime.Current, Is.SameAs(replacement));
                Assert.That(replacementProvider.InitializeCalls, Is.EqualTo(1));
                Assert.That(replacementProvider.DisposeCalls, Is.Zero);
            }
            finally
            {
                RewardedAdRuntime.Uninstall(replacement);
                old.Dispose();
                replacement.Dispose();
                RewardedAdRuntime.ResetForTests();
            }
        }

        [Test]
        public void InvalidOrUnavailableOffer_NeverCallsProviderShow()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter();
            var caps = new RewardedAdFixtures.CapStore();
            var service = RewardedAdFixtures.Service();
            var coordinator = new RewardedAdCoordinator(
                RewardedAdFixtures.Placements(", \"caps\": { \"session\": 1, \"localDate\": 1 }"),
                service, provider, reporter, caps, () => "2026-08-29");
            using (coordinator)
            {
                coordinator.Start();

                Assert.That(coordinator.Show("missing"), Is.EqualTo(RewardedShowOutcome.Unavailable));
                Assert.That(coordinator.Show("disabled"), Is.EqualTo(RewardedShowOutcome.Unavailable));

                service.Ledger.ReplaceStoreGrants(new[]
                {
                    new EntitlementGrant("outfit_conductor", GrantSource.Store)
                });
                Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable));
                service.Ledger.ReplaceStoreGrants(Array.Empty<EntitlementGrant>());

                caps.Seed("p0", "2026-08-29", 1);
                Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable));
                caps.Seed("p0", "2026-08-29", 0);

                provider.IsReady = false;
                Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable));
                provider.IsReady = true;

                reporter.SetReady(false);
                Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable));
                Assert.That(provider.Shows, Is.Empty);
            }
        }

        [Test]
        public void CanShow_UsesProviderPlacementReadinessWhenAvailable()
        {
            var provider = new RewardedAdFixtures.Provider { CappedPlacement = "p0" };
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Start();

            Assert.That(coordinator.CanShow("p0"), Is.False,
                "a dashboard-capped placement must be hidden before the tap");
            Assert.That(coordinator.CanShow("p1"), Is.True);
            Assert.That(provider.PlacementReadinessChecks, Is.EqualTo(new[] { "p0", "p1" }));
        }

        [Test]
        public void MissingLeasePersistenceOrCoordinatorDependency_FailsClosedBeforeProviderShow()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter();
            var service = new PurchaseService(Purchases.PFixtures.TinyCatalog(), clock: () => 1_000L);
            using var missingLease = new RewardedAdCoordinator(RewardedAdFixtures.Placements(),
                service, provider, reporter, new RewardedAdFixtures.CapStore(), () => "2026-08-29");
            missingLease.Start();
            Assert.That(missingLease.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable));

            using var missingProvider = new RewardedAdCoordinator(RewardedAdFixtures.Placements(),
                RewardedAdFixtures.Service(), null, reporter, new RewardedAdFixtures.CapStore(),
                () => "2026-08-29");
            missingProvider.Start();
            Assert.That(missingProvider.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable));

            using var missingReporter = new RewardedAdCoordinator(RewardedAdFixtures.Placements(),
                RewardedAdFixtures.Service(), provider, null, new RewardedAdFixtures.CapStore(),
                () => "2026-08-29");
            missingReporter.Start();
            Assert.That(missingReporter.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable));

            using var missingCaps = new RewardedAdCoordinator(RewardedAdFixtures.Placements(),
                RewardedAdFixtures.Service(), provider, reporter, null, () => "2026-08-29");
            missingCaps.Start();
            Assert.That(missingCaps.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable));

            Assert.That(provider.Shows, Is.Empty);
        }

        [Test]
        public void ReadyTap_CreatesOneExactAttemptAndSecondOpenTapIsBusy()
        {
            var provider = new RewardedAdFixtures.Provider();
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Start();

            Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Started));
            Assert.That(coordinator.Show("p1"), Is.EqualTo(RewardedShowOutcome.Busy));
            Assert.That(provider.Shows, Has.Count.EqualTo(1));
            Assert.That(provider.Shows[0].AttemptId, Is.GreaterThan(0));
            Assert.That(provider.Shows[0].PlacementId, Is.EqualTo("p0"));
        }

        [Test]
        public void SynchronousDisplayFailureDuringRejectedShow_ReloadsExactlyOnce()
        {
            var provider = new RewardedAdFixtures.Provider { ShowAccepted = false };
            provider.OnShow = (attemptId, placementId) => provider.Emit(
                new RewardedAdEvent(RewardedAdEventKind.DisplayFailed, attemptId, placementId));
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Start();
            Assert.That(provider.LoadCalls, Is.EqualTo(1));

            Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable));

            Assert.That(provider.LoadCalls, Is.EqualTo(2),
                "the callback terminal path already owns the single reload");
        }

        [Test]
        public void OnlyMatchingRewardGrants_CloseAloneAndUnknownOrDuplicateCallbacksNeverGrant()
        {
            var provider = new RewardedAdFixtures.Provider();
            var service = RewardedAdFixtures.Service();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt + 99, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False);

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "wrong"));
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False,
                "a terminal callback must match both the exact attempt and placement");
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(service.Ledger.ExportLeases(), Has.Count.EqualTo(1));
        }

        [Test]
        public void CloseBeforeReward_KeepsExactPlacementPendingButReleasesProviderSlot()
        {
            var provider = new RewardedAdFixtures.Provider();
            var service = RewardedAdFixtures.Service();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service);
            coordinator.Start();
            coordinator.Show("p0");
            long first = provider.Shows.Single().AttemptId;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, first, "p0"));

            Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable));
            Assert.That(coordinator.Show("p1"), Is.EqualTo(RewardedShowOutcome.Started));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, first, "p0"));
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True,
                "the old exact ID, not the newest attempt, owns the late reward");
        }

        [Test]
        public void WrongPlacementClose_DoesNotReleaseTheOpenAttempt()
        {
            var provider = new RewardedAdFixtures.Provider();
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Start();
            Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Started));
            long attempt = provider.Shows.Single().AttemptId;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p1"));

            Assert.That(coordinator.Show("p1"), Is.EqualTo(RewardedShowOutcome.Busy),
                "a foreign terminal event must not release the actual open attempt");
        }

        [Test]
        public void WrongPlacementDisplayFailure_DoesNotDiscardTheExactAttempt()
        {
            var provider = new RewardedAdFixtures.Provider();
            var service = RewardedAdFixtures.Service();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service);
            coordinator.Start();
            Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Started));
            long attempt = provider.Shows.Single().AttemptId;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.DisplayFailed, attempt, "p1"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));

            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True,
                "the exact attempt remains eligible when a foreign failure arrives first");
        }

        [Test]
        public void RewardLatchIsSetBeforeGrantSoReentrantDuplicateCannotGrantTwice()
        {
            var provider = new RewardedAdFixtures.Provider();
            var persistence = new RewardedAdFixtures.LeasePersistence();
            var service = RewardedAdFixtures.Service(persistence);
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;
            persistence.OnPersist = () => provider.Emit(
                new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));

            Assert.That(persistence.Calls, Is.EqualTo(1));
            Assert.That(service.Ledger.ExportLeases(), Has.Count.EqualTo(1));
        }

        [Test]
        public void DisplayFailureNeverGrantsAndCoordinatorOwnsTheReload()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter();
            var service = RewardedAdFixtures.Service();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, reporter, service: service);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;
            int loadsBefore = provider.LoadCalls;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.DisplayFailed, attempt, "p0",
                errorCode: 509));

            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False);
            Assert.That(provider.LoadCalls, Is.EqualTo(loadsBefore + 1));
            Assert.That(reporter.Events.Any(e => e.Kind == RewardedAdEventKind.DisplayFailed), Is.False);
            Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Started));
        }

        [Test]
        public void LoadFailureChangesAvailabilityAndForwardsWithoutThrowing()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, reporter);
            int changes = 0;
            coordinator.AvailabilityChanged += () => changes++;
            coordinator.Start();
            provider.IsReady = false;

            Assert.DoesNotThrow(() => provider.Emit(new RewardedAdEvent(
                RewardedAdEventKind.LoadFailed, errorCode: 204)));

            Assert.That(coordinator.CanShow("p0"), Is.False);
            Assert.That(changes, Is.GreaterThan(0));
            Assert.That(reporter.Events.Select(e => e.Kind), Does.Contain(RewardedAdEventKind.LoadFailed));
        }

        [Test]
        public void LoadFailure_DelaysRetry_DuplicateDoesNotMoveDeadline_AndSyncFailureBacksOff()
        {
            var provider = new RewardedAdFixtures.Provider();
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Tick(100d);
            coordinator.Start();

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));
            Assert.That(provider.LoadCalls, Is.EqualTo(1),
                "the callback must not synchronously recurse into Load");
            coordinator.Tick(100d);

            coordinator.Tick(101d);
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));
            coordinator.Tick(101.999d);
            Assert.That(provider.LoadCalls, Is.EqualTo(1),
                "a duplicate callback must not move or accelerate the one pending retry");

            provider.OnLoad = () =>
            {
                provider.OnLoad = null;
                provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed,
                    errorCode: 204));
            };
            coordinator.Tick(102d);
            Assert.That(provider.LoadCalls, Is.EqualTo(2),
                "the exact first deadline issues one retry");
            coordinator.Tick(102d);

            coordinator.Tick(105.999d);
            Assert.That(provider.LoadCalls, Is.EqualTo(2),
                "a synchronous retry failure schedules a later attempt without recursion");
            coordinator.Tick(106d);
            Assert.That(provider.LoadCalls, Is.EqualTo(3),
                "the second failure uses the four-second exponential delay");
        }

        [Test]
        public void LoadFailureAfterClockGap_AnchorsFromObservationTickNotStaleSample()
        {
            var provider = new RewardedAdFixtures.Provider();
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Tick(100d);
            coordinator.Start();

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));
            coordinator.Tick(500d);
            Assert.That(provider.LoadCalls, Is.EqualTo(1),
                "the observation frame anchors the delay; stale t=100 cannot make it due");
            coordinator.Tick(501.999d);
            Assert.That(provider.LoadCalls, Is.EqualTo(1));
            coordinator.Tick(502d);
            Assert.That(provider.LoadCalls, Is.EqualTo(2));
        }

        [Test]
        public void LoadFailureBeforeFirstTick_AnchorsAtFirstValidTimeWithoutImmediateRetry()
        {
            var provider = new RewardedAdFixtures.Provider();
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Start();
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));

            coordinator.Tick(double.NaN);
            coordinator.Tick(double.PositiveInfinity);
            coordinator.Tick(100d);
            Assert.That(provider.LoadCalls, Is.EqualTo(1),
                "the first valid sample anchors an unobserved clock; it is not itself due");
            coordinator.Tick(101.999d);
            Assert.That(provider.LoadCalls, Is.EqualTo(1));
            coordinator.Tick(102d);
            Assert.That(provider.LoadCalls, Is.EqualTo(2));
        }

        [Test]
        public void Loaded_CancelsPendingRetry_AndResetsBackoffToInitialDelay()
        {
            var provider = new RewardedAdFixtures.Provider();
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Tick(10d);
            coordinator.Start();

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Loaded));
            coordinator.Tick(12d);
            Assert.That(provider.LoadCalls, Is.EqualTo(1),
                "a successful load cancels the stale pending retry");

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));
            coordinator.Tick(12d);
            coordinator.Tick(13.999d);
            Assert.That(provider.LoadCalls, Is.EqualTo(1));
            coordinator.Tick(14d);
            Assert.That(provider.LoadCalls, Is.EqualTo(2),
                "success resets the next failure to the initial two-second delay");
        }

        [Test]
        public void RepeatedLoadFailures_UseExponentialBackoffCappedAtThirtySeconds()
        {
            var provider = new RewardedAdFixtures.Provider();
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Tick(0d);
            coordinator.Start();
            provider.OnLoad = () => provider.Emit(new RewardedAdEvent(
                RewardedAdEventKind.LoadFailed, errorCode: 204));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));
            coordinator.Tick(0d);

            var deadlines = new[] { 2d, 6d, 14d, 30d, 60d, 90d, 120d };
            for (int i = 0; i < deadlines.Length; i++)
            {
                coordinator.Tick(deadlines[i] - 0.001d);
                Assert.That(provider.LoadCalls, Is.EqualTo(i + 1));
                coordinator.Tick(deadlines[i]);
                Assert.That(provider.LoadCalls, Is.EqualTo(i + 2));
                coordinator.Tick(deadlines[i]);
            }
        }

        [Test]
        public void InvalidOrRegressiveTick_CannotTriggerOrMovePendingRetry()
        {
            var provider = new RewardedAdFixtures.Provider();
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Tick(10d);
            coordinator.Start();
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));
            coordinator.Tick(10d);

            coordinator.Tick(double.NaN);
            coordinator.Tick(double.PositiveInfinity);
            coordinator.Tick(double.NegativeInfinity);
            coordinator.Tick(-1d);
            coordinator.Tick(9d);
            coordinator.Tick(11.999d);
            Assert.That(provider.LoadCalls, Is.EqualTo(1));

            coordinator.Tick(12d);
            Assert.That(provider.LoadCalls, Is.EqualTo(2));
        }

        [Test]
        public void Dispose_CancelsPendingLoadRetry_AndLaterTicksDoNothing()
        {
            var provider = new RewardedAdFixtures.Provider();
            var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Tick(0d);
            coordinator.Start();
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));
            coordinator.Tick(0d);

            coordinator.Dispose();
            coordinator.Tick(2d);

            Assert.That(provider.LoadCalls, Is.EqualTo(1));
            Assert.That(provider.DisposeCalls, Is.EqualTo(1));
        }

        [Test]
        public void OnlyApprovedLifecycleKindsReachReporterAndIdsRemainDistinct()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, reporter);
            coordinator.Start();
            var forwarded = new[]
            {
                RewardedAdEventKind.Loaded,
                RewardedAdEventKind.Displayed,
                RewardedAdEventKind.Opened,
                RewardedAdEventKind.LoadFailed,
                RewardedAdEventKind.Revenue,
            };
            foreach (var kind in forwarded)
            {
                provider.Emit(new RewardedAdEvent(kind, 7, "p0", "unit", "ad-17", "auction-29",
                    "network", 12, 345L, "USD", AdRevenuePrecision.Exact));
            }
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, 99, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, 99, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.DisplayFailed, 99, "p0"));

            Assert.That(reporter.Events.Select(e => e.Kind), Is.EqualTo(forwarded));
            Assert.That(reporter.Events[0].AdUnitId, Is.EqualTo("unit"));
            Assert.That(reporter.Events[0].AdId, Is.EqualTo("ad-17"));
            Assert.That(reporter.Events[0].AuctionId, Is.EqualTo("auction-29"));
        }

        [Test]
        public void ReporterFailureFailsFutureOffersClosedButCannotRevokeEarnedReward()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter();
            var service = RewardedAdFixtures.Service();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, reporter, service: service);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;
            reporter.ThrowOnReport = true;

            Assert.DoesNotThrow(() => provider.Emit(new RewardedAdEvent(
                RewardedAdEventKind.Displayed, attempt, "p0")));
            reporter.SetReady(false);
            Assert.DoesNotThrow(() => provider.Emit(new RewardedAdEvent(
                RewardedAdEventKind.Rewarded, attempt, "p0")));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));

            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(coordinator.CanShow("p1"), Is.False);
        }

        [Test]
        public void ProviderExceptionsNeverBubbleAndFailFutureOffersClosed()
        {
            var provider = new RewardedAdFixtures.Provider { ThrowOnShow = true };
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Start();

            RewardedShowOutcome outcome = default;
            Assert.DoesNotThrow(() => outcome = coordinator.Show("p0"));
            Assert.That(outcome, Is.EqualTo(RewardedShowOutcome.Unavailable));
            Assert.That(coordinator.CanShow("p0"), Is.False);
        }

        [Test]
        public void ProviderReadinessExceptionPermanentlyFailsFutureOffersClosed()
        {
            var provider = new RewardedAdFixtures.Provider { ThrowOnReady = true };
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Start();

            Assert.DoesNotThrow(() => coordinator.CanShow("p0"));
            provider.ThrowOnReady = false;

            Assert.That(coordinator.CanShow("p0"), Is.False,
                "a vendor exception cannot silently recover into serving an untrusted future offer");
        }

        [Test]
        public void FailedLocalDateCapWriteBlocksSameDateAfterLeaseExpiryButNotTheNextDate()
        {
            var provider = new RewardedAdFixtures.Provider();
            var caps = new RewardedAdFixtures.CapStore { Accept = false };
            var persistence = new RewardedAdFixtures.LeasePersistence();
            var clock = new RewardedAdFixtures.Clock();
            var localDate = new RewardedAdFixtures.LocalDate();
            var service = RewardedAdFixtures.Service(persistence, clock.Read);
            var placements = RewardedAdFixtures.Placements(
                ", \"caps\": { \"localDate\": 1 }");
            using var coordinator = new RewardedAdCoordinator(placements, service, provider,
                new RewardedAdFixtures.Reporter(), caps, localDate.Read);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));

            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(caps.IncrementCalls, Is.EqualTo(1));
            Assert.That(caps.ReadLocalDateCount("p0", "2026-08-29"), Is.Zero);
            clock.Advance(3_601L);
            Assert.That(coordinator.CanShow("p0"), Is.False,
                "failed local-date persistence consumes the opportunity after the lease expires");

            localDate.Key = "2026-08-30";
            Assert.That(coordinator.CanShow("p0"), Is.True,
                "date-A compensation must not become a permanent placement cap on date B");
        }

        [Test]
        public void GrantedRewardAdvancesSessionCapAfterLeaseExpiry()
        {
            var provider = new RewardedAdFixtures.Provider();
            var clock = new RewardedAdFixtures.Clock();
            var service = RewardedAdFixtures.Service(clock: clock.Read);
            var placements = RewardedAdFixtures.Placements(
                ", \"caps\": { \"session\": 1 }");
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service,
                placements: placements);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));

            clock.Advance(3_601L);

            Assert.That(service.CanOfferAdFor("outfit_conductor"), Is.True,
                "the lease must be expired so this assertion isolates the session cap");
            Assert.That(coordinator.CanShow("p0"), Is.False);
        }

        [Test]
        public void ThrowingLedgerObserver_StillAdvancesSessionCapAfterDurableGrantedRewardExpires()
        {
            var provider = new RewardedAdFixtures.Provider();
            var clock = new RewardedAdFixtures.Clock();
            var ledger = new EntitlementLedger();
            ledger.Changed += () => throw new InvalidOperationException("injected observer fault");
            var service = new PurchaseService(Purchases.PFixtures.TinyCatalog(), clock: clock.Read,
                ledger: ledger);
            service.AttachLeasePersistence(new RewardedAdFixtures.LeasePersistence());
            var placements = RewardedAdFixtures.Placements(
                ", \"caps\": { \"session\": 1 }");
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service,
                placements: placements);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));

            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True);
            clock.Advance(3_601L);
            Assert.That(service.CanOfferAdFor("outfit_conductor"), Is.True,
                "the lease must be expired so this assertion isolates the session cap");
            Assert.That(coordinator.CanShow("p0"), Is.False,
                "a durable granted reward advances the authored session cap despite observer faults");
        }

        [Test]
        public void MissingRewardTimeDateConservativelyBlocksLaterDateInThisSession()
        {
            var provider = new RewardedAdFixtures.Provider();
            var clock = new RewardedAdFixtures.Clock();
            var localDate = new RewardedAdFixtures.LocalDate();
            var service = RewardedAdFixtures.Service(clock: clock.Read);
            var placements = RewardedAdFixtures.Placements(
                ", \"caps\": { \"localDate\": 1 }");
            using var coordinator = new RewardedAdCoordinator(placements, service, provider,
                new RewardedAdFixtures.Reporter(), new RewardedAdFixtures.CapStore(),
                localDate.Read);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;
            localDate.ThrowOnRead = true;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));
            clock.Advance(3_601L);
            localDate.ThrowOnRead = false;
            localDate.Key = "2026-08-30";

            Assert.That(service.CanOfferAdFor("outfit_conductor"), Is.True);
            Assert.That(coordinator.CanShow("p0"), Is.False,
                "an unscoped failed increment must remain conservative for this coordinator session");
        }

        [Test]
        public void PersistenceFailedGrantDoesNotAdvanceEitherCap()
        {
            var provider = new RewardedAdFixtures.Provider();
            var caps = new RewardedAdFixtures.CapStore();
            var persistence = new RewardedAdFixtures.LeasePersistence();
            var service = RewardedAdFixtures.Service(persistence);
            var placements = RewardedAdFixtures.Placements(
                ", \"caps\": { \"session\": 1, \"localDate\": 1 }");
            using var coordinator = RewardedAdFixtures.Coordinator(provider, caps: caps,
                service: service, placements: placements);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;
            persistence.Accept = false;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));

            Assert.That(caps.IncrementCalls, Is.Zero);
            Assert.That(coordinator.CanShow("p0"), Is.True);
        }

        [Test]
        public void RetainedClosedAttemptsAreBoundedAndEvictionNeverGrants()
        {
            const int retainedLimit = 16;
            var provider = new RewardedAdFixtures.Provider();
            var service = RewardedAdFixtures.Service();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service,
                placements: RewardedAdFixtures.Placements(count: retainedLimit + 1));
            coordinator.Start();
            long firstAttempt = 0;
            for (int i = 0; i <= retainedLimit; i++)
            {
                Assert.That(coordinator.Show("p" + i), Is.EqualTo(RewardedShowOutcome.Started));
                long attempt = provider.Shows.Last().AttemptId;
                if (i == 0) firstAttempt = attempt;
                provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p" + i));
            }

            Assert.That(coordinator.CanShow("p0"), Is.True,
                "FIFO no-grant eviction bounds callbacks that never arrive");
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, firstAttempt, "p0"));
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False);
        }

        [Test]
        public void ClosedWithoutReward_BlocksReuseButStillGrantsExactlyOnceWithinGrace()
        {
            var provider = new RewardedAdFixtures.Provider();
            var persistence = new RewardedAdFixtures.LeasePersistence();
            var service = RewardedAdFixtures.Service(persistence);
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service,
                placements: RewardedAdFixtures.Placements(count: 1));
            coordinator.Tick(100d);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));
            coordinator.Tick(100d);
            coordinator.Tick(399.999d);
            Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Unavailable),
                "the exact closed attempt owns its placement throughout the grace window");

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));

            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(persistence.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ClosedWithoutReward_ExpiresAtGrace_ReopensPlacement_AndRejectsStaleReward()
        {
            var provider = new RewardedAdFixtures.Provider();
            var service = RewardedAdFixtures.Service();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service,
                placements: RewardedAdFixtures.Placements(count: 1));
            int availabilityChanges = 0;
            coordinator.AvailabilityChanged += () => availabilityChanges++;
            coordinator.Tick(100d);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));
            int changesAfterClose = availabilityChanges;
            coordinator.Tick(100d);

            coordinator.Tick(399.999d);
            Assert.That(coordinator.CanShow("p0"), Is.False);
            Assert.That(availabilityChanges, Is.EqualTo(changesAfterClose));

            coordinator.Tick(400d);
            Assert.That(coordinator.CanShow("p0"), Is.True);
            Assert.That(availabilityChanges, Is.EqualTo(changesAfterClose + 1),
                "expiry must publish the newly available placement");

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False,
                "a reward arriving after the grace boundary has no retained owner");
            Assert.That(coordinator.Show("p0"), Is.EqualTo(RewardedShowOutcome.Started));
            Assert.That(provider.Shows.Last().AttemptId, Is.Not.EqualTo(attempt));
        }

        [Test]
        public void ClosedBeforeFirstTick_ReceivesFullGraceFromFirstValidMonotonicSample()
        {
            var provider = new RewardedAdFixtures.Provider();
            var service = RewardedAdFixtures.Service();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service,
                placements: RewardedAdFixtures.Placements(count: 1));
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));

            coordinator.Tick(400d);
            coordinator.Tick(699.999d);
            Assert.That(coordinator.CanShow("p0"), Is.False,
                "a high-uptime first sample must not consume grace measured from an invented zero");

            coordinator.Tick(700d);
            Assert.That(coordinator.CanShow("p0"), Is.True);
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False);
        }

        [Test]
        public void ClosedAfterClockGap_ReceivesFullGraceFromObservationTick()
        {
            var provider = new RewardedAdFixtures.Provider();
            var service = RewardedAdFixtures.Service();
            using var coordinator = RewardedAdFixtures.Coordinator(provider, service: service,
                placements: RewardedAdFixtures.Placements(count: 1));
            coordinator.Tick(100d);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));

            coordinator.Tick(500d);
            coordinator.Tick(799.999d);
            Assert.That(coordinator.CanShow("p0"), Is.False,
                "the observation frame, not stale t=100, starts the full grace period");
            coordinator.Tick(800d);
            Assert.That(coordinator.CanShow("p0"), Is.True);

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False);
        }

        [Test]
        public void Tick_ServicesDueLoadRetryAndRetainedExpiryWithoutStarvingEither()
        {
            var provider = new RewardedAdFixtures.Provider();
            using var coordinator = RewardedAdFixtures.Coordinator(provider,
                placements: RewardedAdFixtures.Placements(count: 1));
            coordinator.Tick(0d);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed, errorCode: 204));
            Assert.That(provider.LoadCalls, Is.EqualTo(2));
            Assert.That(coordinator.CanShow("p0"), Is.False);
            coordinator.Tick(0d);

            coordinator.Tick(300d);

            Assert.That(provider.LoadCalls, Is.EqualTo(3),
                "the overdue background load must run on this tick");
            Assert.That(coordinator.CanShow("p0"), Is.True,
                "the retained attempt must expire on the same tick, not a later frame");
        }

        [Test]
        public void DisposeIsIdempotentAndStopsCallbacks()
        {
            var provider = new RewardedAdFixtures.Provider();
            var service = RewardedAdFixtures.Service();
            var coordinator = RewardedAdFixtures.Coordinator(provider, service: service);
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;

            coordinator.Dispose();
            coordinator.Dispose();
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));

            Assert.That(provider.DisposeCalls, Is.EqualTo(1));
            Assert.That(service.IsUnlocked("outfit_conductor"), Is.False);
        }

        [Test]
        public void ThrowingProviderEventRemoveDoesNotSkipReporterCleanupOrProviderDispose()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter();
            var coordinator = RewardedAdFixtures.Coordinator(provider, reporter);
            coordinator.Start();
            provider.ThrowOnEventRemove = true;

            Assert.DoesNotThrow(coordinator.Dispose);
            Assert.DoesNotThrow(coordinator.Dispose);

            Assert.That(provider.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(reporter.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(provider.DisposeCalls, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingReporterEventRemoveDoesNotSkipProviderDispose()
        {
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter();
            var coordinator = RewardedAdFixtures.Coordinator(provider, reporter);
            coordinator.Start();
            reporter.ThrowOnEventRemove = true;

            Assert.DoesNotThrow(coordinator.Dispose);
            Assert.DoesNotThrow(coordinator.Dispose);

            Assert.That(provider.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(reporter.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(provider.DisposeCalls, Is.EqualTo(1));
        }

        private static void AssertImmediateExactFailure(RewardedAdCoordinator coordinator,
            string placementId, string entitlementId, RewardedShowOutcome expectedOutcome)
        {
            var completions = new List<RewardedAdCompletion>();

            Assert.That(coordinator.Show(placementId, entitlementId, completions.Add),
                Is.EqualTo(expectedOutcome));

            Assert.That(completions, Has.Count.EqualTo(1));
            Assert.That(completions[0].AttemptId, Is.Zero,
                "a pre-attempt rejection must not invent an attempt identity");
            Assert.That(completions[0].PlacementId, Is.EqualTo(placementId));
            Assert.That(completions[0].EntitlementId, Is.EqualTo(entitlementId));
            Assert.That(completions[0].Kind,
                Is.EqualTo(RewardedAdCompletionKind.Unavailable));
        }

        private static void AssertRejectedExactShow(RewardedAdFixtures.Provider provider)
        {
            using var coordinator = RewardedAdFixtures.Coordinator(provider);
            coordinator.Start();
            var completions = new List<RewardedAdCompletion>();

            Assert.That(coordinator.Show("p0", "outfit_conductor", completions.Add),
                Is.EqualTo(RewardedShowOutcome.Unavailable));

            AssertExactCompletion(completions, RewardedAdCompletionKind.DisplayFailed,
                attemptMustExist: true);
            Assert.That(provider.Shows, Has.Count.EqualTo(1));
            Assert.That(completions[0].AttemptId, Is.EqualTo(provider.Shows[0].AttemptId));
        }

        private static void AssertExactCompletion(IReadOnlyList<RewardedAdCompletion> completions,
            RewardedAdCompletionKind kind, bool attemptMustExist)
        {
            Assert.That(completions, Has.Count.EqualTo(1));
            Assert.That(completions[0].AttemptId,
                attemptMustExist ? Is.GreaterThan(0L) : Is.Zero);
            Assert.That(completions[0].PlacementId, Is.EqualTo("p0"));
            Assert.That(completions[0].EntitlementId, Is.EqualTo("outfit_conductor"));
            Assert.That(completions[0].Kind, Is.EqualTo(kind));
        }
    }
}
