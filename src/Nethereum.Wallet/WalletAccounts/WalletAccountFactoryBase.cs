using System.Text.Json.Nodes;
using System.Text.Json;
using System;

namespace Nethereum.Wallet.WalletAccounts
{
    public abstract class WalletAccountFactoryBase<TWalletAccount>: IWalletAccountJsonFactory
         where TWalletAccount : WalletAccountBase
    {
        public abstract string Type { get; }
        public abstract IWalletAccount FromJson(JsonElement element, WalletVault vault);
        public virtual bool CanHandle(IWalletAccount account)
            => account is TWalletAccount;

        public JsonObject ToJson(IWalletAccount account)
        {
            if (account is not TWalletAccount typedAccount)
                throw new InvalidOperationException($"Cannot convert account of type {account.GetType()} to {typeof(TWalletAccount)}");
            return typedAccount.ToJson();
        }

        public JsonObject ToJson(TWalletAccount account)
        {
            return account.ToJson();
        }
    }
}