using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.Model;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.KeyStore.Tests.SecurityAudit71.S9
{
    public class S9KeyStoreHostileTests
    {
        private const string Password = "correct horse";
        private const string Address = "0x008aeeda4d805471df9b2a5b0f38a0c3bcba786b";
        private static readonly byte[] PrivateKey =
            "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d".HexToByteArray();

        private static string ScryptJson()
        {
            var service = new KeyStoreScryptService();
            return service.EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey, Address,
                new ScryptParams { Dklen = 32, N = 1024, R = 8, P = 1 });
        }

        private static string Pbkdf2Json()
        {
            var service = new KeyStorePbkdf2Service();
            return service.EncryptAndGenerateKeyStoreAsJson(Password, PrivateKey, Address,
                new Pbkdf2Params { Dklen = 32, Count = 1000, Prf = "hmac-sha256" });
        }

        private static string Mutate(string json, Action<JObject> mutation)
        {
            var obj = JObject.Parse(json);
            mutation(obj);
            return obj.ToString();
        }

        private static readonly Type[] UncontrolledFailures =
        {
            typeof(NullReferenceException), typeof(IndexOutOfRangeException), typeof(InvalidCastException),
            typeof(OverflowException), typeof(OutOfMemoryException), typeof(ArithmeticException),
            typeof(DivideByZeroException), typeof(InvalidOperationException)
        };

        private static string Probe(string json)
        {
            try
            {
                new KeyStoreService().DecryptKeyStoreFromJson(Password, json);
                return null;
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return UncontrolledFailures.Any(t => t.IsInstanceOfType(inner)) ? inner.GetType().Name : null;
            }
        }

        private static void AssertAllFailCleanly(Dictionary<string, string> cases)
        {
            var offenders = new List<string>();
            foreach (var c in cases)
            {
                var result = Probe(c.Value);
                if (result != null) offenders.Add(c.Key + " => " + result);
            }
            Assert.True(offenders.Count == 0, string.Join("; ", offenders));
        }

        [Trait("Rule", "S9-1")]
        [Fact]
        public void Given_scrypt_keystore_with_n_2_pow_20_and_r_8_When_decrypting_Then_it_is_rejected_before_allocating_a_gigabyte()
        {
            var json = Mutate(ScryptJson(), o =>
            {
                o["crypto"]["kdfparams"]["n"] = 1 << 20;
                o["crypto"]["kdfparams"]["r"] = 8;
                o["crypto"]["kdfparams"]["p"] = 1;
            });
            var before = GC.GetAllocatedBytesForCurrentThread();
            try { new KeyStoreService().DecryptKeyStoreFromJson(Password, json); } catch (Exception) { }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < 64L * 1024 * 1024, "allocated " + allocated + " bytes for attacker supplied scrypt cost");
        }

        [Trait("Rule", "S9-1")]
        [Fact]
        public void Given_scrypt_keystore_with_p_32_When_decrypting_Then_it_is_rejected_before_burning_cpu()
        {
            var json = Mutate(ScryptJson(), o =>
            {
                o["crypto"]["kdfparams"]["n"] = 16384;
                o["crypto"]["kdfparams"]["r"] = 8;
                o["crypto"]["kdfparams"]["p"] = 32;
            });
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.Throws<ArgumentException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 1024, "p=32 was not rejected before the KDF");
        }

        [Trait("Rule", "S9-2")]
        [Fact]
        public void Given_pbkdf2_keystore_with_c_above_the_cap_When_decrypting_Then_it_is_rejected_before_deriving_and_c_five_million_fails_only_on_the_mac()
        {
            var aboveCap = Mutate(Pbkdf2Json(), o => o["crypto"]["kdfparams"]["c"] = 10_000_001);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.Throws<ArgumentException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, aboveCap));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 1024, "c above the cap was not rejected before the KDF");

            var withinCap = Mutate(Pbkdf2Json(), o => o["crypto"]["kdfparams"]["c"] = 5_000_000);
            Assert.Throws<DecryptionException>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, withinCap));
        }

        [Trait("Rule", "S9-3")]
        [Fact]
        public void Given_pbkdf2_keystore_with_dklen_16_MiB_When_decrypting_Then_it_is_rejected_before_allocating()
        {
            var json = Mutate(Pbkdf2Json(), o =>
            {
                o["crypto"]["kdfparams"]["dklen"] = 1 << 24;
                o["crypto"]["kdfparams"]["c"] = 1;
            });
            var before = GC.GetAllocatedBytesForCurrentThread();
            try { new KeyStoreService().DecryptKeyStoreFromJson(Password, json); } catch (Exception) { }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < 8L * 1024 * 1024, "allocated " + allocated + " bytes");
        }

        [Trait("Rule", "S9-3")]
        [Fact]
        public void Given_scrypt_keystore_with_dklen_16_MiB_When_decrypting_Then_it_is_rejected_before_allocating()
        {
            var json = Mutate(ScryptJson(), o => o["crypto"]["kdfparams"]["dklen"] = 1 << 24);
            var before = GC.GetAllocatedBytesForCurrentThread();
            try { new KeyStoreService().DecryptKeyStoreFromJson(Password, json); } catch (Exception) { }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < 8L * 1024 * 1024, "allocated " + allocated + " bytes");
        }

        [Trait("Rule", "S9-4")]
        [Fact]
        public void Given_degenerate_scrypt_parameters_When_decrypting_Then_every_one_fails_with_a_controlled_exception()
        {
            var cases = new Dictionary<string, string>();
            void Add(string name, string param, JToken value) =>
                cases[name] = Mutate(ScryptJson(), o => o["crypto"]["kdfparams"][param] = value);
            Add("n=0", "n", 0);
            Add("n=-1", "n", -1);
            Add("n=int.Min", "n", int.MinValue);
            Add("r=0", "r", 0);
            Add("r=-1", "r", -1);
            Add("p=0", "p", 0);
            Add("p=-1", "p", -1);
            Add("dklen=0", "dklen", 0);
            Add("dklen=-1", "dklen", -1);
            Add("dklen=16", "dklen", 16);
            Add("dklen=31", "dklen", 31);
            AssertAllFailCleanly(cases);
        }

        [Trait("Rule", "S9-4")]
        [Fact]
        public void Given_degenerate_pbkdf2_parameters_When_decrypting_Then_every_one_fails_with_a_controlled_exception()
        {
            var cases = new Dictionary<string, string>();
            void Add(string name, string param, JToken value) =>
                cases[name] = Mutate(Pbkdf2Json(), o => o["crypto"]["kdfparams"][param] = value);
            Add("c=0", "c", 0);
            Add("c=-1", "c", -1);
            Add("dklen=0", "dklen", 0);
            Add("dklen=-1", "dklen", -1);
            Add("dklen=16", "dklen", 16);
            Add("dklen=31", "dklen", 31);
            AssertAllFailCleanly(cases);
        }

        [Trait("Rule", "S9-5")]
        [Fact]
        public void Given_wrong_length_or_malformed_salt_iv_mac_and_ciphertext_When_decrypting_Then_every_one_fails_with_a_controlled_exception()
        {
            var cases = new Dictionary<string, string>();
            foreach (var kdf in new[] { ("scrypt", ScryptJson()), ("pbkdf2", Pbkdf2Json()) })
            {
                var json = kdf.Item2;
                cases[kdf.Item1 + " salt empty"] = Mutate(json, o => o["crypto"]["kdfparams"]["salt"] = "");
                cases[kdf.Item1 + " salt odd"] = Mutate(json, o => o["crypto"]["kdfparams"]["salt"] = "abc");
                cases[kdf.Item1 + " salt non-hex"] = Mutate(json, o => o["crypto"]["kdfparams"]["salt"] = "zz");
                cases[kdf.Item1 + " iv empty"] = Mutate(json, o => o["crypto"]["cipherparams"]["iv"] = "");
                cases[kdf.Item1 + " iv 8 bytes"] = Mutate(json, o => o["crypto"]["cipherparams"]["iv"] = "0011223344556677");
                cases[kdf.Item1 + " iv 17 bytes"] = Mutate(json, o => o["crypto"]["cipherparams"]["iv"] = new string('1', 34));
                cases[kdf.Item1 + " iv non-hex"] = Mutate(json, o => o["crypto"]["cipherparams"]["iv"] = "not hex at all not hex at all!!");
                cases[kdf.Item1 + " mac empty"] = Mutate(json, o => o["crypto"]["mac"] = "");
                cases[kdf.Item1 + " mac short"] = Mutate(json, o => o["crypto"]["mac"] = "00");
                cases[kdf.Item1 + " ciphertext empty"] = Mutate(json, o => o["crypto"]["ciphertext"] = "");
                cases[kdf.Item1 + " ciphertext non-hex"] = Mutate(json, o => o["crypto"]["ciphertext"] = "xyz");
                cases[kdf.Item1 + " salt missing"] = Mutate(json, o => ((JObject)o["crypto"]["kdfparams"]).Remove("salt"));
                cases[kdf.Item1 + " iv missing"] = Mutate(json, o => ((JObject)o["crypto"]["cipherparams"]).Remove("iv"));
                cases[kdf.Item1 + " mac missing"] = Mutate(json, o => ((JObject)o["crypto"]).Remove("mac"));
                cases[kdf.Item1 + " ciphertext missing"] = Mutate(json, o => ((JObject)o["crypto"]).Remove("ciphertext"));
                cases[kdf.Item1 + " kdfparams missing"] = Mutate(json, o => ((JObject)o["crypto"]).Remove("kdfparams"));
                cases[kdf.Item1 + " cipherparams missing"] = Mutate(json, o => ((JObject)o["crypto"]).Remove("cipherparams"));
                cases[kdf.Item1 + " crypto null"] = Mutate(json, o => o["crypto"] = null);
            }
            AssertAllFailCleanly(cases);
        }

        [Trait("Rule", "S9-6")]
        [Fact]
        public void Given_json_type_confusion_in_kdf_fields_When_decrypting_Then_every_one_fails_with_a_controlled_exception()
        {
            var cases = new Dictionary<string, string>();
            var scrypt = ScryptJson();
            cases["n nested object"] = Mutate(scrypt, o => o["crypto"]["kdfparams"]["n"] = new JObject { ["a"] = 1 });
            cases["n array"] = Mutate(scrypt, o => o["crypto"]["kdfparams"]["n"] = new JArray(1, 2));
            cases["n string garbage"] = Mutate(scrypt, o => o["crypto"]["kdfparams"]["n"] = "lots");
            cases["n float 1e30"] = Mutate(scrypt, o => o["crypto"]["kdfparams"]["n"] = 1e30);
            cases["n 2^40"] = Mutate(scrypt, o => o["crypto"]["kdfparams"]["n"] = 1L << 40);
            cases["n null"] = Mutate(scrypt, o => o["crypto"]["kdfparams"]["n"] = null);
            cases["salt number"] = Mutate(scrypt, o => o["crypto"]["kdfparams"]["salt"] = 12345);
            cases["salt array"] = Mutate(scrypt, o => o["crypto"]["kdfparams"]["salt"] = new JArray("aa"));
            cases["salt object"] = Mutate(scrypt, o => o["crypto"]["kdfparams"]["salt"] = new JObject());
            cases["kdf number"] = Mutate(scrypt, o => o["crypto"]["kdf"] = 7);
            cases["kdf object"] = Mutate(scrypt, o => o["crypto"]["kdf"] = new JObject());
            cases["kdf unknown"] = Mutate(scrypt, o => o["crypto"]["kdf"] = "argon2id");
            cases["kdf missing"] = Mutate(scrypt, o => ((JObject)o["crypto"]).Remove("kdf"));
            cases["root array"] = "[]";
            cases["root string"] = "\"x\"";
            cases["empty"] = "";
            cases["crypto string"] = Mutate(scrypt, o => o["crypto"] = "x");
            cases["c nested"] = Mutate(Pbkdf2Json(), o => o["crypto"]["kdfparams"]["c"] = new JObject());
            cases["c 2^40"] = Mutate(Pbkdf2Json(), o => o["crypto"]["kdfparams"]["c"] = 1L << 40);
            AssertAllFailCleanly(cases);
        }

        [Trait("Rule", "S9-7")]
        [Fact]
        public void Given_a_keystore_with_an_unsupported_cipher_When_decrypting_Then_it_is_rejected()
        {
            var json = Mutate(ScryptJson(), o => o["crypto"]["cipher"] = "aes-256-cbc");
            Assert.ThrowsAny<Exception>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
        }

        [Trait("Rule", "S9-7")]
        [Fact]
        public void Given_a_keystore_with_a_version_other_than_3_When_decrypting_Then_it_is_rejected()
        {
            var json = Mutate(ScryptJson(), o => o["version"] = 4);
            Assert.ThrowsAny<Exception>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
        }

        [Trait("Rule", "S9-7")]
        [Fact]
        public void Given_a_pbkdf2_keystore_with_an_unsupported_prf_When_decrypting_Then_it_is_rejected()
        {
            var json = Mutate(Pbkdf2Json(), o => o["crypto"]["kdfparams"]["prf"] = "hmac-sha512");
            Assert.ThrowsAny<Exception>(() => new KeyStoreService().DecryptKeyStoreFromJson(Password, json));
        }

        [Trait("Rule", "S9-8")]
        [Fact]
        public void Given_a_keystore_whose_kdf_does_not_match_its_parameter_shape_When_decrypting_Then_it_fails_with_a_controlled_exception()
        {
            var cases = new Dictionary<string, string>();
            cases["scrypt params with kdf=pbkdf2"] = Mutate(ScryptJson(), o => o["crypto"]["kdf"] = "pbkdf2");
            cases["pbkdf2 params with kdf=scrypt"] = Mutate(Pbkdf2Json(), o => o["crypto"]["kdf"] = "scrypt");
            AssertAllFailCleanly(cases);
        }

        [Trait("Rule", "S9-9")]
        [Fact]
        public void Given_a_tampered_ciphertext_or_mac_or_salt_or_iv_When_decrypting_Then_the_mac_rejects_it_and_no_key_is_returned()
        {
            var offenders = new List<string>();
            foreach (var original in new[] { ScryptJson(), Pbkdf2Json() })
            {
                var tamperings = new List<(string, string)>
                {
                    ("ciphertext", Mutate(original, o => o["crypto"]["ciphertext"] = Flip((string)o["crypto"]["ciphertext"]))),
                    ("mac", Mutate(original, o => o["crypto"]["mac"] = Flip((string)o["crypto"]["mac"]))),
                    ("salt", Mutate(original, o => o["crypto"]["kdfparams"]["salt"] = Flip((string)o["crypto"]["kdfparams"]["salt"]))),
                    ("iv", Mutate(original, o => o["crypto"]["cipherparams"]["iv"] = Flip((string)o["crypto"]["cipherparams"]["iv"])))
                };
                foreach (var t in tamperings)
                {
                    try
                    {
                        var key = new KeyStoreService().DecryptKeyStoreFromJson(Password, t.Item2);
                        offenders.Add(t.Item1 + " flip accepted and returned " + key.ToHex());
                    }
                    catch (DecryptionException) { }
                }
            }
            Assert.True(offenders.Count == 0, string.Join("; ", offenders));
        }

        private static string Flip(string hex)
        {
            var bytes = hex.HexToByteArray();
            bytes[0] ^= 0x01;
            return bytes.ToHex();
        }

        [Trait("Rule", "S9-9")]
        [Fact]
        public void Given_a_mismatching_mac_When_crypto_decrypt_is_called_directly_Then_no_plaintext_is_returned()
        {
            var crypto = new KeyStoreCrypto();
            var derived = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            var iv = new byte[16];
            var cipherText = crypto.GenerateAesCtrCipher(iv, derived.Take(16).ToArray(), PrivateKey);
            var goodMac = crypto.GenerateMac(derived, cipherText);
            Assert.Equal(PrivateKey, crypto.Decrypt(goodMac, iv, cipherText, derived));
            var badMac = (byte[])goodMac.Clone();
            badMac[31] ^= 1;
            Assert.Throws<DecryptionException>(() => crypto.Decrypt(badMac, iv, cipherText, derived));
            Assert.Throws<DecryptionException>(() => crypto.Decrypt(new byte[0], iv, cipherText, derived));
            Assert.Throws<DecryptionException>(() => crypto.Decrypt(goodMac.Concat(new byte[] { 0 }).ToArray(), iv, cipherText, derived));
        }

        [Trait("Rule", "S9-10")]
        [Fact]
        public void Given_unicode_and_lone_surrogate_passwords_When_round_tripping_Then_scrypt_and_pbkdf2_decrypt_with_the_same_password()
        {
            foreach (var password in new[] { "pässwörd", "pass😀word", "\ud800", "a\ud800b", "" })
            {
                var scrypt = new KeyStoreScryptService();
                var sj = scrypt.EncryptAndGenerateKeyStoreAsJson(password, PrivateKey, Address,
                    new ScryptParams { Dklen = 32, N = 1024, R = 8, P = 1 });
                Assert.Equal(PrivateKey, new KeyStoreService().DecryptKeyStoreFromJson(password, sj));
                var pbkdf2 = new KeyStorePbkdf2Service();
                var pj = pbkdf2.EncryptAndGenerateKeyStoreAsJson(password, PrivateKey, Address,
                    new Pbkdf2Params { Dklen = 32, Count = 1000, Prf = "hmac-sha256" });
                Assert.Equal(PrivateKey, new KeyStoreService().DecryptKeyStoreFromJson(password, pj));
            }
        }

        [Trait("Rule", "S9-11")]
        [Fact]
        public void Given_many_concurrent_encryptions_When_generating_salt_and_iv_Then_none_repeat_and_none_throw()
        {
            var seen = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
            var generator = new RandomBytesGenerator();
            Parallel.For(0, 16, _ =>
            {
                for (var i = 0; i < 2000; i++)
                {
                    Assert.True(seen.TryAdd(generator.GenerateRandomSalt().ToHex(), true));
                    Assert.True(seen.TryAdd(generator.GenerateRandomInitialisationVector().ToHex(), true));
                }
            });
            Assert.Equal(16 * 2000 * 2, seen.Count);
        }

        [Trait("Rule", "S9-12")]
        [Fact]
        public void Given_a_decryption_failure_When_reading_the_exception_Then_it_contains_neither_password_nor_key()
        {
            var json = ScryptJson();
            var ex = Assert.ThrowsAny<Exception>(() => new KeyStoreService().DecryptKeyStoreFromJson("wrong password", json));
            var text = ex.ToString();
            Assert.DoesNotContain("wrong password", text);
            Assert.DoesNotContain(PrivateKey.ToHex(), text);
            Assert.DoesNotContain(Password, text);
        }
    }
}
