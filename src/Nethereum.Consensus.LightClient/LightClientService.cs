using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Ssz;
using Nethereum.Signer.Bls;

namespace Nethereum.Consensus.LightClient
{
    public class LightClientService
    {
        public static readonly byte[] DomainSyncCommittee = { 0x07, 0x00, 0x00, 0x00 };

        private readonly ILightClientApi _apiClient;
        private readonly IBls _bls;
        private readonly LightClientConfig _config;
        private readonly ILightClientStore _store;
        private LightClientState? _state;

        public LightClientService(
            ILightClientApi apiClient,
            IBls bls,
            LightClientConfig config,
            ILightClientStore store)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _bls = bls ?? throw new ArgumentNullException(nameof(bls));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _state = await _store.LoadAsync().ConfigureAwait(false);
            if (_state != null)
            {
                try
                {
                    await UpdateAsync(cancellationToken).ConfigureAwait(false);
                    await UpdateFinalityAsync(cancellationToken).ConfigureAwait(false);
                    await UpdateOptimisticAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                }
                return;
            }

            var bootstrapRoot = _config.WeakSubjectivityRoot;
            if (IsEmptyRoot(bootstrapRoot))
            {
                var finalityResponse = await _apiClient.GetFinalityUpdateAsync().ConfigureAwait(false);
                var finalityUpdate = LightClientResponseMapper.ToDomain(finalityResponse);
                bootstrapRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();
            }
            var blockRootHex = bootstrapRoot.ToHex(true);
            var response = await _apiClient.GetBootstrapAsync(blockRootHex).ConfigureAwait(false);
            var bootstrap = LightClientResponseMapper.ToDomain(response);
            ValidateBootstrap(bootstrap, bootstrapRoot);

            _state = new LightClientState
            {
                FinalizedHeader = bootstrap.Header.Beacon,
                FinalizedExecutionPayload = bootstrap.Header.Execution,
                CurrentSyncCommittee = bootstrap.CurrentSyncCommittee,
                NextSyncCommittee = new SyncCommittee(),
                FinalizedSlot = bootstrap.Header.Beacon.Slot,
                CurrentPeriod = ComputePeriod(bootstrap.Header.Beacon.Slot),
                LastUpdated = DateTimeOffset.UtcNow
            };

            if (bootstrap.Header.Execution != null)
            {
                _state.SetBlockHash(
                    bootstrap.Header.Execution.BlockNumber,
                    bootstrap.Header.Execution.BlockHash,
                    BlockHashFinality.Finalized);
            }

            await _store.SaveAsync(_state).ConfigureAwait(false);
        }

        private static bool IsEmptyRoot(byte[] root)
        {
            if (root == null || root.Length == 0) return true;
            for (int i = 0; i < root.Length; i++)
                if (root[i] != 0) return false;
            return true;
        }

