using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore;
using Nethereum.KeyStore.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class KeyStoreDocExampleTests
    {
        private const string Password = "testPassword123";

        private static readonly ScryptParams FastScryptParams = new ScryptParams
        {
            Dklen = 32,
            N = 8192,
            R = 8,
            P = 1
        };

        private static readonly Pbkdf2Params FastPbkdf2Params = new Pbkdf2Params
        {
            Dklen = 32,
            Count = 1024,
            Prf = "hmac-sha256"
        };

        [Fact]
        [NethereumDocExample(DocSection.Signing, "keystore", "Generate and create Scrypt keystore")]
        public void ShouldGenerateScryptKeystore()
        {
            var ecKey = EthECKey.GenerateKey();
            var privateKeyBytes = ecKey.GetPrivateKeyAsBytes();
            var address = ecKey.GetPublicAddress();

            var scryptService = new KeyStoreScryptService();
            var json = scryptService.EncryptAndGenerateKeyStoreAsJson(Password, privateKeyBytes, address, FastScryptParams);

            Assert.Contains("scrypt", json);
            Assert.Contains(address, json);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "keystore", "Custom Scrypt parameters")]
        public void ShouldCreateKeystoreWithCustomScryptParams()
        {
            var ecKey = EthECKey.GenerateKey();
            var privateKeyBytes = ecKey.GetPrivateKeyAsBytes();
            var address = ecKey.GetPublicAddress();

            var customParams = new ScryptParams { Dklen = 32, N = 4096, R = 8, P = 1 };
            var scryptService = new KeyStoreScryptService();
            var json = scryptService.EncryptAndGenerateKeyStoreAsJson(Password, privateKeyBytes, address, customParams);

            Assert.Contains("4096", json);

            var decryptedBytes = scryptService.DecryptKeyStoreFromJson(Password, json);
            Assert.Equal(privateKeyBytes, decryptedBytes);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "keystore", "PBKDF2 keystore (legacy)")]
        public void ShouldCreatePbkdf2Keystore()
        {
            var ecKey = EthECKey.GenerateKey();
            var privateKeyBytes = ecKey.GetPrivateKeyAsBytes();
            var address = ecKey.GetPublicAddress();

            var pbkdf2Service = new KeyStorePbkdf2Service();
            var json = pbkdf2Service.EncryptAndGenerateKeyStoreAsJson(Password, privateKeyBytes, address, FastPbkdf2Params);

            Assert.Contains("pbkdf2", json);

            var decryptedBytes = pbkdf2Service.DecryptKeyStoreFromJson(Password, json);
            Assert.Equal(privateKeyBytes, decryptedBytes);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "keystore", "Detect KDF type")]
        public void ShouldDetectKdfTypeInJson()
        {
            var ecKey = EthECKey.GenerateKey();
            var privateKeyBytes = ecKey.GetPrivateKeyAsBytes();
            var address = ecKey.GetPublicAddress();

            var scryptService = new KeyStoreScryptService();
            var scryptJson = scryptService.EncryptAndGenerateKeyStoreAsJson(Password, privateKeyBytes, address, FastScryptParams);
            Assert.Contains("scrypt", scryptJson);

            var pbkdf2Service = new KeyStorePbkdf2Service();
            var pbkdf2Json = pbkdf2Service.EncryptAndGenerateKeyStoreAsJson(Password, privateKeyBytes, address, FastPbkdf2Params);
            Assert.Contains("pbkdf2", pbkdf2Json);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "keystore", "Default KeyStoreService facade")]
        public void ShouldUseDefaultKeyStoreServiceWithScrypt()
        {
            var ecKey = EthECKey.GenerateKey();
            var privateKeyBytes = ecKey.GetPrivateKeyAsBytes();
            var address = ecKey.GetPublicAddress();

            var keyStoreService = new KeyStoreService();
            var json = keyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(Password, privateKeyBytes, address);

            Assert.Contains("scrypt", json);

            var decryptedBytes = keyStoreService.DecryptKeyStoreFromJson(Password, json);
            Assert.Equal(privateKeyBytes, decryptedBytes);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "keystore", "Roundtrip: generate key, encrypt, decrypt, verify")]
        public void ShouldRoundtripKeyThroughKeystore()
        {
            var ecKey = EthECKey.GenerateKey();
            var originalPrivateKey = ecKey.GetPrivateKeyAsBytes();
            var originalAddress = ecKey.GetPublicAddress();

            var scryptService = new KeyStoreScryptService();
            var json = scryptService.EncryptAndGenerateKeyStoreAsJson(Password, originalPrivateKey, originalAddress, FastScryptParams);

            var decryptedKey = scryptService.DecryptKeyStoreFromJson(Password, json);
            var recoveredEcKey = new EthECKey(decryptedKey, true);
            var recoveredAddress = recoveredEcKey.GetPublicAddress();

            Assert.Equal(originalPrivateKey, decryptedKey);
            Assert.True(originalAddress.IsTheSameAddress(recoveredAddress));
        }
    }
}
