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
        /// <summary>
        /// <c>DOMAIN_SYNC_COMMITTEE = DomainType('0x07000000')</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/beacon-chain.md">
        /// specs/altair/beacon-chain.md</see>. Four-byte <c>DomainType</c> width per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/phase0/beacon-chain.md">
        /// specs/phase0/beacon-chain.md</see> line 209.
        /// </summary>
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
                return;
            }

            var blockRootHex = _config.WeakSubjectivityRoot.ToHex(true);
            var response = await _apiClient.GetBootstrapAsync(blockRootHex).ConfigureAwait(false);
            var bootstrap = LightClientResponseMapper.ToDomain(response);
            ValidateBootstrap(bootstrap);

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
                _state.AddBlockHash(bootstrap.Header.Execution.BlockNumber, bootstrap.Header.Execution.BlockHash);
            }

            await _store.SaveAsync(_state).ConfigureAwait(false);
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
            var update = LightClientResponseMapper.ToDomain(response);

            if (update?.AttestedHeader?.Beacon == null || update.AttestedHeader.Execution == null)
            {
                return false;
            }

            if (!VerifyOptimisticSyncAggregate(update))
            {
                return false;
            }

            if (!VerifyExecutionBranch(update.AttestedHeader))
            {
                return false;
            }

            _state.OptimisticHeader = update.AttestedHeader.Beacon;
            _state.OptimisticExecutionPayload = update.AttestedHeader.Execution;
            _state.OptimisticSlot = update.AttestedHeader.Beacon.Slot;
            _state.OptimisticLastUpdated = DateTimeOffset.UtcNow;

            if (update.AttestedHeader.Execution != null)
            {
                _state.AddBlockHash(update.AttestedHeader.Execution.BlockNumber, update.AttestedHeader.Execution.BlockHash);
            }

            await _store.SaveAsync(_state).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> UpdateFinalityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_state == null)
            {
                throw new InvalidOperationException("Light client not initialised. Call InitializeAsync first.");
            }

            var response = await _apiClient.GetFinalityUpdateAsync().ConfigureAwait(false);
            var update = LightClientResponseMapper.ToDomain(response);

            if (update?.FinalizedHeader?.Beacon == null || update.FinalizedHeader.Execution == null)
            {
                return false;
            }

            if (!VerifyFinalitySyncAggregate(update))
            {
                return false;
            }

            if (!VerifyExecutionBranch(update.AttestedHeader))
            {
                return false;
            }

            if (!VerifyFinalityBranch(update.AttestedHeader, update.FinalizedHeader, update.FinalityBranch))
            {
                return false;
            }

            if (!VerifyExecutionBranch(update.FinalizedHeader))
            {
                return false;
            }

            _state.FinalizedHeader = update.FinalizedHeader.Beacon;
            _state.FinalizedExecutionPayload = update.FinalizedHeader.Execution;
            _state.FinalizedSlot = update.FinalizedHeader.Beacon.Slot;
            _state.CurrentPeriod = ComputePeriod(update.FinalizedHeader.Beacon.Slot);
            _state.LastUpdated = DateTimeOffset.UtcNow;

            if (update.FinalizedHeader.Execution != null)
            {
                _state.AddBlockHash(update.FinalizedHeader.Execution.BlockNumber, update.FinalizedHeader.Execution.BlockHash);
            }

            await _store.SaveAsync(_state).ConfigureAwait(false);
            return true;
        }

        public LightClientState GetState()
        {
            if (_state == null)
            {
                throw new InvalidOperationException("Light client not initialised.");
            }

            return _state;
        }

        /// <summary>
        /// <c>initialize_light_client_store</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> lines 344–362: asserts the
        /// <c>LightClientHeader</c> validity, the trusted block root equality, and the
        /// <c>current_sync_committee_branch</c> merkle proof against the beacon header
        /// <c>state_root</c>. The branch length and subtree index are fork-aware per the
        /// Electra reshape (gindex 54 → 86) in
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
        /// specs/electra/light-client/sync-protocol.md</see> line 56. The wire format Nethereum
        /// emits is the spec <c>Vector[Bytes32, N]</c> with exact-length N (no leading zero
        /// padding), so <see cref="SszMerkleizer.VerifyProof"/> (strict
        /// <c>is_valid_merkle_branch</c>) yields the same result as
        /// <c>is_valid_normalized_merkle_branch</c> for all current fork gindices
        /// (consensus-spec-tests vectors confirm 241/241).
        /// </summary>
        private void ValidateBootstrap(LightClientBootstrap bootstrap)
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

            var trustedRoot = _config.WeakSubjectivityRoot;
            if (trustedRoot == null || trustedRoot.Length != SszBasicTypes.RootLength)
            {
                throw new InvalidOperationException(
                    $"LightClientConfig.WeakSubjectivityRoot must be exactly {SszBasicTypes.RootLength} bytes.");
            }

            var headerRoot = bootstrap.Header.Beacon.HashTreeRoot();
            if (!headerRoot.SequenceEqual(trustedRoot))
            {
                throw new InvalidOperationException("Bootstrap header does not match weak subjectivity root");
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

        /// <summary>
        /// <c>is_valid_merkle_branch(leaf=hash_tree_root(current_sync_committee), branch=current_sync_committee_branch,
        /// depth=floorlog2(CURRENT_SYNC_COMMITTEE_GINDEX), index=get_subtree_index(CURRENT_SYNC_COMMITTEE_GINDEX),
        /// root=header.beacon.state_root)</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> lines 348–356. Electra+ uses
        /// <c>CURRENT_SYNC_COMMITTEE_GINDEX_ELECTRA = 86</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
        /// specs/electra/light-client/sync-protocol.md</see> line 56.
        /// </summary>
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

        private bool TryApplyUpdate(LightClientState state, LightClientUpdate update)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (update == null) return false;

            if (update.AttestedHeader?.Beacon == null)
            {
                return false;
            }

            if (update.FinalizedHeader?.Beacon == null || update.FinalizedHeader.Execution == null)
            {
                return false;
            }

            if (!(update.SignatureSlot > update.AttestedHeader.Beacon.Slot &&
                  update.AttestedHeader.Beacon.Slot >= update.FinalizedHeader.Beacon.Slot &&
                  update.AttestedHeader.Beacon.Slot >= state.FinalizedSlot))
            {
                return false;
            }

            if (!VerifySyncAggregate(update))
            {
                return false;
            }

            if (!VerifyExecutionBranch(update.AttestedHeader))
            {
                return false;
            }

            if (!VerifyFinalityBranch(update.AttestedHeader, update.FinalizedHeader, update.FinalityBranch))
            {
                return false;
            }

            if (!VerifyExecutionBranch(update.FinalizedHeader))
            {
                return false;
            }

            var isSyncCommitteeUpdate = IsSyncCommitteeUpdate(update);
            if (isSyncCommitteeUpdate)
            {
                if (!VerifyNextSyncCommitteeBranch(
                        update.AttestedHeader,
                        update.NextSyncCommittee,
                        update.NextSyncCommitteeBranch))
                {
                    return false;
                }
            }

            var updatePeriod = ComputePeriod(update.FinalizedHeader.Beacon.Slot);

            if (updatePeriod > state.CurrentPeriod && state.NextSyncCommittee != null)
            {
                state.CurrentSyncCommittee = state.NextSyncCommittee;
            }

            state.FinalizedHeader = update.FinalizedHeader.Beacon;
            state.FinalizedExecutionPayload = update.FinalizedHeader.Execution;
            state.FinalizedSlot = update.FinalizedHeader.Beacon.Slot;
            state.CurrentPeriod = updatePeriod;

            if (update.FinalizedHeader.Execution != null)
            {
                state.AddBlockHash(update.FinalizedHeader.Execution.BlockNumber, update.FinalizedHeader.Execution.BlockHash);
            }

            if (isSyncCommitteeUpdate && update.NextSyncCommittee != null)
            {
                state.NextSyncCommittee = update.NextSyncCommittee;
            }

            return true;
        }

        /// <summary>
        /// <c>is_sync_committee_update(update)</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> line 206: returns true iff the
        /// <c>next_sync_committee_branch</c> field differs from the default all-zero
        /// <c>NextSyncCommitteeBranch()</c> vector. A bare <c>NextSyncCommittee != null</c> check
        /// is insufficient because <c>LightClientUpdate</c> default-constructs a non-null
        /// all-zero <c>SyncCommittee</c>. Length must match the fork-aware
        /// <see cref="LightClientForkSpec.NextSyncCommitteeBranchLength(ConsensusFork)"/>.
        /// </summary>
        private static bool IsSyncCommitteeUpdate(LightClientUpdate update)
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

        /// <summary>
        /// <c>is_valid_merkle_branch(leaf=hash_tree_root(next_sync_committee), branch=next_sync_committee_branch,
        /// depth=floorlog2(NEXT_SYNC_COMMITTEE_GINDEX), index=get_subtree_index(NEXT_SYNC_COMMITTEE_GINDEX),
        /// root=attested_header.beacon.state_root)</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> lines 421–437. Electra+ uses
        /// <c>NEXT_SYNC_COMMITTEE_GINDEX_ELECTRA = 87</c> with depth 6 per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
        /// specs/electra/light-client/sync-protocol.md</see> line 56; pre-Electra forks use
        /// <c>NEXT_SYNC_COMMITTEE_GINDEX = 55</c> with depth 5 per the altair spec line 70.
        /// </summary>
        private static bool VerifyNextSyncCommitteeBranch(
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

        /// <summary>
        /// <c>validate_light_client_update</c> participation floor per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> line 383:
        /// <c>assert sum(sync_aggregate.sync_committee_bits) &gt;= MIN_SYNC_COMMITTEE_PARTICIPANTS</c>.
        /// The strict <c>Bitvector[SYNC_COMMITTEE_SIZE]</c> length is asserted at entry: a
        /// wrong-size buffer is a wire-format violation, not a quorum failure, and is reported
        /// as an exception so callers do not conflate "malformed input" with "sub-quorum".
        /// </summary>
        internal static bool HasBaselineParticipation(SyncAggregate aggregate)
        {
            if (aggregate?.SyncCommitteeBits == null) return false;
            EnsureCommitteeBitsLength(aggregate.SyncCommitteeBits);
            return CountParticipants(aggregate.SyncCommitteeBits) >= LightClientForkSpec.MinSyncCommitteeParticipants;
        }

        /// <summary>
        /// <c>process_light_client_update</c> supermajority finality gate per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> line 543:
        /// <c>sum(sync_committee_bits) * 3 &gt;= len(sync_committee_bits) * 2</c>. With
        /// <c>SYNC_COMMITTEE_SIZE = 512</c> the smallest passing count is 342
        /// (<c>342 * 3 = 1026 &gt;= 512 * 2 = 1024</c>). The <c>Bitvector</c> covers exactly
        /// <c>SYNC_COMMITTEE_SIZE</c> bits with <c>SYNC_COMMITTEE_SIZE % 8 == 0</c> so no
        /// trailing-zero-bit mask check is needed.
        /// </summary>
        internal static bool HasSupermajorityParticipation(SyncAggregate aggregate)
        {
            if (aggregate?.SyncCommitteeBits == null) return false;
            EnsureCommitteeBitsLength(aggregate.SyncCommitteeBits);
            var bitsLength = aggregate.SyncCommitteeBits.Length * 8;
            return CountParticipants(aggregate.SyncCommitteeBits) * 3 >= bitsLength * 2;
        }

        /// <summary>
        /// <c>is_finality_update(update)</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> lines 210–214:
        /// <c>return update.finality_branch != FinalityBranch()</c>. Default
        /// <c>FinalityBranch()</c> is the all-zero vector at the fork-aware length, so a
        /// non-empty, present branch with any non-zero byte signals the update proposes
        /// to move <c>FinalizedHeader</c> and therefore must clear the supermajority gate.
        /// Same non-zero-byte pattern as <see cref="IsSyncCommitteeUpdate"/>.
        /// </summary>
        internal static bool IsFinalityUpdate(LightClientUpdate update)
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

        /// <summary>
        /// <c>process_light_client_update</c> sync-aggregate verification per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> lines 503–548. Applies the
        /// baseline participation floor for any update; additionally requires the
        /// supermajority quorum (line 543) when the update carries a non-default
        /// <c>finality_branch</c> per <see cref="IsFinalityUpdate"/>.
        /// </summary>
        private bool VerifyFullUpdateSyncAggregate(LightClientUpdate update)
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

        /// <summary>
        /// <c>process_light_client_optimistic_update</c> sync-aggregate verification per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> lines 580–590. Optimistic
        /// header updates require only the baseline participation floor; the
        /// <c>get_safety_threshold</c> gate (line 528) requires <c>LightClientStore</c>
        /// fields not yet plumbed and is tracked as a follow-up.
        /// </summary>
        private bool VerifyOptimisticSyncAggregate(LightClientOptimisticUpdate update)
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

        /// <summary>
        /// <c>process_light_client_finality_update</c> sync-aggregate verification per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> lines 569–579. Finality
        /// updates wrap into a <c>LightClientUpdate</c> carrying a non-default
        /// <c>finality_branch</c> and therefore require both the baseline floor
        /// (line 383) and the supermajority quorum (line 543).
        /// </summary>
        private bool VerifyFinalitySyncAggregate(LightClientFinalityUpdate update)
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

            var participants = SelectParticipantPubKeys(_state.CurrentSyncCommittee, bits);
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

        /// <summary>
        /// Derives the BLS signing domain for a sync-aggregate signed at
        /// <paramref name="signatureSlot"/> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> lines 451–454:
        /// <c>fork_version_slot = max(signature_slot, 1) - 1</c>;
        /// <c>fork_version = compute_fork_version(compute_epoch_at_slot(fork_version_slot))</c>;
        /// <c>domain = compute_domain(DOMAIN_SYNC_COMMITTEE, fork_version, genesis_validators_root)</c>.
        /// </summary>
        private byte[] ComputeSyncCommitteeDomain(ulong signatureSlot)
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

        /// <summary>
        /// <c>compute_fork_data_root</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/phase0/beacon-chain.md">
        /// specs/phase0/beacon-chain.md</see> line 938: <c>hash_tree_root(ForkData(current_version,
        /// genesis_validators_root))</c>. Strict 4-byte <c>ForkVersion</c> + 32-byte
        /// <c>GenesisValidatorsRoot</c> width asserts: malformed input throws instead of
        /// silently truncating to produce a plausible-but-wrong root.
        /// </summary>
        private static byte[] ComputeForkDataRoot(byte[] forkVersion, byte[] genesisValidatorsRoot)
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

        /// <summary>
        /// <c>compute_signing_root</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/phase0/beacon-chain.md">
        /// specs/phase0/beacon-chain.md</see> line 973:
        /// <c>hash_tree_root(SigningData(object_root, domain))</c>. Throws on malformed input
        /// instead of returning an empty byte array (caller treated empty as "signature invalid",
        /// masking config bugs).
        /// </summary>
        private static byte[] ComputeSigningRoot(byte[] objectRoot, byte[] domain)
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

            // Pre-Capella headers carry no ExecutionPayloadHeader per
            // specs/altair/light-client/sync-protocol.md, so the verification is
            // vacuously satisfied — there is nothing to prove.
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
