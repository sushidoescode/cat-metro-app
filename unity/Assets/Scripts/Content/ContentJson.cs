using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CatMetro.Content
{
    // CM-C2a criterion 4: THE single JsonSerializerSettings construction site in the whole tree.
    // TypeNameHandling stays None — permanent rule, never relaxed "for schema flexibility"
    // (ADR-0008 MUST 1; RK-34). Later serialising contracts (CM-C7 save, CM-C8 queue) reuse this
    // factory and construct none of their own. This assembly is byte-fed and file-system-free
    // (criterion 2).
    //
    // Depth is enforced three deep (review F1): LevelImporter's pre-parse scan (with comments
    // rejected outright, so bracket counting is sound), then a depth-bounded parse pass driven
    // by Settings.MaxDepth via JsonConvert, then the JToken pass with duplicate-property AND
    // comment rejection. Comments are not JSON and never valid content.
    public static class ContentJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = ContentBounds.CONTENT_MAX_JSON_DEPTH,
            DateParseHandling = DateParseHandling.None, // dates stay strings; no locale surprises
        };

        public static JsonSerializer CreateSerializer() => JsonSerializer.Create(Settings);

        // Note: JsonLoadSettings.CommentHandling has no rejecting member (only Ignore/Load), so
        // comment rejection lives ENTIRELY in LevelImporter's pre-scan slash rule (review F1a) —
        // no comment-bearing document ever reaches this parser.
        public static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
        };

        // Two-pass load: pass 1 enforces Settings.MaxDepth (the parser-level depth belt that was
        // previously unwired — review F1b); pass 2 enforces duplicate-key and comment rejection
        // and yields the token. Both passes reject trailing content.
        public static JToken LoadToken(string json)
        {
            JsonConvert.DeserializeObject<JToken>(json, Settings); // throws on depth > MaxDepth
            return JToken.Parse(json, LoadSettings);
        }
    }
}
