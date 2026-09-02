using System;
using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // Owns only the durable presentation preference. Playback may read this state, but audio
    // never participates in or mutates simulation state.
    public sealed class AudioPreferences
    {
        private readonly SaveStore _store;

        public AudioPreferences(SaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        // Audio is the historical opt-out setting. A missing or malformed value therefore keeps
        // the default-on behaviour instead of silently muting the game.
        public bool Enabled => BooleanValue(Settings?["audio"], defaultValue: true);

        public bool TrySetEnabled(bool enabled) =>
            TryUpdate(settings => settings["audio"] = enabled);

        private JObject Settings => _store.State.Payload?["settings"] as JObject;

        private bool TryUpdate(Action<JObject> update)
        {
            var original = _store.State.Payload;
            var mutated = (JObject)original.DeepClone();
            update(EnsureObject(mutated, "settings"));
            _store.State.Payload = mutated;
            try
            {
                if (_store.TryCommitAtomic()) return true;
                _store.Report("error_caught", "domain=audio_save detail=commit refused");
            }
            catch (Exception ex)
            {
                _store.Report("error_caught", "domain=audio_save detail="
                    + ex.GetType().Name);
            }
            _store.State.Payload = original;
            return false;
        }

        private static bool BooleanValue(JToken token, bool defaultValue) =>
            token is JValue value && value.Type == JTokenType.Boolean
                ? value.Value<bool>()
                : defaultValue;

        private static JObject EnsureObject(JObject parent, string key)
        {
            if (parent[key] is JObject objectValue) return objectValue;
            var created = new JObject();
            parent[key] = created;
            return created;
        }
    }
}
