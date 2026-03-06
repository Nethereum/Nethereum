using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using System.Numerics;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Nethereum.BlockchainStore.EFCore.Repositories
{
    public class TransactionRepository : RepositoryBase, ITransactionRepository, INonCanonicalTransactionRepository
    {
        public TransactionRepository(IBlockchainDbContextFactory contextFactory) : base(contextFactory){}

        private static async Task<BlockchainProcessing.BlockStorage.Entities.Transaction> FindOrCreate(Nethereum.RPC.Eth.DTOs.Transaction transaction, BlockchainDbContextBase context)
        {
            return await context.Transactions
                         .FindByBlockNumberAndHashAsync(transaction.BlockNumber, transaction.TransactionHash).ConfigureAwait(false)  ??
                     new BlockchainProcessing.BlockStorage.Entities.Transaction();
        }

        public async Task<ITransactionView> FindByBlockNumberAndHashAsync(HexBigInteger blockNumber, string hash)
        {
            using (var context = _contextFactory.CreateContext())
            {
                return await context.Transactions.FindByBlockNumberAndHashAsync(blockNumber, hash).ConfigureAwait(false);
            }
        }

        public async Task UpsertAsync(TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var tx = await FindOrCreate(transactionReceiptVO.Transaction, context).ConfigureAwait(false);

                tx.MapToStorageEntityForUpsert(transactionReceiptVO, code, failedCreatingContract);

                if (tx.IsNew())
                    context.Transactions.Add(tx);
                else
                    context.Transactions.Update(tx);

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task UpsertAsync(TransactionReceiptVO transactionReceiptVO)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var tx = await FindOrCreate(transactionReceiptVO.Transaction, context).ConfigureAwait(false);

                tx.MapToStorageEntityForUpsert(transactionReceiptVO);
                tx.IsCanonical = true;


                if (tx.IsNew())
                    context.Transactions.Add(tx);
                else
                    context.Transactions.Update(tx);

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task MarkNonCanonicalAsync(BigInteger blockNumber)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var blockNum = (long)blockNumber;
                var transactions = await context.Transactions
                    .Where(t => t.BlockNumber == blockNum && t.IsCanonical)
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (transactions.Count == 0)
                {
                    return;
                }

                foreach (var transaction in transactions)
                {
                    transaction.IsCanonical = false;
                }

                context.Transactions.UpdateRange(transactions);
                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
