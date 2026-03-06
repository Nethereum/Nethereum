using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.BlockchainStore.EFCore.Repositories
{
    public class AddressTransactionRepository : RepositoryBase, IAddressTransactionRepository
    {
        public AddressTransactionRepository(IBlockchainDbContextFactory contextFactory):base(contextFactory){}

        public async Task<IAddressTransactionView> FindAsync(string address, HexBigInteger blockNumber, string transactionHash)
        {
            using (var context = _contextFactory.CreateContext())
            {
                return await context.AddressTransactions
                    .FindByBlockNumberAndHashAndAddressAsync(blockNumber, transactionHash,
                        address).ConfigureAwait(false);
            }
        }

        private static async Task<AddressTransaction> FindOrCreate(RPC.Eth.DTOs.Transaction transaction, string address, BlockchainDbContextBase context)
        {
            return await context.AddressTransactions
                       .FindByBlockNumberAndHashAndAddressAsync(transaction.BlockNumber, transaction.TransactionHash, address).ConfigureAwait(false)  ??
                   new AddressTransaction();
        }

        public async Task UpsertAsync(TransactionReceiptVO transactionReceiptVO, string address, string error = null, string newContractAddress = null)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var tx = await FindOrCreate(transactionReceiptVO.Transaction, address, context).ConfigureAwait(false);

                tx.MapToStorageEntityForUpsert(transactionReceiptVO, address);

                if (tx.IsNew())
                    context.AddressTransactions.Add(tx);
                else
                    context.AddressTransactions.Update(tx);

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
