using System.Text.Json.Nodes;
using System.Text.Json;

namespace Nethereum.Wallet.WalletAccounts
{
    public interface IWalletAccountJsonFactory
    {
        string Type { get; }
        IWalletAccount FromJson(JsonElement element, WalletVault vault);
        JsonObject ToJson(IWalletAccount account);
        bool CanHandle(IWalletAccount account);
    }
}