using System.Linq;
using System;
using Nethereum.Wallet.Bip32;
using System.Collections.Generic;
using System.Text.Json;

namespace Nethereum.Wallet.WalletAccounts
{
    public class MnemonicWalletAccountFactory : WalletAccountFactoryBase<MnemonicWalletAccount>
    {
        public override string Type => MnemonicWalletAccount.TypeName;

        public MnemonicWalletAccountFactory()
        {
        }

        public override IWalletAccount FromJson(JsonElement element, WalletVault vault)
        {
            var mnemonicId = element.GetProperty("mnemonicId").GetString()!;
            var mnemonic = vault.Mnemonics.FirstOrDefault(m => m.Id == mnemonicId);
            if (mnemonic == null)
            {
                throw new InvalidOperationException($"Mnemonic with ID {mnemonicId} not found.");
            }
            var minimalHDWallet = new MinimalHDWallet(mnemonic.Mnemonic, mnemonic.Passphrase ?? string.Empty);
            return  MnemonicWalletAccount.FromJson(element, minimalHDWallet);

        }
    }
}