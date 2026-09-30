using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Nethereum.EEST.ConformanceRunner
{
    internal static class StateTestFileCache
    {
        private static readonly ConcurrentDictionary<string, Task<Dictionary<string, StateTestLoader.StateTest>>> Cache = new();

        public static Task<Dictionary<string, StateTestLoader.StateTest>> LoadAsync(string file) =>
            Cache.GetOrAdd(file, static f => ParseAsync(f));

        private static async Task<Dictionary<string, StateTestLoader.StateTest>> ParseAsync(string file)
        {
            var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
            return StateTestLoader.LoadFromJson(json);
        }
    }
}
