using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.Explorer.Services;

public class InternalTransactionQueryService : IInternalTransactionQueryService
{
    private readonly IBlockchainDbContextFactory _contextFactory;

    public InternalTransactionQueryService(IBlockchainDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<IInternalTransactionView>> GetInternalTransactionsByTxHashAsync(string txHash)
    {
        using var context = _contextFactory.CreateContext();
        var results = await context.InternalTransactions
            .AsNoTracking()
            .Where(i => i.TransactionHash == txHash && i.IsCanonical)
            .OrderBy(i => i.TraceIndex)
            .ToListAsync();
        return results.Cast<IInternalTransactionView>().ToList();
    }

    public async Task<List<IInternalTransactionView>> GetInternalTransactionsForAddressAsync(
        string address, int page, int pageSize, string? direction = null)
    {
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        var normalizedAddress = address.ToLowerInvariant();
        using var context = _contextFactory.CreateContext();
        IQueryable<InternalTransaction> query = context.InternalTransactions
            .AsNoTracking()
            .Where(i => i.IsCanonical && i.Depth > 0);

        query = DirectionFilterHelper.ApplyDirectionFilter(query, normalizedAddress, direction);

        var results = await query
            .OrderByDescending(i => i.RowIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return results.Cast<IInternalTransactionView>().ToList();
    }

    public async Task<int> GetAddressInternalTransactionCountAsync(string address, string? direction = null)
    {
        var normalizedAddress = address.ToLowerInvariant();
        using var context = _contextFactory.CreateContext();
        IQueryable<InternalTransaction> query = context.InternalTransactions
            .AsNoTracking()
            .Where(i => i.IsCanonical && i.Depth > 0);

        query = DirectionFilterHelper.ApplyDirectionFilter(query, normalizedAddress, direction);

        return await query.CountAsync();
    }
}
