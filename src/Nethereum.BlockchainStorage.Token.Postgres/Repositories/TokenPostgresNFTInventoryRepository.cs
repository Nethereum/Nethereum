using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Npgsql;
using NpgsqlTypes;

namespace Nethereum.BlockchainStorage.Token.Postgres.Repositories
{
    public class TokenPostgresNFTInventoryRepository : INFTInventoryRepository
    {
        private readonly TokenPostgresDbContext _context;

        public TokenPostgresNFTInventoryRepository(TokenPostgresDbContext context)
        {
            _context = context;
        }

        public async Task UpsertAsync(NFTInventory item)
        {
            var addressLower = item.Address?.ToLowerInvariant();
            var contractLower = item.ContractAddress?.ToLowerInvariant();

            var existing = await _context.NFTInventory
                .FirstOrDefaultAsync(r =>
                    r.Address.ToLower() == addressLower &&
                    r.ContractAddress.ToLower() == contractLower &&
                    r.TokenId == item.TokenId)
                .ConfigureAwait(false);

            if (existing != null)
            {
                existing.Amount = item.Amount;
                existing.TokenType = item.TokenType;
                existing.LastUpdatedBlockNumber = item.LastUpdatedBlockNumber;
                existing.UpdateRowDates();
            }
            else
            {
                item.UpdateRowDates();
                _context.NFTInventory.Add(item);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task UpsertBatchAsync(IEnumerable<NFTInventory> items)
        {
            var list = items as IList<NFTInventory> ?? items.ToList();
            if (list.Count == 0) return;

            var now = DateTime.UtcNow;

            const string sql = @"
                INSERT INTO nftinventory (address, contractaddress, tokenid, amount, tokentype, lastupdatedblocknumber, rowcreated, rowupdated)
                SELECT * FROM UNNEST(@addresses, @contracts, @tokenIds, @amounts, @types, @blocks, @created, @updated)
                ON CONFLICT (address, contractaddress, tokenid) DO UPDATE SET
                    amount = EXCLUDED.amount,
                    tokentype = EXCLUDED.tokentype,
                    lastupdatedblocknumber = EXCLUDED.lastupdatedblocknumber,
                    rowupdated = EXCLUDED.rowupdated";

            var addresses = new string[list.Count];
            var contracts = new string[list.Count];
            var tokenIds = new string[list.Count];
            var amounts = new string[list.Count];
            var types = new string[list.Count];
            var blocks = new long[list.Count];
            var created = new DateTime[list.Count];
            var updated = new DateTime[list.Count];

            for (var i = 0; i < list.Count; i++)
            {
                var n = list[i];
                addresses[i] = n.Address?.ToLowerInvariant();
                contracts[i] = n.ContractAddress?.ToLowerInvariant();
                tokenIds[i] = n.TokenId;
                amounts[i] = n.Amount;
                types[i] = n.TokenType;
                blocks[i] = n.LastUpdatedBlockNumber;
                created[i] = now;
                updated[i] = now;
            }

            await _context.Database.ExecuteSqlRawAsync(sql,
                new NpgsqlParameter("addresses", addresses),
                new NpgsqlParameter("contracts", contracts),
                new NpgsqlParameter("tokenIds", tokenIds),
                new NpgsqlParameter("amounts", amounts),
                new NpgsqlParameter("types", types),
                new NpgsqlParameter("blocks", blocks),
                new NpgsqlParameter("created", created),
                new NpgsqlParameter("updated", updated)
            ).ConfigureAwait(false);
        }

        public async Task<IEnumerable<INFTInventoryView>> GetByAddressAsync(string address)
        {
            var addressLower = address?.ToLowerInvariant();
            return await _context.NFTInventory
                .Where(r => r.Address.ToLower() == addressLower)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<INFTInventoryView> GetByTokenAsync(string contractAddress, string tokenId)
        {
            var contractLower = contractAddress?.ToLowerInvariant();
            return await _context.NFTInventory
                .FirstOrDefaultAsync(r =>
                    r.ContractAddress.ToLower() == contractLower &&
                    r.TokenId == tokenId)
                .ConfigureAwait(false);
        }

        public async Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            var blockNum = (long)blockNumber;
            var affected = await _context.NFTInventory
                .Where(r => r.LastUpdatedBlockNumber == blockNum)
                .ToListAsync()
                .ConfigureAwait(false);

            _context.NFTInventory.RemoveRange(affected);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
