using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;

namespace Nethereum.BlockchainStorage.Token.Postgres.Repositories
{
    public class TokenPostgresTransferLogRepository : ITokenTransferLogRepository, INonCanonicalTokenTransferLogRepository
    {
        private readonly TokenPostgresDbContext _context;

        public TokenPostgresTransferLogRepository(TokenPostgresDbContext context)
        {
            _context = context;
        }

        public async Task UpsertAsync(TokenTransferLog log)
        {
            var existing = await _context.TokenTransferLogs
                .FirstOrDefaultAsync(r =>
                    r.TransactionHash == log.TransactionHash &&
                    r.LogIndex == log.LogIndex)
                .ConfigureAwait(false);

            if (existing != null)
            {
                existing.BlockNumber = log.BlockNumber;
                existing.BlockHash = log.BlockHash;
                existing.ContractAddress = log.ContractAddress;
                existing.EventHash = log.EventHash;
                existing.FromAddress = log.FromAddress;
                existing.ToAddress = log.ToAddress;
                existing.Amount = log.Amount;
                existing.TokenId = log.TokenId;
                existing.OperatorAddress = log.OperatorAddress;
                existing.TokenType = log.TokenType;
                existing.IsCanonical = log.IsCanonical;
                existing.UpdateRowDates();
            }
            else
            {
                log.UpdateRowDates();
                _context.TokenTransferLogs.Add(log);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<ITokenTransferLogView> FindByTransactionHashAndLogIndexAsync(string hash, BigInteger logIndex)
        {
            var idx = (long)logIndex;
            return await _context.TokenTransferLogs
                .FirstOrDefaultAsync(r =>
                    r.TransactionHash == hash &&
                    r.LogIndex == idx)
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<ITokenTransferLogView>> GetByAddressAsync(string address, int page, int pageSize)
        {
            var addressLower = address?.ToLowerInvariant();
            return await _context.TokenTransferLogs
                .Where(r => r.IsCanonical &&
                    (r.FromAddress.ToLower() == addressLower ||
                     r.ToAddress.ToLower() == addressLower))
                .OrderByDescending(r => r.BlockNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<ITokenTransferLogView>> GetByContractAsync(string contractAddress, int page, int pageSize)
        {
            var contractLower = contractAddress?.ToLowerInvariant();
            return await _context.TokenTransferLogs
                .Where(r => r.IsCanonical &&
                    r.ContractAddress.ToLower() == contractLower)
                .OrderByDescending(r => r.BlockNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<ITokenTransferLogView>> GetByBlockNumberAsync(long blockNumber)
        {
            return await _context.TokenTransferLogs
                .Where(r => r.BlockNumber == blockNumber)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task MarkNonCanonicalAsync(BigInteger blockNumber)
        {
            var blockNum = (long)blockNumber;
            var affected = await _context.TokenTransferLogs
                .Where(r => r.BlockNumber == blockNum && r.IsCanonical)
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var log in affected)
            {
                log.IsCanonical = false;
                log.UpdateRowDates();
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
