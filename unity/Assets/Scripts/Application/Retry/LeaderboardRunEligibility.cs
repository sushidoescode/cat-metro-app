using CatMetro.Application.Session;

namespace CatMetro.Application.Retry
{
    // ADR-0010: global score submission requires a scoring play, a successful deterministic
    // replay check, and a run with no rewind provenance. This is local eligibility only; it has
    // no provider, network, or leaderboard-backend responsibility.
    public static class LeaderboardRunEligibility
    {
        public static bool CanSubmit(GameSession session, bool isScoringPlay,
            bool deterministicReplaySucceeded) =>
            session != null
            && isScoringPlay
            && deterministicReplaySucceeded
            && !session.HasUsedRewind;
    }
}
