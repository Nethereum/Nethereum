using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Nethereum.RPC.Accounts;
using Nethereum.Accounts.ViewOnly;

namespace Nethereum.Wallet.WalletAccounts;

public class ViewOnlyWalletAccount : WalletAccountBase
{
    public static readonly string TypeName = "viewonly";
    public override string Type => TypeName;

    public override string Name => Label ?? "View Only Account";
    public override object Settings => null;

    public ViewOnlyWalletAccount(string address, string label) : base(address, label) { }

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

    public static ViewOnlyWalletAccount FromJson(JsonElement json)
    {
        var address = json.GetProperty("address").GetString()!;
        var label = json.GetProperty("label").GetString()!;
        return new ViewOnlyWalletAccount(address, label);
    }
}


