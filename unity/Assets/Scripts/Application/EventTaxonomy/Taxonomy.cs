using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using CatMetro.Services;

namespace CatMetro.Application.EventTaxonomy
{
    // CM-C9: the engine-free typed choke point (CM-R43.1-.3). Rows is the compiled 45-row
    // table; TryBuild is the ONE validation path every factory routes through — typed failure
    // (bool + named error), never a throw (A-C9-4: the queue's Log path must never throw, so
    // the layer above it must not either). RED STUB.
    public static class Taxonomy
    {
        public static IReadOnlyList<TaxonomyRow> Rows { get; } = new List<TaxonomyRow>();

        public static bool TryBuild(string name, JObject parameters,
            out AnalyticsEvent e, out string error)
        {
            e = default;
            error = "not implemented (CM-C9 red): " + name;
            return false;
        }
    }
}
