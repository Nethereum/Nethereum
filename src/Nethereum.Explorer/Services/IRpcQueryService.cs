using System.Numerics;

namespace Nethereum.Explorer.Services;

public interface IRpcQueryService
{
    bool IsAvailable { get; }
    Task<BigInteger> GetBalanceAsync(string address);
    Task<BigInteger> GetChainIdAsync();
    Task<BigInteger> GetGasPriceAsync();
    Task<BigInteger> GetTransactionCountAsync(string address);
    Task<string> GetCodeAsync(string address);
    Task<TokenInfo?> GetTokenInfoAsync(string tokenAddress, string holderAddress);
    Task<List<AuthorizationInfo>> GetTransactionAuthorizationsAsync(string txHash);
    Task<BlobTransactionInfo?> GetBlobTransactionInfoAsync(string txHash);
    Task<NftTokenInfo?> GetNftTokenInfoAsync(string contractAddress, string tokenId);
    Task<PendingTransactionsResult> GetPendingTransactionsAsync();
}

public class PendingTransactionsResult
{
    public List<PendingTransactionInfo> Pending { get; set; } = new();
    public List<PendingTransactionInfo> Queued { get; set; } = new();
    public bool IsSupported { get; set; }
}

public class PendingTransactionInfo
{
    public string Hash { get; set; } = "";
    public string From { get; set; } = "";
    public string? To { get; set; }
    public string Value { get; set; } = "0";
    public string GasPrice { get; set; } = "0";
    public string Gas { get; set; } = "0";
    public string Nonce { get; set; } = "0";
    public string? Input { get; set; }
}
