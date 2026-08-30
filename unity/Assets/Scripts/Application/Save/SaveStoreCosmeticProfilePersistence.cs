using System;
using System.Collections.Generic;
using CatMetro.Services.Cosmetics;
using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    public sealed class SaveStoreCosmeticProfilePersistence : ICosmeticProfilePersistence
    {
        private readonly SaveStore _store;

        public SaveStoreCosmeticProfilePersistence(SaveStore store)
        {
            _store = store;
        }

        public bool TryLoad(out CosmeticProfileSnapshot snapshot)
        {
            snapshot = CosmeticProfileSnapshot.Empty;
            if (_store == null || _store.State == null) return false;
            return TryReadSnapshot(_store.State.Payload, out snapshot);
        }

        public bool TryReplace(CosmeticProfileSnapshot snapshot)
        {
            if (_store == null || _store.State == null || snapshot == null) return false;
            var original = _store.State.Payload;
            if (!TryReadSnapshot(original, out _)) return false;

            try
            {
                var candidate = (JObject)original.DeepClone();
                var profile = (JObject)candidate["profile"];
                var cosmetics = (JObject)profile["cosmetics"];
                WriteKnownFields(cosmetics, snapshot);
                _store.State.Payload = candidate;

                if (_store.TryCommitAtomic()) return true;
                _store.Report("error_caught", "domain=cosmetics_save detail=commit refused");
            }
            catch (Exception ex)
            {
                _store.Report("error_caught", "domain=cosmetics_save detail=" +
                    ex.GetType().Name);
            }

            _store.State.Payload = original;
            return false;
        }

        private static bool TryReadSnapshot(JObject payload, out CosmeticProfileSnapshot snapshot)
        {
            snapshot = CosmeticProfileSnapshot.Empty;
            if (payload == null || !(payload["profile"] is JObject profile)
                || !(profile["cosmetics"] is JObject cosmetics))
                return false;

            if (!TryString(cosmetics["selectedCatId"], out var selectedCatId)
                || !TryStringArray(cosmetics["earnedCatIds"], out var earnedCatIds)
                || !TryStringArray(cosmetics["earnedItemIds"], out var earnedItemIds)
                || !(cosmetics["loadouts"] is JArray rows))
                return false;

            var loadouts = new List<CosmeticLoadout>(rows.Count);
            foreach (var token in rows)
            {
                if (!(token is JObject row)
                    || !TryString(row["catId"], out var catId)
                    || !TryString(row["outfitId"], out var outfitId)
                    || !TryString(row["accessoryId"], out var accessoryId)
                    || !TryString(row["frameId"], out var frameId))
                    return false;
                loadouts.Add(new CosmeticLoadout(catId, outfitId, accessoryId, frameId));
            }

            snapshot = new CosmeticProfileSnapshot(selectedCatId, earnedCatIds, earnedItemIds,
                loadouts);
            return true;
        }

        private static bool TryStringArray(JToken token, out IReadOnlyList<string> values)
        {
            values = null;
            if (!(token is JArray array)) return false;
            var parsed = new List<string>(array.Count);
            foreach (var member in array)
            {
                if (!TryString(member, out var value)) return false;
                parsed.Add(value);
            }
            values = parsed;
            return true;
        }

        private static bool TryString(JToken token, out string value)
        {
            value = null;
            if (token == null || token.Type != JTokenType.String) return false;
            value = (string)token;
            return value != null;
        }

        private static void WriteKnownFields(JObject cosmetics, CosmeticProfileSnapshot snapshot)
        {
            cosmetics["selectedCatId"] = snapshot.SelectedCatId;
            cosmetics["earnedCatIds"] = new JArray(snapshot.EarnedCatIds);
            cosmetics["earnedItemIds"] = new JArray(snapshot.EarnedItemIds);

            var existingByCat = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var token in (JArray)cosmetics["loadouts"])
            {
                var row = (JObject)token;
                existingByCat[(string)row["catId"]] = row;
            }

            var loadouts = new JArray();
            for (int i = 0; i < snapshot.Loadouts.Count; i++)
            {
                var loadout = snapshot.Loadouts[i];
                JObject row;
                if (existingByCat.TryGetValue(loadout.CatId, out var existing))
                    row = (JObject)existing.DeepClone();
                else
                    row = new JObject();
                row["catId"] = loadout.CatId;
                row["outfitId"] = loadout.OutfitId;
                row["accessoryId"] = loadout.AccessoryId;
                row["frameId"] = loadout.FrameId;
                loadouts.Add(row);
            }
            cosmetics["loadouts"] = loadouts;
        }
    }
}
