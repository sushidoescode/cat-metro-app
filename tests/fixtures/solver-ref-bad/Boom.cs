// CM-C4 criterion-13 negative fixture (review H2): a runtime-tree file referencing solver types
// must fire the runtime-reference guard under `bash scripts/check.sh --root tests/fixtures/solver-ref-bad`.
// Never compiled; outside every default scan root.
using CatMetro.Domain.Solver;

namespace RefBad
{
    public class Boom
    {
        public string reach = "CatMetro.Domain.Solver.LevelSolver.Solve";
    }
}
