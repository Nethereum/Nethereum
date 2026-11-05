using System;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.Model;
using Xunit;

namespace Nethereum.KeyStore.Tests
{
    public class FoundationsKeyStoreDocExampleTests
    {
        private const string Password = "correct horse";
        private const string Address = "0x008aeeda4d805471df9b2a5b0f38a0c3bcba786b";
        private static readonly byte[] PrivateKey =
            "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d".HexToByteArray();

        private static string ScryptKeyStore(string address)
        {
            return new KeyStoreScryptService().EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey, address,
                new ScryptParams { Dklen = 32, N = 4096, R = 1, P = 1 });
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "keystore-kdf-limits", "Cap the cost a keystore may ask for on decryption")]
        public void Given_AKeyStoreCostingMoreThanTheConfiguredLimit_When_Decrypted_Then_ItIsRefusedUntilTheLimitIsRaised()
        {
            var json = ScryptKeyStore(Address);
            var service = new KeyStoreService
            {
                KdfLimits = new KeyStoreKdfLimits { MaxScryptMemoryBytes = 128L * 4096 - 1 }
            };

            Assert.Throws<ArgumentException>(() => service.DecryptKeyStoreFromJson(Password, json));

            var raised = new KeyStoreKdfLimits { MaxScryptMemoryBytes = 128L * 4096 };
            Assert.Equal(PrivateKey, service.DecryptKeyStoreFromJson(Password, json, raised));
            Assert.Equal(512L * 1024 * 1024, KeyStoreKdfLimits.DefaultMaxScryptMemoryBytes);
            Assert.Equal(16, KeyStoreKdfLimits.DefaultMaxScryptP);
            Assert.Equal(10000000, KeyStoreKdfLimits.DefaultMaxPbkdf2Iterations);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "keystore-address-check", "A keystore whose address does not match its key is refused")]
        public void Given_AKeyStoreWhoseAddressIsNotTheAddressOfItsKey_When_Decrypted_Then_ADecryptionExceptionIsThrown()
        {
            var json = ScryptKeyStore("0x1111111111111111111111111111111111111111");

            Assert.Throws<DecryptionException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
            Assert.Equal(PrivateKey, new KeyStoreService().DecryptKeyStoreFromJson(Password, ScryptKeyStore(Address)));
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "keystore-hostile-fields", "Hostile keystore fields fail with ArgumentException")]
        public void Given_AKeyStoreWithAnUnsupportedCipherOrNonHexField_When_Decrypted_Then_ArgumentExceptionIsThrown()
        {
            var json = ScryptKeyStore(Address);
            var wrongCipher = json.Replace("aes-128-ctr", "aes-256-cbc");
            var notHex = json.Replace("\"iv\":\"", "\"iv\":\"zz");

            Assert.Throws<ArgumentException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, wrongCipher));
            Assert.Throws<ArgumentException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, notHex));
            Assert.Throws<ArgumentException>(() => new KeyStoreKdfChecker().GetKeyStoreKdfType("not json"));
        }
    }
}
