using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Nethereum.Signer;
using Nethereum.RPC.Accounts;
using Nethereum.Web3.Accounts;

namespace Nethereum.Wallet.WalletAccounts
{
    public class PrivateKeyWalletAccount : WalletAccountBase
    {
        public static readonly string TypeName = "privateKey";
        public override string Type => TypeName;
        public string PrivateKey { get; private set; }
        private readonly EthECKey _key;

        public override object Settings => new { PrivateKey };

        public PrivateKeyWalletAccount(string address, string label, string privateKey)
            : base(address, label)
        {
            PrivateKey = privateKey;
            _key = new EthECKey(privateKey);
        }

        public override Task<IAccount> GetAccountAsync()
            => Task.FromResult<IAccount>(new Account(_key));

        public override JsonObject ToJson() => new()
        {
            ["type"] = Type,
            ["address"] = Address,
            ["label"] = Label,
            ["privateKey"] = PrivateKey,
            ["selected"] = IsSelected,
        };

        public static PrivateKeyWalletAccount FromJson(JsonElement json)
        {
            var address = json.GetProperty("address").GetString()!;
            var label = json.GetProperty("label").GetString()!;
            var privateKey = json.GetProperty("privateKey").GetString()!;
            return new PrivateKeyWalletAccount(address, label, privateKey);
        }
    }
}