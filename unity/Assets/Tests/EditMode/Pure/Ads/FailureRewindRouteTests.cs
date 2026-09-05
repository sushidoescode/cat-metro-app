using System;
using System.Collections.Generic;
using CatMetro.Services.Ads;
using CatMetro.Services.Retry;
using NUnit.Framework;

namespace CatMetro.Tests.Ads
{
    public sealed class FailureRewindRouteTests
    {
        [SetUp]
        public void SetUp() => RewardedAdRuntime.ResetForTests();
        [TearDown]
        public void TearDown() => RewardedAdRuntime.ResetForTests();

        [Test]
        public void NoProviderHasNoFailureCapabilityAndRouteFailsClosed()
        {
            Assert.That(RewardedAdRuntime.Current, Is.Not.InstanceOf<IRewardedAdFailureRewindSource>());
            using var route = new RewardedAdFailureRewindRoute();
            var results = new List<FailureRewindAdCompletion>();
            Assert.That(route.CanOffer("rewind_failure"), Is.False);
            route.Request("rewind_failure", _ => Assert.Fail("no ad displayed"), results.Add);
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Unavailable));
        }

        [Test]
        public void RuntimeInstallAndProviderReadinessRaiseAvailability()
        {
            using var route = new RewardedAdFailureRewindRoute();
            int changes = 0;
            route.AvailabilityChanged += () => changes++;
            using var f = new FailureFixture();
            RewardedAdRuntime.Install(f.Coordinator);
            Assert.That(route.CanOffer("rewind_failure"), Is.True);
            int before = changes;
            f.Provider.IsReady = false;
            f.Provider.Emit(new RewardedAdEvent(RewardedAdEventKind.LoadFailed));
            Assert.That(changes, Is.GreaterThan(before));
            Assert.That(route.CanOffer("rewind_failure"), Is.False);
        }

        [Test]
        public void RequestRelaysDisplayedAndExactTerminalMetadataOnlyOnce()
        {
            using var f = new FailureFixture();
            RewardedAdRuntime.Install(f.Coordinator);
            using var route = new RewardedAdFailureRewindRoute();
            route.Request("rewind_failure", f.Lifecycle.Add, f.Results.Add);
            Assert.That(f.Lifecycle, Is.Empty);
            long attempt = f.Provider.Shows[0].AttemptId;
            f.Emit(RewardedAdEventKind.Displayed, attempt);
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            f.Emit(RewardedAdEventKind.Closed, attempt);
            Assert.That(f.Lifecycle, Has.Count.EqualTo(1));
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Granted));
            Assert.That(f.Results[0].AdUnitId, Is.EqualTo("actual-unit"));
            Assert.That(f.Results[0].NetworkName, Is.EqualTo("actual-network"));
            Assert.That(f.SessionUsed, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RuntimeReplacementOrUninstallAbandonsOldAttemptBeforeLateReward(bool uninstall)
        {
            using var old = new FailureFixture();
            using var next = new FailureFixture();
            RewardedAdRuntime.Install(old.Coordinator);
            using var route = new RewardedAdFailureRewindRoute();
            route.Request("rewind_failure", old.Lifecycle.Add, old.Results.Add);
            long attempt = old.Provider.Shows[0].AttemptId;
            if (uninstall) RewardedAdRuntime.Uninstall(old.Coordinator);
            else RewardedAdRuntime.Install(next.Coordinator);
            old.Emit(RewardedAdEventKind.Displayed, attempt);
            old.Emit(RewardedAdEventKind.Rewarded, attempt);
            Assert.That(old.Results, Has.Count.EqualTo(1));
            Assert.That(old.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Cancelled));
            Assert.That(old.Lifecycle, Is.Empty);
            Assert.That(old.SessionUsed, Is.Zero);
            Assert.That(route.CanOffer("rewind_failure"), Is.EqualTo(!uninstall));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelOrDisposeRemovesAttemptAndIgnoresLateProviderReward(bool dispose)
        {
            using var f = new FailureFixture();
            RewardedAdRuntime.Install(f.Coordinator);
            using var route = new RewardedAdFailureRewindRoute();
            route.Request("rewind_failure", f.Lifecycle.Add, f.Results.Add);
            long attempt = f.Provider.Shows[0].AttemptId;
            if (dispose) route.Dispose(); else route.Cancel();
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Cancelled));
            Assert.That(f.SessionUsed, Is.Zero);
        }

        [Test]
        public void CancelDuringSynchronousDisplayedStillAbandonsReturnedAttempt()
        {
            using var f = new FailureFixture();
            RewardedAdRuntime.Install(f.Coordinator);
            using var route = new RewardedAdFailureRewindRoute();
            f.Provider.OnShow = (id, _) => f.Emit(RewardedAdEventKind.Displayed, id);
            route.Request("rewind_failure", _ => route.Cancel(), f.Results.Add);
            long attempt = f.Provider.Shows[0].AttemptId;
            f.Emit(RewardedAdEventKind.Rewarded, attempt);
            Assert.That(f.Results, Has.Count.EqualTo(1));
            Assert.That(f.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Cancelled));
            Assert.That(f.SessionUsed, Is.Zero);
        }

        [Test]
        public void RuntimeReplacementInsideShowBeforeDisplayedPreventsSameStackLateReward()
        {
            using var old = new FailureFixture();
            using var next = new FailureFixture();
            RewardedAdRuntime.Install(old.Coordinator);
            using var route = new RewardedAdFailureRewindRoute();
            old.Provider.OnShow = (attempt, _) =>
            {
                RewardedAdRuntime.Install(next.Coordinator);
                old.Emit(RewardedAdEventKind.Rewarded, attempt);
            };
            route.Request("rewind_failure", old.Lifecycle.Add, old.Results.Add);
            Assert.That(old.Results, Has.Count.EqualTo(1));
            Assert.That(old.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Cancelled));
            Assert.That(old.SessionUsed, Is.Zero);
            Assert.That(old.DailyUsed, Is.Zero);
            Assert.That(old.Results[0].AttemptId, Is.EqualTo(old.Provider.Shows[0].AttemptId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancellationDuringSecondReadinessNeverShowsOrConsumes(bool replaceRuntime)
        {
            using var old = new FailureFixture();
            using var next = new FailureFixture();
            RewardedAdRuntime.Install(old.Coordinator);
            using var route = new RewardedAdFailureRewindRoute();
            int readinessChecks = 0;
            old.Provider.OnPlacementReadinessCheck = _ =>
            {
                if (++readinessChecks != 2) return;
                if (replaceRuntime) RewardedAdRuntime.Install(next.Coordinator);
                else route.Cancel();
            };
            old.Provider.OnShow = (attempt, _) => old.Emit(RewardedAdEventKind.Rewarded, attempt);

            route.Request("rewind_failure", old.Lifecycle.Add, old.Results.Add);

            Assert.That(old.SessionUsed, Is.Zero);
            Assert.That(old.DailyUsed, Is.Zero);
            Assert.That(old.Provider.Shows, Is.Empty);
            Assert.That(old.Results, Has.Count.EqualTo(1));
            Assert.That(old.Results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Cancelled));
            Assert.That(old.Lifecycle, Is.Empty);
            Assert.That(route.CanOffer("rewind_failure"), Is.True);
        }

        [Test]
        public void ConcurrentRequestCannotReplaceFirstAndStaleCallbackCannotCompleteNext()
        {
            var source = new DeferredSource();
            RewardedAdRuntime.Install(source);
            using var route = new RewardedAdFailureRewindRoute();
            var first = new List<FailureRewindAdCompletion>();
            var denied = new List<FailureRewindAdCompletion>();
            var next = new List<FailureRewindAdCompletion>();
            route.Request("rewind_failure", null, first.Add);
            route.Request("rewind_failure", null, denied.Add);
            Assert.That(denied[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Unavailable));
            source.Completions[0](new FailureRewindAdCompletion(1, "rewind_failure",
                RewardedAdCompletionKind.ClosedWithoutReward));
            route.Request("rewind_failure", null, next.Add);
            source.Completions[0](new FailureRewindAdCompletion(1, "rewind_failure",
                RewardedAdCompletionKind.Granted));
            Assert.That(first, Has.Count.EqualTo(1));
            Assert.That(next, Is.Empty);
            source.Completions[1](new FailureRewindAdCompletion(2, "rewind_failure",
                RewardedAdCompletionKind.Granted));
            Assert.That(next[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Granted));
        }

        [TestCase("wrong", 1)]
        [TestCase("rewind_failure", 99)]
        [TestCase("rewind_failure", 0)]
        public void ForeignTerminalNeverGrantsAndAbandonsTheRealAttempt(string placement, int id)
        {
            var source = new DeferredSource();
            RewardedAdRuntime.Install(source);
            using var route = new RewardedAdFailureRewindRoute();
            var results = new List<FailureRewindAdCompletion>();
            route.Request("rewind_failure", null, results.Add);
            source.Completions[0](new FailureRewindAdCompletion(id, placement,
                RewardedAdCompletionKind.Granted));
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Cancelled));
            Assert.That(source.Abandoned, Is.EqualTo(new[] { 1L }));
        }

        [Test]
        public void ReplacementDuringReadinessNeverShowsOnOldSource()
        {
            var old = new DeferredSource();
            var next = new DeferredSource();
            RewardedAdRuntime.Install(old);
            using var route = new RewardedAdFailureRewindRoute();
            old.OnCanShow = () => RewardedAdRuntime.Install(next);
            var results = new List<FailureRewindAdCompletion>();
            route.Request("rewind_failure", null, results.Add);
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Kind, Is.EqualTo(RewardedAdCompletionKind.Cancelled));
            Assert.That(old.Completions, Is.Empty);
            Assert.That(next.Completions, Is.Empty);
            Assert.That(route.CanOffer("rewind_failure"), Is.True);
        }

        private sealed class DeferredSource : IRewardedAds, IRewardedAdFailureRewindSource
        {
            public event Action AvailabilityChanged { add { } remove { } }
            public readonly List<Action<FailureRewindAdCompletion>> Completions =
                new List<Action<FailureRewindAdCompletion>>();
            public readonly List<long> Abandoned = new List<long>();
            public Action OnCanShow;
            public bool CanShow(string placementId) => false;
            public RewardedShowOutcome Show(string placementId) => RewardedShowOutcome.Unavailable;
            public bool CanShowFailureRewind(string placementId) { OnCanShow?.Invoke(); return true; }
            public RewardedShowOutcome ShowFailureRewind(string placementId,
                Action<RewardedAdEvent> lifecycle, Action<FailureRewindAdCompletion> completed,
                out long attemptId)
            {
                Completions.Add(completed);
                attemptId = Completions.Count;
                return RewardedShowOutcome.Started;
            }
            public void AbandonFailureRewind(long attemptId) => Abandoned.Add(attemptId);
        }
    }
}
