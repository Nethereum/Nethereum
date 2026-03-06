using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.Explorer.Services;

public class ContractQueryService : IContractQueryService
{
    private readonly IBlockchainDbContextFactory _contextFactory;

    public ContractQueryService(IBlockchainDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<IContractView>> GetContractsPagedAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        using var context = _contextFactory.CreateContext();
        var contracts = await context.Contracts
            .AsNoTracking()
            .OrderByDescending(c => c.RowIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return contracts.Cast<IContractView>().ToList();
    }

    public async Task<int> GetTotalContractCountAsync()
    {
        using var context = _contextFactory.CreateContext();
        return await context.Contracts
            .AsNoTracking()
            .CountAsync();
    }
}
