// CM-C2a negative fixture for the check.sh Content blocks (criterion 4b; handoff A-C2a-11).
// Never compiled by any csproj; outside every default scan root.
using System.IO;
namespace ContentBad
{
    public class Banned
    {
        // TypeNameHandling outside the single settings site must be flagged:
        public string tnh = "TypeNameHandling";
    }
}
