using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Nethereum.RPC.Accounts;
using Nethereum.Web3;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Wallet.WalletAccounts
{
    public abstract class WalletAccountBase : IWalletAccount
    {
        public string Address { get; protected set; }
        public abstract string Type { get; }
        public string Label { get; set; }
        public abstract string Name { get; }

        public abstract object? Settings { get; }
        public bool IsSelected { get; set; } = false;
        public virtual string? GroupId => null;

        protected WalletAccountBase(string address, string label)
        {
            Address = address;
            Label = label;
        }

        public abstract Task<IAccount> GetAccountAsync();

        public virtual async Task<IWeb3> CreateWeb3Async(IClient client)
        {
            var account = await GetAccountAsync();
            return new Nethereum.Web3.Web3(account, client);
        }

        public abstract JsonObject ToJson();
    }
}