using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Storage.Entities;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Storage.Repositories
{
    public interface IAddressTransactionRepository
    {
        Task UpsertAsync(
            TransactionReceiptVO transactionReceiptVO, string address, string error = null, 
            string newContractAddress = null);

        Task<IAddressTransactionView> FindAsync(
            string address, HexBigInteger blockNumber, string transactionHash);
    }
}