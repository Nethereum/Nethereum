using System;
using System.Collections.Generic;
using Nethereum.Consensus.Ssz;

namespace Nethereum.Consensus.LightClient
{
    public enum BlockHashFinality
    {
        Optimistic = 0,
        Finalized = 1
    }

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
