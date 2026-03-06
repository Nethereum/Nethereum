using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.Explorer.Services;

public class TransactionQueryService : ITransactionQueryService
{
    private readonly IBlockchainDbContextFactory _contextFactory;

    public TransactionQueryService(IBlockchainDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ITransactionView?> GetTransactionAsync(string txHash)
    {
        using var context = _contextFactory.CreateContext();
        return await context.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Hash == txHash && t.IsCanonical);
    }

    public async Task<List<ITransactionView>> GetLatestTransactionsAsync(int count)
    {
        using var context = _contextFactory.CreateContext();
        var txs = await context.Transactions
            .AsNoTracking()
            .Where(t => t.IsCanonical)
            .OrderByDescending(t => t.RowIndex)
            .Take(count)
            .ToListAsync();
        return txs.Cast<ITransactionView>().ToList();
    }

    public async Task<List<ITransactionView>> GetLatestTransactionsPagedAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        using var context = _contextFactory.CreateContext();
        var txs = await context.Transactions
            .AsNoTracking()
            .Where(t => t.IsCanonical)
            .OrderByDescending(t => t.RowIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return txs.Cast<ITransactionView>().ToList();
    }

    public async Task<List<ITransactionView>> GetBlockTransactionsAsync(long blockNumber)
    {
        using var context = _contextFactory.CreateContext();
        var txs = await context.Transactions
            .AsNoTracking()
            .Where(t => t.BlockNumber == blockNumber && t.IsCanonical)
            .OrderBy(t => t.TransactionIndex)
            .ToListAsync();
        return txs.Cast<ITransactionView>().ToList();
    }

    public async Task<List<ITransactionView>> GetAddressTransactionsAsync(
        string address, int page, int pageSize, string? direction = null)
    {
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        var normalizedAddress = address.ToLowerInvariant();
        using var context = _contextFactory.CreateContext();
        IQueryable<TransactionBase> query = context.Transactions
            .AsNoTracking()
            .Where(t => t.IsCanonical);

        query = DirectionFilterHelper.ApplyDirectionFilter(query, normalizedAddress, direction);

        var txs = await query
            .OrderByDescending(t => t.RowIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return txs.Cast<ITransactionView>().ToList();
    }

    public async Task<int> GetAddressTransactionCountAsync(string address, string? direction = null)
    {
        var normalizedAddress = address.ToLowerInvariant();
        using var context = _contextFactory.CreateContext();
        IQueryable<TransactionBase> query = context.Transactions
            .AsNoTracking()
            .Where(t => t.IsCanonical);

        query = DirectionFilterHelper.ApplyDirectionFilter(query, normalizedAddress, direction);

        return await query.CountAsync();
    }

    public async Task<int> GetTotalTransactionCountAsync()
    {
        using var context = _contextFactory.CreateContext();
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.IsCanonical)
            .CountAsync();
    }
}
