using Nethereum.Web3;

namespace Nethereum.Explorer.Services;

public class SearchResolution
{
    public string Type { get; set; } = "";
    public string Url { get; set; } = "";
    public string DisplayQuery { get; set; } = "";
}

public interface ISearchResolverService
{
    Task<SearchResolution> ResolveAsync(string query);
}

public class SearchResolverService : ISearchResolverService
{
    private readonly ITransactionQueryService _transactionQuery;
    private readonly IBlockQueryService _blockQuery;
    private readonly IWeb3 _web3;
    private readonly ILogger<SearchResolverService> _logger;

    public SearchResolverService(
        ITransactionQueryService transactionQuery,
        IBlockQueryService blockQuery,
        IWeb3 web3,
        ILogger<SearchResolverService> logger)
    {
        _transactionQuery = transactionQuery;
        _blockQuery = blockQuery;
        _web3 = web3;
        _logger = logger;
    }

    public async Task<SearchResolution> ResolveAsync(string query)
    {
        query = query.Trim();

        if (long.TryParse(query, out _))
        {
            return new SearchResolution { Type = "Block", Url = $"/block/{query}", DisplayQuery = query };
        }

        if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && query.Length == 42 && IsValidHex(query))
        {
            return new SearchResolution { Type = "Address", Url = $"/account/{query}", DisplayQuery = query };
        }

        if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && query.Length == 66 && IsValidHex(query))
        {
            var tx = await _transactionQuery.GetTransactionAsync(query);
            if (tx != null)
            {
                return new SearchResolution { Type = "Tx", Url = $"/transaction/{query}", DisplayQuery = query };
            }

            var block = await _blockQuery.GetBlockByHashAsync(query);
            if (block != null)
            {
                return new SearchResolution { Type = "Block", Url = $"/block/{query}", DisplayQuery = query };
            }

            return new SearchResolution { Type = "Tx", Url = $"/transaction/{query}", DisplayQuery = query };
        }

        if (query.Contains('.'))
        {
            var resolved = await TryResolveEnsAsync(query);
            if (resolved != null)
                return resolved;
        }

        return new SearchResolution { Type = "Unknown", Url = "", DisplayQuery = query };
    }

    private static bool IsValidHex(string value)
    {
        for (int i = 2; i < value.Length; i++)
        {
            var c = value[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }
        return true;
    }

    private async Task<SearchResolution?> TryResolveEnsAsync(string name)
    {
        try
        {
            var ensService = _web3.Eth.GetEnsService();
            var address = await ensService.ResolveAddressAsync(name);
            if (!string.IsNullOrEmpty(address) &&
                !address.Equals("0x0000000000000000000000000000000000000000", StringComparison.OrdinalIgnoreCase))
            {
                return new SearchResolution { Type = "ENS", Url = $"/account/{address}", DisplayQuery = $"{name} → {address[..10]}..." };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ENS resolution failed for {Name}", name);
        }

        return null;
    }
}
