using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain.RocksDB;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class CliqueSnapshotRocksDbPersistenceTests
    {
        private static readonly EthECKey KeyA = new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private static readonly EthECKey KeyB = new EthECKey("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690");

        private static CliqueEngine EngineWith(ICliqueSnapshotStore store) => new CliqueEngine(
            new CliqueConfig
            {
                InitialSigners = new List<string> { KeyA.GetPublicAddress(), KeyB.GetPublicAddress() },
                LocalSignerAddress = KeyA.GetPublicAddress(),
                BlockPeriodSeconds = 1,
                EpochLength = 30000
            }, null, store);

        [Fact]
        public async Task Given_ASnapshotPersistedAtTheInterval_When_TheDataDirectoryIsReopened_Then_TheNewEngineKnowsTheSnapshotAndItsSigners()
        {
            var dir = Path.Combine(Path.GetTempPath(), "clique-snapshot-" + Guid.NewGuid().ToString("N"));
            var hash = new byte[32];
            hash[0] = 0xAB;
            try
            {
                using (var rocks = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir }))
                {
                    var engine = EngineWith(new RocksDbCliqueSnapshotStore(rocks));
                    var header = new BlockHeader
                    {
                        BlockNumber = CliqueEngine.SnapshotInterval,
                        ParentHash = new byte[32],
                        Timestamp = 5,
                        Nonce = new byte[8]
                    };
                    engine.ApplyBlock(header, KeyA.GetPublicAddress().ToLowerInvariant(), hash);
                }

                using (var reopened = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir }))
                {
                    var engine = EngineWith(new RocksDbCliqueSnapshotStore(reopened));

                    Assert.True(engine.HasSnapshot(hash));
                    var blocks = new InMemoryBlockStore();
                    await blocks.SaveAsync(new BlockHeader
                    {
                        BlockNumber = CliqueEngine.SnapshotInterval,
                        ParentHash = new byte[32],
                        Timestamp = 5,
                        Nonce = new byte[8]
                    }, hash);
                    await new CliqueConsensusBlockGate(engine, blocks).RestoreHeadSnapshotAsync(CancellationToken.None);
                    Assert.Equal(CliqueEngine.SnapshotInterval, engine.CurrentSnapshot.BlockNumber);
                    Assert.Equal(2, engine.CurrentSnapshot.TotalSigners);
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }
    }
}
