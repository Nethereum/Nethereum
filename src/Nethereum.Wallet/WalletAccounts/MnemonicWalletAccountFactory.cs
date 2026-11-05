using System.Linq;
using System;
using Nethereum.Accounts.Bip32;
using System.Collections.Generic;
using System.Text.Json;

namespace Nethereum.Wallet.WalletAccounts
{
    public class MnemonicWalletAccountFactory : WalletAccountFactoryBase<MnemonicWalletAccount>
    {
        public override string Type => MnemonicWalletAccount.TypeName;

        private readonly bool _validateMnemonic;

        public MnemonicWalletAccountFactory() : this(false)
        {
        }

        public MnemonicWalletAccountFactory(bool validateMnemonic)
        {
            _validateMnemonic = validateMnemonic;
        }

        public override IWalletAccount FromJson(JsonElement element, WalletVault vault)
        {
            var mnemonicId = element.GetProperty("mnemonicId").GetString()!;
            var mnemonic = vault.Mnemonics.FirstOrDefault(m => m.Id == mnemonicId);
            if (mnemonic == null)
            {
                throw new InvalidOperationException($"Mnemonic with ID {mnemonicId} not found.");
            }
            if (_validateMnemonic && mnemonic.Mnemonic != Bip39.CanonicalMnemonic(mnemonic.Mnemonic))
                throw new ArgumentException("Stored mnemonic is not in canonical BIP-39 form");
            var minimalHDWallet = new MinimalHDWallet(mnemonic.Mnemonic, mnemonic.Passphrase ?? string.Empty, _validateMnemonic);
            var account = MnemonicWalletAccount.FromJson(element, minimalHDWallet);
            account.RequiresStandardSeedMigration = mnemonic.RequiresStandardSeedMigration;
            return account;

        }
    }
}