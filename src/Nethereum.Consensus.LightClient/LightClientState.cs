using System;
using System.Collections.Generic;
using Nethereum.Consensus.Ssz;

namespace Nethereum.Consensus.LightClient
{
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

        public Dictionary<ulong, byte[]> BlockHashHistory { get; set; } = new Dictionary<ulong, byte[]>();

        public void AddBlockHash(ulong blockNumber, byte[] blockHash)
        {
            if (blockHash == null || blockHash.Length != 32) return;

            BlockHashHistory[blockNumber] = blockHash;

            if (BlockHashHistory.Count > MaxBlockHashHistorySize)
            {
                PruneOldestEntries();
            }
        }

        public byte[] GetBlockHash(ulong blockNumber)
        {
            return BlockHashHistory.TryGetValue(blockNumber, out var hash) ? hash : null;
        }

        private void PruneOldestEntries()
        {
            if (BlockHashHistory.Count <= MaxBlockHashHistorySize) return;

            var toRemove = new List<ulong>();
            ulong minToKeep = FinalizedExecutionPayload?.BlockNumber ?? 0;
            if (minToKeep > MaxBlockHashHistorySize)
            {
                minToKeep -= MaxBlockHashHistorySize;
            }
            else
            {
                minToKeep = 0;
            }

            foreach (var key in BlockHashHistory.Keys)
            {
                if (key < minToKeep)
                {
                    toRemove.Add(key);
                }
            }

            foreach (var key in toRemove)
            {
                BlockHashHistory.Remove(key);
            }
        }
    }
}