        public async Task<bool> UpdateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_state == null)
            {
                throw new InvalidOperationException("Light client not initialised. Call InitializeAsync first.");
            }

            var startPeriod = _state.CurrentPeriod;
            var responses = await _apiClient.GetUpdatesAsync(startPeriod, count: 4).ConfigureAwait(false);
            var updates = LightClientResponseMapper.ToDomain(responses);

            var applied = false;
            foreach (var update in updates ?? Array.Empty<LightClientUpdate>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryApplyUpdate(_state, update))
                {
                    applied = true;
                }
            }

            if (applied)
            {
                _state.LastUpdated = DateTimeOffset.UtcNow;
                await _store.SaveAsync(_state).ConfigureAwait(false);
            }

            return applied;
        }

        public async Task<bool> UpdateOptimisticAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_state == null)
            {
                throw new InvalidOperationException("Light client not initialised. Call InitializeAsync first.");
            }

            var response = await _apiClient.GetOptimisticUpdateAsync().ConfigureAwait(false);
            var optimistic = LightClientResponseMapper.ToDomain(response);

            if (optimistic?.AttestedHeader?.Beacon == null)
            {
                return false;
            }

            var synthesized = SynthesizeUpdate(optimistic);
            var applied = TryApplyUpdate(_state, synthesized);
            if (applied)
            {
                _state.LastUpdated = DateTimeOffset.UtcNow;
                await _store.SaveAsync(_state).ConfigureAwait(false);
            }

            return applied;
        }

        public async Task<bool> UpdateFinalityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_state == null)
            {
                throw new InvalidOperationException("Light client not initialised. Call InitializeAsync first.");
            }

            var response = await _apiClient.GetFinalityUpdateAsync().ConfigureAwait(false);
            var finality = LightClientResponseMapper.ToDomain(response);

            if (finality?.FinalizedHeader?.Beacon == null)
            {
                return false;
            }

            var synthesized = SynthesizeUpdate(finality);
            var applied = TryApplyUpdate(_state, synthesized);
            if (applied)
            {
                _state.LastUpdated = DateTimeOffset.UtcNow;
                await _store.SaveAsync(_state).ConfigureAwait(false);
            }

            return applied;
        }

        public static LightClientUpdate SynthesizeUpdate(LightClientFinalityUpdate finality)
        {
            if (finality == null) return null;

            var branchLen = LightClientForkSpec.NextSyncCommitteeBranchLength(finality.Fork);
            var zeroBranch = new List<byte[]>(branchLen);
            for (var i = 0; i < branchLen; i++)
            {
                zeroBranch.Add(new byte[SszBasicTypes.RootLength]);
            }

            return new LightClientUpdate
            {
                Fork = finality.Fork,
                AttestedHeader = finality.AttestedHeader,
                NextSyncCommittee = new SyncCommittee(),
                NextSyncCommitteeBranch = zeroBranch,
                FinalizedHeader = finality.FinalizedHeader,
                FinalityBranch = finality.FinalityBranch,
                SyncAggregate = finality.SyncAggregate,
                SignatureSlot = finality.SignatureSlot
            };
        }

        public static LightClientUpdate SynthesizeUpdate(LightClientOptimisticUpdate optimistic)
        {
            if (optimistic == null) return null;

            var nextBranchLen = LightClientForkSpec.NextSyncCommitteeBranchLength(optimistic.Fork);
            var finalityBranchLen = LightClientForkSpec.FinalityBranchLength(optimistic.Fork);

            var nextZeroBranch = new List<byte[]>(nextBranchLen);
            for (var i = 0; i < nextBranchLen; i++)
            {
                nextZeroBranch.Add(new byte[SszBasicTypes.RootLength]);
            }

            var finalityZeroBranch = new List<byte[]>(finalityBranchLen);
            for (var i = 0; i < finalityBranchLen; i++)
            {
                finalityZeroBranch.Add(new byte[SszBasicTypes.RootLength]);
            }

            return new LightClientUpdate
            {
                Fork = optimistic.Fork,
                AttestedHeader = optimistic.AttestedHeader,
                NextSyncCommittee = new SyncCommittee(),
                NextSyncCommitteeBranch = nextZeroBranch,
                FinalizedHeader = new LightClientHeader { Fork = optimistic.Fork },
                FinalityBranch = finalityZeroBranch,
                SyncAggregate = optimistic.SyncAggregate,
                SignatureSlot = optimistic.SignatureSlot
            };
        }

        public LightClientState GetState()
        {
            if (_state == null)
            {
                throw new InvalidOperationException("Light client not initialised.");
            }

            return _state;
        }

        private void ValidateBootstrap(LightClientBootstrap bootstrap, byte[] expectedRoot)
        {
            if (bootstrap == null) throw new ArgumentNullException(nameof(bootstrap));
            if (bootstrap.Header?.Beacon == null)
            {
                throw new InvalidOperationException("Bootstrap missing beacon header");
            }
            if (bootstrap.CurrentSyncCommittee == null)
            {
                throw new InvalidOperationException("Bootstrap missing sync committee");
            }

            var fork = bootstrap.Header.Fork;
            var expectedBranchLength = LightClientForkSpec.CurrentSyncCommitteeBranchDepth(fork);
            if (bootstrap.CurrentSyncCommitteeBranch == null ||
                bootstrap.CurrentSyncCommitteeBranch.Count != expectedBranchLength)
            {
                throw new InvalidOperationException(
                    $"Bootstrap current_sync_committee_branch must be exactly {expectedBranchLength} roots for fork {fork}; got {bootstrap.CurrentSyncCommitteeBranch?.Count ?? 0}.");
            }

            if (!IsValidLightClientHeader(bootstrap.Header))
            {
                throw new InvalidOperationException("Bootstrap header failed is_valid_light_client_header");
            }

            if (expectedRoot == null || expectedRoot.Length != SszBasicTypes.RootLength)
            {
                throw new InvalidOperationException(
                    $"Bootstrap expected root must be exactly {SszBasicTypes.RootLength} bytes.");
            }

            var headerRoot = bootstrap.Header.Beacon.HashTreeRoot();
            if (!headerRoot.SequenceEqual(expectedRoot))
            {
                throw new InvalidOperationException(
                    $"Bootstrap header root 0x{headerRoot.ToHex()} does not match requested root 0x{expectedRoot.ToHex()}");
            }

            if (!VerifyCurrentSyncCommitteeBranch(
                    bootstrap.Header,
                    bootstrap.CurrentSyncCommittee,
                    bootstrap.CurrentSyncCommitteeBranch))
            {
                throw new InvalidOperationException("Bootstrap sync committee branch invalid");
            }
        }

        private static bool IsValidLightClientHeader(LightClientHeader header) =>
            VerifyExecutionBranch(header);

        private static bool VerifyCurrentSyncCommitteeBranch(
            LightClientHeader header,
            SyncCommittee committee,
            IList<byte[]> branch)
        {
            if (header?.Beacon == null || committee == null || branch == null)
                return false;

            var fork = header.Fork;
            var depth = LightClientForkSpec.CurrentSyncCommitteeBranchDepth(fork);
            var index = LightClientForkSpec.CurrentSyncCommitteeBranchIndex(fork);

            var leaf = committee.HashTreeRoot();

            return SszMerkleizer.VerifyProof(
                leaf,
                branch,
                depth,
                index,
                header.Beacon.StateRoot);
        }

        public string LastRejectReason { get; private set; }

        private bool Reject(string reason)
        {
            LastRejectReason = reason;
            return false;
        }

        private bool TryApplyUpdate(LightClientState state, LightClientUpdate update)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (update == null) return Reject("null_update");
            if (update.AttestedHeader?.Beacon == null) return Reject("no_attested_beacon");
            if (update.SyncAggregate == null) return Reject("no_sync_aggregate");

            var hasFinality = IsFinalityUpdate(update);
            var hasSyncCommittee = IsSyncCommitteeUpdate(update);

            if (hasFinality)
            {
                if (update.FinalizedHeader?.Beacon == null) return Reject("no_finalized_beacon");
            }

            if (!HasMonotonicSlots(state, update, hasFinality))
            {
                return Reject($"non_monotonic_slots attested={update.AttestedHeader.Beacon.Slot} optSlot={state.OptimisticSlot} finSlot={state.FinalizedSlot}");
            }

            if (!HasValidPeriodWindow(state, update))
            {
                return Reject($"invalid_period_window attestedPeriod={ComputePeriod(update.AttestedHeader.Beacon.Slot)} storeFinPeriod={ComputePeriod(state.FinalizedSlot)}");
            }

            if (!VerifyExecutionBranch(update.AttestedHeader))
            {
                return Reject("exec_branch_attested");
            }

            if (hasSyncCommittee)
            {
                var updateAttestedPeriod = ComputePeriod(update.AttestedHeader.Beacon.Slot);
                var storePeriod = ComputePeriod(state.FinalizedSlot);
                if (updateAttestedPeriod == storePeriod && IsNextSyncCommitteeKnown(state))
                {
                    if (!SyncCommitteeEquals(update.NextSyncCommittee, state.NextSyncCommittee))
                    {
                        return Reject("next_sync_committee_mismatch");
                    }
                }

                if (!VerifyNextSyncCommitteeBranch(
                        update.AttestedHeader,
                        update.NextSyncCommittee,
                        update.NextSyncCommitteeBranch))
                {
                    return Reject("next_sync_committee_branch");
                }
            }

            if (hasFinality)
            {
                if (!VerifyFinalityBranch(update.AttestedHeader, update.FinalizedHeader, update.FinalityBranch))
                {
                    return Reject("finality_branch");
                }

                if (!VerifyExecutionBranch(update.FinalizedHeader))
                {
                    return Reject("exec_branch_finalized");
                }
            }

            if (!VerifyFullUpdateSyncAggregate(update))
            {
                return Reject("sync_aggregate_bls");
            }

            var hasSupermajority = HasSupermajorityParticipation(update.SyncAggregate);
            var finalityAdvances = hasFinality
                                   && update.FinalizedHeader!.Beacon!.Slot > state.FinalizedSlot;
            var updateHasFinalizedNsc = UpdateHasFinalizedNextSyncCommittee(state, update);

            var willApplyFinality = hasSupermajority && (finalityAdvances || updateHasFinalizedNsc);
            var willApplyOptimistic = update.AttestedHeader.Beacon.Slot > state.OptimisticSlot;

            if (!willApplyFinality && !willApplyOptimistic)
            {
                return Reject($"no_advance attested={update.AttestedHeader.Beacon.Slot} optSlot={state.OptimisticSlot} supermajority={hasSupermajority}");
            }

            LastRejectReason = null;
            ApplyLightClientUpdate(state, update, willApplyFinality, willApplyOptimistic);
            return true;
        }

        private bool HasMonotonicSlots(LightClientState state, LightClientUpdate update, bool hasFinality)
        {
            if (update.SignatureSlot <= update.AttestedHeader.Beacon.Slot) return false;

            if (hasFinality && update.AttestedHeader.Beacon.Slot < update.FinalizedHeader!.Beacon!.Slot)
            {
                return false;
            }

            var attestedAdvances = update.AttestedHeader.Beacon.Slot > state.FinalizedSlot;
            var introducesNextCommittee = !IsNextSyncCommitteeKnown(state)
                                          && IsSyncCommitteeUpdate(update)
                                          && ComputePeriod(update.AttestedHeader.Beacon.Slot) == ComputePeriod(state.FinalizedSlot);

            if (!attestedAdvances && !introducesNextCommittee && update.AttestedHeader.Beacon.Slot < state.FinalizedSlot)
            {
                return false;
            }

            return attestedAdvances || introducesNextCommittee || update.AttestedHeader.Beacon.Slot >= state.FinalizedSlot;
        }

        public void ApplyLightClientUpdate(
            LightClientState state,
            LightClientUpdate update,
            bool applyFinality,
            bool applyOptimistic)
        {
            if (applyFinality && update.FinalizedHeader?.Beacon != null)
            {
                var storePeriod = ComputePeriod(state.FinalizedSlot);
                var updateFinalizedPeriod = ComputePeriod(update.FinalizedHeader.Beacon.Slot);
                var carriesNextCommittee = IsSyncCommitteeUpdate(update) && update.NextSyncCommittee != null;

                if (!IsNextSyncCommitteeKnown(state))
                {
                    if (carriesNextCommittee && updateFinalizedPeriod == storePeriod)
                    {
                        state.NextSyncCommittee = update.NextSyncCommittee;
                    }
                }
                else if (updateFinalizedPeriod == storePeriod + 1)
                {
                    state.CurrentSyncCommittee = state.NextSyncCommittee;
                    state.NextSyncCommittee = carriesNextCommittee ? update.NextSyncCommittee : new SyncCommittee();
                }
            }

            if (applyFinality)
            {
                state.FinalizedHeader = update.FinalizedHeader!.Beacon;
                state.FinalizedExecutionPayload = update.FinalizedHeader.Execution;
                state.FinalizedSlot = update.FinalizedHeader.Beacon!.Slot;
                state.CurrentPeriod = ComputePeriod(update.FinalizedHeader.Beacon.Slot);

                if (update.FinalizedHeader.Execution != null)
                {
                    state.SetBlockHash(
                        update.FinalizedHeader.Execution.BlockNumber,
                        update.FinalizedHeader.Execution.BlockHash,
                        BlockHashFinality.Finalized);
                }
            }

            if (applyOptimistic)
            {
                state.OptimisticHeader = update.AttestedHeader.Beacon;
                state.OptimisticExecutionPayload = update.AttestedHeader.Execution;
                state.OptimisticSlot = update.AttestedHeader.Beacon.Slot;
                state.OptimisticLastUpdated = DateTimeOffset.UtcNow;

                if (update.AttestedHeader.Execution != null)
                {
                    state.SetBlockHash(
                        update.AttestedHeader.Execution.BlockNumber,
                        update.AttestedHeader.Execution.BlockHash,
                        BlockHashFinality.Optimistic);
                }
            }

            if (applyFinality && state.FinalizedSlot > state.OptimisticSlot)
            {
                state.OptimisticHeader = state.FinalizedHeader;
                state.OptimisticExecutionPayload = state.FinalizedExecutionPayload;
                state.OptimisticSlot = state.FinalizedSlot;
                state.OptimisticLastUpdated = DateTimeOffset.UtcNow;
            }
        }

        public static bool IsNextSyncCommitteeKnown(LightClientState state)
        {
            var nsc = state?.NextSyncCommittee;
            if (nsc == null) return false;

            if (nsc.AggregatePubKey != null)
            {
                for (var i = 0; i < nsc.AggregatePubKey.Length; i++)
                {
                    if (nsc.AggregatePubKey[i] != 0) return true;
                }
            }

            if (nsc.PubKeys == null || nsc.PubKeys.Count == 0) return false;

            foreach (var pk in nsc.PubKeys)
            {
                if (pk == null) continue;
                for (var i = 0; i < pk.Length; i++)
                {
                    if (pk[i] != 0) return true;
                }
            }

            return false;
        }

        public bool UpdateHasFinalizedNextSyncCommittee(LightClientState state, LightClientUpdate update)
        {
            if (IsNextSyncCommitteeKnown(state)) return false;
            if (!IsSyncCommitteeUpdate(update)) return false;
            if (!IsFinalityUpdate(update)) return false;
            if (update?.FinalizedHeader?.Beacon == null) return false;
            if (update.AttestedHeader?.Beacon == null) return false;

            var finalizedPeriod = ComputePeriod(update.FinalizedHeader.Beacon.Slot);
            var attestedPeriod = ComputePeriod(update.AttestedHeader.Beacon.Slot);
            return finalizedPeriod == attestedPeriod;
        }

        public bool HasValidPeriodWindow(LightClientState state, LightClientUpdate update)
        {
            var storePeriod = ComputePeriod(state.FinalizedSlot);
            var updateSignaturePeriod = ComputePeriod(update.SignatureSlot);

            if (IsNextSyncCommitteeKnown(state))
            {
                return updateSignaturePeriod == storePeriod ||
                       updateSignaturePeriod == storePeriod + 1;
            }

            return updateSignaturePeriod == storePeriod;
        }

        public static bool SyncCommitteeEquals(SyncCommittee a, SyncCommittee b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            byte[] rootA;
            byte[] rootB;
            try
            {
                rootA = a.HashTreeRoot();
                rootB = b.HashTreeRoot();
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(rootA, rootB);
        }

        public static bool IsSyncCommitteeUpdate(LightClientUpdate update)
        {
            if (update?.NextSyncCommitteeBranch == null || update.NextSyncCommitteeBranch.Count == 0)
            {
                return false;
            }

            foreach (var node in update.NextSyncCommitteeBranch)
            {
                if (node == null || node.Length != SszBasicTypes.RootLength)
                {
                    return false;
                }

                for (var i = 0; i < SszBasicTypes.RootLength; i++)
                {
                    if (node[i] != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool VerifyNextSyncCommitteeBranch(
            LightClientHeader attestedHeader,
            SyncCommittee nextSyncCommittee,
            IList<byte[]> nextSyncCommitteeBranch)
        {
            if (attestedHeader?.Beacon == null || nextSyncCommittee == null || nextSyncCommitteeBranch == null)
                return false;

            var fork = attestedHeader.Fork;
            var depth = LightClientForkSpec.NextSyncCommitteeBranchDepth(fork);
            var index = LightClientForkSpec.NextSyncCommitteeBranchIndex(fork);

            if (nextSyncCommitteeBranch.Count != depth)
                return false;

            byte[] leaf;
            try
            {
                leaf = nextSyncCommittee.HashTreeRoot();
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            return SszMerkleizer.VerifyProof(
                leaf,
                nextSyncCommitteeBranch,
                depth,
                index,
                attestedHeader.Beacon.StateRoot);
        }

        public static bool HasBaselineParticipation(SyncAggregate aggregate)
        {
            if (aggregate?.SyncCommitteeBits == null) return false;
            EnsureCommitteeBitsLength(aggregate.SyncCommitteeBits);
            return CountParticipants(aggregate.SyncCommitteeBits) >= LightClientForkSpec.MinSyncCommitteeParticipants;
        }

        public static bool HasSupermajorityParticipation(SyncAggregate aggregate)
        {
            if (aggregate?.SyncCommitteeBits == null) return false;
            EnsureCommitteeBitsLength(aggregate.SyncCommitteeBits);
            var bitsLength = aggregate.SyncCommitteeBits.Length * 8;
            return CountParticipants(aggregate.SyncCommitteeBits) * 3 >= bitsLength * 2;
        }

        public static bool IsFinalityUpdate(LightClientUpdate update)
        {
            if (update?.FinalityBranch == null || update.FinalityBranch.Count == 0)
            {
                return false;
            }

            foreach (var node in update.FinalityBranch)
            {
                if (node == null) continue;
                for (var i = 0; i < node.Length; i++)
                {
                    if (node[i] != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CountParticipants(byte[] bits)
        {
            var sum = 0;
            for (var i = 0; i < bits.Length; i++)
            {
                var b = bits[i];
                b = (byte)(b - ((b >> 1) & 0x55));
                b = (byte)((b & 0x33) + ((b >> 2) & 0x33));
                sum += (byte)((b + (b >> 4)) & 0x0F);
            }

            return sum;
        }

        private static void EnsureCommitteeBitsLength(byte[] bits)
        {
            var expected = SszBasicTypes.SyncCommitteeSize / 8;
            if (bits.Length != expected)
            {
                throw new InvalidOperationException(
                    $"SyncAggregate.SyncCommitteeBits must be exactly {expected} bytes ({SszBasicTypes.SyncCommitteeSize} bits); got {bits.Length}.");
            }
        }

        private bool VerifySyncAggregate(LightClientUpdate update)
        {
            return VerifyFullUpdateSyncAggregate(update);
        }

        public bool VerifyFullUpdateSyncAggregate(LightClientUpdate update)
        {
            if (_state?.CurrentSyncCommittee == null ||
                update?.SyncAggregate == null ||
                update.AttestedHeader?.Beacon == null)
            {
                return false;
            }

            if (!HasBaselineParticipation(update.SyncAggregate))
            {
                return false;
            }

            if (IsFinalityUpdate(update) && !HasSupermajorityParticipation(update.SyncAggregate))
            {
                return false;
            }

            return VerifyAggregateSignature(
                update.SyncAggregate.SyncCommitteeBits,
                update.SyncAggregate.SyncCommitteeSignature,
                update.AttestedHeader.Beacon,
                update.SignatureSlot);
        }

        public bool VerifyOptimisticSyncAggregate(LightClientOptimisticUpdate update)
        {
            if (_state?.CurrentSyncCommittee == null ||
                update?.SyncAggregate == null ||
                update.AttestedHeader?.Beacon == null)
            {
                return false;
            }

            if (!HasBaselineParticipation(update.SyncAggregate))
            {
                return false;
            }

            return VerifyAggregateSignature(
                update.SyncAggregate.SyncCommitteeBits,
                update.SyncAggregate.SyncCommitteeSignature,
                update.AttestedHeader.Beacon,
                update.SignatureSlot);
        }

        public bool VerifyFinalitySyncAggregate(LightClientFinalityUpdate update)
        {
            if (_state?.CurrentSyncCommittee == null ||
                update?.SyncAggregate == null ||
                update.AttestedHeader?.Beacon == null)
            {
                return false;
            }

            if (!HasBaselineParticipation(update.SyncAggregate))
            {
                return false;
            }

            if (!HasSupermajorityParticipation(update.SyncAggregate))
            {
                return false;
            }

            return VerifyAggregateSignature(
                update.SyncAggregate.SyncCommitteeBits,
                update.SyncAggregate.SyncCommitteeSignature,
                update.AttestedHeader.Beacon,
                update.SignatureSlot);
        }

        private bool VerifyAggregateSignature(byte[] bits, byte[] signature, BeaconBlockHeader attestedHeader, ulong signatureSlot)
        {
            if (bits == null || signature == null || attestedHeader == null)
            {
                return false;
            }

            var signaturePeriod = ComputePeriod(signatureSlot);
            var storePeriod = ComputePeriod(_state.FinalizedSlot);
            SyncCommittee committee;
            if (signaturePeriod == storePeriod)
                committee = _state.CurrentSyncCommittee;
            else if (signaturePeriod == storePeriod + 1)
                committee = _state.NextSyncCommittee;
            else
                return false;
            if (committee == null)
            {
                return false;
            }

            var participants = SelectParticipantPubKeys(committee, bits);
            if (participants.Count == 0)
            {
                return false;
            }

            var domain = ComputeSyncCommitteeDomain(signatureSlot);
            var message = ComputeSigningRoot(attestedHeader.HashTreeRoot(), domain);

            return _bls.VerifyAggregate(signature, participants.ToArray(), new[] { message }, domain);
        }

        private ulong ComputePeriod(ulong slot)
        {
            var slotsPerPeriod = _config.ChainSpec.SlotsPerEpoch * 256;
            if (slotsPerPeriod == 0)
            {
                return 0;
            }

            return slot / slotsPerPeriod;
        }

        private List<byte[]> SelectParticipantPubKeys(SyncCommittee committee, byte[] bits)
        {
            var pubKeys = committee?.PubKeys;
            var participants = new List<byte[]>();

            if (pubKeys == null || pubKeys.Count == 0 || bits == null)
            {
                return participants;
            }

            if (pubKeys.Count != SszBasicTypes.SyncCommitteeSize)
            {
                throw new InvalidOperationException(
                    $"SyncCommittee.PubKeys must contain exactly {SszBasicTypes.SyncCommitteeSize} keys; got {pubKeys.Count}.");
            }

            EnsureCommitteeBitsLength(bits);

            var memberIndex = 0;
            for (var byteIndex = 0; byteIndex < bits.Length && memberIndex < pubKeys.Count; byteIndex++)
            {
                var value = bits[byteIndex];
                for (var bitIndex = 0; bitIndex < 8 && memberIndex < pubKeys.Count; bitIndex++, memberIndex++)
                {
                    if ((value & (1 << bitIndex)) != 0)
                    {
                        participants.Add(pubKeys[memberIndex]);
                    }
                }
            }

            return participants;
        }

        public byte[] ComputeSyncCommitteeDomain(ulong signatureSlot)
        {
            var forkVersionSlot = signatureSlot == 0UL ? 0UL : signatureSlot - 1UL;
            var forkVersion = _config.ChainSpec.GetForkVersionAtSlot(forkVersionSlot);

            var forkDataRoot = ComputeForkDataRoot(forkVersion, _config.GenesisValidatorsRoot);
            if (forkDataRoot.Length != SszBasicTypes.RootLength)
                throw new InvalidOperationException(
                    $"forkDataRoot must be exactly {SszBasicTypes.RootLength} bytes; got {forkDataRoot.Length}.");

            var domain = new byte[32];
            Buffer.BlockCopy(DomainSyncCommittee, 0, domain, 0, 4);
            Buffer.BlockCopy(forkDataRoot, 0, domain, 4, 28);
            return domain;
        }

        public static byte[] ComputeForkDataRoot(byte[] forkVersion, byte[] genesisValidatorsRoot)
        {
            if (forkVersion == null) throw new ArgumentNullException(nameof(forkVersion));
            if (genesisValidatorsRoot == null) throw new ArgumentNullException(nameof(genesisValidatorsRoot));
            if (forkVersion.Length != 4)
                throw new InvalidOperationException(
                    $"ForkVersion must be exactly 4 bytes; got {forkVersion.Length}.");
            if (genesisValidatorsRoot.Length != SszBasicTypes.RootLength)
                throw new InvalidOperationException(
                    $"GenesisValidatorsRoot must be exactly {SszBasicTypes.RootLength} bytes; got {genesisValidatorsRoot.Length}.");

            var fieldRoots = new[]
            {
                SszBasicTypes.HashTreeRootFixedBytes(forkVersion, 4),
                SszBasicTypes.HashTreeRootFixedBytes(genesisValidatorsRoot, SszBasicTypes.RootLength)
            };

            return SszMerkleizer.Merkleize(fieldRoots);
        }

        public static byte[] ComputeSigningRoot(byte[] objectRoot, byte[] domain)
        {
            if (objectRoot == null) throw new ArgumentNullException(nameof(objectRoot));
            if (domain == null) throw new ArgumentNullException(nameof(domain));
            if (objectRoot.Length != SszBasicTypes.RootLength)
                throw new InvalidOperationException(
                    $"objectRoot must be exactly {SszBasicTypes.RootLength} bytes; got {objectRoot.Length}.");
            if (domain.Length != 32)
                throw new InvalidOperationException(
                    $"domain must be exactly 32 bytes; got {domain.Length}.");

            var fieldRoots = new[]
            {
                SszBasicTypes.HashTreeRootFixedBytes(objectRoot, SszBasicTypes.RootLength),
                SszBasicTypes.HashTreeRootFixedBytes(domain, 32)
            };

            return SszMerkleizer.Merkleize(fieldRoots);
        }

        private static bool VerifyExecutionBranch(LightClientHeader header)
        {
            if (header?.Beacon == null)
                return false;

            if (!LightClientForkSpec.HasExecutionPayloadHeader(header.Fork))
                return true;

            if (header.Execution == null || header.ExecutionBranch == null)
                return false;

            var depth = LightClientForkSpec.ExecutionBranchDepth(header.Fork);
            var index = LightClientForkSpec.ExecutionBranchIndex(header.Fork);

            var executionRoot = header.Execution.HashTreeRoot(header.Fork);

            return SszMerkleizer.VerifyProof(
                executionRoot,
                header.ExecutionBranch,
                depth,
                index,
                header.Beacon.BodyRoot
            );
        }

        private static bool VerifyFinalityBranch(LightClientHeader attestedHeader, LightClientHeader finalizedHeader, IList<byte[]> finalityBranch)
        {
            if (attestedHeader?.Beacon == null || finalizedHeader?.Beacon == null || finalityBranch == null)
                return false;

            // Branch length/depth/gindex depend on the active fork at the finalized header's slot.
            // EIP-7251 (Electra) reshaped BeaconState so the merkle path to FINALIZED_CHECKPOINT.root
            // is longer (depth 7 vs 6) and rooted at a different generalised index (169 vs 105).
            var fork = finalizedHeader.Fork;
            var depth = LightClientForkSpec.FinalityBranchDepth(fork);
            var index = LightClientForkSpec.FinalityBranchIndex(fork);

            var finalizedRoot = finalizedHeader.Beacon.HashTreeRoot();

            return SszMerkleizer.VerifyProof(
                finalizedRoot,
                finalityBranch,
                depth,
                index,
                attestedHeader.Beacon.StateRoot
            );
        }
    }
}
