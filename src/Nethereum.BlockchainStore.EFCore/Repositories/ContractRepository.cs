using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Nethereum.BlockchainStore.EFCore.Repositories
{
    public class ContractRepository : RepositoryBase, IContractRepository
    {
        private readonly ConcurrentDictionary<string, Contract> _cachedContracts = new ConcurrentDictionary<string, Contract>();

        public ContractRepository(IBlockchainDbContextFactory contextFactory) : base(contextFactory)
        {
        }

        public bool IsCached(string contractAddress)
        {
            return _cachedContracts.TryGetValue(contractAddress, out Contract val);
        }

        public async Task FillCacheAsync()
        {
            _cachedContracts.Clear();
            using (var context = _contextFactory.CreateContext())
            {
                var contracts = await context.Contracts.ToListAsync();
                foreach (var contract in contracts)
                {
                    _cachedContracts.AddOrUpdate(contract.Address, contract, (s, contract1) => contract);
                }
            }
        }

        public async Task<bool> ExistsAsync(string contractAddress)
        {
            if(IsCached(contractAddress))
                return true;

            using (var context = _contextFactory.CreateContext())
            {
                var contract = await context.Contracts.FindByContractAddressAsync(contractAddress).ConfigureAwait(false) ;
                return contract != null;
            }
        }

        public async Task<IContractView> FindByAddressAsync(string contractAddress)
        {
            using (var ctx = _contextFactory.CreateContext())
            {
                return await ctx.Contracts.FindByContractAddressAsync(contractAddress)
                    .ConfigureAwait(false);
            }
        }

        public async Task UpsertAsync(ContractCreationVO contractCreation)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var contract = await context.Contracts.FindByContractAddressAsync(contractCreation.ContractAddress).ConfigureAwait(false) ?? new Contract();

                contract.MapToStorageEntityForUpsert(contractCreation);

                if (contract.IsNew())
                    context.Contracts.Add(contract);
                else
                    context.Contracts.Update(contract);

                await context.SaveChangesAsync().ConfigureAwait(false);

                _cachedContracts.AddOrUpdate(contract.Address, contract,
                    (s, existingContract) => contract);
            }
        }
    }
}
