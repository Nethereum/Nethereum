using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface IInternalTransactionRepository
    {
        Task UpsertAsync(InternalTransaction internalTransaction);
        Task<List<IInternalTransactionView>> GetByTransactionHashAsync(string txHash);
        Task<List<IInternalTransactionView>> GetByAddressAsync(string address, int page, int pageSize);

        /// <summary>
        /// Returns the canonical contract-call transactions in the given block range that have not yet
        /// been internal-traced. Used by InternalTransactionOrchestrator to enumerate outstanding work.
        /// </summary>
        Task<List<TransactionToTrace>> GetContractTransactionsInRangeAsync(BigInteger fromBlock, BigInteger toBlock);
    }

    public interface INonCanonicalInternalTransactionRepository
    {
        Task MarkNonCanonicalAsync(BigInteger blockNumber);
    }
}
