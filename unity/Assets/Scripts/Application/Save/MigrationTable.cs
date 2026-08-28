using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // CM-C7 criterion 7: an ORDERED list of (from, to, step) applied in sequence from the file's
    // saveVersion to the build's. Downgrade is refused upstream (SaveStore), never attempted
    // here. A migration step never deletes a key it does not understand (ADR-0006:72) — steps
    // receive and return the whole JObject. The production v1->v2 migration adds Daily Live
    // progress without inferring completions from legacy "played" dates.
    public sealed class MigrationTable
    {
        public sealed class Step
        {
            public readonly int From;
            public readonly int To;
            public readonly System.Func<JObject, JObject> Apply;

            public Step(int from, int to, System.Func<JObject, JObject> apply)
            {
                if (to != from + 1)
                    throw new System.ArgumentException(
                        "migration steps advance exactly one version: " + from + "->" + to);
                From = from; To = to; Apply = apply;
            }
        }

        private readonly List<Step> _steps = new List<Step>();

        public MigrationTable()
        {
            _steps.Add(new Step(1, 2, MigrateV1ToV2));
        }

        private static JObject MigrateV1ToV2(JObject payload)
        {
            var daily = payload["daily"] as JObject;
            if (daily == null)
            {
                daily = new JObject();
                payload["daily"] = daily;
            }

            // Legacy playedKeys recorded attempts, not wins, so it cannot honestly seed the
            // lifetime completion tally.
            if (daily["trustedDateKey"] == null) daily["trustedDateKey"] = "";
            if (daily["completedKeys"] == null) daily["completedKeys"] = new JArray();
            if (daily["lifetimeCompletions"] == null) daily["lifetimeCompletions"] = 0;
            return payload;
        }

        public MigrationTable Register(int from, int to, System.Func<JObject, JObject> apply)
        {
            _steps.Add(new Step(from, to, apply));
            return this;
        }

        // Applies every step from fileVersion up to targetVersion, in order. Returns null when a
        // required step is missing (a gap is corruption-adjacent: the caller falls back, never
        // guesses).
        public JObject Migrate(JObject payload, int fileVersion, int targetVersion)
        {
            var current = payload;
            for (int v = fileVersion; v < targetVersion; v++)
            {
                Step step = null;
                foreach (var s in _steps)
                    if (s.From == v) { step = s; break; }
                if (step == null) return null;
                current = step.Apply(current);
                if (current == null) return null;
                current["saveVersion"] = step.To;
            }
            return current;
        }
    }
}
