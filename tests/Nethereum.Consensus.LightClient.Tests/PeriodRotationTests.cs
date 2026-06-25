using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Signer.Bls;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    /// <summary>
    /// Validates the rotation predicates and <c>apply_light_client_update</c> branch
    /// selection per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 275–277
    /// (<c>is_next_sync_committee_known</c>), 390–395 (period-skip guard), 428–430
    /// (same-period equality), 460–478 (rotation branches), 534–541
    /// (<c>update_has_finalized_next_sync_committee</c>), 542–548 (finality gate).
    /// Tests focus on the predicate and rotation logic; branch verification is
    /// covered separately in <see cref="NextSyncCommitteeBranchVerificationTests"/>
    /// and <see cref="TryApplyUpdateOrderingTests"/>.
    /// </summary>
    public class PeriodRotationTests
    {
        private const ulong ElectraActivationSlot = 11_649_024UL;
        private const ulong SlotsPerPeriod = 8192UL;

        [Fact]
        public void Given_BootstrapWithDefaultNextSyncCommittee_When_IsNextSyncCommitteeKnown_Then_ReturnsFalse()
        {
            var state = new LightClientState
            {
                NextSyncCommittee = new SyncCommittee()
            };

            Assert.False(InvokeIsNextSyncCommitteeKnown(state));
        }

        [Fact]
        public void Given_StateWithPinnedNextSyncCommittee_When_IsNextSyncCommitteeKnown_Then_ReturnsTrue()
        {
            var state = new LightClientState
            {
                NextSyncCommittee = BuildCommittee(0x42)
            };

            Assert.True(InvokeIsNextSyncCommitteeKnown(state));
        }

        [Fact]
        public void Given_UpdatedFinalityFromUnknownState_When_AllPredicatesAlign_Then_UpdateHasFinalizedNextSyncCommitteeTrue()
        {
            var state = BuildBootstrapState(slot: ElectraActivationSlot);
            var update = BuildUpdateAtSlot(
                attestedSlot: ElectraActivationSlot + 1,
                finalizedSlot: ElectraActivationSlot + 1,
                withNextSyncCommitteeBranch: true,
                withFinalityBranch: true);

            var service = CreateService(state);
            var result = InvokeUpdateHasFinalizedNextSyncCommittee(service, state, update);

            Assert.True(result);
        }

        [Fact]
        public void Given_NextKnownState_When_CheckingUpdateHasFinalizedNextSyncCommittee_Then_ReturnsFalse()
        {
            var state = BuildBootstrapState(slot: ElectraActivationSlot);
            state.NextSyncCommittee = BuildCommittee(0x42);

            var update = BuildUpdateAtSlot(
                attestedSlot: ElectraActivationSlot + 1,
                finalizedSlot: ElectraActivationSlot + 1,
                withNextSyncCommitteeBranch: true,
                withFinalityBranch: true);

            var service = CreateService(state);
            var result = InvokeUpdateHasFinalizedNextSyncCommittee(service, state, update);

            Assert.False(result);
        }

        [Fact]
        public void Given_StateAtSamePeriodAsUpdate_When_NextUnknown_Then_HasValidPeriodWindowAllowsEquality()
        {
            var state = BuildBootstrapState(slot: ElectraActivationSlot);
            var update = BuildUpdateAtSlot(
                attestedSlot: ElectraActivationSlot + 1,
                finalizedSlot: ElectraActivationSlot + 1);

            var service = CreateService(state);
            Assert.True(InvokeHasValidPeriodWindow(service, state, update));
        }

        [Fact]
        public void Given_StateAndUpdateOnePeriodApart_When_NextKnown_Then_HasValidPeriodWindowAllowsBranchB()
        {
            var state = BuildBootstrapState(slot: ElectraActivationSlot);
            state.NextSyncCommittee = BuildCommittee(0x42);

            var update = BuildUpdateAtSlot(
                attestedSlot: ElectraActivationSlot + SlotsPerPeriod,
                finalizedSlot: ElectraActivationSlot + SlotsPerPeriod);

            var service = CreateService(state);
            Assert.True(InvokeHasValidPeriodWindow(service, state, update));
        }

        [Fact]
        public void Given_UpdateTwoPeriodsAhead_When_HasValidPeriodWindow_Then_RejectedBySpecL390_L395()
        {
            var state = BuildBootstrapState(slot: ElectraActivationSlot);
            state.NextSyncCommittee = BuildCommittee(0x42);

            var update = BuildUpdateAtSlot(
                attestedSlot: ElectraActivationSlot + (SlotsPerPeriod * 2),
                finalizedSlot: ElectraActivationSlot + (SlotsPerPeriod * 2));

            var service = CreateService(state);
            Assert.False(InvokeHasValidPeriodWindow(service, state, update));
        }

        [Fact]
        public void Given_UpdateNextPeriodWhenNextUnknown_When_HasValidPeriodWindow_Then_RejectedPerSpecL395()
        {
            var state = BuildBootstrapState(slot: ElectraActivationSlot);

            var update = BuildUpdateAtSlot(
                attestedSlot: ElectraActivationSlot + SlotsPerPeriod,
                finalizedSlot: ElectraActivationSlot + SlotsPerPeriod);

            var service = CreateService(state);
            Assert.False(InvokeHasValidPeriodWindow(service, state, update));
        }

        [Fact]
        public void Given_TwoCommitteesWithSameRoot_When_SyncCommitteeEquals_Then_True()
        {
            var a = BuildCommittee(0x42);
            var b = BuildCommittee(0x42);

            Assert.True(InvokeSyncCommitteeEquals(a, b));
        }

        [Fact]
        public void Given_TwoCommitteesWithDifferentRoots_When_SyncCommitteeEquals_Then_False()
        {
            var a = BuildCommittee(0x42);
            var b = BuildCommittee(0x84);

            Assert.False(InvokeSyncCommitteeEquals(a, b));
        }

        [Fact]
        public void Given_BootstrapAndApplyAtSamePeriod_When_RotationBranchA_Then_NextSyncCommitteePinnedCurrentUnchanged()
        {
            var state = BuildBootstrapState(slot: ElectraActivationSlot);
            var initialCurrent = state.CurrentSyncCommittee;

            var pinned = BuildCommittee(0x42);
            var update = BuildUpdateAtSlot(
                attestedSlot: ElectraActivationSlot + 1,
                finalizedSlot: ElectraActivationSlot + 1,
                withNextSyncCommitteeBranch: true,
                withFinalityBranch: true);
            update.NextSyncCommittee = pinned;

            var service = CreateService(state);
            InvokeApplyLightClientUpdate(service, state, update, applyFinality: true, applyOptimistic: true);

            Assert.Same(initialCurrent, state.CurrentSyncCommittee);
            Assert.Equal(pinned.HashTreeRoot(), state.NextSyncCommittee.HashTreeRoot());
        }

        [Fact]
        public void Given_NextKnownStoreAtPeriodN_And_UpdateAtPeriodNPlusOne_When_RotationBranchB_Then_CurrentPromotedNextSet()
        {
            var state = BuildBootstrapState(slot: ElectraActivationSlot);
            var initialCurrent = state.CurrentSyncCommittee;
            var pinnedNext = BuildCommittee(0x42);
            state.NextSyncCommittee = pinnedNext;

            var incoming = BuildCommittee(0x84);
            var update = BuildUpdateAtSlot(
                attestedSlot: ElectraActivationSlot + SlotsPerPeriod,
                finalizedSlot: ElectraActivationSlot + SlotsPerPeriod,
                withNextSyncCommitteeBranch: true,
                withFinalityBranch: true);
            update.NextSyncCommittee = incoming;

            var service = CreateService(state);
            InvokeApplyLightClientUpdate(service, state, update, applyFinality: true, applyOptimistic: true);

            Assert.Equal(pinnedNext.HashTreeRoot(), state.CurrentSyncCommittee.HashTreeRoot());
            Assert.Equal(incoming.HashTreeRoot(), state.NextSyncCommittee.HashTreeRoot());
        }

        [Fact]
        public void Given_NoSyncCommitteeUpdate_When_RotationBlockTriggered_Then_NextSyncCommitteeUnchanged()
        {
            var state = BuildBootstrapState(slot: ElectraActivationSlot);
            var initialNext = state.NextSyncCommittee;

            var update = BuildUpdateAtSlot(
                attestedSlot: ElectraActivationSlot + 1,
                finalizedSlot: ElectraActivationSlot + 1,
                withNextSyncCommitteeBranch: false,
                withFinalityBranch: true);

            var service = CreateService(state);
            InvokeApplyLightClientUpdate(service, state, update, applyFinality: true, applyOptimistic: true);

            Assert.Same(initialNext, state.NextSyncCommittee);
        }

        private static LightClientService CreateService(LightClientState state)
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(ElectraActivationSlot, 100);
            var config = LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap);
            var store = new InMemoryLightClientStore();
            var bls = new AcceptingBls();

            return new LightClientService(new StubApi(), bls, config, store);
        }

        private static LightClientState BuildBootstrapState(ulong slot)
        {
            var current = BuildCommittee(0xAA);

            return new LightClientState
            {
                FinalizedHeader = new BeaconBlockHeader
                {
                    Slot = slot,
                    ProposerIndex = 1,
                    ParentRoot = Enumerable.Repeat((byte)0x01, SszBasicTypes.RootLength).ToArray(),
                    StateRoot = Enumerable.Repeat((byte)0x02, SszBasicTypes.RootLength).ToArray(),
                    BodyRoot = Enumerable.Repeat((byte)0x03, SszBasicTypes.RootLength).ToArray()
                },
                CurrentSyncCommittee = current,
                NextSyncCommittee = new SyncCommittee(),
                FinalizedSlot = slot,
                CurrentPeriod = slot / SlotsPerPeriod,
                OptimisticSlot = 0
            };
        }

        private static LightClientUpdate BuildUpdateAtSlot(
            ulong attestedSlot,
            ulong finalizedSlot,
            bool withNextSyncCommitteeBranch = false,
            bool withFinalityBranch = false)
        {
            var fork = ConsensusFork.Electra;
            var nextBranchLen = LightClientForkSpec.NextSyncCommitteeBranchLength(fork);
            var finalityBranchLen = LightClientForkSpec.FinalityBranchLength(fork);

            var nextBranch = new List<byte[]>(nextBranchLen);
            for (var i = 0; i < nextBranchLen; i++)
            {
                var sibling = new byte[SszBasicTypes.RootLength];
                if (withNextSyncCommitteeBranch) sibling[0] = (byte)(i + 1);
                nextBranch.Add(sibling);
            }

            var finalityBranch = new List<byte[]>(finalityBranchLen);
            for (var i = 0; i < finalityBranchLen; i++)
            {
                var sibling = new byte[SszBasicTypes.RootLength];
                if (withFinalityBranch) sibling[0] = (byte)(0x80 + i);
                finalityBranch.Add(sibling);
            }

            return new LightClientUpdate
            {
                Fork = fork,
                AttestedHeader = new LightClientHeader
                {
                    Fork = fork,
                    Beacon = new BeaconBlockHeader
                    {
                        Slot = attestedSlot,
                        ProposerIndex = 1,
                        ParentRoot = new byte[SszBasicTypes.RootLength],
                        StateRoot = new byte[SszBasicTypes.RootLength],
                        BodyRoot = new byte[SszBasicTypes.RootLength]
                    }
                },
                FinalizedHeader = new LightClientHeader
                {
                    Fork = fork,
                    Beacon = new BeaconBlockHeader
                    {
                        Slot = finalizedSlot,
                        ProposerIndex = 1,
                        ParentRoot = new byte[SszBasicTypes.RootLength],
                        StateRoot = new byte[SszBasicTypes.RootLength],
                        BodyRoot = new byte[SszBasicTypes.RootLength]
                    },
                    Execution = new ExecutionPayloadHeader
                    {
                        Fork = fork,
                        BlockNumber = finalizedSlot,
                        BlockHash = Enumerable.Repeat((byte)0x55, SszBasicTypes.RootLength).ToArray()
                    }
                },
                NextSyncCommittee = new SyncCommittee(),
                NextSyncCommitteeBranch = nextBranch,
                FinalityBranch = finalityBranch,
                SyncAggregate = SyncAggregateQuorumTests.CreateAggregate(342),
                SignatureSlot = attestedSlot + 1
            };
        }

        private static SyncCommittee BuildCommittee(byte seed)
        {
            var pubkeys = new List<byte[]>(SszBasicTypes.SyncCommitteeSize);
            for (var i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
            {
                var key = new byte[SszBasicTypes.PubKeyLength];
                for (var j = 0; j < key.Length; j++)
                {
                    key[j] = (byte)((seed + i + j) & 0xFF);
                }
                pubkeys.Add(key);
            }

            return new SyncCommittee
            {
                PubKeys = pubkeys,
                AggregatePubKey = Enumerable.Repeat(seed, SszBasicTypes.PubKeyLength).ToArray()
            };
        }

        private static bool InvokeIsNextSyncCommitteeKnown(LightClientState state)
        {
            var method = typeof(LightClientService).GetMethod(
                "IsNextSyncCommitteeKnown",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (bool)method!.Invoke(null, new object[] { state });
        }

        private static bool InvokeUpdateHasFinalizedNextSyncCommittee(LightClientService service, LightClientState state, LightClientUpdate update)
        {
            var method = typeof(LightClientService).GetMethod(
                "UpdateHasFinalizedNextSyncCommittee",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (bool)method!.Invoke(service, new object[] { state, update });
        }

        private static bool InvokeHasValidPeriodWindow(LightClientService service, LightClientState state, LightClientUpdate update)
        {
            var method = typeof(LightClientService).GetMethod(
                "HasValidPeriodWindow",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (bool)method!.Invoke(service, new object[] { state, update });
        }

        private static bool InvokeSyncCommitteeEquals(SyncCommittee a, SyncCommittee b)
        {
            var method = typeof(LightClientService).GetMethod(
                "SyncCommitteeEquals",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (bool)method!.Invoke(null, new object[] { a, b });
        }

        private static void InvokeApplyLightClientUpdate(LightClientService service, LightClientState state, LightClientUpdate update, bool applyFinality, bool applyOptimistic)
        {
            var method = typeof(LightClientService).GetMethod(
                "ApplyLightClientUpdate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method!.Invoke(service, new object[] { state, update, applyFinality, applyOptimistic });
        }

        private sealed class StubApi : ILightClientApi
        {
            public System.Threading.Tasks.Task<Nethereum.Beaconchain.LightClient.Responses.LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot)
                => System.Threading.Tasks.Task.FromResult<Nethereum.Beaconchain.LightClient.Responses.LightClientBootstrapResponse>(null);
            public System.Threading.Tasks.Task<IReadOnlyList<Nethereum.Beaconchain.LightClient.Responses.LightClientUpdateResponse>> GetUpdatesAsync(ulong fromPeriod, ulong count)
                => System.Threading.Tasks.Task.FromResult<IReadOnlyList<Nethereum.Beaconchain.LightClient.Responses.LightClientUpdateResponse>>(Array.Empty<Nethereum.Beaconchain.LightClient.Responses.LightClientUpdateResponse>());
            public System.Threading.Tasks.Task<Nethereum.Beaconchain.LightClient.Responses.LightClientFinalityUpdateResponse> GetFinalityUpdateAsync()
                => System.Threading.Tasks.Task.FromResult<Nethereum.Beaconchain.LightClient.Responses.LightClientFinalityUpdateResponse>(null);
            public System.Threading.Tasks.Task<Nethereum.Beaconchain.LightClient.Responses.LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync()
                => System.Threading.Tasks.Task.FromResult<Nethereum.Beaconchain.LightClient.Responses.LightClientOptimisticUpdateResponse>(null);
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
