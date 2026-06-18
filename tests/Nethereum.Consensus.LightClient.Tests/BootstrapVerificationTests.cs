using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Beaconchain.LightClient.Responses;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Signer.Bls;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class BootstrapVerificationTests
    {
        [Theory]
        [InlineData(ConsensusFork.Capella, 6_209_536UL)]
        [InlineData(ConsensusFork.Deneb, 8_626_176UL)]
        [InlineData(ConsensusFork.Electra, 11_649_024UL)]
        public async Task Given_ValidBootstrap_When_Initialize_Then_Succeeds(ConsensusFork fork, ulong forkActivationSlot)
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(slot: forkActivationSlot, blockNumber: 100, fork: fork);
            var beaconClient = new StubBootstrapOnlyApi(bootstrap);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap);
            var store = new InMemoryLightClientStore();
            var bls = new RecordingBls();

            var service = new LightClientService(beaconClient, bls, config, store);

            await service.InitializeAsync();

            var state = service.GetState();
            Assert.NotNull(state);
            Assert.Equal(bootstrap.Header.Beacon.Slot, state.FinalizedSlot);
        }

        [Fact]
        public async Task Given_NullBootstrap_When_Initialize_Then_ThrowsArgumentNull()
        {
            var beaconClient = new NullBootstrapApi();
            var config = LightClientServiceTests.TestDataFactory.CreateConfig();
            var store = new InMemoryLightClientStore();
            var bls = new RecordingBls();

            var service = new LightClientService(beaconClient, bls, config, store);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.InitializeAsync());
        }

        [Fact]
        public async Task Given_BootstrapMissingBeaconHeader_When_Initialize_Then_Throws()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(slot: 11649024, blockNumber: 100);
            bootstrap.Header.Beacon = null;
            var beaconClient = new StubBootstrapOnlyApi(bootstrap);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig();
            var store = new InMemoryLightClientStore();
            var bls = new RecordingBls();

            var service = new LightClientService(beaconClient, bls, config, store);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync());
            Assert.Contains("beacon header", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Given_BootstrapMissingSyncCommittee_When_Initialize_Then_Throws()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(slot: 11649024, blockNumber: 100);
            bootstrap.CurrentSyncCommittee = null;
            var beaconClient = new StubBootstrapOnlyApi(bootstrap);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap);
            var store = new InMemoryLightClientStore();
            var bls = new RecordingBls();

            var service = new LightClientService(beaconClient, bls, config, store);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync());
            Assert.Contains("sync committee", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(ConsensusFork.Capella, 6_209_536UL)]
        [InlineData(ConsensusFork.Deneb, 8_626_176UL)]
        [InlineData(ConsensusFork.Electra, 11_649_024UL)]
        public async Task Given_BootstrapWithWrongBranchLength_When_Initialize_Then_Throws(ConsensusFork fork, ulong forkActivationSlot)
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(slot: forkActivationSlot, blockNumber: 100, fork: fork);
            bootstrap.CurrentSyncCommitteeBranch.Add(new byte[SszBasicTypes.RootLength]);

            var beaconClient = new StubBootstrapOnlyApi(bootstrap);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap);
            var store = new InMemoryLightClientStore();
            var bls = new RecordingBls();

            var service = new LightClientService(beaconClient, bls, config, store);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync());
            Assert.Contains("current_sync_committee_branch", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Given_BootstrapWithWrongTrustedRoot_When_Initialize_Then_Throws()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(slot: 11649024, blockNumber: 100);
            var beaconClient = new StubBootstrapOnlyApi(bootstrap);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig();
            var store = new InMemoryLightClientStore();
            var bls = new RecordingBls();

            var service = new LightClientService(beaconClient, bls, config, store);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync());
            Assert.Contains("weak subjectivity root", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Given_BootstrapWithTamperedSyncCommitteeLeaf_When_Initialize_Then_Throws()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(slot: 11649024, blockNumber: 100);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap);

            bootstrap.CurrentSyncCommittee.AggregatePubKey = Enumerable.Repeat((byte)0xFF, SszBasicTypes.PubKeyLength).ToArray();

            var beaconClient = new StubBootstrapOnlyApi(bootstrap);
            var store = new InMemoryLightClientStore();
            var bls = new RecordingBls();

            var service = new LightClientService(beaconClient, bls, config, store);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync());
            Assert.Contains("sync committee branch", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Given_ValidBootstrap_When_Initialize_Then_NextSyncCommitteeIsDefault()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(slot: 11649024, blockNumber: 100);
            var beaconClient = new StubBootstrapOnlyApi(bootstrap);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap);
            var store = new InMemoryLightClientStore();
            var bls = new RecordingBls();

            var service = new LightClientService(beaconClient, bls, config, store);
            await service.InitializeAsync();

            var state = service.GetState();
            Assert.NotNull(state.NextSyncCommittee);
            Assert.NotSame(bootstrap.CurrentSyncCommittee, state.NextSyncCommittee);

            var defaultCommittee = new SyncCommittee();
            Assert.Equal(defaultCommittee.PubKeys.Count, state.NextSyncCommittee.PubKeys.Count);
            Assert.Equal(defaultCommittee.AggregatePubKey, state.NextSyncCommittee.AggregatePubKey);
        }

        private sealed class StubBootstrapOnlyApi : ILightClientApi
        {
            private readonly LightClientBootstrap _bootstrap;

            public StubBootstrapOnlyApi(LightClientBootstrap bootstrap)
            {
                _bootstrap = bootstrap;
            }

            public Task<LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot)
            {
                if (_bootstrap == null)
                {
                    return Task.FromResult<LightClientBootstrapResponse>(null);
                }

                var response = BuildResponse(_bootstrap);
                return Task.FromResult(response);
            }

            public Task<IReadOnlyList<LightClientUpdateResponse>> GetUpdatesAsync(ulong fromPeriod, ulong count) =>
                Task.FromResult<IReadOnlyList<LightClientUpdateResponse>>(Array.Empty<LightClientUpdateResponse>());

            public Task<LightClientFinalityUpdateResponse> GetFinalityUpdateAsync() =>
                Task.FromResult<LightClientFinalityUpdateResponse>(null);

            public Task<LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync() =>
                Task.FromResult<LightClientOptimisticUpdateResponse>(null);

            private static LightClientBootstrapResponse BuildResponse(LightClientBootstrap bootstrap)
            {
                return new LightClientBootstrapResponse
                {
                    Version = bootstrap.Fork.ToString().ToLowerInvariant(),
                    Data = new LightClientBootstrapData
                    {
                        Header = BuildHeaderDto(bootstrap.Header),
                        CurrentSyncCommittee = bootstrap.CurrentSyncCommittee == null
                            ? null
                            : BuildSyncCommitteeDto(bootstrap.CurrentSyncCommittee),
                        CurrentSyncCommitteeBranch = bootstrap.CurrentSyncCommitteeBranch?.Select(ToHex).ToList()
                    }
                };
            }

            private static LightClientHeaderDto BuildHeaderDto(LightClientHeader header)
            {
                if (header == null) return null;
                return new LightClientHeaderDto
                {
                    Beacon = header.Beacon == null ? null : new BeaconBlockHeaderDto
                    {
                        Slot = header.Beacon.Slot.ToString(),
                        ProposerIndex = header.Beacon.ProposerIndex.ToString(),
                        ParentRoot = ToHex(header.Beacon.ParentRoot),
                        StateRoot = ToHex(header.Beacon.StateRoot),
                        BodyRoot = ToHex(header.Beacon.BodyRoot)
                    },
                    Execution = header.Execution == null ? null : new ExecutionPayloadHeaderDto
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
                    ExecutionBranch = header.ExecutionBranch?.Select(ToHex).ToList()
                };
            }

            private static SyncCommitteeDto BuildSyncCommitteeDto(SyncCommittee committee)
            {
                return new SyncCommitteeDto
                {
                    PubKeys = committee.PubKeys.Select(ToHex).ToList(),
                    AggregatePubKey = ToHex(committee.AggregatePubKey)
                };
            }

            private static string ToHex(byte[] bytes)
            {
                if (bytes == null) return "0x";
                return "0x" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        private sealed class NullBootstrapApi : ILightClientApi
        {
            public Task<LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot) =>
                Task.FromResult<LightClientBootstrapResponse>(null);

            public Task<IReadOnlyList<LightClientUpdateResponse>> GetUpdatesAsync(ulong fromPeriod, ulong count) =>
                Task.FromResult<IReadOnlyList<LightClientUpdateResponse>>(Array.Empty<LightClientUpdateResponse>());

            public Task<LightClientFinalityUpdateResponse> GetFinalityUpdateAsync() =>
                Task.FromResult<LightClientFinalityUpdateResponse>(null);

            public Task<LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync() =>
                Task.FromResult<LightClientOptimisticUpdateResponse>(null);
        }

        private sealed class RecordingBls : IBls
        {
            public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain) => true;

            public byte[] AggregateSignatures(byte[][] signatures) => throw new NotSupportedException();

            public bool Verify(byte[] signature, byte[] publicKey, byte[] message) => throw new NotSupportedException();

            public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey) =>
                throw new NotSupportedException();
        }
    }
}
