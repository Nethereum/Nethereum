using Nethereum.Wallet.Bip32;
using System.Collections.Generic;
using System.Text.Json;

namespace Nethereum.Wallet.WalletAccounts
{
    public class MnemonicWalletAccountFactory : WalletAccountFactoryBase<MnemonicWalletAccount>
    {
        public override string Type => MnemonicWalletAccount.TypeName;

        private readonly IMnemonicProvider _provider;

        public MnemonicWalletAccountFactory(IMnemonicProvider provider)
        {
            _provider = provider;
        }

        public override IWalletAccount FromJson(JsonElement element)
        {
            var mnemonicId = element.GetProperty("mnemonicId").GetString()!;
            var mnemonic = _provider.GetMnemonic(mnemonicId);
            var minimalHDWallet = new MinimalHDWallet(mnemonic.Mnemonic, mnemonic.Passphrase);
            return  MnemonicWalletAccount.FromJson(element, minimalHDWallet);

        }
    }
}