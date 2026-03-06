using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;

namespace Nethereum.BlockchainStorage.Token.Postgres.Repositories
{
    public class TokenPostgresMetadataRepository : ITokenMetadataRepository
    {
        private readonly TokenPostgresDbContext _context;

        public TokenPostgresMetadataRepository(TokenPostgresDbContext context)
        {
            _context = context;
        }

        public async Task UpsertAsync(TokenMetadata metadata)
        {
            var contractLower = metadata.ContractAddress?.ToLowerInvariant();

            var existing = await _context.TokenMetadata
                .FirstOrDefaultAsync(r =>
                    r.ContractAddress.ToLower() == contractLower)
                .ConfigureAwait(false);

            if (existing != null)
            {
                existing.Name = metadata.Name;
                existing.Symbol = metadata.Symbol;
                existing.Decimals = metadata.Decimals;
                existing.TokenType = metadata.TokenType;
                existing.UpdateRowDates();
            }
            else
            {
                metadata.UpdateRowDates();
                _context.TokenMetadata.Add(metadata);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<ITokenMetadataView> GetByContractAsync(string contractAddress)
        {
            var contractLower = contractAddress?.ToLowerInvariant();
            return await _context.TokenMetadata
                .FirstOrDefaultAsync(r =>
                    r.ContractAddress.ToLower() == contractLower)
                .ConfigureAwait(false);
        }
    }
}
