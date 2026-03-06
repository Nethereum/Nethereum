using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryNFTInventoryRepository : INFTInventoryRepository
    {
        public List<NFTInventory> Records { get; set; }

        public InMemoryNFTInventoryRepository()
        {
            Records = new List<NFTInventory>();
        }

        public InMemoryNFTInventoryRepository(List<NFTInventory> records)
        {
            Records = records;
        }

        public Task UpsertAsync(NFTInventory item)
        {
            var contractLower = item.ContractAddress?.ToLowerInvariant();

            var existing = Records.FirstOrDefault(r =>
                r.Address?.ToLowerInvariant() == item.Address?.ToLowerInvariant() &&
                r.ContractAddress?.ToLowerInvariant() == contractLower &&
                r.TokenId == item.TokenId);

            if (existing != null) Records.Remove(existing);
            item.UpdateRowDates();
            Records.Add(item);
            return Task.FromResult(0);
        }

        public Task UpsertBatchAsync(IEnumerable<NFTInventory> items)
        {
            foreach (var item in items)
                UpsertAsync(item);
            return Task.FromResult(0);
        }

        public Task<IEnumerable<INFTInventoryView>> GetByAddressAsync(string address)
        {
            var addressLower = address?.ToLowerInvariant();
            var results = Records
                .Where(r => r.Address?.ToLowerInvariant() == addressLower)
                .Cast<INFTInventoryView>();
            return Task.FromResult(results);
        }

        public Task<INFTInventoryView> GetByTokenAsync(string contractAddress, string tokenId)
        {
            var contractLower = contractAddress?.ToLowerInvariant();
            var result = Records.FirstOrDefault(r =>
                r.ContractAddress?.ToLowerInvariant() == contractLower &&
                r.TokenId == tokenId &&
                r.Amount != "0");

            if (result == null)
            {
                result = Records.FirstOrDefault(r =>
                    r.ContractAddress?.ToLowerInvariant() == contractLower &&
                    r.TokenId == tokenId);
            }

            return Task.FromResult<INFTInventoryView>(result);
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            var blockNum = (long)blockNumber;
            Records.RemoveAll(r => r.LastUpdatedBlockNumber == blockNum);
            return Task.FromResult(0);
        }
    }
}
