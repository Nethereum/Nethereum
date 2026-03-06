namespace Nethereum.Explorer.Services;

public interface IAccountQueryService
{
    Task<List<AccountSummary>> GetAccountsPagedAsync(int page, int pageSize);
    Task<int> GetTotalAccountCountAsync();
    Task<AccountStateInfo?> GetAccountStateAsync(string address);
}

public class AccountStateInfo
{
    public string? Balance { get; set; }
    public long Nonce { get; set; }
    public bool IsContract { get; set; }
    public long LastUpdatedBlock { get; set; }
}
