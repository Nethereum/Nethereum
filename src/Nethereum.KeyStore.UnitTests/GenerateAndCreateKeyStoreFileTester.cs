using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.KeyStore.UnitTests
{
    public class GenerateAndCreateKeyStoreFileTester
    {
        [Fact]
        public void ShouldGenerateAccountAndCreateKeyStoreFileScrypt()
        {
            var ecKey = Nethereum.Signer.EthECKey.GenerateKey();
            var keyStoreScryptService = new KeyStoreScryptService();
            var password = "testPassword";
            var json = keyStoreScryptService.EncryptAndGenerateKeyStoreAsJson(password, ecKey.GetPrivateKeyAsBytes(), ecKey.GetPublicAddress());
            var key = keyStoreScryptService.DecryptKeyStoreFromJson(password, json);
            Assert.Equal(ecKey.GetPrivateKey(), key.ToHex(true));
        }

        [Fact]
        public void ShouldGenerateAccountAndCreateKeyStoreFilePbkdf2()
        {
            var ecKey = Nethereum.Signer.EthECKey.GenerateKey();
            var keyStorePbkdf2Service = new KeyStorePbkdf2Service();
            var password = "testPassword";
            var json = keyStorePbkdf2Service.EncryptAndGenerateKeyStoreAsJson(password, ecKey.GetPrivateKeyAsBytes(), ecKey.GetPublicAddress());
            var key = keyStorePbkdf2Service.DecryptKeyStoreFromJson(password, json);
            Assert.Equal(ecKey.GetPrivateKey(), key.ToHex(true));
        }

        [Fact]
        public void ShouldGenerateAccountAndCreateKeyStoreFileDefaultService()
        {
            var ecKey = Nethereum.Signer.EthECKey.GenerateKey();
            var keyStoreService = new KeyStoreService();
            var password = "testPassword";
            var json = keyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(password, ecKey.GetPrivateKeyAsBytes(), ecKey.GetPublicAddress());
            var key = keyStoreService.DecryptKeyStoreFromJson(password, json);
            Assert.Equal(ecKey.GetPrivateKey(), key.ToHex(true));
        }
    }
}