using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface ITransactionRepository
    {
        Task UpsertAsync(TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract);

        Task UpsertAsync(TransactionReceiptVO transactionReceiptVO);

        Task<Entities.ITransactionView> FindByBlockNumberAndHashAsync(HexBigInteger blockNumber, string hash);

        /// <summary>
        /// Persists a revert reason for a stored transaction, if the transaction exists and does not
        /// already have a revert reason set. Implementations own the transactional boundary so callers
        /// (orchestration services) can remain storage-agnostic.
        /// </summary>
        Task UpdateRevertReasonAsync(string txHash, string revertReason);
    }
}
