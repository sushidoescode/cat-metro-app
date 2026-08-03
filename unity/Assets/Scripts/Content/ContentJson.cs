using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CatMetro.Content
{
    // CM-C2a criterion 4: THE single JsonSerializerSettings construction site in the whole tree.
    // TypeNameHandling stays None — permanent rule, never relaxed "for schema flexibility"
    // (ADR-0008 MUST 1; RK-34). Later serialising contracts (CM-C7 save, CM-C8 queue) reuse this
    // factory and construct none of their own. No System.IO appears in this assembly (criterion 2):
    // depth is rejected by LevelImporter's pre-parse scan BEFORE deserialization (criterion 6),
    // with Settings.MaxDepth as the in-parser belt behind it.
    public static class ContentJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = ContentBounds.CONTENT_MAX_JSON_DEPTH,
            DateParseHandling = DateParseHandling.None, // dates stay strings; no locale surprises
        };

        public static JsonSerializer CreateSerializer() => JsonSerializer.Create(Settings);

        // Two-phase load (A-C2a-8): JToken first — with duplicate-property rejection, available
        // via JsonLoadSettings since Newtonsoft 12.0.1 — then materialization through Settings.
        public static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
        };

        // JToken.Parse reads exactly one document and rejects trailing content.
        public static JToken LoadToken(string json) => JToken.Parse(json, LoadSettings);
    }
}
