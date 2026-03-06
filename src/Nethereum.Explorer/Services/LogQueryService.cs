using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.Explorer.Services;

public class LogQueryService : ILogQueryService
{
    private readonly IBlockchainDbContextFactory _contextFactory;

    public LogQueryService(IBlockchainDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<ITransactionLogView>> GetTransactionLogsAsync(string txHash)
    {
        using var context = _contextFactory.CreateContext();
        var logs = await context.TransactionLogs
            .AsNoTracking()
            .Where(l => l.TransactionHash == txHash && l.IsCanonical)
            .OrderBy(l => l.LogIndex)
            .ToListAsync();
        return logs.Cast<ITransactionLogView>().ToList();
    }

    public async Task<List<ITransactionLogView>> GetContractLogsAsync(string contractAddress, int page, int pageSize)
    {
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        var normalizedAddress = contractAddress.ToLowerInvariant();
        using var context = _contextFactory.CreateContext();
        var logs = await context.TransactionLogs
            .AsNoTracking()
            .Where(l => l.Address == normalizedAddress && l.IsCanonical)
            .OrderByDescending(l => l.RowIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return logs.Cast<ITransactionLogView>().ToList();
    }

    public async Task<List<string>> GetTokenAddressesForAccountAsync(string accountAddress)
    {
        using var context = _contextFactory.CreateContext();
        var paddedAddress = "0x" + accountAddress.Replace("0x", "").PadLeft(64, '0').ToLowerInvariant();

        var tokenAddresses = await context.TransactionLogs
            .AsNoTracking()
            .Where(l => l.IsCanonical
                && l.EventHash == ExplorerFormatUtils.ERC20_TRANSFER_TOPIC
                && (l.IndexVal1 == paddedAddress || l.IndexVal2 == paddedAddress))
            .Select(l => l.Address)
            .Distinct()
            .Take(50)
            .ToListAsync();

        return tokenAddresses.Where(a => !string.IsNullOrEmpty(a)).ToList()!;
    }

    public async Task<List<NftTransferRecord>> GetNftTransfersForAccountAsync(string accountAddress)
    {
        using var context = _contextFactory.CreateContext();
        var paddedAddress = "0x" + accountAddress.Replace("0x", "").PadLeft(64, '0').ToLowerInvariant();

        var logs = await context.TransactionLogs
            .AsNoTracking()
            .Where(l => l.IsCanonical
                && l.EventHash == ExplorerFormatUtils.ERC20_TRANSFER_TOPIC
                && !string.IsNullOrEmpty(l.IndexVal3)
                && (l.IndexVal1 == paddedAddress || l.IndexVal2 == paddedAddress))
            .OrderByDescending(l => l.RowIndex)
            .Take(200)
            .ToListAsync();

        return logs.Select(l => new NftTransferRecord
        {
            ContractAddress = l.Address ?? "",
            TokenId = ExplorerFormatUtils.HexToDecimalString(l.IndexVal3 ?? "0"),
            From = ExplorerFormatUtils.ExtractAddressFromTopic(l.IndexVal1 ?? ""),
            To = ExplorerFormatUtils.ExtractAddressFromTopic(l.IndexVal2 ?? "")
        }).ToList();
    }
}
