using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sync
{
    public interface IBatchStore
    {
        Task SaveBatchAsync(BatchInfo batch);

        Task<BatchInfo?> GetBatchAsync(BigInteger fromBlock, BigInteger toBlock);

        Task<BatchInfo?> GetBatchByHashAsync(byte[] batchHash);

        Task<BatchInfo?> GetBatchContainingBlockAsync(BigInteger blockNumber);

        Task<BatchInfo?> GetLatestBatchAsync();

        Task<BigInteger> GetLatestImportedBlockAsync();

        Task<IReadOnlyList<BatchInfo>> GetBatchesAfterAsync(BigInteger fromBlock, int limit = 100);

        Task<IReadOnlyList<BatchInfo>> GetPendingBatchesAsync();

        Task UpdateBatchStatusAsync(BigInteger fromBlock, BigInteger toBlock, BatchStatus status);

        Task<bool> IsBatchImportedAsync(byte[] batchHash);
    }
}
