using System.Collections.Generic;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Beaconchain.LightClient.Responses;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    /// <summary>
    /// Regression for C4-F5: the mapper must stamp <c>update.Fork</c> from
    /// <c>signature_slot</c> for <c>LightClientUpdate</c>, <c>LightClientFinalityUpdate</c>,
    /// and <c>LightClientOptimisticUpdate</c> per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 451–454
    /// (<c>fork_version = compute_fork_version(compute_epoch_at_slot(update.signature_slot))</c>).
    /// For <c>LightClientBootstrap</c> the bootstrap header slot remains the anchor since the
    /// container has no <c>signature_slot</c>.
    /// </summary>
    public class LightClientResponseMapperForkStampTests
    {
        private const ulong DenebSlot = 9_000_000UL;
        private const ulong ElectraSlot = 11_700_000UL;
        private const ulong AltairSlot = 2_400_000UL;

        [Fact]
        public void Update_StampsForkFromSignatureSlot_NotAttestedHeaderSlot()
        {
            var response = new LightClientUpdateResponse
            {
                Data = new LightClientUpdateData
                {
                    AttestedHeader = HeaderDto(DenebSlot),
                    FinalizedHeader = HeaderDto(DenebSlot),
                    NextSyncCommittee = SampleSyncCommitteeDto(),
                    NextSyncCommitteeBranch = new List<string>(),
                    FinalityBranch = new List<string>(),
                    SyncAggregate = SampleSyncAggregateDto(),
                    SignatureSlot = ElectraSlot.ToString()
                }
            };

            var update = LightClientResponseMapper.ToDomain(response);

            Assert.Equal(ConsensusFork.Electra, update.Fork);
            Assert.Equal(ElectraSlot, update.SignatureSlot);
            Assert.Equal(ConsensusFork.Electra, update.AttestedHeader.Fork);
            Assert.Equal(ConsensusFork.Electra, update.FinalizedHeader.Fork);
        }

        [Fact]
        public void FinalityUpdate_StampsForkFromSignatureSlot_NotAttestedHeaderSlot()
        {
            var response = new LightClientFinalityUpdateResponse
            {
                Data = new LightClientFinalityUpdateData
                {
                    AttestedHeader = HeaderDto(DenebSlot),
                    FinalizedHeader = HeaderDto(DenebSlot),
                    FinalityBranch = new List<string>(),
                    SyncAggregate = SampleSyncAggregateDto(),
                    SignatureSlot = ElectraSlot.ToString()
                }
            };

            var update = LightClientResponseMapper.ToDomain(response);

            Assert.Equal(ConsensusFork.Electra, update.Fork);
            Assert.Equal(ElectraSlot, update.SignatureSlot);
        }

        [Fact]
        public void OptimisticUpdate_StampsForkFromSignatureSlot_NotAttestedHeaderSlot()
        {
            var response = new LightClientOptimisticUpdateResponse
            {
                Data = new LightClientOptimisticUpdateData
                {
                    AttestedHeader = HeaderDto(DenebSlot),
                    SyncAggregate = SampleSyncAggregateDto(),
                    SignatureSlot = ElectraSlot.ToString()
                }
            };

            var update = LightClientResponseMapper.ToDomain(response);

            Assert.Equal(ConsensusFork.Electra, update.Fork);
            Assert.Equal(ElectraSlot, update.SignatureSlot);
        }

        [Fact]
        public void Bootstrap_StampsForkFromHeaderSlot_NoSignatureSlot()
        {
            var response = new LightClientBootstrapResponse
            {
                Data = new LightClientBootstrapData
                {
                    Header = HeaderDto(AltairSlot),
                    CurrentSyncCommittee = SampleSyncCommitteeDto(),
                    CurrentSyncCommitteeBranch = new List<string>()
                }
            };

            var bootstrap = LightClientResponseMapper.ToDomain(response);

            Assert.Equal(ConsensusFork.Altair, bootstrap.Fork);
        }

        [Fact]
        public void Update_AtDenebSignatureSlot_StaysDeneb()
        {
            var response = new LightClientUpdateResponse
            {
                Data = new LightClientUpdateData
                {
                    AttestedHeader = HeaderDto(DenebSlot),
                    FinalizedHeader = HeaderDto(DenebSlot),
                    NextSyncCommittee = SampleSyncCommitteeDto(),
                    NextSyncCommitteeBranch = new List<string>(),
                    FinalityBranch = new List<string>(),
                    SyncAggregate = SampleSyncAggregateDto(),
                    SignatureSlot = DenebSlot.ToString()
                }
            };

            var update = LightClientResponseMapper.ToDomain(response);

            Assert.Equal(ConsensusFork.Deneb, update.Fork);
        }

        private static LightClientHeaderDto HeaderDto(ulong slot) => new LightClientHeaderDto
        {
            Beacon = new BeaconBlockHeaderDto
            {
                Slot = slot.ToString(),
                ProposerIndex = "1",
                ParentRoot = "0x" + new string('0', 64),
                StateRoot = "0x" + new string('0', 64),
                BodyRoot = "0x" + new string('0', 64)
            },
            Execution = new ExecutionPayloadHeaderDto
            {
                ParentHash = "0x" + new string('0', 64),
                FeeRecipient = "0x" + new string('0', 40),
                StateRoot = "0x" + new string('0', 64),
                ReceiptsRoot = "0x" + new string('0', 64),
                LogsBloom = "0x" + new string('0', 512),
                PrevRandao = "0x" + new string('0', 64),
                BlockNumber = "0",
                GasLimit = "0",
                GasUsed = "0",
                Timestamp = "0",
                ExtraData = "0x",
                BaseFeePerGas = "0",
                BlockHash = "0x" + new string('0', 64),
                TransactionsRoot = "0x" + new string('0', 64),
                WithdrawalsRoot = "0x" + new string('0', 64),
                BlobGasUsed = "0",
                ExcessBlobGas = "0"
            },
            ExecutionBranch = new List<string>()
        };

        private static SyncCommitteeDto SampleSyncCommitteeDto() => new SyncCommitteeDto
        {
            PubKeys = new List<string>(),
            AggregatePubKey = "0x" + new string('0', 96)
        };

        private static SyncAggregateDto SampleSyncAggregateDto() => new SyncAggregateDto
        {
            SyncCommitteeBits = "0x" + new string('0', 128),
            SyncCommitteeSignature = "0x" + new string('0', 192)
        };
    }
}
