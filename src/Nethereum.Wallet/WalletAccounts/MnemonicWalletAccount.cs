using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Nethereum.RPC.Accounts;
using Nethereum.Web3.Accounts;
using Nethereum.Wallet.Bip32;
using Nethereum.Web3;
using Nethereum.JsonRpc.Client;
using System.Xml.Linq;

namespace Nethereum.Wallet.WalletAccounts;

public class MnemonicWalletAccount : WalletAccountBase
{
    public static readonly string TypeName = "mnemonic";
    public override string Type => TypeName;
    public int Index { get; private set; }
    public string MnemonicId { get; private set; }
    private readonly MinimalHDWallet _wallet;

    public override object Settings => new { Index, MnemonicId };

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


//public class GnosisSafeWalletAccount : WalletAccountBase
//{
//    public override string Type => "gnosis_safe";
//    private readonly IAccount _ownerAccount;

//    public override object Settings => new { SafeAddress = Address };

//    public GnosisSafeWalletAccount(string safeAddress, string label, IAccount ownerAccount)
//        : base(safeAddress, label)
//    {
//        _ownerAccount = ownerAccount;
//    }

//    public override Task<IAccount> GetAccountAsync() => Task.FromResult(_ownerAccount);

//    public override async Task<IWeb3> CreateWeb3Async(IClient client)
//    {
//        var web3 = new Nethereum.Web3.Web3(_ownerAccount, client);
//        web3.Eth.Safe

//        return web3;
//    }

//    public override JsonObject ToJson() => new()
//    {
//        ["type"] = Type,
//        ["address"] = Address,
//        ["label"] = Label
//    };
//}

