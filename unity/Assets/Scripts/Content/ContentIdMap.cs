using System;
using System.Collections.Generic;

namespace CatMetro.Content
{
    // Criterion 9: the importer returns this map ALONGSIDE the LevelGraph — the shipped
    // LevelGraph carries only dense integer arrays by design (adding a lookup member to it would
    // be a frozen-Domain edit, stop condition 3). Index order == authored file order, which is
    // part of the digest contract (LevelGraph.cs:19-20; A-C1-10).
    public sealed class ContentIdMap
    {
        private readonly string[] _indexToId;
        private readonly Dictionary<string, int> _idToIndex;

        public ContentIdMap(string[] indexToId)
        {
            _indexToId = indexToId;
            _idToIndex = new Dictionary<string, int>(indexToId.Length);
            for (int i = 0; i < indexToId.Length; i++) _idToIndex.Add(indexToId[i], i);
        }

        public int Count => _indexToId.Length;
        public string IdOf(int index) => _indexToId[index];
        public int IndexOf(string id) => _idToIndex[id];
        public bool TryIndexOf(string id, out int index) => _idToIndex.TryGetValue(id, out index);
        public ReadOnlyMemory<string> Ids => _indexToId;
    }

    public sealed class LevelIdMaps
    {
        public readonly ContentIdMap Nodes;
        public readonly ContentIdMap Edges;
        public readonly ContentIdMap Switches;
        public readonly ContentIdMap Stations;
        public readonly ContentIdMap Sources;   // by nodeId, authored order
        public readonly int WaveCount;          // waves carry no ids; order is the identity

        public LevelIdMaps(ContentIdMap nodes, ContentIdMap edges, ContentIdMap switches,
            ContentIdMap stations, ContentIdMap sources, int waveCount)
        {
            Nodes = nodes; Edges = edges; Switches = switches;
            Stations = stations; Sources = sources; WaveCount = waveCount;
        }
    }
}
