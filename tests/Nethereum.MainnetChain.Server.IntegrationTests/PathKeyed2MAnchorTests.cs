using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    [Collection("PathKeyed2MDb")]
    public class PathKeyed2MAnchorTests
    {
        [Fact]
        public async Task StoredForkBoundaryRoots_MatchPinnedMainnetCheckpoints()
        {
            var dir = Environment.GetEnvironmentVariable("NETHEREUM_PATHKEYED_2M_DB");
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return;

            using var bundle = RocksDbChainStoreBundle.Open(
                dir,
                journalOptions: HistoricalStateOptions.FullArchive,
                storageOptions: new RocksDbStorageOptions
                {
                    DatabasePath = dir,
                    PathKeyedState = true,
                    TrieNodeHistoryBlocks = 1024,
                    TrieNodeHistoryIndex = true,
                });

            var pinned = new MainnetKnownCheckpoints();

            foreach (var n in new ulong[] { 1, 1_920_000 })
            {
                var header = await bundle.Blocks.GetByNumberAsync(n);
                Assert.NotNull(header);
                var (root, _) = await pinned.GetCanonicalAsync(n, CancellationToken.None);
                Assert.NotNull(root);
                Assert.Equal(root.ToHex(), header.StateRoot.ToHex());
            }

            var tip = await bundle.Blocks.GetByNumberAsync(2_000_000);
            Assert.NotNull(tip);
        }
    }
}
