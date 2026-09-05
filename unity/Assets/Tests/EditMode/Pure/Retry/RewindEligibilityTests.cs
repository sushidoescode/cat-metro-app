using CatMetro.Application.Retry;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Tests.Validation;
using NUnit.Framework;

namespace CatMetro.Tests.Retry
{
    public sealed class RewindEligibilityTests
    {
        [Test]
        public void TryCreateRewindBeforeLastDecision_WithoutAcceptedReceipt_ReturnsFalseWithoutChangingSession()
        {
            var session = new GameSession(L001());
            session.AdvanceMs(3 * TickInterpolator.TICK_MS);
            int tickBefore = session.State.Tick;

            bool created = session.TryCreateRewindBeforeLastDecision(out var rewound);

            Assert.That(created, Is.False);
            Assert.That(rewound, Is.Null);
            Assert.That(session.State.Tick, Is.EqualTo(tickBefore));
            Assert.That(session.Log.Entries.Count, Is.Zero);
            Assert.That(session.HasUsedRewind, Is.False);
        }

        [Test]
        public void TryCreateRewindBeforeLastDecision_ImpossiblePrefixThatTerminatesEarly_IsRejected()
        {
            var session = new GameSession(L001());
            session.Log.Append(new ToggleSwitchCommand(0, 2));
            session.Log.Append(new ToggleSwitchCommand(0, 181));

            bool created = session.TryCreateRewindBeforeLastDecision(out var rewound);

            Assert.That(created, Is.False);
            Assert.That(rewound, Is.Null);
        }

        [Test]
        public void TryCreateRewindBeforeLastDecision_ReplaysThePrefixAndMatchesAnIndependentTerminalReplayDigest()
        {
            var session = new GameSession(L001());
            session.AdvanceMs(2 * TickInterpolator.TICK_MS);
            Assert.That(session.EnqueueToggle(0), Is.True); // receipt 0 at tick 2
            session.AdvanceMs(2 * TickInterpolator.TICK_MS);
            Assert.That(session.EnqueueToggle(0), Is.True); // selected receipt at tick 4

            // This is deliberately independent of the rewind helper: it spells out the expected
            // append-order prefix, then uses the Domain replay path to reach terminal state.
            var expectedPrefix = new CommandLog();
            expectedPrefix.Append(new ToggleSwitchCommand(0, 2));
            var expectedTerminal = ReplayHasher.RunToEnd(
                session.Level.Graph, (ulong)session.Level.Dto.Seed, expectedPrefix);

            Assert.That(session.TryCreateRewindBeforeLastDecision(out var rewound), Is.True);

            Assert.That(rewound.HasUsedRewind, Is.True);
            Assert.That(rewound.State.Tick, Is.EqualTo(4));
            Assert.That(rewound.Log.Entries.Count, Is.EqualTo(1));
            Assert.That(rewound.Log.Entries[0].SwitchId, Is.EqualTo(0));
            Assert.That(rewound.Log.Entries[0].Tick, Is.EqualTo(2));
            Assert.That(session.Log.Entries.Count, Is.EqualTo(2), "the receiver is unchanged");
            Assert.That(session.HasUsedRewind, Is.False);

            while (rewound.State.Outcome.Kind == OutcomeKind.Running)
                rewound.AdvanceMs(TickInterpolator.TICK_MS);

            Assert.That(DigestBytes(rewound.State), Is.EqualTo(DigestBytes(expectedTerminal)),
                "the rewound session must be bit-identical to the independently replayed prefix");
        }

        [Test]
        public void TryCreateRewindBeforeLastDecision_OfARewoundRun_RemainsMarked()
        {
            var session = new GameSession(L001());
            session.AdvanceMs(2 * TickInterpolator.TICK_MS);
            Assert.That(session.EnqueueToggle(0), Is.True);
            session.AdvanceMs(2 * TickInterpolator.TICK_MS);
            Assert.That(session.EnqueueToggle(0), Is.True);
            Assert.That(session.TryCreateRewindBeforeLastDecision(out var firstRewind), Is.True);

            Assert.That(firstRewind.TryCreateRewindBeforeLastDecision(out var secondRewind), Is.True);

            Assert.That(secondRewind.HasUsedRewind, Is.True);
        }

        [Test]
        public void LeaderboardEligibility_AllowsOnlyCleanScoringPlayWithSuccessfulReplay()
        {
            var clean = new GameSession(L001());
            Assert.That(LeaderboardRunEligibility.CanSubmit(
                clean, isScoringPlay: true, deterministicReplaySucceeded: true), Is.True);
            Assert.That(LeaderboardRunEligibility.CanSubmit(
                clean, isScoringPlay: false, deterministicReplaySucceeded: true), Is.False);
            Assert.That(LeaderboardRunEligibility.CanSubmit(
                clean, isScoringPlay: true, deterministicReplaySucceeded: false), Is.False);

            var session = new GameSession(L001());
            session.AdvanceMs(2 * TickInterpolator.TICK_MS);
            Assert.That(session.EnqueueToggle(0), Is.True);
            Assert.That(session.TryCreateRewindBeforeLastDecision(out var rewound), Is.True);

            Assert.That(LeaderboardRunEligibility.CanSubmit(
                rewound, isScoringPlay: true, deterministicReplaySucceeded: true), Is.False);
        }

        private static ImportedLevel L001() => VFixtures.Import(VFixtures.L001Bytes());

        private static byte[] DigestBytes(SimulationState state)
        {
            var digest = new byte[state.DigestLength()];
            state.WriteDigest(digest);
            return digest;
        }
    }
}
