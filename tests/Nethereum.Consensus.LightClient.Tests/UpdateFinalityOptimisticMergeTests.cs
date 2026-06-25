using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Beaconchain.LightClient.Responses;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Signer.Bls;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    /// <summary>
    /// Validates the <see cref="LightClientService.UpdateFinalityAsync"/> and
    /// <see cref="LightClientService.UpdateOptimisticAsync"/> entrypoints synthesize a
    /// <see cref="LightClientUpdate"/> with default <c>next_sync_committee</c> and
    /// <c>next_sync_committee_branch</c> per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 549–572
    /// (<c>process_light_client_finality_update</c>) and lines 573–586
    /// (<c>process_light_client_optimistic_update</c>), then delegate to
    /// <c>process_light_client_update</c> so the supermajority gate, period-window,
    /// and slot monotonicity apply uniformly.
    /// </summary>
    public class UpdateFinalityOptimisticMergeTests
    {
        private const ulong ElectraActivationSlot = 11_649_024UL;

        [Fact]
        public void Given_FinalityUpdate_When_SynthesizeUpdate_Then_NextSyncCommitteeBranchIsAllZero()
        {
            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot + 1,
                blockNumber: 101);

            var finality = new LightClientFinalityUpdate
            {
                Fork = update.Fork,
                AttestedHeader = update.AttestedHeader,
                FinalizedHeader = update.FinalizedHeader,
                FinalityBranch = update.FinalityBranch,
                SyncAggregate = update.SyncAggregate,
                SignatureSlot = update.SignatureSlot
            };

            var synthesized = InvokeSynthesizeFinality(finality);

            Assert.NotNull(synthesized);
            Assert.Equal(update.Fork, synthesized.Fork);
            Assert.Equal(LightClientForkSpec.NextSyncCommitteeBranchLength(update.Fork), synthesized.NextSyncCommitteeBranch.Count);
            foreach (var root in synthesized.NextSyncCommitteeBranch)
            {
                Assert.True(root.All(b => b == 0));
            }

            Assert.NotNull(synthesized.NextSyncCommittee);
            Assert.True(synthesized.NextSyncCommittee.AggregatePubKey.All(b => b == 0));
        }

        [Fact]
        public void Given_OptimisticUpdate_When_SynthesizeUpdate_Then_BothBranchesZeroAndFinalizedHeaderDefault()
        {
            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot + 1,
                blockNumber: 101);

            var optimistic = new LightClientOptimisticUpdate
            {
                Fork = update.Fork,
                AttestedHeader = update.AttestedHeader,
                SyncAggregate = update.SyncAggregate,
                SignatureSlot = update.SignatureSlot
            };

            var synthesized = InvokeSynthesizeOptimistic(optimistic);

            Assert.NotNull(synthesized);
            Assert.Equal(update.Fork, synthesized.Fork);
            Assert.Equal(LightClientForkSpec.NextSyncCommitteeBranchLength(update.Fork), synthesized.NextSyncCommitteeBranch.Count);
            Assert.Equal(LightClientForkSpec.FinalityBranchLength(update.Fork), synthesized.FinalityBranch.Count);
            foreach (var root in synthesized.NextSyncCommitteeBranch)
            {
                Assert.True(root.All(b => b == 0));
            }
            foreach (var root in synthesized.FinalityBranch)
            {
                Assert.True(root.All(b => b == 0));
            }

            Assert.NotNull(synthesized.FinalizedHeader);
        }

        [Fact]
        public async Task Given_UpdateOptimisticAsync_With_AttestedSlotAdvancingForward_When_Calling_Then_Applied()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(
                slot: ElectraActivationSlot,
                blockNumber: 100);

            var optimistic = BuildOptimisticUpdateAtSlot(bootstrap, attestedSlot: ElectraActivationSlot + 1);

            var (service, state) = await ApplyOptimisticAsync(bootstrap, optimistic);

            Assert.Equal(ElectraActivationSlot + 1, state.OptimisticSlot);
        }

        [Fact]
        public async Task Given_UpdateFinalityAsync_With_ValidFinalityUpdate_When_Calling_Then_FinalizedAdvances()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(
                slot: ElectraActivationSlot,
                blockNumber: 100);

            var finality = BuildFinalityUpdate(bootstrap, attestedSlot: ElectraActivationSlot + 1);

            var (service, state) = await ApplyFinalityAsync(bootstrap, finality);

            Assert.Equal(finality.FinalizedHeader.Beacon.Slot, state.FinalizedSlot);
        }

        private static LightClientOptimisticUpdate BuildOptimisticUpdateAtSlot(LightClientBootstrap bootstrap, ulong attestedSlot)
        {
            var sourceUpdate = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: attestedSlot,
                blockNumber: 101);

            return new LightClientOptimisticUpdate
            {
                Fork = sourceUpdate.Fork,
                AttestedHeader = sourceUpdate.AttestedHeader,
                SyncAggregate = sourceUpdate.SyncAggregate,
                SignatureSlot = sourceUpdate.SignatureSlot
            };
        }

        private static LightClientFinalityUpdate BuildFinalityUpdate(LightClientBootstrap bootstrap, ulong attestedSlot)
        {
            var sourceUpdate = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: attestedSlot,
                blockNumber: 101);

            return new LightClientFinalityUpdate
            {
                Fork = sourceUpdate.Fork,
                AttestedHeader = sourceUpdate.AttestedHeader,
                FinalizedHeader = sourceUpdate.FinalizedHeader,
                FinalityBranch = sourceUpdate.FinalityBranch,
                SyncAggregate = sourceUpdate.SyncAggregate,
                SignatureSlot = sourceUpdate.SignatureSlot
            };
        }

        private static async Task<(LightClientService Service, LightClientState State)> ApplyOptimisticAsync(
            LightClientBootstrap bootstrap,
            LightClientOptimisticUpdate optimistic)
        {
            var beaconClient = new StubLightClientApi(bootstrap, optimistic, null);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap);
            var store = new InMemoryLightClientStore();
            var bls = new AcceptingBls();

            var service = new LightClientService(beaconClient, bls, config, store);
            await service.InitializeAsync();
            await service.UpdateOptimisticAsync();

            return (service, service.GetState());
        }

        private static async Task<(LightClientService Service, LightClientState State)> ApplyFinalityAsync(
            LightClientBootstrap bootstrap,
            LightClientFinalityUpdate finality)
        {
            var beaconClient = new StubLightClientApi(bootstrap, null, finality);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap);
            var store = new InMemoryLightClientStore();
            var bls = new AcceptingBls();

            var service = new LightClientService(beaconClient, bls, config, store);
            await service.InitializeAsync();
            await service.UpdateFinalityAsync();

            return (service, service.GetState());
        }

        private static LightClientUpdate InvokeSynthesizeFinality(LightClientFinalityUpdate finality)
        {
            var method = typeof(LightClientService).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m =>
                    m.Name == "SynthesizeUpdate" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(LightClientFinalityUpdate));
            Assert.NotNull(method);
            return (LightClientUpdate)method!.Invoke(null, new object[] { finality });
        }

        private static LightClientUpdate InvokeSynthesizeOptimistic(LightClientOptimisticUpdate optimistic)
        {
            var method = typeof(LightClientService).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m =>
                    m.Name == "SynthesizeUpdate" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(LightClientOptimisticUpdate));
            Assert.NotNull(method);
            return (LightClientUpdate)method!.Invoke(null, new object[] { optimistic });
        }

        private sealed class StubLightClientApi : ILightClientApi
        {
            private readonly LightClientBootstrap _bootstrap;
            private readonly LightClientOptimisticUpdate _optimistic;
            private readonly LightClientFinalityUpdate _finality;

            public StubLightClientApi(
                LightClientBootstrap bootstrap,
                LightClientOptimisticUpdate optimistic,
                LightClientFinalityUpdate finality)
            {
                _bootstrap = bootstrap;
                _optimistic = optimistic;
                _finality = finality;
            }

            public Task<LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot)
            {
                return Task.FromResult(LightClientServiceTests.TestDataFactory.CreateBootstrapResponse(_bootstrap));
            }

            public Task<IReadOnlyList<LightClientUpdateResponse>> GetUpdatesAsync(ulong fromPeriod, ulong count) =>
                Task.FromResult<IReadOnlyList<LightClientUpdateResponse>>(Array.Empty<LightClientUpdateResponse>());

            public Task<LightClientFinalityUpdateResponse> GetFinalityUpdateAsync()
            {
                if (_finality == null) return Task.FromResult<LightClientFinalityUpdateResponse>(null);
                return Task.FromResult(BuildFinalityResponse(_finality));
            }

            public Task<LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync()
            {
                if (_optimistic == null) return Task.FromResult<LightClientOptimisticUpdateResponse>(null);
                return Task.FromResult(BuildOptimisticResponse(_optimistic));
            }

            private static LightClientFinalityUpdateResponse BuildFinalityResponse(LightClientFinalityUpdate finality)
            {
                var attestedDto = ConvertHeader(finality.AttestedHeader);
                var finalizedDto = ConvertHeader(finality.FinalizedHeader);

                return new LightClientFinalityUpdateResponse
                {
                    Version = "deneb",
                    Data = new LightClientFinalityUpdateData
                    {
                        AttestedHeader = attestedDto,
                        FinalizedHeader = finalizedDto,
                        FinalityBranch = finality.FinalityBranch.Select(ToHex).ToList(),
                        SyncAggregate = ConvertAggregate(finality.SyncAggregate),
                        SignatureSlot = finality.SignatureSlot.ToString()
                    }
                };
            }

            private static LightClientOptimisticUpdateResponse BuildOptimisticResponse(LightClientOptimisticUpdate optimistic)
            {
                return new LightClientOptimisticUpdateResponse
                {
                    Version = "deneb",
                    Data = new LightClientOptimisticUpdateData
                    {
                        AttestedHeader = ConvertHeader(optimistic.AttestedHeader),
                        SyncAggregate = ConvertAggregate(optimistic.SyncAggregate),
                        SignatureSlot = optimistic.SignatureSlot.ToString()
                    }
                };
            }

            private static LightClientHeaderDto ConvertHeader(LightClientHeader header)
            {
                return new LightClientHeaderDto
                {
                    Beacon = new BeaconBlockHeaderDto
                    {
                        Slot = header.Beacon.Slot.ToString(),
                        ProposerIndex = header.Beacon.ProposerIndex.ToString(),
                        ParentRoot = ToHex(header.Beacon.ParentRoot),
                        StateRoot = ToHex(header.Beacon.StateRoot),
                        BodyRoot = ToHex(header.Beacon.BodyRoot)
                    },
                    Execution = new ExecutionPayloadHeaderDto
                    {
                        ParentHash = ToHex(header.Execution.ParentHash),
                        FeeRecipient = ToHex(header.Execution.FeeRecipient),
                        StateRoot = ToHex(header.Execution.StateRoot),
                        ReceiptsRoot = ToHex(header.Execution.ReceiptsRoot),
                        LogsBloom = ToHex(header.Execution.LogsBloom),
                        PrevRandao = ToHex(header.Execution.PrevRandao),
                        BlockNumber = header.Execution.BlockNumber.ToString(),
                        GasLimit = header.Execution.GasLimit.ToString(),
                        GasUsed = header.Execution.GasUsed.ToString(),
                        Timestamp = header.Execution.Timestamp.ToString(),
                        ExtraData = ToHex(header.Execution.ExtraData),
                        BaseFeePerGas = "0",
                        BlockHash = ToHex(header.Execution.BlockHash),
                        TransactionsRoot = ToHex(header.Execution.TransactionsRoot),
                        WithdrawalsRoot = ToHex(header.Execution.WithdrawalsRoot),
                        BlobGasUsed = header.Execution.BlobGasUsed.ToString(),
                        ExcessBlobGas = header.Execution.ExcessBlobGas.ToString()
                    },
                    ExecutionBranch = header.ExecutionBranch.Select(ToHex).ToList()
                };
            }

            private static SyncAggregateDto ConvertAggregate(SyncAggregate aggregate)
            {
                return new SyncAggregateDto
                {
                    SyncCommitteeBits = ToHex(aggregate.SyncCommitteeBits),
                    SyncCommitteeSignature = ToHex(aggregate.SyncCommitteeSignature)
                };
            }

            private static string ToHex(byte[] bytes) =>
                "0x" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private sealed class AcceptingBls : IBls
        {
            public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain) => true;
            public byte[] AggregateSignatures(byte[][] signatures) => throw new NotSupportedException();
            public bool Verify(byte[] signature, byte[] publicKey, byte[] message) => throw new NotSupportedException();
            public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey) => throw new NotSupportedException();
        }
    }
}
