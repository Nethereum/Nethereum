using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Nethereum.RPC.Accounts;
using Nethereum.Web3.Accounts;
using Nethereum.Wallet.Bip32;
using Nethereum.Web3;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Wallet.WalletAccounts;

public class MnemonicWalletAccount : WalletAccountBase
{
    public static readonly string TypeName = "mnemonic";
    public override string Type => TypeName;
    public int Index { get; private set; }
    public string MnemonicId { get; private set; }
    private readonly MinimalHDWallet _wallet;

    public override string Name => Label ?? $"Account {Index}";
    public override object Settings => new { Index, MnemonicId };
    public override string? GroupId => MnemonicId;

    public MnemonicWalletAccount(string address, string label, int index, string mnemonicId, MinimalHDWallet wallet)
        : base(address, label)
    {
        Index = index;
        MnemonicId = mnemonicId;
        _wallet = wallet;
    }

    public override Task<IAccount> GetAccountAsync()
    {
        var key = _wallet.GetEthereumKey(Index);
        return Task.FromResult<IAccount>(new Account(key));
    }

    public override JsonObject ToJson() => new()
    {
        ["type"] = Type,
        ["address"] = Address,
        ["label"] = Label,
        ["index"] = Index,
        ["mnemonicId"] = MnemonicId,
        ["selected"] = IsSelected, 
    };

    public static MnemonicWalletAccount FromJson(JsonElement json, MinimalHDWallet minimalHDWallet)
    {
        var address = json.GetProperty("address").GetString()!;
        var label = json.GetProperty("label").GetString()!;
        var index = json.GetProperty("index").GetInt32();
        var mnemonicId = json.GetProperty("mnemonicId").GetString()!;
        return new MnemonicWalletAccount(address, label, index, mnemonicId, minimalHDWallet);
    }
}



