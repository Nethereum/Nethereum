using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Nethereum.EEST.ConformanceRunner
{
    internal static class EnginePayloadFileCache
    {
        private static readonly ConcurrentDictionary<string, Task<Dictionary<string, List<EnginePayloadLoader.EnginePayloadEntry>>>> Cache = new();
        private static readonly ConcurrentDictionary<string, int> Remaining = new();

        public static void Prime(IEnumerable<(string File, string Name)> cases)
        {
            foreach (var group in cases.GroupBy(c => c.File))
                Remaining[group.Key] = group.Count();
        }

        public static async Task<List<EnginePayloadLoader.EnginePayloadEntry>> GetEntriesAsync(string file, string testName)
        {
            var byName = await Cache.GetOrAdd(file, static f => ParseAsync(f)).ConfigureAwait(false);
            return byName.TryGetValue(testName, out var entries)
                ? entries
                : new List<EnginePayloadLoader.EnginePayloadEntry>();
        }

        public static void Release(string file)
        {
            if (Remaining.AddOrUpdate(file, 0, (_, count) => count - 1) <= 0)
            {
                Cache.TryRemove(file, out _);
                Remaining.TryRemove(file, out _);
            }
        }

        private static async Task<Dictionary<string, List<EnginePayloadLoader.EnginePayloadEntry>>> ParseAsync(string file)
        {
            var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
            var root = JObject.Parse(json);

            var result = new Dictionary<string, List<EnginePayloadLoader.EnginePayloadEntry>>();
            foreach (var property in root.Properties())
            {
                if (property.Value is JObject test)
                    result[property.Name] = EnginePayloadLoader.ReadEntries(test);
            }

            return result;
        }
    }
}
