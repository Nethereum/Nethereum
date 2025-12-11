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
        private static readonly byte[] DomainSyncCommitteeType = { 0x07, 0x00, 0x00, 0x00 };

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
                NextSyncCommittee = bootstrap.CurrentSyncCommittee,
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

        private static void ValidateBootstrap(LightClientBootstrap bootstrap)
        {
            if (bootstrap == null) throw new ArgumentNullException(nameof(bootstrap));
            if (bootstrap.Header?.Beacon == null)
            {
                throw new InvalidOperationException("Bootstrap must include a beacon header.");
            }
            if (bootstrap.CurrentSyncCommittee == null)
            {
                throw new InvalidOperationException("Bootstrap missing sync committee.");
            }
        }

        private bool TryApplyUpdate(LightClientState state, LightClientUpdate update)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (update == null) return false;

            if (update.FinalizedHeader?.Beacon == null || update.FinalizedHeader.Execution == null)
            {
                return false;
            }

            if (!VerifySyncAggregate(update))
            {
                return false;
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

            if (update.NextSyncCommittee != null)
            {
                state.NextSyncCommittee = update.NextSyncCommittee;
            }

            return true;
        }

        private bool VerifySyncAggregate(LightClientUpdate update)
        {
            if (_state?.CurrentSyncCommittee == null ||
                update?.SyncAggregate == null ||
                update.AttestedHeader?.Beacon == null)
            {
                return false;
            }

            return VerifySyncAggregateCore(
                update.SyncAggregate.SyncCommitteeBits,
                update.SyncAggregate.SyncCommitteeSignature,
                update.AttestedHeader.Beacon);
        }

        private bool VerifyOptimisticSyncAggregate(LightClientOptimisticUpdate update)
        {
            if (_state?.CurrentSyncCommittee == null ||
                update?.SyncAggregate == null ||
                update.AttestedHeader?.Beacon == null)
            {
                return false;
            }

            return VerifySyncAggregateCore(
                update.SyncAggregate.SyncCommitteeBits,
                update.SyncAggregate.SyncCommitteeSignature,
                update.AttestedHeader.Beacon);
        }

        private bool VerifyFinalitySyncAggregate(LightClientFinalityUpdate update)
        {
            if (_state?.CurrentSyncCommittee == null ||
                update?.SyncAggregate == null ||
                update.AttestedHeader?.Beacon == null)
            {
                return false;
            }

            return VerifySyncAggregateCore(
                update.SyncAggregate.SyncCommitteeBits,
                update.SyncAggregate.SyncCommitteeSignature,
                update.AttestedHeader.Beacon);
        }

        private bool VerifySyncAggregateCore(byte[] bits, byte[] signature, BeaconBlockHeader attestedHeader)
        {
            if (bits == null || signature == null || attestedHeader == null)
            {
                return false;
            }

            if (bits.Length != SszBasicTypes.SyncCommitteeSize / 8)
            {
                return false;
            }

            var participants = SelectParticipantPubKeys(_state.CurrentSyncCommittee, bits);
            if (participants.Count == 0)
            {
                return false;
            }

            var domain = ComputeSyncCommitteeDomain();
            var message = ComputeSigningRoot(attestedHeader.HashTreeRoot(), domain);
            if (message == null || message.Length == 0)
            {
                return false;
            }

            return _bls.VerifyAggregate(signature, participants.ToArray(), new[] { message }, domain);
        }

        private ulong ComputePeriod(ulong slot)
        {
            var slotsPerPeriod = _config.SlotsPerEpoch * 256;
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

        private byte[] ComputeSyncCommitteeDomain()
        {
            var domain = new byte[32];
            Buffer.BlockCopy(DomainSyncCommitteeType, 0, domain, 0, DomainSyncCommitteeType.Length);
            var forkDataRoot = ComputeForkDataRoot(_config.CurrentForkVersion, _config.GenesisValidatorsRoot);
            Buffer.BlockCopy(forkDataRoot, 0, domain, DomainSyncCommitteeType.Length, Math.Min(28, forkDataRoot.Length));
            return domain;
        }

        private static byte[] ComputeForkDataRoot(byte[] forkVersion, byte[] genesisValidatorsRoot)
        {
            var version = new byte[4];
            if (forkVersion != null)
            {
                Buffer.BlockCopy(forkVersion, 0, version, 0, Math.Min(4, forkVersion.Length));
            }

            var genesis = new byte[SszBasicTypes.RootLength];
            if (genesisValidatorsRoot != null)
            {
                Buffer.BlockCopy(genesisValidatorsRoot, 0, genesis, 0, Math.Min(SszBasicTypes.RootLength, genesisValidatorsRoot.Length));
            }

            var fieldRoots = new[]
            {
                SszBasicTypes.HashTreeRootFixedBytes(version, version.Length),
                SszBasicTypes.HashTreeRootFixedBytes(genesis, genesis.Length)
            };

            return SszMerkleizer.Merkleize(fieldRoots);
        }

        private static byte[] ComputeSigningRoot(byte[] objectRoot, byte[] domain)
        {
            if (objectRoot == null || domain == null || domain.Length != 32)
            {
                return Array.Empty<byte>();
            }

            var fieldRoots = new[]
            {
                SszBasicTypes.HashTreeRootFixedBytes(objectRoot, objectRoot.Length),
                SszBasicTypes.HashTreeRootFixedBytes(domain, domain.Length)
            };

            return SszMerkleizer.Merkleize(fieldRoots);
        }
    }
}
