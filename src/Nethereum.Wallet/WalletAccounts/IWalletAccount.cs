using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.Web3;
using System.Text.Json.Nodes;

public interface IWalletAccount
{
    string Address { get; }
    string Type { get; }
    string Label { get; set; }
    string Name { get; }
    object? Settings { get; }

    bool IsSelected { get; set; }
    string? GroupId { get; }

    Task<IAccount> GetAccountAsync();
    Task EnsureReadyAsync(System.Threading.CancellationToken cancellationToken = default);
    Task<IWeb3> CreateWeb3Async(IClient client);
    JsonObject ToJson();
}
