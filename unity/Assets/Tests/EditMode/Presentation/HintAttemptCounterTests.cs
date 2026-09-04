using NUnit.Framework;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-05 criterion 1, red-first: the pure attempt counter — FailureReview ENTRIES
    // only, edge-triggered, Halted/Won-blind, reset-and-re-arm per level attempt-run.
    // Every negative carries a positive control in the same fixture (anti-vacuity rule).
    public sealed class HintAttemptCounterTests
    {
        [Test]
        public void TransitionIntoFailureReview_CountsOneEntry()
        {
            var c = new HintAttemptCounter();
            c.Observe("Playing");
            Assert.That(c.Count, Is.EqualTo(0), "pre-flip control: Playing alone counts nothing");
            c.Observe("FailureReview");
            Assert.That(c.Count, Is.EqualTo(1), "the entry edge increments exactly once");
        }

        [Test]
        public void ConsecutiveFailureReviewObservations_CountOnce()
        {
            var c = new HintAttemptCounter();
            c.Observe("Playing");
            c.Observe("FailureReview");
            c.Observe("FailureReview");
            c.Observe("FailureReview");
            Assert.That(c.Count, Is.EqualTo(1),
                "polled frames inside one FailureReview stay one entry — edge, not level");
        }

        [Test]
        public void TwoEntries_AcrossARetryRoundTrip_CountTwo()
        {
            var c = new HintAttemptCounter();
            c.Observe("Playing");
            c.Observe("FailureReview");
            c.Observe("Playing"); // the retry rebuilds and play resumes
            c.Observe("FailureReview");
            Assert.That(c.Count, Is.EqualTo(2),
                "each re-entry after a retry is a new counted attempt end");
        }

        [Test]
        public void FirstObservation_AlreadyFailureReview_IsAnEntry()
        {
            var c = new HintAttemptCounter();
            c.Observe("FailureReview");
            Assert.That(c.Count, Is.EqualTo(1),
                "attaching mid-review still observes an entry, never a silent zero");
        }

        [Test]
        public void HaltedEntries_NeverCount_PositiveControlInSameFixture()
        {
            var c = new HintAttemptCounter();
            c.Observe("Playing");
            c.Observe("Halted");
            c.Observe("Playing");
            c.Observe("Halted");
            Assert.That(c.Count, Is.EqualTo(0),
                "unexpected Halted edges are not completed gameplay attempts");
            // positive control: the same counter demonstrably CAN count
            c.Observe("FailureReview");
            Assert.That(c.Count, Is.EqualTo(1), "the fixture's counter is live, not inert");
        }

        [Test]
        public void WonEntries_NeverCount_PositiveControlInSameFixture()
        {
            var c = new HintAttemptCounter();
            c.Observe("Playing");
            c.Observe("Won");
            Assert.That(c.Count, Is.EqualTo(0), "a win is not a counted attempt end");
            c.Observe("FailureReview");
            Assert.That(c.Count, Is.EqualTo(1), "positive control: counting still works");
        }

        [Test]
        public void Reset_Zeroes_AndReArms()
        {
            var c = new HintAttemptCounter();
            c.Observe("Playing");
            c.Observe("FailureReview");
            c.Observe("Playing");
            c.Observe("FailureReview");
            Assert.That(c.Count, Is.EqualTo(2), "pre-reset control");
            c.Reset();
            Assert.That(c.Count, Is.EqualTo(0), "a new level starts a fresh attempt-run");
            c.Observe("FailureReview");
            Assert.That(c.Count, Is.EqualTo(1),
                "reset re-arms the edge — the first entry of the new run counts");
        }
    }
}
