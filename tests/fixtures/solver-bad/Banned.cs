// CM-C4 negative fixtures for the check.sh solver blocks (criterion 2 + 8 proofs). Never
// compiled; outside every default scan root. Each line below must fire its block under
// `bash scripts/check.sh --root tests/fixtures/solver-bad`.
namespace SolverBad
{
    public class Banned
    {
        public void TickWrite(object state) { /* state.Tick++ ; state.Tick = 5; Deliveries++ ; OverloadTimers[0] */ }
        public string tickWrite = "state.Tick++";
        public string delivWrite = "state.Deliveries = 3";
        public string timerWrite = "state.OverloadTimers[1] = 0";
        public string scoreRead = "var x = state.Score;";
        public string chainRead = "var y = state.Chain;";
        public string secondStep = "public static void Step(ref SimulationState s, int x) { }";
    }
}
