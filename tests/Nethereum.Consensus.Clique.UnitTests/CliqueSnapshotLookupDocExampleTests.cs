using System.Collections.Generic;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Consensus.Clique.UnitTests
{
    public class CliqueSnapshotLookupDocExampleTests
    {
        private static readonly EthECKey KeyA = new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private static readonly EthECKey KeyB = new EthECKey("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690");

        private static CliqueEngine EngineWith(ICliqueSnapshotStore? store) => new CliqueEngine(
            new CliqueConfig
            {
                InitialSigners = new List<string> { KeyA.GetPublicAddress(), KeyB.GetPublicAddress() },
                LocalSignerAddress = KeyA.GetPublicAddress(),
                BlockPeriodSeconds = 1,
                EpochLength = 30000
            }, null, store);

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "clique", "Ask whether a snapshot exists for a block hash")]
        public void HasSnapshot_IsTrueForAppliedBlocksAndPersistedSnapshotsOnly()
        {
            var store = new InMemoryCliqueSnapshotStore();
            var engine = EngineWith(store);
            var header = new BlockHeader
            {
                BlockNumber = CliqueEngine.SnapshotInterval,
                ParentHash = new byte[32],
                Timestamp = 100
            };
            var blockHash = new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header));

            Assert.False(engine.HasSnapshot(blockHash));

            engine.ApplyBlock(header, KeyA.GetPublicAddress(), blockHash);

            Assert.True(engine.HasSnapshot(blockHash));
            Assert.True(EngineWith(store).HasSnapshot(blockHash));
            Assert.False(EngineWith(null).HasSnapshot(blockHash));
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "clique", "The earliest timestamp a produced block may carry")]
        public void NextBlockTimestamp_IsTheLaterOfParentPlusPeriodAndNow()
        {
            var engine = EngineWith(null);
            var now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var future = new BlockHeader { BlockNumber = 1, Timestamp = now + 3600 };
            var past = new BlockHeader { BlockNumber = 1, Timestamp = 100 };

            Assert.Equal(now + 3601, engine.NextBlockTimestamp(future));
            Assert.True(engine.NextBlockTimestamp(past) >= now);
            Assert.True(engine.NextBlockTimestamp(null) >= now);
        }
    }
}
