using System.Collections.Generic;
using System.Linq;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.Ssz.Tests
{
    /// <summary>
    /// Regression for the C2-A2 wire-field bug: pre-fix, <c>LightClientUpdate.Encode</c>
    /// and <c>Decode</c> read the <c>NextSyncCommitteeBranch</c> vector length from a flat
    /// <c>CurrentSyncCommitteeBranchLength = 5</c> constant, which made Electra updates
    /// (depth 6 per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
    /// specs/electra/light-client/sync-protocol.md</see> line 56) unparseable. Post-fix the
    /// vector length is derived from <see cref="LightClientForkSpec.NextSyncCommitteeBranchLength"/>.
    /// </summary>
    public class SszLightClientUpdateElectraWireFieldTests
    {
        [Fact]
        public void Encode_Decode_RoundTrips_With_Electra_NextSyncCommitteeBranch_Depth_6()
        {
            var fork = ConsensusFork.Electra;
            var update = BuildElectraUpdate();

            Assert.Equal(6, update.NextSyncCommitteeBranch.Count);
            Assert.Equal(7, update.FinalityBranch.Count);

            var encoded = update.Encode(fork);
            var decoded = LightClientUpdate.Decode(encoded, fork);

            Assert.Equal(6, decoded.NextSyncCommitteeBranch.Count);
            Assert.Equal(7, decoded.FinalityBranch.Count);
            for (var i = 0; i < update.NextSyncCommitteeBranch.Count; i++)
                Assert.Equal(update.NextSyncCommitteeBranch[i], decoded.NextSyncCommitteeBranch[i]);
            for (var i = 0; i < update.FinalityBranch.Count; i++)
                Assert.Equal(update.FinalityBranch[i], decoded.FinalityBranch[i]);
            Assert.Equal(update.SignatureSlot, decoded.SignatureSlot);
        }

        [Fact]
        public void Encode_Rejects_Electra_NextSyncCommitteeBranch_Of_Wrong_Depth_5()
        {
            var update = BuildElectraUpdate();
            update.NextSyncCommitteeBranch = CreateBranch(5, 0x77);

            Assert.Throws<System.InvalidOperationException>(() => update.Encode(ConsensusFork.Electra));
        }

        [Theory]
        [InlineData(ConsensusFork.Deneb, 5)]
        [InlineData(ConsensusFork.Electra, 6)]
        public void NextSyncCommitteeBranchLength_PerFork_MatchesSpec(ConsensusFork fork, int expected)
        {
            Assert.Equal(expected, LightClientForkSpec.NextSyncCommitteeBranchLength(fork));
        }

        private static LightClientUpdate BuildElectraUpdate()
        {
            var fork = ConsensusFork.Electra;
            return new LightClientUpdate
            {
                Fork = fork,
                AttestedHeader = BuildHeader(fork),
                NextSyncCommittee = BuildSyncCommittee(),
                NextSyncCommitteeBranch = CreateBranch(
                    LightClientForkSpec.NextSyncCommitteeBranchLength(fork), 0x60),
                FinalizedHeader = BuildHeader(fork),
                FinalityBranch = CreateBranch(
                    LightClientForkSpec.FinalityBranchLength(fork), 0x70),
                SyncAggregate = BuildSyncAggregate(),
                SignatureSlot = 11_700_000UL
            };
        }

        private static LightClientHeader BuildHeader(ConsensusFork fork) => new LightClientHeader
        {
            Fork = fork,
            Beacon = new BeaconBlockHeader
            {
                Slot = 11_650_000,
                ProposerIndex = 1,
                ParentRoot = Bytes(SszBasicTypes.RootLength, 0x01),
                StateRoot = Bytes(SszBasicTypes.RootLength, 0x02),
                BodyRoot = Bytes(SszBasicTypes.RootLength, 0x03)
            },
            Execution = new ExecutionPayloadHeader
            {
                Fork = fork,
                ParentHash = Bytes(SszBasicTypes.RootLength, 0x10),
                FeeRecipient = Bytes(20, 0x11),
                StateRoot = Bytes(SszBasicTypes.RootLength, 0x12),
                ReceiptsRoot = Bytes(SszBasicTypes.RootLength, 0x13),
                LogsBloom = Bytes(SszBasicTypes.LogsBloomLength, 0x14),
                PrevRandao = Bytes(SszBasicTypes.RootLength, 0x15),
                BlockNumber = 100,
                GasLimit = 30_000_000,
                GasUsed = 1_000_000,
                Timestamp = 123456,
                ExtraData = new byte[] { 0xAA },
                BaseFeePerGas = Bytes(SszBasicTypes.RootLength, 0x16),
                BlockHash = Bytes(SszBasicTypes.RootLength, 0x17),
                TransactionsRoot = Bytes(SszBasicTypes.RootLength, 0x18),
                WithdrawalsRoot = Bytes(SszBasicTypes.RootLength, 0x19),
                BlobGasUsed = 0,
                ExcessBlobGas = 0
            },
            ExecutionBranch = CreateBranch(LightClientForkSpec.ExecutionBranchDepth(fork), 0x40)
        };

        private static SyncCommittee BuildSyncCommittee()
        {
            var pubkeys = Enumerable.Range(0, SszBasicTypes.SyncCommitteeSize)
                .Select(i => Bytes(SszBasicTypes.PubKeyLength, (byte)(i % 255)))
                .ToList();
            return new SyncCommittee
            {
                PubKeys = pubkeys,
                AggregatePubKey = Bytes(SszBasicTypes.PubKeyLength, 0xAA)
            };
        }

        private static SyncAggregate BuildSyncAggregate() => new SyncAggregate
        {
            SyncCommitteeBits = Bytes(SszBasicTypes.SyncCommitteeSize / 8, 0xCC),
            SyncCommitteeSignature = Bytes(SszBasicTypes.SignatureLength, 0xDD)
        };

        private static List<byte[]> CreateBranch(int length, byte seed)
        {
            var branch = new List<byte[]>(length);
            for (var i = 0; i < length; i++)
                branch.Add(Bytes(SszBasicTypes.RootLength, (byte)(seed + i)));
            return branch;
        }

        private static byte[] Bytes(int length, byte seed)
        {
            var data = new byte[length];
            for (var i = 0; i < length; i++) data[i] = (byte)(seed + i);
            return data;
        }
    }
}
