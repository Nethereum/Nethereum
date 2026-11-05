using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.Model;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.KeyStore.Tests.SecurityAudit71.S9.Round3
{
    public class S9KeyStoreRound3Tests
    {
        private const string Password = "correct horse";
        private static readonly byte[] PrivateKey =
            "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d".HexToByteArray();
        private static readonly string Address = new Nethereum.Signer.EthECKey(PrivateKey, true).GetPublicAddress();

        private static string ScryptJson(int p = 1, int n = 1024)
        {
            var json = new KeyStoreScryptService().EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey, Address,
                new ScryptParams { Dklen = 32, N = 1024, R = 8, P = 1 });
            var obj = JObject.Parse(json);
            obj["crypto"]["kdfparams"]["p"] = p;
            obj["crypto"]["kdfparams"]["n"] = n;
            return obj.ToString();
        }

        [Trait("Rule", "S9-R3-20")]
        [Fact]
        public void Given_one_shared_service_and_many_threads_with_different_per_call_limits_When_racing_Then_no_caller_sees_anothers_limits()
        {
            var service = new KeyStoreService();
            var p17 = ScryptJson(17);
            var permissive = new KeyStoreKdfLimits { MaxScryptP = 64 };
            var strict = new KeyStoreKdfLimits();
            var leaks = 0;
            var wrongRejections = 0;
            var stop = false;
            var feeders = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            {
                while (!stop)
                {
                    try { service.DecryptKeyStoreFromJson("wrong", p17, permissive); }
                    catch (DecryptionException) { }
                    catch (ArgumentException) { Interlocked.Increment(ref wrongRejections); }
                }
            })).ToArray();
            for (var i = 0; i < 3000; i++)
            {
                try { service.DecryptKeyStoreFromJson("wrong", p17, strict); Interlocked.Increment(ref leaks); }
                catch (ArgumentException) { }
                catch (DecryptionException) { Interlocked.Increment(ref leaks); }
                try { service.DecryptKeyStoreFromJson("wrong", p17); Interlocked.Increment(ref leaks); }
                catch (ArgumentException) { }
                catch (DecryptionException) { Interlocked.Increment(ref leaks); }
            }
            stop = true;
            Task.WaitAll(feeders);
            Assert.Equal(0, leaks);
            Assert.Equal(0, wrongRejections);
        }

        [Trait("Rule", "S9-R3-21")]
        [Fact]
        public void Given_null_or_permissive_limits_When_decrypting_Then_null_means_the_defaults_and_permissive_reaches_the_mac()
        {
            var service = new KeyStoreService();
            var p17 = ScryptJson(17);
            Assert.Throws<ArgumentException>(() => service.DecryptKeyStoreFromJson(Password, p17, null));
            Assert.Throws<ArgumentException>(() => service.DecryptKeyStoreFromJson(Password, p17));
            Assert.Throws<DecryptionException>(() => service.DecryptKeyStoreFromJson("wrong", p17, new KeyStoreKdfLimits { MaxScryptP = 64 }));
            var real17 = new KeyStoreScryptService().EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey, Address,
                new ScryptParams { Dklen = 32, N = 1024, R = 8, P = 17 });
            Assert.Throws<ArgumentException>(() => service.DecryptKeyStoreFromJson(Password, real17));
            Assert.Equal(PrivateKey, service.DecryptKeyStoreFromJson(Password, real17, new KeyStoreKdfLimits { MaxScryptP = 64 }));
        }

        [Trait("Rule", "S9-R3-22")]
        [Fact]
        public void Given_limits_set_to_their_maximum_When_given_wrapping_scrypt_costs_Then_the_inner_scrypt_still_refuses_them()
        {
            var limits = new KeyStoreKdfLimits { MaxScryptMemoryBytes = long.MaxValue, MaxScryptP = int.MaxValue, MaxPbkdf2Iterations = int.MaxValue };
            var service = new KeyStoreService();
            foreach (var cost in new[] { (1 << 25, 1, 1), (1 << 22, 8, 1), (1 << 30, 1, 1), (1024, 1 << 23, 1), (1024, 1, 1 << 24) })
            {
                var obj = JObject.Parse(ScryptJson());
                obj["crypto"]["kdfparams"]["n"] = cost.Item1;
                obj["crypto"]["kdfparams"]["r"] = cost.Item2;
                obj["crypto"]["kdfparams"]["p"] = cost.Item3;
                Assert.Throws<ArgumentException>(() => service.DecryptKeyStoreFromJson(Password, obj.ToString(), limits));
            }
        }

        [Trait("Rule", "S9-R3-23")]
        [Fact]
        public void Given_the_low_level_helpers_When_called_with_costs_over_the_default_cap_Then_the_default_overloads_refuse_and_the_limit_overloads_obey()
        {
            var crypto = new KeyStoreCrypto();
            Assert.Throws<ArgumentException>(() => crypto.DecryptScrypt("pw", new byte[32], new byte[16], new byte[32], 1 << 20, 1, 8, new byte[32], 32));
            Assert.Throws<ArgumentException>(() => crypto.DecryptPbkdf2Sha256("pw", new byte[32], new byte[16], new byte[32], 10_000_001, new byte[32], 32));
            Assert.Throws<ArgumentException>(() => crypto.GeneratePbkdf2Sha256DerivedKey("pw", new byte[32], 10_000_001, 32));
            Assert.Throws<ArgumentException>(() => crypto.DecryptPbkdf2Sha256("pw", new byte[32], new byte[16], new byte[32], 1000, new byte[32], 32,
                new KeyStoreKdfLimits { MaxPbkdf2Iterations = 999 }));
            Assert.Throws<ArgumentException>(() => crypto.DecryptScrypt("pw", new byte[32], new byte[16], new byte[32], 1024, 1, 8, new byte[32], 32,
                new KeyStoreKdfLimits { MaxScryptMemoryBytes = 1024 }));
            Assert.Throws<DecryptionException>(() => crypto.DecryptScrypt("pw", new byte[32], new byte[16], new byte[32], 1024, 1, 8, new byte[32], 32));
        }

        [Trait("Rule", "S9-R3-24")]
        [Fact]
        public void Given_zero_or_negative_limits_When_decrypting_Then_everything_is_rejected_rather_than_crashing()
        {
            var service = new KeyStoreService();
            foreach (var limits in new[]
            {
                new KeyStoreKdfLimits { MaxScryptMemoryBytes = 0 }, new KeyStoreKdfLimits { MaxScryptMemoryBytes = -1 },
                new KeyStoreKdfLimits { MaxScryptP = 0 }, new KeyStoreKdfLimits { MaxScryptP = -5 }
            })
                Assert.ThrowsAny<Exception>(() => service.DecryptKeyStoreFromJson(Password, ScryptJson(), limits));
            Assert.ThrowsAny<Exception>(() => service.DecryptKeyStoreFromJson(Password,
                JObject.Parse(new KeyStorePbkdf2Service().EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey, Address,
                    new Pbkdf2Params { Dklen = 32, Count = 10, Prf = "hmac-sha256" })).ToString(),
                new KeyStoreKdfLimits { MaxPbkdf2Iterations = 0 }));
        }
    }
}
