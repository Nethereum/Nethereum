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
    }

    public interface INonCanonicalInternalTransactionRepository
    {
        Task MarkNonCanonicalAsync(BigInteger blockNumber);
    }
}
