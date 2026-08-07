using System.Collections.Generic;

namespace CatMetro.Application.EventTaxonomy
{
    // CM-C9 criterion 1: one row of the 45-row taxonomy table — compiled C# data, drift-tested
    // field-for-field against docs/plan/data/analytics_event_taxonomy.csv (the source of truth
    // this contract copies and never edits). The CSV's `trigger` column is prose documentation
    // and is deliberately not carried.
    public sealed class TaxonomyRow
    {
        public readonly string Name;
        public readonly string Area;
        public readonly IReadOnlyList<string> RequiredParams;   // bare names
        public readonly IReadOnlyList<string> OptionalParams;   // bare names
        public readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ValueDomains;
        public readonly IReadOnlyList<string> UserProperties;
        public readonly IReadOnlyList<string> Destinations;
        public readonly string PrivacyClass;
        public readonly string QaProcedure;

        public TaxonomyRow(string name, string area,
            IReadOnlyList<string> requiredParams, IReadOnlyList<string> optionalParams,
            IReadOnlyDictionary<string, IReadOnlyList<string>> valueDomains,
            IReadOnlyList<string> userProperties, IReadOnlyList<string> destinations,
            string privacyClass, string qaProcedure)
        {
            Name = name; Area = area;
            RequiredParams = requiredParams; OptionalParams = optionalParams;
            ValueDomains = valueDomains;
            UserProperties = userProperties; Destinations = destinations;
            PrivacyClass = privacyClass; QaProcedure = qaProcedure;
        }
    }
}
