using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Consensus.Clique.UnitTests
{
    public class CliqueSnapshotRebuildTests
    {
        private static readonly EthECKey KeyA = new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private static readonly EthECKey KeyB = new EthECKey("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690");
        private static readonly EthECKey KeyC = new EthECKey("0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365");

        private static CliqueEngine EngineWithSigners(int epoch, params EthECKey[] signers) => new CliqueEngine(new CliqueConfig
        {
            InitialSigners = signers.Select(k => k.GetPublicAddress()).ToList(),
            LocalSignerAddress = signers[0].GetPublicAddress(),
            BlockPeriodSeconds = 1,
            EpochLength = epoch
        });

        private static byte[] HashOf(BlockHeader header) =>
            new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header));

        private static byte[] CheckpointExtra(params EthECKey[] signers)
        {
            var sorted = CliqueSnapshot.NormalizedAndSorted(signers.Select(k => k.GetPublicAddress()));
            var extra = new byte[CliqueEngine.EXTRA_VANITY + sorted.Count * 20 + CliqueEngine.EXTRA_SEAL];
            for (int i = 0; i < sorted.Count; i++)
                Array.Copy(sorted[i].HexToByteArray(), 0, extra, CliqueEngine.EXTRA_VANITY + i * 20, 20);
            return extra;
        }

        private static BlockHeader Sealed(CliqueEngine builder, EthECKey signer, long number, byte[] parentHash, byte[]? extra = null)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = parentHash,
                Difficulty = (EvmUInt256)builder.GetDifficulty(number, signer.GetPublicAddress()),
                MixHash = new byte[32],
                Nonce = new byte[8],
                ExtraData = extra ?? new byte[CliqueEngine.EXTRA_VANITY + CliqueEngine.EXTRA_SEAL],
                Timestamp = number * 10
            };
            var sealHash = BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);
            var signature = signer.SignAndCalculateV(sealHash).CreateStringSignature().HexToByteArray();
            Array.Copy(signature, 0, header.ExtraData, header.ExtraData.Length - CliqueEngine.EXTRA_SEAL, CliqueEngine.EXTRA_SEAL);
            return header;
        }

        private static async Task<(InMemoryBlockStore Store, List<(BlockHeader Header, byte[] Hash)> Chain)> ChainAsync(
            CliqueEngine builder, params (EthECKey Signer, byte[]? Extra)[] signers)
        {
            var store = new InMemoryBlockStore();
            var chain = new List<(BlockHeader, byte[])>();
            var parentHash = new byte[32];
            for (int i = 0; i < signers.Length; i++)
            {
                var header = Sealed(builder, signers[i].Signer, i + 1, parentHash, signers[i].Extra);
                var hash = HashOf(header);
                builder.ApplyBlock(header, signers[i].Signer.GetPublicAddress(), hash);
                await store.SaveAsync(header, hash);
                chain.Add((header, hash));
                parentHash = hash;
            }
            return (store, chain);
        }

        [Fact]
        public async Task Given_AnEngineThatNeverSawTheChain_When_ItValidatesTheNextBlock_Then_ItRebuildsTheParentSnapshotFromTheStore()
        {
            var builder = EngineWithSigners(30000, KeyA, KeyB, KeyC);
            var (store, chain) = await ChainAsync(builder, (KeyA, null), (KeyB, null));
            var fresh = EngineWithSigners(30000, KeyA, KeyB, KeyC);
            var gate = new CliqueConsensusBlockGate(fresh, store);

            var repeatsB = Sealed(builder, KeyB, 3, chain[1].Hash);
            var nextC = Sealed(builder, KeyC, 3, chain[1].Hash);

            var refused = await gate.IsBlockCanonicalAsync(repeatsB, HashOf(repeatsB), CancellationToken.None);
            var accepted = await gate.IsBlockCanonicalAsync(nextC, HashOf(nextC), CancellationToken.None);

            Assert.False(refused.Accepted);
            Assert.True(accepted.Accepted, accepted.Reason);
        }

        [Fact]
        public async Task Given_ABlockWhoseAncestorsAreRebuilt_When_ItIsValidated_Then_TheHeadSnapshotStaysWhereTheCanonicalChainIs()
        {
            var builder = EngineWithSigners(30000, KeyA, KeyB, KeyC);
            var (store, chain) = await ChainAsync(builder, (KeyA, null), (KeyB, null), (KeyC, null));
            var engine = EngineWithSigners(30000, KeyA, KeyB, KeyC);
            engine.ApplyBlock(chain[2].Header, KeyC.GetPublicAddress(), chain[2].Hash);
            var gate = new CliqueConsensusBlockGate(engine, store);

            var sibling = Sealed(builder, KeyA, 3, chain[1].Hash);
            await gate.IsBlockCanonicalAsync(sibling, HashOf(sibling), CancellationToken.None);

            Assert.Equal(3, engine.CurrentSnapshot.BlockNumber);
        }

        [Fact]
        public async Task Given_AStoreHoldingAChain_When_ANewEngineRestoresItsHead_Then_TheHeadSnapshotRemembersTheRecentSigners()
        {
            var builder = EngineWithSigners(30000, KeyA, KeyB, KeyC);
            var (store, _) = await ChainAsync(builder, (KeyA, null), (KeyB, null));
            var restarted = EngineWithSigners(30000, KeyA, KeyB, KeyC);

            await new CliqueConsensusBlockGate(restarted, store).RestoreHeadSnapshotAsync(CancellationToken.None);

            Assert.Equal(2, restarted.CurrentSnapshot.BlockNumber);
            Assert.True(restarted.CurrentSnapshot.SignedWithinLimit(KeyB.GetPublicAddress(), 3, 2));
        }

        [Fact]
        public async Task Given_ACheckpointWhoseParentIsMissingFromTheStore_When_TheNextBlockIsValidated_Then_TheSnapshotIsSeededFromTheCheckpointSigners()
        {
            var builder = EngineWithSigners(3, KeyA, KeyB, KeyC);
            var (_, chain) = await ChainAsync(builder,
                (KeyA, null), (KeyB, null), (KeyC, CheckpointExtra(KeyA, KeyB, KeyC)), (KeyA, null));

            var store = new InMemoryBlockStore();
            await store.SaveAsync(chain[2].Header, chain[2].Hash);
            await store.SaveAsync(chain[3].Header, chain[3].Hash);

            var restarted = EngineWithSigners(3, KeyA, KeyB);
            var gate = new CliqueConsensusBlockGate(restarted, store);
            var next = Sealed(builder, KeyB, 5, chain[3].Hash);

            var verdict = await gate.IsBlockCanonicalAsync(next, HashOf(next), CancellationToken.None);

            Assert.True(verdict.Accepted, verdict.Reason);
        }

        [Fact]
        public async Task Given_ASignerThatSealedBlockOneAndAStoreRewoundToGenesis_When_TheHeadSnapshotIsRestored_Then_ThatSignerMaySealBlockOneAgain()
        {
            var engine = new CliqueEngine(new CliqueConfig
            {
                InitialSigners = new List<string> { KeyA.GetPublicAddress(), KeyB.GetPublicAddress() },
                LocalSignerAddress = KeyA.GetPublicAddress(),
                LocalSignerPrivateKey = KeyA.GetPrivateKey(),
                BlockPeriodSeconds = 1,
                EpochLength = 30000
            });
            var block1 = Sealed(engine, KeyA, 1, new byte[32]);
            engine.ApplyBlock(block1, KeyA.GetPublicAddress(), HashOf(block1));
            Assert.False(engine.CanProduceBlock(1));

            await new CliqueConsensusBlockGate(engine, new InMemoryBlockStore()).RestoreHeadSnapshotAsync(CancellationToken.None);

            Assert.Equal(0, engine.CurrentSnapshot.BlockNumber);
            Assert.True(engine.CanProduceBlock(1));
        }
    }
}
