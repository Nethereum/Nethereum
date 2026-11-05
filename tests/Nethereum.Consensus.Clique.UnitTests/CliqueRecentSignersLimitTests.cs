using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Consensus.Clique;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Consensus.Clique.UnitTests
{
    /// <summary>
    /// EIP-225, Specification: "SIGNER_LIMIT: Number of consecutive blocks out of which a signer may
    /// only sign one. Must be floor(SIGNER_COUNT / 2) + 1". These exercise <see cref="CliqueEngine"/>
    /// itself, never a copy of the formula.
    /// </summary>
    public class CliqueRecentSignersLimitTests
    {
        private static readonly EthECKey KeyA = new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private static readonly EthECKey KeyB = new EthECKey("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690");
        private static readonly EthECKey KeyC = new EthECKey("0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365");

        private static CliqueEngine EngineWithSigners(params EthECKey[] signers) => new CliqueEngine(new CliqueConfig
        {
            InitialSigners = signers.Select(k => k.GetPublicAddress()).ToList(),
            LocalSignerAddress = signers[0].GetPublicAddress(),
            BlockPeriodSeconds = 1,
            EpochLength = 30000
        });

        private static byte[] HashOf(BlockHeader header) =>
            new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header));

        private static BlockHeader BuildSealedHeader(EthECKey signer, long number, BigInteger difficulty, BlockHeader? parent = null)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = parent == null ? new byte[32] : HashOf(parent),
                Difficulty = (EvmUInt256)difficulty,
                MixHash = new byte[32],
                Nonce = new byte[8],
                ExtraData = new byte[CliqueEngine.EXTRA_VANITY + CliqueEngine.EXTRA_SEAL],
                Timestamp = number
            };

            var sealHash = BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);
            var signature = signer.SignAndCalculateV(sealHash).CreateStringSignature().HexToByteArray();
            Array.Copy(signature, 0, header.ExtraData, header.ExtraData.Length - CliqueEngine.EXTRA_SEAL, CliqueEngine.EXTRA_SEAL);
            return header;
        }

        [Fact]
        public void Given_ASignerThatAlreadySealedWithinTheLimit_When_ItSealsAgain_Then_EverySignerRefusesTheBlock()
        {
            var engine = EngineWithSigners(KeyA, KeyB, KeyC);

            var header1 = BuildSealedHeader(KeyA, 1, engine.GetDifficulty(1, KeyA.GetPublicAddress()));
            Assert.True(engine.ValidateBlock(header1, null));
            engine.ApplyBlock(header1, KeyA.GetPublicAddress(), HashOf(header1));

            var header2 = BuildSealedHeader(KeyA, 2, engine.GetDifficulty(2, KeyA.GetPublicAddress()), header1);
            var verdict = engine.ValidateBlockInternal(header2, header1);

            Assert.False(verdict.IsValid);
            Assert.Contains("signed recently", verdict.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Given_ASignerOutsideTheRecentWindow_When_ItSeals_Then_TheBlockIsAccepted()
        {
            var engine = EngineWithSigners(KeyA, KeyB, KeyC);

            var header1 = BuildSealedHeader(KeyA, 1, engine.GetDifficulty(1, KeyA.GetPublicAddress()));
            engine.ApplyBlock(header1, KeyA.GetPublicAddress(), HashOf(header1));

            var header2 = BuildSealedHeader(KeyB, 2, engine.GetDifficulty(2, KeyB.GetPublicAddress()), header1);
            engine.ApplyBlock(header2, KeyB.GetPublicAddress(), HashOf(header2));

            var header3 = BuildSealedHeader(KeyA, 3, engine.GetDifficulty(3, KeyA.GetPublicAddress()), header2);
            var verdict = engine.ValidateBlockInternal(header3, header2);

            Assert.True(verdict.IsValid, verdict.Error);
        }

        [Fact]
        public void Given_ASingleSigner_When_ItSealsEveryConsecutiveBlock_Then_EveryBlockIsAccepted()
        {
            var engine = EngineWithSigners(KeyA);
            BlockHeader? parent = null;

            for (long number = 1; number <= 5; number++)
            {
                var header = BuildSealedHeader(KeyA, number, engine.GetDifficulty(number, KeyA.GetPublicAddress()), parent);
                var verdict = engine.ValidateBlockInternal(header, parent);

                Assert.True(verdict.IsValid, verdict.Error);
                engine.ApplyBlock(header, KeyA.GetPublicAddress(), HashOf(header));
                parent = header;
            }
        }

        [Fact]
        public void Given_FiveSignersWhereTheLimitNeedsTheFloorAndThePlusOne_When_ASignerSealsTooSoon_Then_TheBlockIsRefused()
        {
            var keyD = EthECKey.GenerateKey();
            var keyE = EthECKey.GenerateKey();
            var engine = EngineWithSigners(KeyA, KeyB, KeyC, keyD, keyE);

            var header1 = BuildSealedHeader(KeyA, 1, engine.GetDifficulty(1, KeyA.GetPublicAddress()));
            engine.ApplyBlock(header1, KeyA.GetPublicAddress(), HashOf(header1));

            var header2 = BuildSealedHeader(KeyB, 2, engine.GetDifficulty(2, KeyB.GetPublicAddress()), header1);
            engine.ApplyBlock(header2, KeyB.GetPublicAddress(), HashOf(header2));

            var tooSoon = engine.ValidateBlockInternal(
                BuildSealedHeader(KeyA, 3, engine.GetDifficulty(3, KeyA.GetPublicAddress()), header2), header2);
            Assert.False(tooSoon.IsValid);

            var header3 = BuildSealedHeader(KeyC, 3, engine.GetDifficulty(3, KeyC.GetPublicAddress()), header2);
            engine.ApplyBlock(header3, KeyC.GetPublicAddress(), HashOf(header3));

            var header4 = BuildSealedHeader(KeyA, 4, engine.GetDifficulty(4, KeyA.GetPublicAddress()), header3);
            var farEnough = engine.ValidateBlockInternal(header4, header3);
            Assert.True(farEnough.IsValid, farEnough.Error);
        }
    }
}
