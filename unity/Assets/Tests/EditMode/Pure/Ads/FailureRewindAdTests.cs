using System;
using System.Collections.Generic;
using CatMetro.Application.Save;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using CatMetro.Tests.Save;
using NUnit.Framework;

namespace CatMetro.Tests.Ads
{
    public sealed class FailureRewindAdTests
    {
        [Test]
        public void FailureCapabilityRejectsEntitlementsAndEntitlementApisRejectFailure()
        {
            using var f = new FailureFixture();
            Assert.That(f.Coordinator, Is.InstanceOf<IRewardedAdFailureRewindSource>());
            Assert.That(f.Coordinator.CanShowFailureRewind("rewind_failure"), Is.True);
            Assert.That(f.Coordinator.CanShowFailureRewind("cosmetic"), Is.False);
            Assert.That(f.Coordinator.CanShow("rewind_failure"), Is.False);
            Assert.That(f.Coordinator.CanShow("rewind_failure", "outfit_conductor"), Is.False);
            Assert.That(f.Coordinator.Show("rewind_failure"), Is.EqualTo(RewardedShowOutcome.Unavailable));
            Assert.That(f.Coordinator.Show("rewind_failure", "outfit_conductor", _ => { }),
                Is.EqualTo(RewardedShowOutcome.Unavailable));
            Assert.That(f.Coordinator.ShowFailureRewind("cosmetic", null, f.Results.Add, out _),
                Is.EqualTo(RewardedShowOutcome.Unavailable));
            Assert.That(f.Provider.Shows, Is.Empty);
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Unavailable));
        }

        [Test]
        public void AvailabilityRequiresDurableTouchAndProviderPlacementReadiness()
        {
            using var f = new FailureFixture(touch: false);
            string before = f.Store.State.Payload.ToString();
            Assert.That(f.Coordinator.CanShowFailureRewind("rewind_failure"), Is.False);
            Assert.That(f.Store.State.Payload.ToString(), Is.EqualTo(before));
            f.Coordinator.TouchFailureRewindSession();
            Assert.That(f.Coordinator.CanShowFailureRewind("rewind_failure"), Is.True);
            f.Provider.CappedPlacement = "rewind_failure";
            Assert.That(f.Coordinator.CanShowFailureRewind("rewind_failure"), Is.False);
            f.Provider.CappedPlacement = null;
            f.Reporter.SetReady(false);
            Assert.That(f.Coordinator.CanShowFailureRewind("rewind_failure"), Is.False);
        }

        [Test]
        public void OnlyExactRewardConsumesBothCapsOnceAndReturnsOriginalMetadataWithoutLease()
        {
            using var f = new FailureFixture();
            long attempt = f.Show();
            Assert.That(f.Results, Is.Empty);
            Assert.That(f.Lifecycle, Is.Empty);
            foreach (var kind in new[] { RewardedAdEventKind.Displayed, RewardedAdEventKind.Closed,
                RewardedAdEventKind.DisplayFailed, RewardedAdEventKind.Rewarded })
            {
                f.Provider.Emit(new RewardedAdEvent(kind, attempt + 1, "rewind_failure"));
                f.Provider.Emit(new RewardedAdEvent(kind, attempt, "cosmetic"));
            }
            Assert.That(f.Results, Is.Empty);
            f.Emit(RewardedAdEventKind.Displayed, attempt);
            f.Emit(RewardedAdEventKind.Displayed, attempt);
            Assert.That(f.Lifecycle, Has.Count.EqualTo(1));
            Assert.That(f.Lifecycle[0].AdUnitId, Is.EqualTo("actual-unit"));
            Assert.That(f.Lifecycle[0].NetworkName, Is.EqualTo("actual-network"));
            Assert.That(f.Lifecycle[0].AuctionId, Is.EqualTo("actual-auction"));
            f.Fs.Calls.Clear();
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Granted));
            Assert.That(f.Results[0].AttemptId, Is.EqualTo(attempt));
            Assert.That(f.Results[0].PlacementId, Is.EqualTo("rewind_failure"));
            Assert.That(f.Results[0].AdUnitId, Is.EqualTo("actual-unit"));
            Assert.That(f.Results[0].NetworkName, Is.EqualTo("actual-network"));
            Assert.That(f.SessionUsed, Is.EqualTo(1));
            Assert.That(f.DailyUsed, Is.EqualTo(1));
            Assert.That(f.Fs.Calls, Is.EqualTo(new[] { "WriteTempDurable:save.dat.tmp", "Replace:save.dat" }));
            Assert.That(f.Service.Ledger.ExportLeases(), Is.Empty);
            Assert.That(f.Coordinator.CanShowFailureRewind("rewind_failure"), Is.False,
                "reward delivery does not end the physical ad display");
            f.Emit(RewardedAdEventKind.Closed, attempt);
            Assert.That(f.Coordinator.CanShowFailureRewind("rewind_failure"), Is.True);
        }

        [TestCase(RewardedAdEventKind.Closed, RewardedAdCompletionKind.ClosedWithoutReward)]
        [TestCase(RewardedAdEventKind.DisplayFailed, RewardedAdCompletionKind.DisplayFailed)]
        public void UnearnedTerminalImmediatelyReleasesAttemptAndLateRewardCannotConsume(
            RewardedAdEventKind terminal, RewardedAdCompletionKind expected)
        {
            using var f = new FailureFixture();
            long attempt = f.Show();
            f.Emit(terminal, attempt, error: 503);
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Results[0].Kind, Is.EqualTo(expected));
            Assert.That(f.Results[0].ErrorCode, Is.EqualTo(503));
            Assert.That(f.Results[0].NetworkName, Is.EqualTo("actual-network"));
            Assert.That(f.Results[0].AdUnitId, Is.EqualTo("actual-unit"));
            Assert.That(f.Coordinator.CanShowFailureRewind("rewind_failure"), Is.True);
            long next = f.Show();
            Assert.That(next, Is.GreaterThan(attempt));
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            f.Emit(RewardedAdEventKind.Displayed, attempt);
            f.Emit(terminal, attempt);
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Lifecycle, Is.Empty);
            Assert.That(f.SessionUsed, Is.Zero);
            f.Emit(RewardedAdEventKind.Rewarded, next);
            Assert.That(f.Results, Has.Count.EqualTo(2));
            Assert.That(f.SessionUsed, Is.EqualTo(1));
        }

        [Test]
        public void AbandonOnlyMatchesExactOpenFailureAttemptAndCompletesOnce()
        {
            using var f = new FailureFixture();
            long attempt = f.Show();
            f.Coordinator.AbandonFailureRewind(attempt + 1);
            Assert.That(f.Results, Is.Empty);
            f.Emit(RewardedAdEventKind.Displayed, attempt);
            f.Coordinator.AbandonFailureRewind(attempt);
            f.Coordinator.AbandonFailureRewind(attempt);
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Cancelled));
            Assert.That(f.Results[0].AdUnitId, Is.EqualTo("actual-unit"));
            Assert.That(f.SessionUsed, Is.Zero);
            Assert.That(f.Coordinator.CanShowFailureRewind("rewind_failure"), Is.True);
            Assert.That(f.Coordinator.Show("cosmetic"), Is.EqualTo(RewardedShowOutcome.Started));
            var cosmetic = f.Provider.Shows[1].AttemptId;
            f.Coordinator.AbandonFailureRewind(cosmetic);
            f.Provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, cosmetic, "cosmetic"));
            Assert.That(f.Service.IsUnlocked("outfit_conductor"), Is.True);
        }

        [Test]
        public void DisposeCancelsOnceAndLateCallbackCannotWrite()
        {
            using var f = new FailureFixture();
            long attempt = f.Show();
            f.Coordinator.Dispose();
            f.Coordinator.Dispose();
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Cancelled));
            Assert.That(f.SessionUsed, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProviderRejectOrThrowCompletesOnceAndRemovesAttempt(bool throws)
        {
            using var f = new FailureFixture();
            f.Provider.ShowAccepted = false;
            f.Provider.ThrowOnShow = throws;
            Assert.That(f.Coordinator.ShowFailureRewind("rewind_failure", f.Lifecycle.Add,
                f.Results.Add, out long attempt), Is.EqualTo(RewardedShowOutcome.Unavailable));
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            Assert.That(attempt, Is.GreaterThan(0));
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.DisplayFailed));
            Assert.That(f.SessionUsed, Is.Zero);
        }

        [Test]
        public void SynchronousDisplayFailureKeepsProviderErrorWhenShowAlsoRejects()
        {
            using var f = new FailureFixture();
            f.Provider.ShowAccepted = false;
            f.Provider.OnShow = (id, _) => f.Emit(RewardedAdEventKind.DisplayFailed, id, 701);
            f.Coordinator.ShowFailureRewind("rewind_failure", null, f.Results.Add, out _);
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Results[0].ErrorCode, Is.EqualTo(701));
            Assert.That(f.Results[0].AdUnitId, Is.EqualTo("actual-unit"));
        }

        [Test]
        public void UnavailableOrBusyCompletesOnceWithoutStartingOrConsuming()
        {
            using var f = new FailureFixture();
            f.Provider.IsReady = false;
            Assert.That(f.Coordinator.ShowFailureRewind("rewind_failure", null, f.Results.Add,
                out long unavailable), Is.EqualTo(RewardedShowOutcome.Unavailable));
            Assert.That(unavailable, Is.Zero);
            f.Provider.IsReady = true;
            f.Show();
            Assert.That(f.Coordinator.ShowFailureRewind("rewind_failure", null, f.Results.Add,
                out long busy), Is.EqualTo(RewardedShowOutcome.Busy));
            Assert.That(busy, Is.Zero);
            Assert.That(f.Results, Has.Count.EqualTo(2));
            Assert.That(f.Provider.Shows, Has.Count.EqualTo(1));
            Assert.That(f.SessionUsed, Is.Zero);
        }

        [Test]
        public void CapDenialAfterShowIsGrantFailedAndDoesNotIncrementEitherCounter()
        {
            using var f = new FailureFixture();
            long attempt = f.Show();
            Assert.That(f.Caps.TryConsumeFailureRewind(f.Now, FailureFixture.Today), Is.True);
            Assert.That(f.Caps.TryConsumeFailureRewind(f.Now, FailureFixture.Today), Is.True);
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.GrantFailed));
            Assert.That(f.SessionUsed, Is.EqualTo(2));
            Assert.That(f.DailyUsed, Is.EqualTo(2));
        }

        [Test]
        public void CapWriteFailureRollsBackAndReportsGrantFailedExactlyOnce()
        {
            using var f = new FailureFixture();
            long attempt = f.Show();
            f.Fs.FaultPoint = SFixtures.Fault.InReplace;
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.GrantFailed));
            Assert.That(f.SessionUsed, Is.Zero);
            Assert.That(f.DailyUsed, Is.Zero);
            Assert.That(f.Service.Ledger.ExportLeases(), Is.Empty);
        }

        [Test]
        public void QuickCoordinatorRelaunchRetainsCapAndSessionBoundaryReenablesOffer()
        {
            using var f = new FailureFixture();
            for (int i = 0; i < 2; i++)
            {
                long attempt = f.Show();
                f.Emit(RewardedAdEventKind.Rewarded, attempt);
                f.Emit(RewardedAdEventKind.Closed, attempt);
            }
            f.Coordinator.Dispose();
            var reloaded = SFixtures.Store(f.Root);
            reloaded.Load();
            var caps = new RewardedAdSaveStore(reloaded);
            long now = f.Now + 60;
            using var restarted = new RewardedAdCoordinator(FailureFixture.Placements(), f.Service,
                new RewardedAdFixtures.Provider(), f.Reporter, caps, () => FailureFixture.Today, () => now);
            restarted.Start();
            restarted.TouchFailureRewindSession();
            Assert.That(restarted.CanShowFailureRewind("rewind_failure"), Is.False);
            now += 1799;
            restarted.TouchFailureRewindSession();
            Assert.That(restarted.CanShowFailureRewind("rewind_failure"), Is.False);
            now += 1800;
            restarted.TouchFailureRewindSession();
            Assert.That(restarted.CanShowFailureRewind("rewind_failure"), Is.True);
            Assert.That((int)reloaded.State.Payload["profile"]["sessionCount"], Is.EqualTo(2));
        }

        [Test]
        public void NoProviderTouchAndQueriesNeverMutateTheSave()
        {
            using var f = new FailureFixture(touch: false);
            string before = f.Store.State.Payload.ToString();
            f.Fs.Calls.Clear();
            using var coordinator = new RewardedAdCoordinator(FailureFixture.Placements(), f.Service,
                null, f.Reporter, f.Caps, () => FailureFixture.Today, () => f.Now);
            coordinator.Start();
            coordinator.TouchFailureRewindSession();
            Assert.That(coordinator.CanShowFailureRewind("rewind_failure"), Is.False);
            coordinator.ShowFailureRewind("rewind_failure", null, f.Results.Add, out _);
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Unavailable));
            Assert.That(f.Store.State.Payload.ToString(), Is.EqualTo(before));
            Assert.That(f.Fs.Calls, Is.Empty);
        }
    }

    internal sealed class FailureFixture : IDisposable
    {
        internal const string Today = "2026-09-05";
        internal long Now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        internal readonly SFixtures.TempRoot Root = new SFixtures.TempRoot();
        internal readonly SFixtures.RecordingFs Fs = new SFixtures.RecordingFs();
        internal readonly RewardedAdFixtures.Provider Provider = new RewardedAdFixtures.Provider();
        internal readonly RewardedAdFixtures.Reporter Reporter = new RewardedAdFixtures.Reporter();
        internal readonly List<FailureRewindAdCompletion> Results = new List<FailureRewindAdCompletion>();
        internal readonly List<RewardedAdEvent> Lifecycle = new List<RewardedAdEvent>();
        internal readonly SaveStore Store;
        internal readonly RewardedAdSaveStore Caps;
        internal readonly PurchaseService Service;
        internal readonly RewardedAdCoordinator Coordinator;
        internal int SessionUsed => (int)Store.State.Payload["caps"]["sessionCounters"]["rewind_failure"];
        internal int DailyUsed => (int)Store.State.Payload["caps"]["counters"]["rewind_failure"];

        internal FailureFixture(bool touch = true)
        {
            Store = SFixtures.Store(Root, Fs);
            Store.Load();
            Caps = new RewardedAdSaveStore(Store);
            Service = RewardedAdFixtures.Service(Caps, () => Now);
            Coordinator = new RewardedAdCoordinator(Placements(), Service, Provider, Reporter,
                Caps, () => Today, () => Now);
            Coordinator.Start();
            if (touch) Coordinator.TouchFailureRewindSession();
        }

        internal static RewardedPlacementCatalog Placements() => RewardedPlacementCatalog.Parse(@"{
            ""placements"": [
                { ""id"": ""rewind_failure"", ""rewardKind"": ""failure_rewind"", ""enabled"": true,
                  ""caps"": { ""session"": 2, ""localDate"": 5 } },
                { ""id"": ""cosmetic"", ""entitlement"": ""outfit_conductor"", ""enabled"": true }
            ] }", Purchases.PFixtures.TinyCatalog());

        internal long Show()
        {
            Assert.That(Coordinator.ShowFailureRewind("rewind_failure", Lifecycle.Add, Results.Add,
                out long attempt), Is.EqualTo(RewardedShowOutcome.Started));
            return attempt;
        }

        internal void Emit(RewardedAdEventKind kind, long attempt, int? error = null)
            => Provider.Emit(new RewardedAdEvent(kind, attempt, "rewind_failure", "actual-unit",
                "actual-ad", "actual-auction", "actual-network", error));

        public void Dispose() { Coordinator.Dispose(); Root.Dispose(); }
    }
}
