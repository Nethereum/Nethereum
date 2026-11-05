using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Nethereum.Testing;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    [Collection("PathKeyed2MDb")]
    [Trait("Requires", "Externals")]
    public class PathKeyed2MAnchorTests
    {
        [SkippableFact]
        [Trait("Category", "LargeFixture")]
        public async Task StoredForkBoundaryRoots_MatchPinnedMainnetCheckpoints()
        {
            Skip.IfNot(TestEnvironment.HasExternalPath("PathKeyed2MDb"),
                "ExternalPaths:PathKeyed2MDb is empty or missing; set it in "
                + TestEnvironment.LocalSettingsRelativePath + " to a path-keyed 2M RocksDB.");
            var dir = TestEnvironment.ExternalPath("PathKeyed2MDb");

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
