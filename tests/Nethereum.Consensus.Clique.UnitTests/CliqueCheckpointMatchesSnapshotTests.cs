using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Consensus.Clique.UnitTests
{
    public class CliqueCheckpointMatchesSnapshotTests
    {
        private static readonly EthECKey KeyA = new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private static readonly EthECKey KeyB = new EthECKey("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690");
        private static readonly EthECKey KeyC = new EthECKey("0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365");

        private static CliqueEngine EngineWith(params EthECKey[] signers) => new CliqueEngine(new CliqueConfig
        {
            InitialSigners = signers.Select(k => k.GetPublicAddress()).ToList(),
            LocalSignerAddress = signers[0].GetPublicAddress(),
            BlockPeriodSeconds = 1,
            EpochLength = 1
        });

        private static BlockHeader CheckpointBlockOne(CliqueEngine engine, params EthECKey[] listed)
        {
            var sorted = CliqueSnapshot.NormalizedAndSorted(listed.Select(k => k.GetPublicAddress()));
            var extra = new byte[CliqueEngine.EXTRA_VANITY + sorted.Count * 20 + CliqueEngine.EXTRA_SEAL];
            for (int i = 0; i < sorted.Count; i++)
                Array.Copy(sorted[i].HexToByteArray(), 0, extra, CliqueEngine.EXTRA_VANITY + i * 20, 20);

            var header = new BlockHeader
            {
                BlockNumber = 1,
                ParentHash = new byte[32],
                Difficulty = (EvmUInt256)engine.GetDifficulty(1, KeyA.GetPublicAddress()),
                MixHash = new byte[32],
                Nonce = new byte[8],
                ExtraData = extra,
                Timestamp = 10
            };
            var sealHash = BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);
            var signature = KeyA.SignAndCalculateV(sealHash).CreateStringSignature().HexToByteArray();
            Array.Copy(signature, 0, header.ExtraData, header.ExtraData.Length - CliqueEngine.EXTRA_SEAL, CliqueEngine.EXTRA_SEAL);
            return header;
        }

        [Fact]
        public void Given_ACheckpointListingASignerTheSnapshotDoesNotHave_When_ItIsValidated_Then_ItIsRefused()
        {
            var engine = EngineWith(KeyA, KeyB);
            var header = CheckpointBlockOne(engine, KeyA, KeyB, KeyC);

            var verdict = engine.ValidateBlockInternal(header, null);

            Assert.False(verdict.IsValid);
            Assert.Contains("checkpoint", verdict.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Given_ACheckpointOmittingASignerTheSnapshotHas_When_ItIsValidated_Then_ItIsRefused()
        {
            var engine = EngineWith(KeyA, KeyB, KeyC);
            var header = CheckpointBlockOne(engine, KeyA, KeyB);

            var verdict = engine.ValidateBlockInternal(header, null);

            Assert.False(verdict.IsValid);
            Assert.Contains("checkpoint", verdict.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Given_ACheckpointListingExactlyTheSnapshotSigners_When_ItIsValidated_Then_ItIsAccepted()
        {
            var engine = EngineWith(KeyA, KeyB, KeyC);
            var header = CheckpointBlockOne(engine, KeyA, KeyB, KeyC);

            var verdict = engine.ValidateBlockInternal(header, null);

            Assert.True(verdict.IsValid, verdict.Error);
        }

        [Fact]
        public void Given_ACheckpointBlockApplied_When_ItsListDiffersFromTheSnapshot_Then_TheSnapshotSignersAreNotReplaced()
        {
            var engine = EngineWith(KeyA, KeyB);
            var header = CheckpointBlockOne(engine, KeyA, KeyB, KeyC);

            engine.ApplyBlock(header, KeyA.GetPublicAddress());

            Assert.Equal(2, engine.CurrentSnapshot.TotalSigners);
            Assert.False(engine.IsAuthorizedSigner(KeyC.GetPublicAddress()));
        }
    }
}
