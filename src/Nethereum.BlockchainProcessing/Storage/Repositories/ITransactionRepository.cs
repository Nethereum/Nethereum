using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Storage.Repositories
{
    public interface ITransactionRepository
    {
        Task UpsertAsync(TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract);

        Task UpsertAsync(TransactionReceiptVO transactionReceiptVO);

        Task<Entities.ITransactionView> FindByBlockNumberAndHashAsync(HexBigInteger blockNumber, string hash);
    }
}