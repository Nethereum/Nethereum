using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Beaconchain.LightClient.Responses;
using Nethereum.Consensus.Ssz;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;
using mcl;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class LightClientPeriodCatchUpTests
    {
        private const ulong SlotsPerPeriod = 8192UL;
        private const ulong StorePeriod = 1430UL;
        private const ulong NucStorePeriod = 1874UL;

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "light-client", "Catch up through every missed sync committee period", Order = 10)]
        public async Task Given_StoreAtPeriodP_And_BeaconHeadAtPeriodPPlus2_When_LightClientResumes_Then_CatchesUpThroughPPlus1AndAdvancesToPPlus2()
        {
            var chain = new PeriodChain();
            var beacon = new PeriodBeacon(chain, StorePeriod, StorePeriod + 1, StorePeriod + 2) { Head = chain.Update(StorePeriod + 2) };
            var service = await ResumeAsync(chain, beacon, StorePeriod);

            var state = service.GetState();
            Assert.Equal(chain.FinalizedSlot(StorePeriod + 2), state.FinalizedSlot);
            Assert.Equal(StorePeriod + 2, state.CurrentPeriod);
            Assert.True(LightClientService.SyncCommitteeEquals(chain.Committee(StorePeriod + 2), state.CurrentSyncCommittee));
            Assert.Equal(0, beacon.BootstrapRequests);
        }

        [Fact]
        public async Task Given_StoreAtPeriodP_And_BeaconHeadAtPeriodPPlus1_When_FinalityUpdateArrives_Then_NormalPathAppliesWithoutCatchUp()
        {
            var chain = new PeriodChain();
            var beacon = new PeriodBeacon(chain) { Head = chain.Update(StorePeriod + 1) };
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(chain, beacon, StorePeriod, progress);

            var state = service.GetState();
            Assert.Equal(chain.FinalizedSlot(StorePeriod + 1), state.FinalizedSlot);
            Assert.True(LightClientService.SyncCommitteeEquals(chain.Committee(StorePeriod + 1), state.CurrentSyncCommittee));
            Assert.Empty(progress);
            Assert.Equal(0, beacon.BootstrapRequests);
        }

        [Fact]
        public async Task Given_IntermediateUpdateSignedByWrongCommittee_When_CatchingUp_Then_RejectedStoreNotAdvancedAndNoRebootstrap()
        {
            var chain = new PeriodChain();
            var beacon = new PeriodBeacon(chain, StorePeriod, StorePeriod + 2) { Head = chain.Update(StorePeriod + 2) };
            beacon.Updates[StorePeriod + 1] = chain.Update(StorePeriod + 1, signerPeriod: StorePeriod + 7);
            beacon.AddBootstrap(chain.Bootstrap(StorePeriod + 2));
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(chain, beacon, StorePeriod, progress, allowBeaconRebootstrap: true);

            var finalityApplied = await service.UpdateFinalityAsync();

            Assert.False(finalityApplied);
            var state = service.GetState();
            Assert.Equal(chain.FinalizedSlot(StorePeriod), state.FinalizedSlot);
            Assert.True(LightClientService.SyncCommitteeEquals(chain.Committee(StorePeriod), state.CurrentSyncCommittee));
            Assert.Equal(0, beacon.BootstrapRequests);
            Assert.Contains(progress, p => p.Stage == LightClientCatchUpStage.UpdateRejected);
            Assert.DoesNotContain(progress, p => p.Stage == LightClientCatchUpStage.Rebootstrapped);
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "light-client", "Re-bootstrap from the beacon finalized root when allowed", Order = 11)]
        public async Task Given_NucShapedGapWithPeriod1875Missing_And_WallClockAgrees_And_BeaconRebootstrapAllowed_When_LightClientResumes_Then_RecoversToTheHeadPeriod()
        {
            var chain = new PeriodChain();
            var beacon = new PeriodBeacon(chain, NucStorePeriod, NucStorePeriod + 2, 1783UL, 1784UL) { Head = chain.Update(NucStorePeriod + 2), IgnoresRequestedCount = true };
            beacon.AddBootstrap(chain.Bootstrap(NucStorePeriod + 2));
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(chain, beacon, NucStorePeriod, progress, allowBeaconRebootstrap: true);

            var state = service.GetState();
            Assert.Equal(chain.FinalizedSlot(NucStorePeriod + 2), state.FinalizedSlot);
            Assert.True(LightClientService.SyncCommitteeEquals(chain.Committee(NucStorePeriod + 2), state.CurrentSyncCommittee));
            Assert.Equal(chain.AttestedSlot(NucStorePeriod + 2), state.OptimisticSlot);
            Assert.Equal(1, beacon.BootstrapRequests);
            Assert.Contains(progress, p => p.Stage == LightClientCatchUpStage.Rebootstrapped
                                           && p.Reason.Contains("range_unavailable period=" + (NucStorePeriod + 1)));
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "light-client", "Keep the verified head when beacon re-bootstrap is not allowed", Order = 12)]
        public async Task Given_NucShapedGapWithPeriod1875Missing_And_BeaconRebootstrapNotAllowed_When_PolledRepeatedly_Then_KeepsTheVerifiedHeadAndReportsOnce()
        {
            var chain = new PeriodChain();
            var beacon = new PeriodBeacon(chain, NucStorePeriod, NucStorePeriod + 2) { Head = chain.Update(NucStorePeriod + 2) };
            beacon.AddBootstrap(chain.Bootstrap(NucStorePeriod + 2));
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(chain, beacon, NucStorePeriod, progress);

            for (var i = 0; i < 3; i++)
            {
                Assert.False(await service.UpdateFinalityAsync());
            }

            var state = service.GetState();
            Assert.Equal(chain.FinalizedSlot(NucStorePeriod), state.FinalizedSlot);
            Assert.True(LightClientService.SyncCommitteeEquals(chain.Committee(NucStorePeriod), state.CurrentSyncCommittee));
            Assert.Equal(0, beacon.BootstrapRequests);
            var refusal = Assert.Single(progress);
            Assert.Equal(LightClientCatchUpStage.RebootstrapFailed, refusal.Stage);
            Assert.Contains("beacon_rebootstrap_disabled", refusal.Reason);
        }

        [Fact]
        public async Task Given_UpdatesByRangeRequestFails_And_BeaconRebootstrapAllowed_When_FinalityUpdateAtPPlus2Arrives_Then_RebootstrapsFromBeaconFinalizedCheckpoint()
        {
            var chain = new PeriodChain();
            var beacon = new PeriodBeacon(chain, StorePeriod, StorePeriod + 1, StorePeriod + 2) { Head = chain.Update(StorePeriod + 2), RangeRequestsFail = true };
            beacon.AddBootstrap(chain.Bootstrap(StorePeriod + 2));
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(chain, beacon, StorePeriod, progress, allowBeaconRebootstrap: true);

            Assert.Equal(chain.FinalizedSlot(StorePeriod + 2), service.GetState().FinalizedSlot);
            Assert.Contains(progress, p => p.Stage == LightClientCatchUpStage.Rebootstrapped
                                           && p.Reason.Contains("range_request_failed"));
        }

        [Fact]
        public async Task Given_GapBeyondMaxCatchUpPeriods_And_BeaconRebootstrapAllowed_When_FinalityUpdateArrives_Then_RebootstrapsWithoutWalkingTheRange()
        {
            var chain = new PeriodChain();
            var beacon = new PeriodBeacon(chain) { Head = chain.Update(StorePeriod + 3) };
            beacon.AddBootstrap(chain.Bootstrap(StorePeriod + 3));
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(chain, beacon, StorePeriod, progress, maxCatchUpPeriods: 2, allowBeaconRebootstrap: true);

            Assert.Equal(chain.FinalizedSlot(StorePeriod + 3), service.GetState().FinalizedSlot);
            Assert.All(beacon.RangeRequests, r => Assert.Equal((StorePeriod, 4UL), r));
            Assert.DoesNotContain(progress, p => p.Stage == LightClientCatchUpStage.PeriodApplied);
            Assert.Contains(progress, p => p.Stage == LightClientCatchUpStage.Rebootstrapped
                                           && p.Reason.Contains("gap_exceeds_max"));
        }

        [Fact]
        public async Task Given_StoreSixPeriodsBehindBeyondTheFourPeriodPollWindow_When_LightClientResumes_Then_RemainingPeriodsAreAppliedInOrderAndHeadAdvances()
        {
            var chain = new PeriodChain();
            var beacon = new PeriodBeacon(chain, StorePeriod, StorePeriod + 1, StorePeriod + 2, StorePeriod + 3, StorePeriod + 4, StorePeriod + 5) { Head = chain.Update(StorePeriod + 6) };
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(chain, beacon, StorePeriod, progress);

            var state = service.GetState();
            Assert.Equal(chain.FinalizedSlot(StorePeriod + 6), state.FinalizedSlot);
            Assert.True(LightClientService.SyncCommitteeEquals(chain.Committee(StorePeriod + 6), state.CurrentSyncCommittee));
            Assert.Equal(
                new[] { StorePeriod + 4, StorePeriod + 5 },
                progress.Where(p => p.Stage == LightClientCatchUpStage.PeriodApplied).Select(p => p.StorePeriod).ToArray());
            Assert.Equal(0, beacon.BootstrapRequests);
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "light-client", "Reject an update signed in a slot that has not happened yet", Order = 13)]
        public async Task Given_MaliciousBeaconServesUnsignedHeadFromAFuturePeriod_When_FinalityUpdateArrives_Then_RejectedByWallClockWithoutCatchUpOrRebootstrap()
        {
            var honest = new PeriodChain();
            var forged = new PeriodChain(seedOffset: 100);
            var beacon = new PeriodBeacon(forged) { Head = forged.Update(5000UL) };
            beacon.AddBootstrap(forged.Bootstrap(5000UL));
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(honest, beacon, StorePeriod, progress, allowBeaconRebootstrap: true, clockSlot: honest.AttestedSlot(StorePeriod + 2));

            Assert.False(await service.UpdateFinalityAsync());

            var state = service.GetState();
            Assert.Equal(honest.FinalizedSlot(StorePeriod), state.FinalizedSlot);
            Assert.True(LightClientService.SyncCommitteeEquals(honest.Committee(StorePeriod), state.CurrentSyncCommittee));
            Assert.StartsWith("future_signature_slot", service.LastRejectReason);
            Assert.Equal(0, beacon.BootstrapRequests);
            Assert.Empty(beacon.RangeRequests.Where(r => r.Count != 4UL));
            Assert.Empty(progress);
        }

        [Fact]
        public async Task Given_MaliciousBeaconForgesHeadAndBootstrapWithEmptyRange_And_BeaconRebootstrapNotAllowed_When_FinalityUpdateArrives_Then_VerifiedStoreIsNotSwapped()
        {
            var honest = new PeriodChain();
            var forged = new PeriodChain(seedOffset: 100);
            var beacon = new PeriodBeacon(forged) { Head = forged.Update(StorePeriod + 2) };
            beacon.AddBootstrap(forged.Bootstrap(StorePeriod + 2));
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(honest, beacon, StorePeriod, progress);

            Assert.False(await service.UpdateFinalityAsync());

            var state = service.GetState();
            Assert.Equal(honest.FinalizedSlot(StorePeriod), state.FinalizedSlot);
            Assert.True(LightClientService.SyncCommitteeEquals(honest.Committee(StorePeriod), state.CurrentSyncCommittee));
            Assert.Equal(0, beacon.BootstrapRequests);
            Assert.DoesNotContain(progress, p => p.Stage == LightClientCatchUpStage.Rebootstrapped);
        }

        [Fact]
        public async Task Given_MaliciousBeaconAnchorsRebootstrapOnAFutureFinalizedHeader_And_BeaconRebootstrapAllowed_When_FinalityUpdateArrives_Then_AnchorRejectedByWallClock()
        {
            var honest = new PeriodChain();
            var forged = new PeriodChain(seedOffset: 100);
            var head = forged.Update(StorePeriod + 2, finalizedPeriod: 5000UL);
            var beacon = new PeriodBeacon(forged) { Head = head };
            beacon.AddBootstrap(forged.Bootstrap(5000UL));
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(honest, beacon, StorePeriod, progress, allowBeaconRebootstrap: true);

            Assert.False(await service.UpdateFinalityAsync());

            var state = service.GetState();
            Assert.Equal(honest.FinalizedSlot(StorePeriod), state.FinalizedSlot);
            Assert.True(LightClientService.SyncCommitteeEquals(honest.Committee(StorePeriod), state.CurrentSyncCommittee));
            Assert.DoesNotContain(progress, p => p.Stage == LightClientCatchUpStage.Rebootstrapped);
            Assert.Contains(progress, p => p.Stage == LightClientCatchUpStage.RebootstrapFailed && p.Reason.Contains("bootstrap_from_future"));
        }

        [Fact]
        public async Task Given_ConfiguredWeakSubjectivityRootOlderThanTheStore_When_GapCannotBeBridgedAcrossPolls_Then_BootstrapIsFetchedOnceAndReportedOnce()
        {
            var chain = new PeriodChain();
            var beacon = new PeriodBeacon(chain, StorePeriod) { Head = chain.Update(StorePeriod + 2) };
            beacon.AddBootstrap(chain.Bootstrap(StorePeriod - 1));
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(chain, beacon, StorePeriod, progress, weakSubjectivityRoot: chain.FinalizedHeader(StorePeriod - 1).Beacon.HashTreeRoot());

            for (var i = 0; i < 3; i++)
            {
                await service.UpdateFinalityAsync();
            }

            Assert.Equal(chain.FinalizedSlot(StorePeriod), service.GetState().FinalizedSlot);
            Assert.Equal(1, beacon.BootstrapRequests);
            Assert.Single(progress, p => p.Stage == LightClientCatchUpStage.RebootstrapFailed && p.Reason.Contains("bootstrap_not_newer"));
        }

        [Fact]
        public async Task Given_RebootstrapLandsBeforeTheHeadsSignaturePeriodWindow_When_HeadIsRetried_Then_ReportsTheRetryRejection()
        {
            var chain = new PeriodChain();
            var head = chain.Update(StorePeriod + 2, finalizedPeriod: StorePeriod + 1, finalizedOffset: 8000, attestedOffset: 10);
            var beacon = new PeriodBeacon(chain);
            beacon.AddBootstrap(chain.Bootstrap(StorePeriod + 1, finalizedOffset: 8000));
            var service = await ResumeAsync(chain, beacon, StorePeriod, allowBeaconRebootstrap: true, clockSlot: head.SignatureSlot + 10);
            beacon.Head = head;

            var applied = await service.UpdateFinalityAsync();

            Assert.False(applied);
            Assert.Equal(chain.FinalizedSlot(StorePeriod + 1, 8000), service.GetState().FinalizedSlot);
            Assert.StartsWith("invalid_period_window", service.LastRejectReason);
        }

        [Fact]
        public async Task Given_RealSyncCommitteeSignatures_When_StoreIsTwoPeriodsBehind_Then_CatchUpVerifiesEachPeriodAgainstItsCommittee()
        {
            var bls = new NativeBls(new HerumiNativeBindings());
            await bls.InitializeAsync();
            var chain = PeriodChain.WithRealSignatures();
            var beacon = new PeriodBeacon(chain, StorePeriod + 1) { Head = chain.Update(StorePeriod + 2) };
            var service = await ResumeAsync(chain, beacon, StorePeriod, bls: bls, resumeWithRangePoll: false);

            Assert.True(await service.UpdateFinalityAsync());
            Assert.Equal(chain.FinalizedSlot(StorePeriod + 2), service.GetState().FinalizedSlot);
            Assert.True(LightClientService.SyncCommitteeEquals(chain.Committee(StorePeriod + 2), service.GetState().CurrentSyncCommittee));
        }

        [Fact]
        public async Task Given_RealSyncCommitteeSignatures_And_IntermediateUpdateSignedByTheFollowingCommittee_When_CatchingUp_Then_RejectedAndStoreNotAdvanced()
        {
            var bls = new NativeBls(new HerumiNativeBindings());
            await bls.InitializeAsync();
            var chain = PeriodChain.WithRealSignatures();
            var beacon = new PeriodBeacon(chain) { Head = chain.Update(StorePeriod + 2) };
            beacon.Updates[StorePeriod + 1] = chain.Update(StorePeriod + 1, signerPeriod: StorePeriod + 2);
            var progress = new List<LightClientCatchUpProgress>();
            var service = await ResumeAsync(chain, beacon, StorePeriod, progress, bls: bls, resumeWithRangePoll: false);

            Assert.False(await service.UpdateFinalityAsync());
            Assert.Equal(chain.FinalizedSlot(StorePeriod), service.GetState().FinalizedSlot);
            Assert.Contains(progress, p => p.Stage == LightClientCatchUpStage.UpdateRejected && p.Reason.Contains("sync_aggregate_bls"));
        }

        private static async Task<LightClientService> ResumeAsync(
            PeriodChain chain,
            PeriodBeacon beacon,
            ulong storePeriod,
            List<LightClientCatchUpProgress> progress = null,
            ulong maxCatchUpPeriods = 16,
            bool allowBeaconRebootstrap = false,
            byte[] weakSubjectivityRoot = null,
            ulong? clockSlot = null,
            IBls bls = null,
            bool resumeWithRangePoll = true)
        {
            var store = new InMemoryLightClientStore();
            await store.SaveAsync(chain.StoreAt(storePeriod));
            var config = PeriodChain.Config();
            config.MaxCatchUpPeriods = maxCatchUpPeriods;
            config.AllowBeaconRebootstrap = allowBeaconRebootstrap;
            if (weakSubjectivityRoot != null)
            {
                config.WeakSubjectivityRoot = weakSubjectivityRoot;
            }
            var nowSlot = clockSlot ?? (beacon.Head?.SignatureSlot ?? chain.AttestedSlot(storePeriod)) + 10;
            config.Clock = () => DateTimeOffset.FromUnixTimeSeconds((long)(LightClientNetworks.MainnetGenesisTime + (nowSlot * 12)));
            var service = new LightClientService(beacon, bls ?? new CommitteeBoundBls(), config, store);
            if (progress != null)
            {
                service.CatchUpProgress += progress.Add;
            }

            beacon.ServesRange = resumeWithRangePoll;
            await service.InitializeAsync();
            beacon.ServesRange = true;
            return service;
        }

        private sealed class PeriodChain
        {
            private readonly Dictionary<ulong, SyncCommittee> _committees = new Dictionary<ulong, SyncCommittee>();
            private readonly int _seedOffset;
            private readonly Func<ulong, SyncCommittee> _realCommittee;
            private readonly Func<LightClientUpdate, ulong, byte[]> _realSigner;

            public PeriodChain(int seedOffset = 0)
            {
                _seedOffset = seedOffset;
            }

            private PeriodChain(Func<ulong, SyncCommittee> committee, Func<LightClientUpdate, ulong, byte[]> signer)
            {
                _realCommittee = committee;
                _realSigner = signer;
            }

            public static LightClientConfig Config() => new LightClientConfig
            {
                GenesisValidatorsRoot = Enumerable.Repeat((byte)0xAA, SszBasicTypes.RootLength).ToArray()
            };

            public static PeriodChain WithRealSignatures()
            {
                var domainService = new LightClientService(new PeriodBeacon(null), new CommitteeBoundBls(), Config(), new InMemoryLightClientStore());
                return new PeriodChain(
                    period =>
                    {
                        var publicKey = SecretKey(period).GetPublicKey().Serialize();
                        return new SyncCommittee
                        {
                            PubKeys = Enumerable.Repeat(publicKey, SszBasicTypes.SyncCommitteeSize).ToList(),
                            AggregatePubKey = publicKey
                        };
                    },
                    (update, signerPeriod) =>
                    {
                        var domain = domainService.ComputeSyncCommitteeDomain(update.SignatureSlot);
                        var signingRoot = LightClientService.ComputeSigningRoot(update.AttestedHeader.Beacon.HashTreeRoot(), domain);
                        var single = SecretKey(signerPeriod).Sign(signingRoot);
                        var aggregate = single;
                        for (var i = 1; i < SszBasicTypes.SyncCommitteeSize; i++)
                        {
                            aggregate.Add(single);
                        }
                        return aggregate.Serialize();
                    });
            }

            private static BLS.SecretKey SecretKey(ulong period)
            {
                var secretKey = new BLS.SecretKey();
                secretKey.SetHashOf("lc-catch-up-period-" + period);
                return secretKey;
            }

            private static ConsensusFork ForkAt(ulong slot) => ChainSpec.Mainnet.GetForkAtSlot(slot);

            private byte Seed(ulong period) => (byte)(1 + ((period + (ulong)_seedOffset) % 250));

            public SyncCommittee Committee(ulong period)
            {
                if (_committees.TryGetValue(period, out var committee)) return committee;
                committee = _realCommittee != null ? _realCommittee(period) : CommitteeFromSeed(Seed(period), (byte)_seedOffset);
                _committees[period] = committee;
                return committee;
            }

            public ulong FinalizedSlot(ulong period, ulong finalizedOffset = 64) => (period * SlotsPerPeriod) + finalizedOffset;

            public ulong AttestedSlot(ulong period, ulong attestedOffset = 100) => (period * SlotsPerPeriod) + attestedOffset;

            public LightClientHeader FinalizedHeader(ulong period, ulong finalizedOffset = 64)
            {
                var slot = FinalizedSlot(period, finalizedOffset);
                var fork = ForkAt(slot);
                var stateRoot = SparseMerkle.Root(new Dictionary<long, byte[]>
                {
                    [LightClientForkSpec.CurrentSyncCommitteeGIndex(fork)] = Committee(period).HashTreeRoot()
                });
                return LightClientServiceTests.TestDataFactory.CreateHeader(slot, slot + (ulong)_seedOffset, stateRoot, fork);
            }

            public LightClientBootstrap Bootstrap(ulong period, ulong finalizedOffset = 64)
            {
                var fork = ForkAt(FinalizedSlot(period, finalizedOffset));
                var gindex = LightClientForkSpec.CurrentSyncCommitteeGIndex(fork);
                var leaves = new Dictionary<long, byte[]> { [gindex] = Committee(period).HashTreeRoot() };
                return new LightClientBootstrap
                {
                    Fork = fork,
                    Header = FinalizedHeader(period, finalizedOffset),
                    CurrentSyncCommittee = Committee(period),
                    CurrentSyncCommitteeBranch = SparseMerkle.Branch(gindex, leaves)
                };
            }

            public LightClientUpdate Update(
                ulong period,
                ulong? signerPeriod = null,
                ulong? finalizedPeriod = null,
                ulong finalizedOffset = 64,
                ulong attestedOffset = 100)
            {
                var finalized = FinalizedHeader(finalizedPeriod ?? period, finalizedOffset);
                var attestedSlot = AttestedSlot(period, attestedOffset);
                var fork = ForkAt(attestedSlot + 1);
                var leaves = new Dictionary<long, byte[]>
                {
                    [LightClientForkSpec.NextSyncCommitteeGIndex(fork)] = Committee(period + 1).HashTreeRoot(),
                    [LightClientForkSpec.FinalizedRootGIndex(fork)] = finalized.Beacon.HashTreeRoot()
                };
                var attested = LightClientServiceTests.TestDataFactory.CreateHeader(attestedSlot, attestedSlot, SparseMerkle.Root(leaves), fork);

                var update = new LightClientUpdate
                {
                    Fork = fork,
                    AttestedHeader = attested,
                    NextSyncCommittee = Committee(period + 1),
                    NextSyncCommitteeBranch = SparseMerkle.Branch(LightClientForkSpec.NextSyncCommitteeGIndex(fork), leaves),
                    FinalizedHeader = finalized,
                    FinalityBranch = SparseMerkle.Branch(LightClientForkSpec.FinalizedRootGIndex(fork), leaves),
                    SyncAggregate = new SyncAggregate
                    {
                        SyncCommitteeBits = Enumerable.Repeat((byte)0xFF, SszBasicTypes.SyncCommitteeSize / 8).ToArray()
                    },
                    SignatureSlot = attestedSlot + 1
                };
                var signer = signerPeriod ?? period;
                update.SyncAggregate.SyncCommitteeSignature = _realSigner != null
                    ? _realSigner(update, signer)
                    : Enumerable.Repeat(Seed(signer), SszBasicTypes.SignatureLength).ToArray();
                return update;
            }

            public LightClientState StoreAt(ulong period)
            {
                var finalized = FinalizedHeader(period);
                return new LightClientState
                {
                    FinalizedHeader = finalized.Beacon,
                    FinalizedExecutionPayload = finalized.Execution,
                    CurrentSyncCommittee = Committee(period),
                    NextSyncCommittee = Committee(period + 1),
                    FinalizedSlot = finalized.Beacon.Slot,
                    CurrentPeriod = period,
                    OptimisticHeader = finalized.Beacon,
                    OptimisticSlot = finalized.Beacon.Slot,
                    LastUpdated = DateTimeOffset.UtcNow
                };
            }

            private static SyncCommittee CommitteeFromSeed(byte seed, byte chainTag)
            {
                var pubKeys = new List<byte[]>(SszBasicTypes.SyncCommitteeSize);
                for (var i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
                {
                    var key = Enumerable.Repeat((byte)0x01, SszBasicTypes.PubKeyLength).ToArray();
                    key[0] = seed;
                    key[1] = (byte)(i & 0xFF);
                    key[2] = (byte)(i >> 8);
                    key[3] = chainTag;
                    pubKeys.Add(key);
                }

                return new SyncCommittee
                {
                    PubKeys = pubKeys,
                    AggregatePubKey = Enumerable.Repeat(seed, SszBasicTypes.PubKeyLength).ToArray()
                };
            }
        }

        private static class SparseMerkle
        {
            public static byte[] Root(Dictionary<long, byte[]> leaves) => Node(1, leaves);

            public static List<byte[]> Branch(long gindex, Dictionary<long, byte[]> leaves)
            {
                var branch = new List<byte[]>();
                for (var node = gindex; node > 1; node >>= 1)
                {
                    branch.Add(Node(node ^ 1, leaves));
                }
                return branch;
            }

            private static byte[] Node(long gindex, Dictionary<long, byte[]> leaves)
            {
                if (leaves.TryGetValue(gindex, out var leaf)) return leaf;
                if (!leaves.Keys.Any(k => IsUnder(k, gindex))) return new byte[SszBasicTypes.RootLength];
                return LightClientServiceTests.TestDataFactory.HashPair(Node(gindex * 2, leaves), Node((gindex * 2) + 1, leaves));
            }

            private static bool IsUnder(long descendant, long ancestor)
            {
                while (descendant > ancestor) descendant >>= 1;
                return descendant == ancestor;
            }
        }

        private sealed class CommitteeBoundBls : IBls
        {
            public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain) =>
                publicKeys.Length > 0 && publicKeys.All(k => k[0] == aggregateSignature[0]);

            public byte[] AggregateSignatures(byte[][] signatures) => throw new NotSupportedException();
            public bool Verify(byte[] signature, byte[] publicKey, byte[] message) => throw new NotSupportedException();
            public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey) => throw new NotSupportedException();
        }

        private sealed class PeriodBeacon : ILightClientApi
        {
            private readonly Dictionary<string, LightClientBootstrap> _bootstraps = new Dictionary<string, LightClientBootstrap>();

            public PeriodBeacon(PeriodChain chain, params ulong[] servedPeriods)
            {
                foreach (var period in servedPeriods)
                {
                    Updates[period] = chain.Update(period);
                }
            }

            public Dictionary<ulong, LightClientUpdate> Updates { get; } = new Dictionary<ulong, LightClientUpdate>();
            public LightClientUpdate Head { get; set; }
            public bool RangeRequestsFail { get; set; }
            public bool IgnoresRequestedCount { get; set; }
            public bool ServesRange { get; set; } = true;
            public int BootstrapRequests { get; private set; }
            public List<(ulong Start, ulong Count)> RangeRequests { get; } = new List<(ulong Start, ulong Count)>();

            public void AddBootstrap(LightClientBootstrap bootstrap) =>
                _bootstraps[bootstrap.Header.Beacon.HashTreeRoot().ToHex(true)] = bootstrap;

            public Task<LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot)
            {
                BootstrapRequests++;
                if (!_bootstraps.TryGetValue(blockRoot, out var bootstrap))
                {
                    throw new InvalidOperationException("no bootstrap for " + blockRoot);
                }
                return Task.FromResult(LightClientServiceTests.TestDataFactory.CreateBootstrapResponse(bootstrap));
            }

            public Task<IReadOnlyList<LightClientUpdateResponse>> GetUpdatesAsync(ulong startPeriod, ulong count)
            {
                RangeRequests.Add((startPeriod, count));
                if (RangeRequestsFail) throw new InvalidOperationException("beacon light_client/updates unavailable");
                if (!ServesRange) return Task.FromResult<IReadOnlyList<LightClientUpdateResponse>>(Array.Empty<LightClientUpdateResponse>());
                var served = Updates
                    .Where(kv => IgnoresRequestedCount || (kv.Key >= startPeriod && kv.Key < startPeriod + count))
                    .OrderBy(kv => kv.Key >= startPeriod ? 0 : 1)
                    .ThenBy(kv => kv.Key)
                    .Select(kv => LightClientServiceTests.TestDataFactory.CreateUpdateResponse(kv.Value))
                    .ToList();
                return Task.FromResult<IReadOnlyList<LightClientUpdateResponse>>(served);
            }

            public Task<LightClientFinalityUpdateResponse> GetFinalityUpdateAsync()
            {
                if (Head == null) return Task.FromResult<LightClientFinalityUpdateResponse>(null);
                var data = LightClientServiceTests.TestDataFactory.CreateUpdateResponse(Head).Data;
                return Task.FromResult(new LightClientFinalityUpdateResponse
                {
                    Version = "electra",
                    Data = new LightClientFinalityUpdateData
                    {
                        AttestedHeader = data.AttestedHeader,
                        FinalizedHeader = data.FinalizedHeader,
                        FinalityBranch = data.FinalityBranch,
                        SyncAggregate = data.SyncAggregate,
                        SignatureSlot = data.SignatureSlot
                    }
                });
            }

            public Task<LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync() =>
                Task.FromResult<LightClientOptimisticUpdateResponse>(null);
        }
    }
}
