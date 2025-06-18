using System.Text.Json;

namespace Nethereum.Wallet.WalletAccounts
{
    public class PrivateKeyWalletAccountFactory : WalletAccountFactoryBase<PrivateKeyWalletAccount>
    {
        public override string Type => PrivateKeyWalletAccount.TypeName;
        public override IWalletAccount FromJson(JsonElement element)
            => PrivateKeyWalletAccount.FromJson(element);
    }
}