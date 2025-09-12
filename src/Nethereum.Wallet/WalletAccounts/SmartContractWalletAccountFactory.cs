#nullable enable

using System.Text.Json;
using System.Threading.Tasks;

namespace Nethereum.Wallet.WalletAccounts
{
    public class SmartContractWalletAccountFactory : WalletAccountFactoryBase<SmartContractWalletAccount>
    {
        public override string Type => SmartContractWalletAccount.TypeName;

        public override SmartContractWalletAccount FromJson(JsonElement element, WalletVault vault)
        {
            return SmartContractWalletAccount.FromJson(element);
        }
    }
}
