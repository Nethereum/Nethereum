using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface INFTInventoryRepository
    {
        Task UpsertAsync(NFTInventory item);
        Task UpsertBatchAsync(IEnumerable<NFTInventory> items);
        Task<IEnumerable<INFTInventoryView>> GetByAddressAsync(string address);
        Task<INFTInventoryView> GetByTokenAsync(string contractAddress, string tokenId);
        Task DeleteByBlockNumberAsync(BigInteger blockNumber);
    }
}
