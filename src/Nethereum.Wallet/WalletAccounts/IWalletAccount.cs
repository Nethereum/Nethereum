using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;

public interface IWalletAccount
{
    string Address { get; }
    string Type { get; }
    string Label { get; set; }
    object Settings { get; }

    bool IsSelected { get; set; }

    Task<IAccount> GetAccountAsync();
}