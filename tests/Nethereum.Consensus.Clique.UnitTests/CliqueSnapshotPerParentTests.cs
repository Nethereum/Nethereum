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
    public class CliqueSnapshotPerParentTests
    {
        private static readonly EthECKey KeyA = new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private static readonly EthECKey KeyB = new EthECKey("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690");

        private static CliqueEngine EngineWithSigners(params EthECKey[] signers) => new CliqueEngine(new CliqueConfig
        {
            InitialSigners = signers.Select(k => k.GetPublicAddress()).ToList(),
            LocalSignerAddress = signers[0].GetPublicAddress(),
            BlockPeriodSeconds = 1,
            EpochLength = 30000
        });

        private static byte[] HashOf(BlockHeader header) =>
            new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header));

        private static BlockHeader Sealed(CliqueEngine engine, EthECKey signer, long number, byte[] parentHash, long timestamp)
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
            System.Array.Copy(signature, 0, header.ExtraData, header.ExtraData.Length - CliqueEngine.EXTRA_SEAL, CliqueEngine.EXTRA_SEAL);
            return header;
        }

        [Fact]
        public void Given_TwoCompetingBlocksAtTheSameHeight_When_EachIsValidated_Then_EachUsesItsParentsSnapshot()
        {
            var engine = EngineWithSigners(KeyA, KeyB);
            var genesisHash = new byte[32];

            var first = Sealed(engine, KeyA, 1, genesisHash, 100);
            engine.ApplyBlock(first, KeyA.GetPublicAddress(), HashOf(first));

            var sibling = Sealed(engine, KeyA, 1, genesisHash, 101);
            var siblingVerdict = engine.ValidateBlockInternal(sibling, null);
            Assert.True(siblingVerdict.IsValid, siblingVerdict.Error);

            var child = Sealed(engine, KeyB, 2, HashOf(first), 102);
            var childVerdict = engine.ValidateBlockInternal(child, first);
            Assert.True(childVerdict.IsValid, childVerdict.Error);
        }

        [Fact]
        public void Given_ABlockWhoseParentWasNeverApplied_When_ItIsValidated_Then_ItIsRefusedAsAnUnknownAncestor()
        {
            var engine = EngineWithSigners(KeyA, KeyB);

            var orphan = Sealed(engine, KeyB, 2, new byte[32].Select(b => (byte)7).ToArray(), 100);
            var verdict = engine.ValidateBlockInternal(orphan, null);

            Assert.False(verdict.IsValid);
            Assert.Contains("ancestor", verdict.Error, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
