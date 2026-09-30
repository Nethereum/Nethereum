using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainStore.EFCore.Repositories
{
    public class BlockAccessListRepository : RepositoryBase, IBlockAccessListRepository
    {
        public BlockAccessListRepository(IBlockchainDbContextFactory contextFactory) : base(contextFactory)
        {
        }

        public async Task UpsertAsync(AccountAccess account, long blockNumber, string blockHash)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var entity = await context.BlockAccessListAccounts
                    .FindByBlockNumberAndAddressAsync(blockNumber, account.Address).ConfigureAwait(false)
                    ?? new BlockAccessListAccount();

                entity.MapToStorageEntityForUpsert(account, blockNumber, blockHash);

                if (entity.IsNew())
                    context.BlockAccessListAccounts.Add(entity);
                else
                    context.BlockAccessListAccounts.Update(entity);

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task<IReadOnlyList<AccountAccess>> GetForBlockAsync(long blockNumber)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var records = await context.BlockAccessListAccounts
                    .Where(a => a.BlockNumber == blockNumber && a.IsCanonical)
                    .ToListAsync().ConfigureAwait(false);

                return records.Select(r => r.ToAccountAccess()).ToList();
            }
        }

        public async Task MarkNonCanonicalAsync(BigInteger blockNumber)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var blockNum = (long)blockNumber;

                var records = await context.BlockAccessListAccounts
                    .Where(a => a.BlockNumber == blockNum && a.IsCanonical)
                    .ToListAsync().ConfigureAwait(false);

                foreach (var record in records)
                    record.IsCanonical = false;

                if (records.Count > 0)
                {
                    context.BlockAccessListAccounts.UpdateRange(records);
                    await context.SaveChangesAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
