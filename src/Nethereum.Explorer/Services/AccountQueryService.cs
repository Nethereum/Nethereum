using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.Explorer.Services;

public class AccountQueryService : IAccountQueryService
{
    private readonly IBlockchainDbContextFactory _contextFactory;

    public AccountQueryService(IBlockchainDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<AccountSummary>> GetAccountsPagedAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        using var context = _contextFactory.CreateContext();

        var accounts = await context.AddressTransactions
            .AsNoTracking()
            .GroupBy(a => a.Address)
            .Select(g => new { Address = g.Key, Count = g.Count() })
            .OrderByDescending(a => a.Count)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var addresses = accounts.Select(a => a.Address).ToList();

        var contractAddresses = await context.Contracts
            .AsNoTracking()
            .Where(c => addresses.Contains(c.Address))
            .Select(c => c.Address)
            .ToListAsync();

        var contractSet = new HashSet<string>(contractAddresses, StringComparer.OrdinalIgnoreCase);

        return accounts.Select(a => new AccountSummary
        {
            Address = a.Address,
            TransactionCount = a.Count,
            IsContract = contractSet.Contains(a.Address)
        }).ToList();
    }

    public async Task<int> GetTotalAccountCountAsync()
    {
        using var context = _contextFactory.CreateContext();
        return await context.AddressTransactions
            .AsNoTracking()
            .Select(a => a.Address)
            .Distinct()
            .CountAsync();
    }

    public async Task<AccountStateInfo?> GetAccountStateAsync(string address)
    {
        using var context = _contextFactory.CreateContext();
        var normalizedAddress = address.ToLowerInvariant();
        var state = await context.AccountStates
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Address == normalizedAddress);

        if (state == null) return null;

        return new AccountStateInfo
        {
            Balance = state.Balance,
            Nonce = state.Nonce,
            IsContract = state.IsContract,
            LastUpdatedBlock = state.LastUpdatedBlock
        };
    }
}
