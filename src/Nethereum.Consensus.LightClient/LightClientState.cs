using System;
using System.Collections.Generic;
using Nethereum.Consensus.Ssz;

namespace Nethereum.Consensus.LightClient
{
    /// <summary>
    /// Provenance marker for block hashes stored on the light-client state per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 513–515 (optimistic header
    /// update condition) and lines 542–548 (finalized header update gate). Optimistic
    /// entries may be overwritten when promoted to finalized; finalized entries are
    /// authoritative and reject conflicting writes.
    /// </summary>
    public enum BlockHashFinality
    {
        Optimistic = 0,
        Finalized = 1
    }

    /// <summary>
    /// Block-hash entry tracked on the light-client state with its
    /// <see cref="BlockHashFinality"/> provenance. A single dictionary keyed by block
    /// number stores either an optimistic or a finalized entry; promotion from
    /// optimistic to finalized is allowed, but a finalized entry rejects any
    /// conflicting overwrite.
    /// </summary>
    public readonly struct ProvenancedBlockHash
    {
        public ProvenancedBlockHash(byte[] blockHash, BlockHashFinality finality)
        {
            BlockHash = blockHash;
            Finality = finality;
        }

        public byte[] BlockHash { get; }
        public BlockHashFinality Finality { get; }
    }

    public class LightClientState
    {
        public const int MaxBlockHashHistorySize = 256;

        public BeaconBlockHeader? FinalizedHeader { get; set; }
        public ExecutionPayloadHeader? FinalizedExecutionPayload { get; set; }
        public SyncCommittee? CurrentSyncCommittee { get; set; }
        public SyncCommittee? NextSyncCommittee { get; set; }

        public ulong FinalizedSlot { get; set; }
        public ulong CurrentPeriod { get; set; }
        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.MinValue;

        public BeaconBlockHeader? OptimisticHeader { get; set; }
        public ExecutionPayloadHeader? OptimisticExecutionPayload { get; set; }
        public ulong OptimisticSlot { get; set; }
        public DateTimeOffset OptimisticLastUpdated { get; set; } = DateTimeOffset.MinValue;

        public Dictionary<ulong, ProvenancedBlockHash> BlockHashHistory { get; set; }
            = new Dictionary<ulong, ProvenancedBlockHash>();

        /// <summary>
        /// Records a block hash at <paramref name="blockNumber"/> with explicit
        /// <see cref="BlockHashFinality"/> provenance. Promotion (optimistic to finalized,
        /// or idempotent finalized re-write of the same hash) is permitted; a finalized
        /// entry will reject a conflicting optimistic overwrite and throw on a
        /// conflicting finalized overwrite (chain-split signal). Silently no-ops on
        /// null or non-32-byte input.
        /// </summary>
        public void SetBlockHash(ulong blockNumber, byte[] blockHash, BlockHashFinality finality)
        {
            if (blockHash == null || blockHash.Length != 32) return;

            if (BlockHashHistory.TryGetValue(blockNumber, out var existing))
            {
                if (existing.Finality == BlockHashFinality.Finalized)
                {
                    if (!ByteArrayEquals(existing.BlockHash, blockHash))
                    {
                        if (finality == BlockHashFinality.Finalized)
                        {
                            throw new InvalidOperationException(
                                $"Finalized block hash conflict at block {blockNumber}.");
                        }

                        return;
                    }
                }
            }

            BlockHashHistory[blockNumber] = new ProvenancedBlockHash(blockHash, finality);

            if (BlockHashHistory.Count > MaxBlockHashHistorySize)
            {
                PruneOldestEntries();
            }
        }

        public void AddBlockHash(ulong blockNumber, byte[] blockHash)
            => SetBlockHash(blockNumber, blockHash, BlockHashFinality.Finalized);

        public byte[]? GetBlockHash(ulong blockNumber)
        {
            return BlockHashHistory.TryGetValue(blockNumber, out var entry) ? entry.BlockHash : null;
        }

        public byte[]? GetFinalizedBlockHash(ulong blockNumber)
        {
            return BlockHashHistory.TryGetValue(blockNumber, out var entry)
                   && entry.Finality == BlockHashFinality.Finalized
                ? entry.BlockHash
                : null;
        }

        private void PruneOldestEntries()
        {
            while (BlockHashHistory.Count > MaxBlockHashHistorySize)
            {
                ulong oldest = ulong.MaxValue;
                foreach (var key in BlockHashHistory.Keys)
                {
                    if (key < oldest) oldest = key;
                }

                if (oldest == ulong.MaxValue) break;
                BlockHashHistory.Remove(oldest);
            }
        }

        private static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }
    }
}
