using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IBundleBatch : IDisposable
    {
        void PutHeader(BlockHeader header, byte[] blockHash);

        void PutUncles(byte[] blockHash, IList<BlockHeader> uncles);

        void PutTransactions(
            byte[] blockHash,
            BigInteger blockNumber,
            IList<Nethereum.Model.ISignedTransaction> transactions);

        void PutReceipt(
            Receipt receipt,
            byte[] txHash,
            byte[] blockHash,
            BigInteger blockNumber,
            int txIndex,
            BigInteger gasUsed,
            string contractAddress,
            BigInteger effectiveGasPrice);

        void SetLastFetchedHeader(ulong blockNumber);

        void SetLastFetchedBody(ulong blockNumber);

        void SetLastFetchedHeaderAndBody(ulong headerBlock, ulong bodyBlock);

        void Commit(ulong lastBlock, byte[] lastBlockHash);

        void SaveSnapSyncState(SnapSyncState state);

        void UpsertDeferredStorageDebt(DeferredStorageDebt debt);

        void SaveDeferredHealCodeBlob(byte[] blob);

        Task CommitAsync(CancellationToken ct = default);

        void Discard();
    }
}
