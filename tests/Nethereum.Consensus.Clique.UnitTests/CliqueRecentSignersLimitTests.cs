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

        private static BlockHeader BuildSealedHeader(EthECKey signer, long number, BigInteger difficulty)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = new byte[32],
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
            engine.ApplyBlock(header1, KeyA.GetPublicAddress());

            var header2 = BuildSealedHeader(KeyA, 2, engine.GetDifficulty(2, KeyA.GetPublicAddress()));
            var verdict = engine.ValidateBlockInternal(header2, header1);

            Assert.False(verdict.IsValid);
            Assert.Contains("signed recently", verdict.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Given_ASignerOutsideTheRecentWindow_When_ItSeals_Then_TheBlockIsAccepted()
        {
            var engine = EngineWithSigners(KeyA, KeyB, KeyC);

            var header1 = BuildSealedHeader(KeyA, 1, engine.GetDifficulty(1, KeyA.GetPublicAddress()));
            engine.ApplyBlock(header1, KeyA.GetPublicAddress());

            var header2 = BuildSealedHeader(KeyB, 2, engine.GetDifficulty(2, KeyB.GetPublicAddress()));
            engine.ApplyBlock(header2, KeyB.GetPublicAddress());

            var header3 = BuildSealedHeader(KeyA, 3, engine.GetDifficulty(3, KeyA.GetPublicAddress()));
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
                var header = BuildSealedHeader(KeyA, number, engine.GetDifficulty(number, KeyA.GetPublicAddress()));
                var verdict = engine.ValidateBlockInternal(header, parent);

                Assert.True(verdict.IsValid, verdict.Error);
                engine.ApplyBlock(header, KeyA.GetPublicAddress());
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
            engine.ApplyBlock(header1, KeyA.GetPublicAddress());

            var header3 = BuildSealedHeader(KeyA, 3, engine.GetDifficulty(3, KeyA.GetPublicAddress()));
            var tooSoon = engine.ValidateBlockInternal(header3, header1);
            Assert.False(tooSoon.IsValid);

            var header4 = BuildSealedHeader(KeyA, 4, engine.GetDifficulty(4, KeyA.GetPublicAddress()));
            var farEnough = engine.ValidateBlockInternal(header4, header1);
            Assert.True(farEnough.IsValid, farEnough.Error);
        }
    }
}
