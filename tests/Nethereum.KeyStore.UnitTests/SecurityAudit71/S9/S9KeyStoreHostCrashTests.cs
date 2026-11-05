using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore.Model;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.KeyStore.Tests.SecurityAudit71.S9
{
    public class S9KeyStoreHostCrashTests
    {
        private static string ScryptJson()
        {
            return new KeyStoreScryptService().EncryptAndGenerateKeyStoreAsJson("pw",
                "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d".HexToByteArray(),
                "0x008aeeda4d805471df9b2a5b0f38a0c3bcba786b",
                new ScryptParams { Dklen = 32, N = 1024, R = 8, P = 1 });
        }

        [Trait("Rule", "S9-13")]
        [Trait("Crash", "kills the test host with a native fault until fixed")]
        [Fact]
        public void Given_scrypt_keystore_with_n_2_pow_25_and_r_1_When_decrypting_Then_128_times_r_times_n_does_not_wrap_into_an_undersized_buffer()
        {
            var json = JObject.Parse(ScryptJson());
            json["crypto"]["kdfparams"]["n"] = 1 << 25;
            json["crypto"]["kdfparams"]["r"] = 1;
            json["crypto"]["kdfparams"]["p"] = 1;
            Assert.ThrowsAny<System.Exception>(() => new KeyStoreService().DecryptKeyStoreFromJson("pw", json.ToString()));
        }

        [Trait("Rule", "S9-13")]
        [Trait("Crash", "kills the test host with a native fault until fixed")]
        [Fact]
        public void Given_scrypt_keystore_with_n_2_pow_22_and_r_8_When_decrypting_Then_128_times_r_times_n_does_not_wrap_into_an_undersized_buffer()
        {
            var json = JObject.Parse(ScryptJson());
            json["crypto"]["kdfparams"]["n"] = 1 << 22;
            json["crypto"]["kdfparams"]["r"] = 8;
            json["crypto"]["kdfparams"]["p"] = 1;
            Assert.ThrowsAny<System.Exception>(() => new KeyStoreService().DecryptKeyStoreFromJson("pw", json.ToString()));
        }

        [Trait("Rule", "S9-13")]
        [Trait("Crash", "kills the test host with a native fault until fixed")]
        [Fact]
        public void Given_scrypt_keystore_with_n_1_When_decrypting_Then_it_is_rejected_instead_of_corrupting_memory()
        {
            var json = JObject.Parse(ScryptJson());
            json["crypto"]["kdfparams"]["n"] = 1;
            Assert.ThrowsAny<System.Exception>(() => new KeyStoreService().DecryptKeyStoreFromJson("pw", json.ToString()));
        }

        [Trait("Rule", "S9-13")]
        [Trait("Crash", "kills the test host with a native fault until fixed")]
        [Fact]
        public void Given_scrypt_keystore_with_n_3_When_decrypting_Then_it_is_rejected_instead_of_corrupting_memory()
        {
            var json = JObject.Parse(ScryptJson());
            json["crypto"]["kdfparams"]["n"] = 3;
            Assert.ThrowsAny<System.Exception>(() => new KeyStoreService().DecryptKeyStoreFromJson("pw", json.ToString()));
        }

        [Trait("Rule", "S9-13")]
        [Trait("Crash", "kills the test host with a native fault until fixed")]
        [Fact]
        public void Given_scrypt_keystore_with_n_5_When_decrypting_Then_it_is_rejected_instead_of_corrupting_memory()
        {
            var json = JObject.Parse(ScryptJson());
            json["crypto"]["kdfparams"]["n"] = 5;
            Assert.ThrowsAny<System.Exception>(() => new KeyStoreService().DecryptKeyStoreFromJson("pw", json.ToString()));
        }
    }
}