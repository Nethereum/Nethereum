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
    /// Validates the three-way sync-aggregate verifier split per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see>:
    /// optimistic updates apply only the baseline floor (lines 580–590); full
    /// <c>LightClientUpdate</c> evaluation adds the supermajority quorum when
    /// <c>is_finality_update</c> holds (lines 543, 503–548); finality updates require both
    /// gates (lines 569–579). The BLS aggregate verification is stubbed to <c>true</c> so
    /// the test focuses on quorum gating.
    /// </summary>
    public class SyncAggregateVerifierSplitTests
    {
        private const ulong ElectraActivationSlot = 11_649_024UL;

        [Fact]
        public async Task Given_FullUpdate_With_341Participants_When_Verifying_Then_RejectedAsSubSupermajority()
        {
            var service = await CreateInitializedServiceAsync();
            var update = CreateUpdateWithParticipants(341);

            Assert.False(InvokeVerifyFullUpdateSyncAggregate(service, update));
        }

        [Fact]
        public async Task Given_FullUpdate_With_342Participants_When_Verifying_Then_AcceptedAtExactMinimum()
        {
            var service = await CreateInitializedServiceAsync();
            var update = CreateUpdateWithParticipants(342);

            Assert.True(InvokeVerifyFullUpdateSyncAggregate(service, update));
        }

        [Fact]
        public async Task Given_NonFinalityUpdate_With_1Participant_When_Verifying_Then_AcceptedAtBaselineFloor()
        {
            var service = await CreateInitializedServiceAsync();
            var update = CreateUpdateWithParticipants(1);
            update.FinalityBranch = LightClientServiceTests.TestDataFactory.CreateZeroBranch(
                LightClientForkSpec.FinalityBranchDepth(ConsensusFork.Electra));

            Assert.True(InvokeVerifyFullUpdateSyncAggregate(service, update));
        }

        [Fact]
        public async Task Given_NonFinalityUpdate_With_0Participants_When_Verifying_Then_RejectedAsBelowBaseline()
        {
            var service = await CreateInitializedServiceAsync();
            var update = CreateUpdateWithParticipants(0);
            update.FinalityBranch = LightClientServiceTests.TestDataFactory.CreateZeroBranch(
                LightClientForkSpec.FinalityBranchDepth(ConsensusFork.Electra));

            Assert.False(InvokeVerifyFullUpdateSyncAggregate(service, update));
        }

        [Fact]
        public async Task Given_OptimisticUpdate_With_1Participant_When_Verifying_Then_AcceptedAtBaselineFloor()
        {
            var service = await CreateInitializedServiceAsync();
            var optimistic = CreateOptimisticUpdate(participantCount: 1);

            Assert.True(InvokeVerifyOptimisticSyncAggregate(service, optimistic));
        }

        [Fact]
        public async Task Given_OptimisticUpdate_With_0Participants_When_Verifying_Then_RejectedAsBelowBaseline()
        {
            var service = await CreateInitializedServiceAsync();
            var optimistic = CreateOptimisticUpdate(participantCount: 0);

            Assert.False(InvokeVerifyOptimisticSyncAggregate(service, optimistic));
        }

        [Fact]
        public async Task Given_OptimisticUpdate_With_341Participants_When_Verifying_Then_AcceptedAsSupermajorityNotRequired()
        {
            var service = await CreateInitializedServiceAsync();
            var optimistic = CreateOptimisticUpdate(participantCount: 341);

            Assert.True(InvokeVerifyOptimisticSyncAggregate(service, optimistic));
        }

        [Fact]
        public async Task Given_FinalityUpdate_With_341Participants_When_Verifying_Then_RejectedAsSubSupermajority()
        {
            var service = await CreateInitializedServiceAsync();
            var finality = CreateFinalityUpdate(participantCount: 341);

            Assert.False(InvokeVerifyFinalitySyncAggregate(service, finality));
        }

        [Fact]
        public async Task Given_FinalityUpdate_With_342Participants_When_Verifying_Then_AcceptedAtExactMinimum()
        {
            var service = await CreateInitializedServiceAsync();
            var finality = CreateFinalityUpdate(participantCount: 342);

            Assert.True(InvokeVerifyFinalitySyncAggregate(service, finality));
        }

        private static async Task<LightClientService> CreateInitializedServiceAsync()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(
                slot: ElectraActivationSlot,
                blockNumber: 100);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap);
            var store = new InMemoryLightClientStore();
            var api = new StubLightClientApi(bootstrap);
            var bls = new AlwaysTrueBls();

            var service = new LightClientService(api, bls, config, store);
            await service.InitializeAsync();
            return service;
        }

        private static LightClientUpdate CreateUpdateWithParticipants(int participantCount)
        {
            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot + 1,
                blockNumber: 101);
            update.SyncAggregate = SyncAggregateQuorumTests.CreateAggregate(participantCount);
            return update;
        }

        private static LightClientOptimisticUpdate CreateOptimisticUpdate(int participantCount)
        {
            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot + 1,
                blockNumber: 101);

            return new LightClientOptimisticUpdate
            {
                Fork = update.Fork,
                AttestedHeader = update.AttestedHeader,
                SyncAggregate = SyncAggregateQuorumTests.CreateAggregate(participantCount),
                SignatureSlot = update.SignatureSlot
            };
        }

        private static LightClientFinalityUpdate CreateFinalityUpdate(int participantCount)
        {
            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot + 1,
                blockNumber: 101);

            return new LightClientFinalityUpdate
            {
                Fork = update.Fork,
                AttestedHeader = update.AttestedHeader,
                FinalizedHeader = update.FinalizedHeader,
                FinalityBranch = update.FinalityBranch,
                SyncAggregate = SyncAggregateQuorumTests.CreateAggregate(participantCount),
                SignatureSlot = update.SignatureSlot
            };
        }

        private static bool InvokeVerifyFullUpdateSyncAggregate(LightClientService service, LightClientUpdate update)
        {
            var method = typeof(LightClientService).GetMethod(
                "VerifyFullUpdateSyncAggregate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (bool)method!.Invoke(service, new object[] { update });
        }

        private static bool InvokeVerifyOptimisticSyncAggregate(LightClientService service, LightClientOptimisticUpdate update)
        {
            var method = typeof(LightClientService).GetMethod(
                "VerifyOptimisticSyncAggregate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (bool)method!.Invoke(service, new object[] { update });
        }

        private static bool InvokeVerifyFinalitySyncAggregate(LightClientService service, LightClientFinalityUpdate update)
        {
            var method = typeof(LightClientService).GetMethod(
                "VerifyFinalitySyncAggregate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (bool)method!.Invoke(service, new object[] { update });
        }

        private sealed class StubLightClientApi : ILightClientApi
        {
            private readonly LightClientBootstrap _bootstrap;

            public StubLightClientApi(LightClientBootstrap bootstrap)
            {
                _bootstrap = bootstrap;
            }

            public Task<LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot)
            {
                return Task.FromResult(CreateBootstrapResponse(_bootstrap));
            }

            public Task<IReadOnlyList<LightClientUpdateResponse>> GetUpdatesAsync(ulong fromPeriod, ulong count) =>
                Task.FromResult<IReadOnlyList<LightClientUpdateResponse>>(Array.Empty<LightClientUpdateResponse>());

            public Task<LightClientFinalityUpdateResponse> GetFinalityUpdateAsync() =>
                Task.FromResult<LightClientFinalityUpdateResponse>(null);

            public Task<LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync() =>
                Task.FromResult<LightClientOptimisticUpdateResponse>(null);

            private static LightClientBootstrapResponse CreateBootstrapResponse(LightClientBootstrap bootstrap)
            {
                return new LightClientBootstrapResponse
                {
                    Version = "deneb",
                    Data = new LightClientBootstrapData
                    {
                        Header = new LightClientHeaderDto
                        {
                            Beacon = new BeaconBlockHeaderDto
                            {
                                Slot = bootstrap.Header.Beacon.Slot.ToString(),
                                ProposerIndex = bootstrap.Header.Beacon.ProposerIndex.ToString(),
                                ParentRoot = ToHex(bootstrap.Header.Beacon.ParentRoot),
                                StateRoot = ToHex(bootstrap.Header.Beacon.StateRoot),
                                BodyRoot = ToHex(bootstrap.Header.Beacon.BodyRoot)
                            },
                            Execution = new ExecutionPayloadHeaderDto
                            {
                                ParentHash = ToHex(bootstrap.Header.Execution.ParentHash),
                                FeeRecipient = ToHex(bootstrap.Header.Execution.FeeRecipient),
                                StateRoot = ToHex(bootstrap.Header.Execution.StateRoot),
                                ReceiptsRoot = ToHex(bootstrap.Header.Execution.ReceiptsRoot),
                                LogsBloom = ToHex(bootstrap.Header.Execution.LogsBloom),
                                PrevRandao = ToHex(bootstrap.Header.Execution.PrevRandao),
                                BlockNumber = bootstrap.Header.Execution.BlockNumber.ToString(),
                                GasLimit = bootstrap.Header.Execution.GasLimit.ToString(),
                                GasUsed = bootstrap.Header.Execution.GasUsed.ToString(),
                                Timestamp = bootstrap.Header.Execution.Timestamp.ToString(),
                                ExtraData = ToHex(bootstrap.Header.Execution.ExtraData),
                                BaseFeePerGas = "0",
                                BlockHash = ToHex(bootstrap.Header.Execution.BlockHash),
                                TransactionsRoot = ToHex(bootstrap.Header.Execution.TransactionsRoot),
                                WithdrawalsRoot = ToHex(bootstrap.Header.Execution.WithdrawalsRoot),
                                BlobGasUsed = bootstrap.Header.Execution.BlobGasUsed.ToString(),
                                ExcessBlobGas = bootstrap.Header.Execution.ExcessBlobGas.ToString()
                            },
                            ExecutionBranch = bootstrap.Header.ExecutionBranch.Select(ToHex).ToList()
                        },
                        CurrentSyncCommittee = new SyncCommitteeDto
                        {
                            PubKeys = bootstrap.CurrentSyncCommittee.PubKeys.Select(ToHex).ToList(),
                            AggregatePubKey = ToHex(bootstrap.CurrentSyncCommittee.AggregatePubKey)
                        },
                        CurrentSyncCommitteeBranch = bootstrap.CurrentSyncCommitteeBranch.Select(ToHex).ToList()
                    }
                };
            }

            private static string ToHex(byte[] bytes) =>
                "0x" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private sealed class AlwaysTrueBls : IBls
        {
            public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain) => true;

            public byte[] AggregateSignatures(byte[][] signatures) => throw new NotSupportedException();

            public bool Verify(byte[] signature, byte[] publicKey, byte[] message) => throw new NotSupportedException();

            public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey) => throw new NotSupportedException();
        }
    }
}
