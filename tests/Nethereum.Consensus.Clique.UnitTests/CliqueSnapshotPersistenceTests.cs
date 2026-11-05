using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Consensus.Clique.UnitTests
{
    public class CliqueSnapshotPersistenceTests
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

        private static byte[] HashOf(BlockHeader header) =>
            new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header));

        private static BlockHeader Sealed(CliqueEngine builder, EthECKey signer, long number, byte[] parentHash)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = parentHash,
                Difficulty = (EvmUInt256)builder.GetDifficulty(number, signer.GetPublicAddress()),
                MixHash = new byte[32],
                Nonce = new byte[8],
                ExtraData = new byte[CliqueEngine.EXTRA_VANITY + CliqueEngine.EXTRA_SEAL],
                Timestamp = number * 10
            };
            var sealHash = BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);
            var signature = signer.SignAndCalculateV(sealHash).CreateStringSignature().HexToByteArray();
            Array.Copy(signature, 0, header.ExtraData, header.ExtraData.Length - CliqueEngine.EXTRA_SEAL, CliqueEngine.EXTRA_SEAL);
            return header;
        }

        private static async Task<List<(BlockHeader Header, byte[] Hash)>> ProduceChainAsync(CliqueEngine builder, int length)
        {
            var chain = new List<(BlockHeader, byte[])>();
            var parentHash = new byte[32];
            for (var number = 1; number <= length; number++)
            {
                var signer = number % 2 == 1 ? KeyA : KeyB;
                var header = Sealed(builder, signer, number, parentHash);
                var hash = HashOf(header);
                builder.ApplyBlock(header, signer.GetPublicAddress(), hash);
                chain.Add((header, hash));
                parentHash = hash;
            }
            return await Task.FromResult(chain);
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "clique", "Restore the head snapshot after a restart")]
        public async Task Given_ASignerChainPastTheSnapshotInterval_When_TheNodeRestartsWithOnlyTheRecentHeaders_Then_ItResumesFromThePersistedSnapshotWithoutWalkingFromGenesis()
        {
            var persisted = new InMemoryCliqueSnapshotStore();
            var builder = EngineWith(persisted);
            var length = CliqueEngine.SnapshotInterval + 6;
            var chain = await ProduceChainAsync(builder, length);

            var headersSinceTheSnapshot = new InMemoryBlockStore();
            foreach (var (header, hash) in chain.Where(c => (long)c.Header.BlockNumber >= CliqueEngine.SnapshotInterval))
                await headersSinceTheSnapshot.SaveAsync(header, hash);

            var restarted = EngineWith(persisted);
            await new CliqueConsensusBlockGate(restarted, headersSinceTheSnapshot).RestoreHeadSnapshotAsync(CancellationToken.None);

            Assert.Equal(length, restarted.CurrentSnapshot.BlockNumber);
            Assert.Equal(2, restarted.CurrentSnapshot.TotalSigners);
            Assert.True(restarted.CurrentSnapshot.SignedWithinLimit(KeyB.GetPublicAddress(), length + 1, 2));
        }

        [Fact]
        public async Task Given_NoPersistedSnapshotAndOnlyRecentHeaders_When_TheNodeRestarts_Then_TheHeadCannotBeRestored()
        {
            var builder = EngineWith(null);
            var length = CliqueEngine.SnapshotInterval + 6;
            var chain = await ProduceChainAsync(builder, length);

            var headersSinceTheSnapshot = new InMemoryBlockStore();
            foreach (var (header, hash) in chain.Where(c => (long)c.Header.BlockNumber >= CliqueEngine.SnapshotInterval))
                await headersSinceTheSnapshot.SaveAsync(header, hash);

            var restarted = EngineWith(null);
            await new CliqueConsensusBlockGate(restarted, headersSinceTheSnapshot).RestoreHeadSnapshotAsync(CancellationToken.None);

            Assert.NotEqual(length, restarted.CurrentSnapshot.BlockNumber);
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "clique", "Encode and decode a snapshot for storage")]
        public void Given_ASnapshotWithVotesAndRecents_When_ItIsEncodedAndDecoded_Then_EveryFieldSurvives()
        {
            var snapshot = new CliqueSnapshot
            {
                BlockNumber = 2048,
                Timestamp = 777,
                BlockHash = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(),
                Signers = new List<string> { KeyA.GetPublicAddress().ToLowerInvariant(), KeyB.GetPublicAddress().ToLowerInvariant() }
            };
            snapshot.Recents[2047] = snapshot.Signers[0];
            snapshot.Votes["a:b"] = new CliqueVote { Signer = "a", Target = "b", Authorize = true, BlockNumber = 2040 };
            snapshot.VoteTally["b:True"] = 1;

            var decoded = CliqueSnapshotCodec.Decode(CliqueSnapshotCodec.Encode(snapshot));

            Assert.NotNull(decoded);
            Assert.Equal(snapshot.BlockNumber, decoded!.BlockNumber);
            Assert.Equal(snapshot.Timestamp, decoded.Timestamp);
            Assert.Equal(snapshot.BlockHash, decoded.BlockHash);
            Assert.Equal(snapshot.Signers, decoded.Signers);
            Assert.Equal(snapshot.Recents, decoded.Recents);
            Assert.Equal(1, decoded.VoteTally["b:True"]);
            Assert.True(decoded.Votes["a:b"].Authorize);
            Assert.Equal(2040, decoded.Votes["a:b"].BlockNumber);
        }

        [Fact]
        public void Given_CorruptBytes_When_TheyAreDecoded_Then_NoSnapshotIsReturned()
        {
            Assert.Null(CliqueSnapshotCodec.Decode(new byte[] { 1, 2, 3 }));
        }
    }
}
