using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.Model;
using Xunit;

namespace Nethereum.KeyStore.Tests.SecurityAudit71.S9
{
    public class S9KeyStoreLimitsTests
    {
        private const string Password = "correct horse";
        private const string Address = "0x008aeeda4d805471df9b2a5b0f38a0c3bcba786b";
        private static readonly byte[] PrivateKey =
            "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d".HexToByteArray();

        private static string Scrypt(int n, int r, int p)
        {
            return new KeyStoreScryptService().EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey, Address,
                new ScryptParams { Dklen = 32, N = n, R = r, P = p });
        }

        private static string Pbkdf2(int c)
        {
            return new KeyStorePbkdf2Service().EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey, Address,
                new Pbkdf2Params { Dklen = 32, Count = c, Prf = "hmac-sha256" });
        }

        [Trait("Rule", "S9-1")]
        [Fact]
        public void Given_a_standard_geth_scrypt_keystore_When_decrypting_with_default_limits_Then_it_decrypts()
        {
            Assert.Equal(PrivateKey, new KeyStoreService().DecryptKeyStoreFromJson(Password, Scrypt(262144, 8, 1)));
        }

        [Trait("Rule", "S9-1")]
        [Fact]
        public void Given_a_light_geth_scrypt_keystore_When_decrypting_with_default_limits_Then_it_decrypts()
        {
            Assert.Equal(PrivateKey, new KeyStoreService().DecryptKeyStoreFromJson(Password, Scrypt(4096, 6, 1)));
        }

        [Trait("Rule", "S9-1")]
        [Fact]
        public void Given_a_scrypt_keystore_above_a_configured_memory_limit_When_decrypting_Then_it_is_rejected_and_a_raised_limit_accepts_it()
        {
            var json = Scrypt(16384, 8, 1);
            var strict = new KeyStoreService { KdfLimits = new KeyStoreKdfLimits { MaxScryptMemoryBytes = 1024 * 1024 } };
            Assert.Throws<ArgumentException>(() => strict.DecryptKeyStoreFromJson(Password, json));
            var relaxed = new KeyStoreKdfLimits { MaxScryptMemoryBytes = 64L * 1024 * 1024 };
            Assert.Equal(PrivateKey, new KeyStoreService().DecryptKeyStoreFromJson(Password, json, relaxed));
        }

        [Trait("Rule", "S9-1")]
        [Fact]
        public void Given_scrypt_p_17_When_decrypting_Then_the_default_limit_rejects_it_and_a_raised_limit_accepts_it()
        {
            var json = Scrypt(1024, 8, 17);
            Assert.Throws<ArgumentException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
            var relaxed = new KeyStoreKdfLimits { MaxScryptP = 17 };
            Assert.Equal(PrivateKey, new KeyStoreService().DecryptKeyStoreFromJson(Password, json, relaxed));
        }

        [Trait("Rule", "S9-2")]
        [Fact]
        public void Given_pbkdf2_c_above_a_configured_limit_When_decrypting_Then_it_is_rejected_and_the_default_limit_accepts_it()
        {
            var json = Pbkdf2(2000);
            var strict = new KeyStoreKdfLimits { MaxPbkdf2Iterations = 1000 };
            Assert.Throws<ArgumentException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, json, strict));
            Assert.Equal(PrivateKey, new KeyStoreService().DecryptKeyStoreFromJson(Password, json, new KeyStoreKdfLimits()));
        }

        [Trait("Rule", "S9-2")]
        [Fact]
        public void Given_pbkdf2_c_above_ten_million_When_decrypting_Then_the_default_limit_rejects_it_immediately()
        {
            var json = Pbkdf2(1000).Replace("\"c\": 1000", "\"c\": 10000001").Replace("\"c\":1000", "\"c\":10000001");
            Assert.Contains("10000001", json);
            Assert.Throws<ArgumentException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
        }

        [Trait("Rule", "S9-13")]
        [Fact]
        public void Given_direct_scrypt_calls_with_overflowing_cost_parameters_When_deriving_Then_they_are_rejected_before_allocation()
        {
            var password = new byte[] { 1 };
            var salt = new byte[] { 1 };
            Assert.Throws<ArgumentException>(() => ScryptNet.CryptoScrypt(password, salt, 1 << 30, 8, 1, 32));
            Assert.Throws<ArgumentException>(() => ScryptNet.CryptoScrypt(password, salt, 1 << 20, 1 << 12, 1, 32));
            Assert.Throws<ArgumentException>(() => ScryptNet.CryptoScrypt(password, salt, 1024, 1 << 26, 1 << 5, 32));
            Assert.Throws<ArgumentException>(() => ScryptNet.CryptoScrypt(password, salt, 1000, 8, 1, 32));
        }
    }
}
