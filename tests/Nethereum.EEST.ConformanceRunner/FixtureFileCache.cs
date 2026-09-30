using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Nethereum.EEST.ConformanceRunner
{
    internal static class FixtureFileCache
    {
        private static readonly ConcurrentDictionary<string, Task<List<BlockchainTestLoader.BlockchainTest>>> Cache = new();

        public static Task<List<BlockchainTestLoader.BlockchainTest>> LoadAsync(string file) =>
            Cache.GetOrAdd(file, static f => ParseAsync(f));

        private static async Task<List<BlockchainTestLoader.BlockchainTest>> ParseAsync(string file)
        {
            var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
            return BlockchainTestLoader.LoadFromJson(json);
        }
    }
}
