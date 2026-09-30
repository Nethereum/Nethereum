using System;

namespace Nethereum.CoreChain.Storage
{
    public interface IChainMetadataStore
    {
        ulong GetLastBlock();

        byte[] GetLastBlockHash();

        void Commit(ulong lastBlock, byte[] lastBlockHash);

        ulong GetDurableStateBlock();

        void CommitDurableState(ulong block, byte[] hash);

        ulong GetLastFetchedHeader();

        void SetLastFetchedHeader(ulong blockNumber);

        ulong GetLastFetchedBody();

        void SetLastFetchedBody(ulong blockNumber);

        void SetLastFetchedHeaderAndBody(ulong headerBlock, ulong bodyBlock);

        ulong GetReceiptBackfillCursor();

        void SetReceiptBackfillCursor(ulong blockNumber);

        bool IsGenesisLoaded();

        void MarkGenesisLoaded();

        void SaveCheckpoint(ulong blockNumber, byte[] stateRoot, byte[] blockHash);

        ulong GetLatestCheckpoint();

        ChainCheckpoint? GetCheckpoint(ulong blockNumber);

        ChainCheckpoint? GetNearestCheckpointAtOrBefore(ulong upToBlock);

        ChainCheckpoint RewindToCheckpointAtOrBefore(ulong targetBlock);

        System.Collections.Generic.IReadOnlyList<ulong> ListCheckpointBlockNumbers();

        void DeleteCheckpoint(ulong blockNumber);

        int DeleteCheckpointsAbove(ulong targetBlock);

        void ResetForStateRebuild();

        SnapSyncState GetSnapSyncState();
        void SaveSnapSyncState(SnapSyncState state);
        void ClearSnapSyncState();

        void ClearCommittedHead();

        byte[] GetLightClientStateBlob();
        void SaveLightClientStateBlob(byte[] blob);

        HeaderSyncState GetHeaderSyncState();

        void SaveHeaderSyncState(HeaderSyncState state);

        byte[] GetDeferredHealAccountsBlob();
        void SaveDeferredHealAccountsBlob(byte[] blob);
        void ClearDeferredHealAccountsBlob();

        byte[] GetDeferredHealCodeBlob();
        void SaveDeferredHealCodeBlob(byte[] blob);
        void ClearDeferredHealCodeBlob();

        void UpsertDeferredStorageDebt(DeferredStorageDebt debt);
        System.Collections.Generic.IReadOnlyList<DeferredStorageDebt> ListOpenDeferredStorageDebts(int max = int.MaxValue);
        ulong CountOpenDeferredStorageDebts();
        void ClearDeferredStorageDebt(byte[] accountHash, byte[] discoveredStorageRoot);

        void ClearAllDeferredStorageDebts();
    }

    public readonly struct ChainCheckpoint
    {
        public ulong BlockNumber { get; }
        public byte[] StateRoot { get; }
        public byte[] BlockHash { get; }
        public ulong UnixTimestamp { get; }

        public ChainCheckpoint(ulong blockNumber, byte[] stateRoot, byte[] blockHash, ulong unixTimestamp)
        {
            BlockNumber = blockNumber;
            StateRoot = stateRoot ?? throw new ArgumentNullException(nameof(stateRoot));
            BlockHash = blockHash ?? throw new ArgumentNullException(nameof(blockHash));
            UnixTimestamp = unixTimestamp;
        }
    }
}
