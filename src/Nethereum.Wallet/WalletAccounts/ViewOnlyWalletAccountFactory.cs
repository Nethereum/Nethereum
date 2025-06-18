using System.Text.Json;

namespace Nethereum.Wallet.WalletAccounts;

public class ViewOnlyWalletAccountFactory : WalletAccountFactoryBase<ViewOnlyWalletAccount>
{
    public override string Type => ViewOnlyWalletAccount.TypeName;
    public override IWalletAccount FromJson(JsonElement element)
        => ViewOnlyWalletAccount.FromJson(element);
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

