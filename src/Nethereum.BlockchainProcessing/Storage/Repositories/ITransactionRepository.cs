using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Storage.Repositories
{
    public interface ITransactionRepository
    {
        Task UpsertAsync(string contractAddress, string code, Transaction transaction, TransactionReceipt transactionReceipt, bool failedCreatingContract, HexBigInteger blockTimestamp);

        Task UpsertAsync(
            Transaction transaction, TransactionReceipt receipt, bool failed,
            HexBigInteger timeStamp, bool hasVmStack = false, string error = null);

        Task<Entities.ITransactionView> FindByBlockNumberAndHashAsync(HexBigInteger blockNumber, string hash);
    }
}