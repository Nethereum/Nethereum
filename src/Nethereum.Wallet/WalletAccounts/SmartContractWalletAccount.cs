using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Nethereum.RPC.Accounts;
using Nethereum.Accounts.ViewOnly;

namespace Nethereum.Wallet.WalletAccounts;

public class SmartContractWalletAccount : WalletAccountBase
{
    public static readonly string TypeName = "smartcontract";
    public override string Type => TypeName;

    public override string Name => Label ?? "Smart Contract Account";
    public override object Settings => null;

    public SmartContractWalletAccount(string address, string label) : base(address, label) { }

    public override Task<IAccount> GetAccountAsync()
    {
        return Task.FromResult<IAccount>(new ViewOnlyAccount(this.Address));
    }

    public override JsonObject ToJson() => new()
    {
        ["type"] = Type,
        ["address"] = Address,
        ["label"] = Label,
        ["selected"] = IsSelected,
    };

    public static SmartContractWalletAccount FromJson(JsonElement json)
    {
        var address = json.GetProperty("address").GetString()!;
        var label = json.GetProperty("label").GetString()!;
        return new SmartContractWalletAccount(address, label);
    }
}