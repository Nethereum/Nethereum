using System.Text.Json;

namespace Nethereum.Wallet.WalletAccounts;

public class ViewOnlyWalletAccountFactory : WalletAccountFactoryBase<ViewOnlyWalletAccount>
{
    public override string Type => ViewOnlyWalletAccount.TypeName;
    public override IWalletAccount FromJson(JsonElement element, WalletVault vault)
        => ViewOnlyWalletAccount.FromJson(element);
}

