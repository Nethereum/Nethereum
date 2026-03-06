using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryTokenMetadataRepository : ITokenMetadataRepository
    {
        public List<TokenMetadata> Records { get; set; }

        public InMemoryTokenMetadataRepository()
        {
            Records = new List<TokenMetadata>();
        }

        public InMemoryTokenMetadataRepository(List<TokenMetadata> records)
        {
            Records = records;
        }

        public Task UpsertAsync(TokenMetadata metadata)
        {
            var contractLower = metadata.ContractAddress?.ToLowerInvariant();
            var existing = Records.FirstOrDefault(r =>
                r.ContractAddress?.ToLowerInvariant() == contractLower);

            if (existing != null) Records.Remove(existing);
            metadata.UpdateRowDates();
            Records.Add(metadata);
            return Task.FromResult(0);
        }

        public Task<ITokenMetadataView> GetByContractAsync(string contractAddress)
        {
            var contractLower = contractAddress?.ToLowerInvariant();
            return Task.FromResult<ITokenMetadataView>(
                Records.FirstOrDefault(r =>
                    r.ContractAddress?.ToLowerInvariant() == contractLower));
        }
    }
}
