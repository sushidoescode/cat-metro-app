using System;

namespace CatMetro.Domain
{
    // Exactly three members, contract-tested (CM-R03.1; ADR-0002 §10). Members are published
    // in player-facing copy and the analytics taxonomy — adding one is an ADR change.
    public enum FailReason : byte
    {
        QueueOverflow = 1,
        PlatformOverflow = 2,
        TimeOut = 3,
    }

    public enum OutcomeKind : byte
    {
        Running = 0,
        Won = 1,
        Failed = 2,
    }

    // Value type so it can live inside SimulationState and the digest (1-byte tag + 1-byte reason).
    public readonly struct SimOutcome
    {
        public readonly OutcomeKind Kind;
        public readonly FailReason Reason; // 0 when not Failed

        private SimOutcome(OutcomeKind kind, FailReason reason)
        {
            Kind = kind;
            Reason = reason;
        }

        public static SimOutcome Running => new SimOutcome(OutcomeKind.Running, 0);
        public static SimOutcome Won => new SimOutcome(OutcomeKind.Won, 0);

        public static SimOutcome MakeFailed(FailReason reason)
        {
            throw new NotImplementedException("CM-C1: MakeFailed not implemented yet (TDD red)");
        }
    }
}
