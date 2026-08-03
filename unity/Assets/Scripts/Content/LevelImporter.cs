using System;
using System.Threading;
using System.Threading.Tasks;
using CatMetro.Domain;
using CatMetro.Services;

namespace CatMetro.Content
{
    // The one import pipeline (CM-C2a): bytes -> [size cap] -> [UTF-8 decode, BOM-tolerant] ->
    // [depth pre-scan] -> [JToken parse: syntax, duplicate keys, trailing content] ->
    // [schemaVersion] -> [typed DTO walk: integer strictness, missing fields] ->
    // [post-parse bounds + caps + duplicate ids + referential integrity] ->
    // [pin pre-checks: second source, wild color — typed failures naming the pin] ->
    // [LevelGraph + id maps]. Total and non-throwing: every failure is a ContentResult error.
    public sealed class ImportedLevel
    {
        public readonly LevelDto Dto;
        public readonly LevelGraph Graph;
        public readonly LevelIdMaps IdMaps;

        public ImportedLevel(LevelDto dto, LevelGraph graph, LevelIdMaps idMaps)
        {
            Dto = dto; Graph = graph; IdMaps = idMaps;
        }
    }

    public static class LevelImporter
    {
        public static ContentResult<ImportedLevel> Import(byte[] bytes)
        {
            throw new NotImplementedException("CM-C2a: Import not implemented yet (TDD red)");
        }

        // Criterion 11: the read seam. Content never touches a filesystem; it receives bytes.
        public static async Task<ContentResult<ImportedLevel>> ImportFromSourceAsync(
            IContentSource source, string relativePath, CancellationToken ct)
        {
            var bytes = await source.ReadAsync(relativePath, ct).ConfigureAwait(false);
            return Import(bytes);
        }
    }
}
