using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryTokenBalanceRepository : ITokenBalanceRepository
    {
        public List<TokenBalance> Records { get; set; }

        public InMemoryTokenBalanceRepository()
        {
            Records = new List<TokenBalance>();
        }

        public InMemoryTokenBalanceRepository(List<TokenBalance> records)
        {
            Records = records;
        }

        public Task UpsertAsync(TokenBalance balance)
        {
            var addressLower = balance.Address?.ToLowerInvariant();
            var contractLower = balance.ContractAddress?.ToLowerInvariant();

            var existing = Records.FirstOrDefault(r =>
                r.Address?.ToLowerInvariant() == addressLower &&
                r.ContractAddress?.ToLowerInvariant() == contractLower);

            if (existing != null) Records.Remove(existing);
            balance.UpdateRowDates();
            Records.Add(balance);
            return Task.FromResult(0);
        }

        public Task UpsertBatchAsync(IEnumerable<TokenBalance> balances)
        {
            foreach (var balance in balances)
                UpsertAsync(balance);
            return Task.FromResult(0);
        }

        public Task<IEnumerable<ITokenBalanceView>> GetByAddressAsync(string address)
        {
            var addressLower = address?.ToLowerInvariant();
            var results = Records
                .Where(r => r.Address?.ToLowerInvariant() == addressLower)
                .Cast<ITokenBalanceView>();
            return Task.FromResult(results);
        }

        public Task<IEnumerable<ITokenBalanceView>> GetByContractAsync(string contractAddress, int page, int pageSize)
        {
            var contractLower = contractAddress?.ToLowerInvariant();
            var results = Records
                .Where(r => r.ContractAddress?.ToLowerInvariant() == contractLower)
                .OrderByDescending(r => r.Balance)
                .Skip(page * pageSize)
                .Take(pageSize)
                .Cast<ITokenBalanceView>();
            return Task.FromResult(results);
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            var blockNum = (long)blockNumber;
            Records.RemoveAll(r => r.LastUpdatedBlockNumber == blockNum);
            return Task.FromResult(0);
        }
    }
}
