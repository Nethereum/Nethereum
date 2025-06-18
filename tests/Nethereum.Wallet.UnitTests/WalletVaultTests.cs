
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.Wallet.Bip32;
using Nethereum.Wallet;
using Nethereum.Wallet.WalletAccounts;

public class WalletVaultTests
{
    [Fact]
    public async Task CanCreateEncryptDecryptAndLoadVault()
    {
        // Arrange
        var password = "securepass123";
        var mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        var mnemonicId = "main";
        var hdWallet = new MinimalHDWallet(mnemonic);
        var accountIndex = 0;
        var key = hdWallet.GetEthereumKey(accountIndex);
        var address = key.GetPublicAddress();

        var vault = new WalletVault();
        vault.Mnemonics[mnemonicId] = new MnemonicInfo("01", mnemonic, null);

        vault.Accounts.Add(new MnemonicWalletAccount(address, "main account", accountIndex, mnemonicId, hdWallet));

        var privateKey = EthECKey.GenerateKey().GetPrivateKey();
        var privateAccount = new PrivateKeyWalletAccount(new Account(privateKey).Address, "cold wallet", privateKey);
        vault.Accounts.Add(privateAccount);

        // Act
        var encrypted = vault.Encrypt(password);

        var decryptedVault = new WalletVault();
        decryptedVault.Decrypt(encrypted, password);

        // Assert
        Assert.Equal(2, decryptedVault.Accounts.Count);
        var recoveredMain = (await decryptedVault.Accounts[0].GetAccountAsync()).Address;
        var recoveredCold = (await decryptedVault.Accounts[1].GetAccountAsync()).Address;
        Assert.Equal(address, recoveredMain);
        Assert.Equal(privateAccount.Address, recoveredCold);
    }
}