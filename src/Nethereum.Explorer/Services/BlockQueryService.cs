using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainStore.EFCore;
namespace Nethereum.Explorer.Services;

public class BlockQueryService : IBlockQueryService
{
    private readonly IBlockchainDbContextFactory _contextFactory;

    public BlockQueryService(IBlockchainDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IBlockView?> GetBlockAsync(long blockNumber)
    {
        using var context = _contextFactory.CreateContext();
        return await context.Blocks
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BlockNumber == blockNumber && b.IsCanonical);
    }

    public async Task<IBlockView?> GetLatestBlockAsync()
    {
        using var context = _contextFactory.CreateContext();
        return await context.Blocks
            .AsNoTracking()
            .Where(b => b.IsCanonical)
            .OrderByDescending(b => b.RowIndex)
            .FirstOrDefaultAsync();
    }

    public async Task<List<IBlockView>> GetLatestBlocksAsync(int count)
    {
        count = ExplorerConstants.ClampPageSize(count);
        using var context = _contextFactory.CreateContext();
        var blocks = await context.Blocks
            .AsNoTracking()
            .Where(b => b.IsCanonical)
            .OrderByDescending(b => b.RowIndex)
            .Take(count)
            .ToListAsync();
        return blocks.Cast<IBlockView>().ToList();
    }

    public async Task<List<IBlockView>> GetBlocksPagedAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        using var context = _contextFactory.CreateContext();
        var blocks = await context.Blocks
            .AsNoTracking()
            .Where(b => b.IsCanonical)
            .OrderByDescending(b => b.RowIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return blocks.Cast<IBlockView>().ToList();
    }

    public async Task<IBlockView?> GetBlockByHashAsync(string hash)
    {
        using var context = _contextFactory.CreateContext();
        return await context.Blocks
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Hash == hash && b.IsCanonical);
    }

    public async Task<int> GetTotalBlockCountAsync()
    {
        using var context = _contextFactory.CreateContext();
        return await context.Blocks
            .AsNoTracking()
            .Where(b => b.IsCanonical)
            .CountAsync();
    }
}
