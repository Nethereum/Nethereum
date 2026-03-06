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
    public class TokenPostgresBalanceRepository : ITokenBalanceRepository
    {
        private readonly TokenPostgresDbContext _context;

        public TokenPostgresBalanceRepository(TokenPostgresDbContext context)
        {
            _context = context;
        }

        public async Task UpsertAsync(TokenBalance balance)
        {
            var addressLower = balance.Address?.ToLowerInvariant();
            var contractLower = balance.ContractAddress?.ToLowerInvariant();

            var existing = await _context.TokenBalances
                .FirstOrDefaultAsync(r =>
                    r.Address.ToLower() == addressLower &&
                    r.ContractAddress.ToLower() == contractLower)
                .ConfigureAwait(false);

            if (existing != null)
            {
                existing.Balance = balance.Balance;
                existing.TokenType = balance.TokenType;
                existing.LastUpdatedBlockNumber = balance.LastUpdatedBlockNumber;
                existing.UpdateRowDates();
            }
            else
            {
                balance.UpdateRowDates();
                _context.TokenBalances.Add(balance);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task UpsertBatchAsync(IEnumerable<TokenBalance> balances)
        {
            var items = balances as IList<TokenBalance> ?? balances.ToList();
            if (items.Count == 0) return;

            var now = DateTime.UtcNow;

            const string sql = @"
                INSERT INTO tokenbalances (address, contractaddress, balance, tokentype, lastupdatedblocknumber, rowcreated, rowupdated)
                SELECT * FROM UNNEST(@addresses, @contracts, @balances, @types, @blocks, @created, @updated)
                ON CONFLICT (address, contractaddress) DO UPDATE SET
                    balance = EXCLUDED.balance,
                    tokentype = EXCLUDED.tokentype,
                    lastupdatedblocknumber = EXCLUDED.lastupdatedblocknumber,
                    rowupdated = EXCLUDED.rowupdated";

            var addresses = new string[items.Count];
            var contracts = new string[items.Count];
            var balanceValues = new string[items.Count];
            var types = new string[items.Count];
            var blocks = new long[items.Count];
            var created = new DateTime[items.Count];
            var updated = new DateTime[items.Count];

            for (var i = 0; i < items.Count; i++)
            {
                var b = items[i];
                addresses[i] = b.Address?.ToLowerInvariant();
                contracts[i] = b.ContractAddress?.ToLowerInvariant();
                balanceValues[i] = b.Balance;
                types[i] = b.TokenType;
                blocks[i] = b.LastUpdatedBlockNumber;
                created[i] = now;
                updated[i] = now;
            }

            await _context.Database.ExecuteSqlRawAsync(sql,
                new NpgsqlParameter("addresses", addresses),
                new NpgsqlParameter("contracts", contracts),
                new NpgsqlParameter("balances", balanceValues),
                new NpgsqlParameter("types", types),
                new NpgsqlParameter("blocks", blocks),
                new NpgsqlParameter("created", created),
                new NpgsqlParameter("updated", updated)
            ).ConfigureAwait(false);
        }

        public async Task<IEnumerable<ITokenBalanceView>> GetByAddressAsync(string address)
        {
            var addressLower = address?.ToLowerInvariant();
            return await _context.TokenBalances
                .Where(r => r.Address.ToLower() == addressLower)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<ITokenBalanceView>> GetByContractAsync(string contractAddress, int page, int pageSize)
        {
            var contractLower = contractAddress?.ToLowerInvariant();
            return await _context.TokenBalances
                .Where(r => r.ContractAddress.ToLower() == contractLower)
                .OrderByDescending(r => r.Balance)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            var blockNum = (long)blockNumber;
            var affected = await _context.TokenBalances
                .Where(r => r.LastUpdatedBlockNumber == blockNum)
                .ToListAsync()
                .ConfigureAwait(false);

            _context.TokenBalances.RemoveRange(affected);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
