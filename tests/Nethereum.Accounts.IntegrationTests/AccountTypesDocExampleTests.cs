using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.Accounts.ViewOnly;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    public class AccountTypesDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "account-types", "Create a ViewOnlyAccount for read-only queries")]
        public void ShouldCreateViewOnlyAccount()
        {
            var address = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
            var viewOnly = new ViewOnlyAccount(address);

            Assert.Equal(address, viewOnly.Address);
            Assert.NotNull(viewOnly.TransactionManager);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "account-types", "Create Account from private key with chain ID")]
        public void ShouldCreateAccountWithChainId()
        {
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var account = new Account(privateKey, Chain.MainNet);

            Assert.Equal("0x12890D2cce102216644c59daE5baed380d84830c", account.Address);
            Assert.Equal(new BigInteger(1), account.ChainId);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "account-types", "Create Account for different chains")]
        public void ShouldCreateAccountForDifferentChains()
        {
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

            var mainnet = new Account(privateKey, Chain.MainNet);
            var sepolia = new Account(privateKey, 11155111);
            var polygon = new Account(privateKey, 137);

            Assert.Equal(mainnet.Address, sepolia.Address);
            Assert.Equal(mainnet.Address, polygon.Address);
            Assert.NotEqual(mainnet.ChainId, sepolia.ChainId);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "keystore", "Encrypt private key to keystore JSON and decrypt it back")]
        public void ShouldEncryptAndDecryptKeystore()
        {
            var ecKey = EthECKey.GenerateKey();
            var privateKeyBytes = ecKey.GetPrivateKeyAsBytes();
            var address = ecKey.GetPublicAddress();
            var password = "test-password-123";

            var keyStoreService = new KeyStoreService();
            var keystoreJson = keyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(
                password, privateKeyBytes, address);

            Assert.Contains("crypto", keystoreJson);
            Assert.Contains(address.ToLower().RemoveHexPrefix(), keystoreJson.ToLower());

            var decryptedKey = keyStoreService.DecryptKeyStoreFromJson(password, keystoreJson);

            Assert.Equal(privateKeyBytes, decryptedKey);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "keystore", "Load Account from keystore JSON")]
        public void ShouldLoadAccountFromKeystore()
        {
            var ecKey = EthECKey.GenerateKey();
            var privateKeyBytes = ecKey.GetPrivateKeyAsBytes();
            var address = ecKey.GetPublicAddress();
            var password = "my-password";

            var keyStoreService = new KeyStoreService();
            var json = keyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(
                password, privateKeyBytes, address);

            var account = Account.LoadFromKeyStore(json, password);

            Assert.Equal(address, account.Address);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "keystore", "Generate UTC filename for keystore file")]
        public void ShouldGenerateKeystoreFilename()
        {
            var address = "0x12890D2cce102216644c59daE5baed380d84830c";
            var keyStoreService = new KeyStoreService();

            var filename = keyStoreService.GenerateUTCFileName(address);

            Assert.Contains("UTC", filename);
            Assert.Contains(address.ToLower().RemoveHexPrefix(), filename.ToLower());
        }
    }
}
