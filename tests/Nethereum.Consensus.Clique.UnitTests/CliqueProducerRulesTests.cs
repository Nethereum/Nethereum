using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Consensus.Clique.UnitTests
{
    public class CliqueProducerRulesTests
    {
        private static readonly EthECKey KeyA = new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private static readonly EthECKey KeyB = new EthECKey("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690");
        private static readonly EthECKey KeyC = new EthECKey("0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365");
        private static readonly EthECKey KeyD = new EthECKey("0x7c852118294e51e653712a81e05800f419141751be58f605c371e15141b007a6");

        private static CliqueEngine EngineFor(EthECKey local, int period, params EthECKey[] signers) => new CliqueEngine(new CliqueConfig
        {
            InitialSigners = signers.Select(k => k.GetPublicAddress()).ToList(),
            LocalSignerAddress = local.GetPublicAddress(),
            LocalSignerPrivateKey = local.GetPrivateKey(),
            BlockPeriodSeconds = period,
            EpochLength = 30000
        });

        private static byte[] HashOf(BlockHeader header) =>
            new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header));

        private static BlockHeader SealedBy(CliqueEngine engine, EthECKey signer, long number, byte[] parentHash, long timestamp)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = parentHash,
                Difficulty = (EvmUInt256)engine.GetDifficulty(number, signer.GetPublicAddress()),
                MixHash = new byte[32],
                Nonce = new byte[8],
                ExtraData = new byte[CliqueEngine.EXTRA_VANITY + CliqueEngine.EXTRA_SEAL],
                Timestamp = timestamp
            };
            var sealHash = BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);
            var signature = signer.SignAndCalculateV(sealHash).CreateStringSignature().HexToByteArray();
            Array.Copy(signature, 0, header.ExtraData, header.ExtraData.Length - CliqueEngine.EXTRA_SEAL, CliqueEngine.EXTRA_SEAL);
            return header;
        }

        [Theory]
        [InlineData(2, 2)]
        [InlineData(3, 2)]
        [InlineData(4, 3)]
        public void Given_ASignerThatSealedTheParent_When_ItAsksToSealTheNextBlock_Then_ItIsRefusedUntilTheLimitShiftsIt(
            int signerCount, int limit)
        {
            var all = new[] { KeyA, KeyB, KeyC, KeyD }.Take(signerCount).ToArray();
            var engine = EngineFor(KeyA, 1, all);
            Assert.True(engine.CanProduceBlock(1));

            var first = SealedBy(engine, KeyA, 1, new byte[32], 100);
            engine.ApplyBlock(first, KeyA.GetPublicAddress(), HashOf(first));

            for (long next = 2; next < 1 + limit; next++)
                Assert.False(engine.CanProduceBlock(next), $"block {next} must be refused for a signer of block 1 with limit {limit}");

            Assert.True(engine.CanProduceBlock(1 + limit));
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "clique", "A produced block never carries a timestamp earlier than parent plus period")]
        public void Given_AParentAndAPeriod_When_TheNextBlockIsPrepared_Then_ItsTimestampIsNoEarlierThanParentPlusPeriod()
        {
            var engine = EngineFor(KeyA, 15, KeyA, KeyB);
            var strategy = new CliqueBlockProductionStrategy(new ChainConfig(), engine);
            var parentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
            var parent = SealedBy(engine, KeyB, 1, new byte[32], parentTime);

            var options = strategy.PrepareBlockOptions(2, parent);

            Assert.True(options.Timestamp >= parentTime + 15, $"timestamp {options.Timestamp} < parent {parentTime} + 15");
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "clique", "Validation refuses a header earlier than parent plus period")]
        public void Given_AHeaderEarlierThanParentPlusPeriod_When_ItIsValidated_Then_ItIsRefusedAsAnInvalidTimestamp()
        {
            var engine = EngineFor(KeyA, 15, KeyA, KeyB);
            var parent = SealedBy(engine, KeyA, 1, new byte[32], 100);
            engine.ApplyBlock(parent, KeyA.GetPublicAddress(), HashOf(parent));

            var tooEarly = SealedBy(engine, KeyB, 2, HashOf(parent), 100 + 14);
            var onTime = SealedBy(engine, KeyB, 2, HashOf(parent), 100 + 15);

            var refused = engine.ValidateBlockInternal(tooEarly, parent);
            Assert.False(refused.IsValid);
            Assert.Contains("timestamp", refused.Error, StringComparison.OrdinalIgnoreCase);
            Assert.True(engine.ValidateBlockInternal(onTime, parent).IsValid);
        }

        [Fact]
        public void Given_APeriodOfZero_When_AHeaderSharesTheParentTimestamp_Then_ItIsAcceptedAndOneBeforeTheParentIsRefused()
        {
            var engine = EngineFor(KeyA, 0, KeyA, KeyB);
            var parent = SealedBy(engine, KeyA, 1, new byte[32], 100);
            engine.ApplyBlock(parent, KeyA.GetPublicAddress(), HashOf(parent));

            var sameSecond = SealedBy(engine, KeyB, 2, HashOf(parent), 100);
            var earlier = SealedBy(engine, KeyB, 2, HashOf(parent), 99);

            Assert.True(engine.ValidateBlockInternal(sameSecond, parent).IsValid);
            Assert.False(engine.ValidateBlockInternal(earlier, parent).IsValid);
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "clique", "Validation refuses a header whose supplied parent is not its parent")]
        public void Given_AHeaderWhoseSuppliedParentIsNotItsParent_When_ItIsValidated_Then_ItIsRefusedAsAnUnknownAncestor()
        {
            var engine = EngineFor(KeyA, 1, KeyA, KeyB);
            var parent = SealedBy(engine, KeyA, 1, new byte[32], 100);
            engine.ApplyBlock(parent, KeyA.GetPublicAddress(), HashOf(parent));
            var stranger = SealedBy(engine, KeyB, 1, new byte[32], 50);

            var child = SealedBy(engine, KeyB, 2, HashOf(parent), 200);

            Assert.True(engine.ValidateBlockInternal(child, parent).IsValid);

            var wrongHash = engine.ValidateBlockInternal(child, stranger);
            Assert.False(wrongHash.IsValid);
            Assert.Contains("ancestor", wrongHash.Error, StringComparison.OrdinalIgnoreCase);

            var wrongNumber = SealedBy(engine, KeyB, 3, HashOf(parent), 300);
            var gap = engine.ValidateBlockInternal(wrongNumber, parent);
            Assert.False(gap.IsValid);
            Assert.Contains("ancestor", gap.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "clique", "Recover the signer that authored a block")]
        public void Given_ASealedAndAnUnsealedHeader_When_TheAuthorIsAsked_Then_TheSealedOneNamesItsSignerAndTheUnsealedOneTheLocalSigner()
        {
            var engine = EngineFor(KeyA, 1, KeyA, KeyB);
            var sealedByB = SealedBy(engine, KeyB, 1, new byte[32], 100);
            var unsealed = new BlockHeader
            {
                BlockNumber = 1,
                ParentHash = new byte[32],
                ExtraData = new byte[CliqueEngine.EXTRA_VANITY + CliqueEngine.EXTRA_SEAL],
                Coinbase = KeyB.GetPublicAddress()
            };

            Assert.Equal(KeyB.GetPublicAddress().ToLowerInvariant(), engine.AuthorOf(sealedByB));
            Assert.Equal(KeyA.GetPublicAddress().ToLowerInvariant(), engine.AuthorOf(unsealed));
        }
    }
}
