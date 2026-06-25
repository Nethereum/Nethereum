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
    /// <summary>
    /// Validates <c>TryApplyUpdate</c> ordering per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 381–457
    /// (<c>validate_light_client_update</c>): slot monotonicity
    /// (<c>current_slot &gt;= signature_slot &gt; update_attested_slot &gt;= update_finalized_slot</c>,
    /// line 389) must precede branch verification, and the <c>next_sync_committee_branch</c>
    /// gate must be evaluated only when <c>is_sync_committee_update</c> holds (line 206).
    /// </summary>
    public class TryApplyUpdateOrderingTests
    {
        private const ulong ElectraActivationSlot = 11_649_024UL;

        [Fact]
        public async Task Given_AttestedSlotBelowFinalizedState_When_Updating_Then_ReturnsFalseFromMonotonicityGate()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(
                slot: ElectraActivationSlot + 32,
                blockNumber: 200);

            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot,
                blockNumber: 100);

            await AssertUpdateNotAppliedAsync(bootstrap, update);
        }

        [Fact]
        public async Task Given_SignatureSlotNotStrictlyGreaterThanAttestedSlot_When_Updating_Then_ReturnsFalseFromMonotonicityGate()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(
                slot: ElectraActivationSlot,
                blockNumber: 100);

            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot + 1,
                blockNumber: 101);
            update.SignatureSlot = update.AttestedHeader.Beacon.Slot;

            await AssertUpdateNotAppliedAsync(bootstrap, update);
        }

        [Fact]
        public async Task Given_AttestedSlotBelowFinalizedSlotInUpdate_When_Updating_Then_ReturnsFalseFromMonotonicityGate()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(
                slot: ElectraActivationSlot,
                blockNumber: 100);

            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot + 10,
                blockNumber: 110);
            update.AttestedHeader.Beacon.Slot = update.FinalizedHeader.Beacon.Slot - 1;

            await AssertUpdateNotAppliedAsync(bootstrap, update);
        }

        [Fact]
        public async Task Given_ValidSlotsWithInvalidNextCommitteeBranch_When_Updating_Then_ReturnsFalseFromBranchGate()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(
                slot: ElectraActivationSlot,
                blockNumber: 100);

            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot + 1,
                blockNumber: 101);

            var depth = LightClientForkSpec.NextSyncCommitteeBranchDepth(ConsensusFork.Electra);
            var bogusBranch = new List<byte[]>(depth);
            for (var i = 0; i < depth; i++)
            {
                var sibling = new byte[SszBasicTypes.RootLength];
                for (var j = 0; j < sibling.Length; j++)
                {
                    sibling[j] = (byte)(0xCC ^ (i + j));
                }
                bogusBranch.Add(sibling);
            }
            update.NextSyncCommitteeBranch = bogusBranch;

            await AssertUpdateNotAppliedAsync(bootstrap, update);
        }

        [Fact]
        public async Task Given_AttestedSlotBelowStateAndInvalidBranch_When_Updating_Then_RejectsViaMonotonicityNotBranch()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(
                slot: ElectraActivationSlot + 64,
                blockNumber: 300);

            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot,
                blockNumber: 100);

            var depth = LightClientForkSpec.NextSyncCommitteeBranchDepth(ConsensusFork.Electra);
            var bogusBranch = new List<byte[]>(depth);
            for (var i = 0; i < depth; i++)
            {
                var sibling = new byte[SszBasicTypes.RootLength];
                for (var j = 0; j < sibling.Length; j++)
                {
                    sibling[j] = (byte)(0xEF ^ (i + j));
                }
                bogusBranch.Add(sibling);
            }
            update.NextSyncCommitteeBranch = bogusBranch;
            update.NextSyncCommittee.AggregatePubKey = Enumerable.Repeat((byte)0xFE, SszBasicTypes.PubKeyLength).ToArray();

            var initialNextCommittee = new SyncCommittee
            {
                PubKeys = new List<byte[]>(),
                AggregatePubKey = new byte[SszBasicTypes.PubKeyLength]
            };

            var (service, state) = await ApplyAsync(bootstrap, update, captureState: true);

            Assert.NotNull(state);
            Assert.Equal(bootstrap.Header.Beacon.Slot, state.FinalizedSlot);
            Assert.Equal(initialNextCommittee.AggregatePubKey, state.NextSyncCommittee.AggregatePubKey);
        }

        [Fact]
        public async Task Given_ValidUpdateWithZeroNextCommitteeBranch_When_Updating_Then_AppliesWithoutTouchingNextCommittee()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(
                slot: ElectraActivationSlot,
                blockNumber: 100);

            var update = LightClientServiceTests.TestDataFactory.CreateUpdate(
                slot: ElectraActivationSlot + 1,
                blockNumber: 101);

            update.NextSyncCommittee.AggregatePubKey = Enumerable.Repeat((byte)0x99, SszBasicTypes.PubKeyLength).ToArray();

            var (service, state) = await ApplyAsync(bootstrap, update, captureState: true);

            Assert.NotNull(state);
            Assert.Equal(update.FinalizedHeader.Beacon.Slot, state.FinalizedSlot);

            var defaultAggregate = new byte[SszBasicTypes.PubKeyLength];
            Assert.Equal(defaultAggregate, state.NextSyncCommittee.AggregatePubKey);
        }

        private static async Task AssertUpdateNotAppliedAsync(LightClientBootstrap bootstrap, LightClientUpdate update)
        {
            var (_, state) = await ApplyAsync(bootstrap, update, captureState: true);
            Assert.NotNull(state);
            Assert.Equal(bootstrap.Header.Beacon.Slot, state.FinalizedSlot);
        }

        private static async Task<(LightClientService Service, LightClientState State)> ApplyAsync(
            LightClientBootstrap bootstrap,
            LightClientUpdate update,
            bool captureState)
        {
            var beaconClient = new StubLightClientApi(bootstrap, new[] { update });
            var config = LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap);
            var store = new InMemoryLightClientStore();
            var bls = new AcceptingBls();

            var service = new LightClientService(beaconClient, bls, config, store);

            await service.InitializeAsync();
            await service.UpdateAsync();

            return (service, captureState ? service.GetState() : null);
        }

        private sealed class StubLightClientApi : ILightClientApi
        {
            private readonly LightClientBootstrap _bootstrap;
            private readonly IReadOnlyList<LightClientUpdate> _updates;

            public StubLightClientApi(LightClientBootstrap bootstrap, IReadOnlyList<LightClientUpdate> updates)
            {
                _bootstrap = bootstrap;
                _updates = updates;
            }

            public Task<LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot)
            {
                var response = LightClientServiceTests.TestDataFactory.CreateBootstrapResponse(_bootstrap);
                return Task.FromResult(response);
            }

            public Task<IReadOnlyList<LightClientUpdateResponse>> GetUpdatesAsync(ulong fromPeriod, ulong count)
            {
                var responses = _updates.Select(LightClientServiceTests.TestDataFactory.CreateUpdateResponse).ToList();
                return Task.FromResult<IReadOnlyList<LightClientUpdateResponse>>(responses);
            }

            public Task<LightClientFinalityUpdateResponse> GetFinalityUpdateAsync() =>
                Task.FromResult<LightClientFinalityUpdateResponse>(null);

            public Task<LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync() =>
                Task.FromResult<LightClientOptimisticUpdateResponse>(null);
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
