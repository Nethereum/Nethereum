using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.Model;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.KeyStore.Tests.SecurityAudit71.S9.Round2
{
    public class S9KeyStoreRound2Tests
    {
        private const string Password = "correct horse";
        private static readonly byte[] PrivateKey =
            "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d".HexToByteArray();
        private static readonly string RealAddress = new Nethereum.Signer.EthECKey(PrivateKey, true).GetPublicAddress();

        private static string ScryptJson(string address = null)
        {
            return new KeyStoreScryptService().EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey,
                address ?? RealAddress, new ScryptParams { Dklen = 32, N = 1024, R = 8, P = 1 });
        }

        private static string Pbkdf2Json()
        {
            return new KeyStorePbkdf2Service().EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey, RealAddress,
                new Pbkdf2Params { Dklen = 32, Count = 1000, Prf = "hmac-sha256" });
        }

        private static string Mutate(string json, Action<JObject> mutation)
        {
            var obj = JObject.Parse(json);
            mutation(obj);
            return obj.ToString();
        }

        private static string Flip(string hex)
        {
            var bytes = hex.HexToByteArray();
            bytes[0] ^= 0x01;
            return bytes.ToHex();
        }

        [Trait("Rule", "S9-R2-1")]
        [Fact]
        public void Given_scrypt_parameters_at_integer_edges_When_decrypting_Then_each_is_rejected_with_ArgumentException_before_allocating()
        {
            var cases = new (string, long, long, long)[]
            {
                ("n=2^30 r=2^29 p=1", 1L << 30, 1L << 29, 1),
                ("n=2^30 r=2^30-1 p=1", 1L << 30, (1L << 30) - 1, 1),
                ("n=2^30 r=1 p=1", 1L << 30, 1, 1),
                ("n=2^22 r=2 p=1", 1L << 22, 2, 1),
                ("n=2^20 r=8 p=1", 1L << 20, 8, 1),
                ("n=1024 r=2^22+1 p=1", 1024, (1L << 22) + 1, 1),
                ("n=1024 r=2^29 p=2", 1024, 1L << 29, 2),
                ("n=1024 r=int.Max p=1", 1024, int.MaxValue, 1),
                ("n=1024 r=1 p=int.Max", 1024, 1, int.MaxValue),
                ("n=1024 r=1 p=17", 1024, 1, 17),
                ("n=1024 r=8 p=2^31", 1024, 8, 1L << 31),
                ("n=2^31", 1L << 31, 8, 1),
                ("n=2^32+1024", (1L << 32) + 1024, 8, 1),
                ("r=2^32+8", 1024, (1L << 32) + 8, 1)
            };
            var offenders = new List<string>();
            var before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var c in cases)
            {
                var json = Mutate(ScryptJson(), o =>
                {
                    o["crypto"]["kdfparams"]["n"] = c.Item2;
                    o["crypto"]["kdfparams"]["r"] = c.Item3;
                    o["crypto"]["kdfparams"]["p"] = c.Item4;
                });
                try
                {
                    new KeyStoreService().DecryptKeyStoreFromJson(Password, json);
                    offenders.Add(c.Item1 + " accepted");
                }
                catch (ArgumentException) { }
                catch (Exception ex) { offenders.Add(c.Item1 + " => " + ex.GetType().Name); }
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(offenders.Count == 0 && allocated < 32L * 1024 * 1024,
                string.Join("; ", offenders) + " allocated " + allocated);
        }

        [Trait("Rule", "S9-R2-1")]
        [Fact]
        public void Given_pbkdf2_counts_at_integer_edges_When_decrypting_Then_each_is_rejected_with_ArgumentException_immediately()
        {
            var offenders = new List<string>();
            var before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var count in new long[] { 10_000_001, int.MaxValue, 1L << 31, 1L << 32, long.MaxValue, 0, -1, int.MinValue })
            {
                var json = Mutate(Pbkdf2Json(), o => o["crypto"]["kdfparams"]["c"] = count);
                try
                {
                    new KeyStoreService().DecryptKeyStoreFromJson(Password, json);
                    offenders.Add(count + " accepted");
                }
                catch (ArgumentException) { }
                catch (Exception ex) { offenders.Add(count + " => " + ex.GetType().Name); }
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(offenders.Count == 0 && allocated < 4L * 1024 * 1024, string.Join("; ", offenders) + " allocated " + allocated);
        }

        [Trait("Rule", "S9-R2-2")]
        [Fact]
        public void Given_values_exactly_at_the_caps_When_decrypting_with_a_wrong_password_Then_they_pass_validation_and_fail_only_on_the_mac()
        {
            var scryptP16 = Mutate(ScryptJson(), o => { o["crypto"]["kdfparams"]["p"] = 16; o["crypto"]["kdfparams"]["n"] = 1024; });
            Assert.Throws<DecryptionException>(() => new KeyStoreService().DecryptKeyStoreFromJson("wrong", scryptP16));
            var scrypt512MiB = Mutate(ScryptJson(), o => { o["crypto"]["kdfparams"]["n"] = 1 << 22; o["crypto"]["kdfparams"]["r"] = 1; o["crypto"]["kdfparams"]["p"] = 1; });
            Assert.Throws<DecryptionException>(() => new KeyStoreService().DecryptKeyStoreFromJson("wrong", scrypt512MiB));
        }

        [Trait("Rule", "S9-R2-3")]
        [Fact]
        public void Given_permissive_limits_passed_to_one_call_When_a_later_call_uses_the_defaults_Then_the_defaults_apply_again()
        {
            var service = new KeyStoreService();
            var permissive = new KeyStoreKdfLimits { MaxScryptP = 64 };
            Assert.Equal(PrivateKey, service.DecryptKeyStoreFromJson(Password, ScryptJson(), permissive));
            var p17 = Mutate(ScryptJson(), o => { o["crypto"]["kdfparams"]["p"] = 17; o["crypto"]["kdfparams"]["n"] = 1024; });
            Assert.Throws<ArgumentException>(() => service.DecryptKeyStoreFromJson(Password, p17));
        }

        [Trait("Rule", "S9-R2-3")]
        [Fact]
        public void Given_two_threads_decrypting_with_different_limits_on_one_service_When_racing_Then_the_strict_caller_is_never_given_the_permissive_limit()
        {
            var service = new KeyStoreService();
            var p17 = Mutate(ScryptJson(), o => { o["crypto"]["kdfparams"]["p"] = 17; o["crypto"]["kdfparams"]["n"] = 1024; });
            var permissive = new KeyStoreKdfLimits { MaxScryptP = 64 };
            var leaks = 0;
            var stop = false;
            var feeder = System.Threading.Tasks.Task.Run(() =>
            {
                while (!stop)
                {
                    try { service.DecryptKeyStoreFromJson("wrong", p17, permissive); } catch (Exception) { }
                }
            });
            var strict = new KeyStoreKdfLimits();
            for (var i = 0; i < 3000; i++)
            {
                try
                {
                    service.DecryptKeyStoreFromJson("wrong", p17, strict);
                    leaks++;
                }
                catch (ArgumentException) { }
                catch (DecryptionException) { leaks++; }
            }
            stop = true;
            feeder.Wait();
            Assert.Equal(0, leaks);
        }

        [Trait("Rule", "S9-R2-4")]
        [Fact]
        public void Given_scryptnet_called_directly_with_wrapping_costs_When_deriving_Then_each_is_rejected_with_ArgumentException()
        {
            var offenders = new List<string>();
            var cases = new (string, int, int, int)[]
            {
                ("n=2^25 r=1", 1 << 25, 1, 1), ("n=2^22 r=8", 1 << 22, 8, 1), ("n=2^24 r=1", 1 << 24, 1, 1),
                ("n=2^30 r=1", 1 << 30, 1, 1), ("n=1 r=8", 1, 8, 1), ("n=3 r=8", 3, 8, 1), ("n=0", 0, 8, 1),
                ("p=2^24 r=1", 1024, 1, 1 << 24), ("r=2^23 p=1", 1024, 1 << 23, 1), ("r=int.Max p=2", 1024, int.MaxValue, 2),
                ("n=-1", -1, 8, 1), ("r=0", 1024, 0, 1), ("p=0", 1024, 8, 0)
            };
            foreach (var c in cases)
            {
                try
                {
                    ScryptNet.CryptoScrypt(new byte[] { 1 }, new byte[32], c.Item2, c.Item3, c.Item4, 32);
                    offenders.Add(c.Item1 + " accepted");
                }
                catch (ArgumentException) { }
                catch (Exception ex) { offenders.Add(c.Item1 + " => " + ex.GetType().Name); }
            }
            Assert.True(offenders.Count == 0, string.Join("; ", offenders));
        }

        [Trait("Rule", "S9-R2-5")]
        [Fact]
        public void Given_the_public_low_level_crypto_api_When_called_with_an_unbounded_pbkdf2_count_Then_it_is_rejected()
        {
            var crypto = new KeyStoreCrypto();
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.Throws<ArgumentException>(() => crypto.GeneratePbkdf2Sha256DerivedKey("pw", new byte[32], 10_000_001, 32));
            Assert.Throws<ArgumentException>(() => crypto.GeneratePbkdf2Sha256DerivedKey("pw", new byte[32], int.MaxValue, 32));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 1024, "the cap was not enforced before deriving");
        }

        [Trait("Rule", "S9-R2-5")]
        [Fact]
        public void Given_the_public_decrypt_helpers_When_called_with_unbounded_costs_Then_they_do_not_bypass_the_cap()
        {
            var crypto = new KeyStoreCrypto();
            var before = GC.GetAllocatedBytesForCurrentThread();
            try { crypto.DecryptScrypt("pw", new byte[32], new byte[16], new byte[32], 1 << 20, 1, 8, new byte[32], 32); } catch (Exception) { }
            try { crypto.DecryptPbkdf2Sha256("pw", new byte[32], new byte[16], new byte[32], 10_000_001, new byte[32], 32); } catch (Exception) { }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < 4L * 1024 * 1024, "allocated " + allocated);
        }

        [Trait("Rule", "S9-R2-6")]
        [Fact]
        public void Given_hostile_costs_on_the_encrypt_side_and_object_decrypt_side_When_called_Then_they_fail_with_ArgumentException()
        {
            var service = new KeyStoreScryptService();
            Assert.ThrowsAny<ArgumentException>(() => service.EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey, RealAddress,
                new ScryptParams { Dklen = 32, N = 1 << 25, R = 1, P = 1 }));
            var store = service.DeserializeKeyStoreFromJson(ScryptJson());
            store.Crypto.Kdfparams.N = 1 << 20;
            store.Crypto.Kdfparams.R = 8;
            Assert.ThrowsAny<ArgumentException>(() => service.DecryptKeyStore(Password, store));
            Assert.ThrowsAny<ArgumentException>(() => service.DecryptCryptoStoreFromJson(Password, Mutate(
                new KeyStoreService().EncryptPayloadAsJson(Password, new byte[] { 1, 2, 3 }),
                o => { o["crypto"]["kdfparams"]["n"] = 1 << 20; o["crypto"]["kdfparams"]["r"] = 8; })));
        }

        [Trait("Rule", "S9-R2-7")]
        [Trait("AcceptedRisk", "S9-R2-F")]
        [Fact]
        public void Given_a_flipped_iv_and_an_absent_malformed_or_all_zero_address_When_decrypting_Then_the_address_check_does_not_apply_and_the_v3_format_cannot_detect_it()
        {
            var offenders = new List<string>();
            var addresses = new Dictionary<string, Action<JObject>>
            {
                ["address removed"] = o => o.Remove("address"),
                ["address empty"] = o => o["address"] = "",
                ["address zeros"] = o => o["address"] = "0x0000000000000000000000000000000000000000",
                ["address malformed"] = o => o["address"] = "not-an-address",
                ["address wrong length"] = o => o["address"] = "0x1234",
                ["address null"] = o => o["address"] = null
            };
            foreach (var a in addresses)
            {
                var json = Mutate(ScryptJson(), o =>
                {
                    o["crypto"]["cipherparams"]["iv"] = Flip((string)o["crypto"]["cipherparams"]["iv"]);
                    a.Value(o);
                });
                try
                {
                    var key = new KeyStoreService().DecryptKeyStoreFromJson(Password, json);
                    if (key.SequenceEqual(PrivateKey)) offenders.Add(a.Key + " returned the original key");
                }
                catch (Exception ex) { offenders.Add(a.Key + " threw " + ex.GetType().Name); }
            }
            Assert.True(offenders.Count == 0, string.Join("; ", offenders));
        }

        [Trait("Rule", "S9-R2-7")]
        [Fact]
        public void Given_a_flipped_iv_and_a_real_address_When_decrypting_Then_the_key_is_rejected_for_scrypt_and_pbkdf2()
        {
            foreach (var original in new[] { ScryptJson(), Pbkdf2Json() })
            {
                var json = Mutate(original, o => o["crypto"]["cipherparams"]["iv"] = Flip((string)o["crypto"]["cipherparams"]["iv"]));
                Assert.Throws<DecryptionException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
            }
        }

        [Trait("Rule", "S9-R2-7")]
        [Fact]
        public void Given_addresses_in_checksum_case_and_0x_prefix_forms_When_decrypting_Then_the_matching_key_is_accepted()
        {
            var lower = RealAddress.ToLowerInvariant();
            foreach (var form in new[] { RealAddress, lower, lower.Replace("0x", ""), "0X" + lower.Substring(2).ToUpperInvariant() })
            {
                var json = ScryptJson(form);
                Assert.Equal(PrivateKey, new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
            }
        }

        [Trait("Rule", "S9-R2-8")]
        [Fact]
        public void Given_a_mismatching_real_address_When_decrypting_Then_the_keystore_is_rejected_with_DecryptionException()
        {
            var json = Mutate(ScryptJson(), o => o["address"] = "0x1111111111111111111111111111111111111111");
            Assert.Throws<DecryptionException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
        }

        [Trait("Rule", "S9-R2-9")]
        [Fact]
        public void Given_hex_fields_with_0x_prefixes_and_mixed_case_When_decrypting_Then_a_valid_keystore_still_loads()
        {
            var json = Mutate(ScryptJson(), o =>
            {
                o["crypto"]["mac"] = "0x" + ((string)o["crypto"]["mac"]).ToUpperInvariant();
                o["crypto"]["ciphertext"] = "0x" + (string)o["crypto"]["ciphertext"];
            });
            Assert.Equal(PrivateKey, new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
        }
    }
}
