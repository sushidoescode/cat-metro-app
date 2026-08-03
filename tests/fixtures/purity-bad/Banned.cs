// CM-C1 criterion 6 negative fixture. Never compiled by any csproj; lives outside every default
// scan root so `bash scripts/check.sh` stays green while
// `bash scripts/check.sh --root tests/fixtures/purity-bad` must fail on the banned symbol below.
namespace PurityBad
{
    public class Banned
    {
        public double x;
    }
}
