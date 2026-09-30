using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.Services
{
    public interface IInternalTransactionSource
    {
        Task<List<InternalTransaction>> ProduceAsync(string transactionHash);
    }
}
