using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NBitcoin;
using Nethereum.Accounts.Bip32;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Wallet.WalletAccounts;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9.Round3
{
    public class S9Round3Tests : IDisposable
    {
        private const string Password = "vault password";
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string PrivateKeyHex = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d";
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "s9r3-" + Guid.NewGuid().ToString("N"));

        public S9Round3Tests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        private string NewPath() => Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".vault");

        private class IdentityStrategy : IEncryptionStrategy
        {
            public byte[] Encrypt(byte[] data, string password) => data;
            public byte[] Decrypt(byte[] data, string password) => data;
        }

        private static string KeyAddress(string key) => new Nethereum.Signer.EthECKey(key).GetPublicAddress();

        private static byte[] PlainVaultJson(string deviceLabel = "d", string accountLabel = "cold")
        {
            var vault = new WalletVault(new IdentityStrategy());
            vault.AddMnemonic(new MnemonicInfo("main", Abandon, "pp") { Id = "m1" });
            var hd = new MinimalHDWallet(Abandon, "pp");
            vault.Accounts.Add(new MnemonicWalletAccount(hd.GetEthereumAddress(3), "hd", 3, "m1", hd));
            vault.Accounts.Add(new PrivateKeyWalletAccount(KeyAddress(PrivateKeyHex), accountLabel, PrivateKeyHex.ToUpperInvariant()));
            vault.Accounts.Add(new ViewOnlyWalletAccount("0x1111111111111111111111111111111111111111", "watch"));
            vault.AddOrUpdateHardwareDevice("d1", "ledger", deviceLabel);
            return Convert.FromBase64String(vault.Encrypt(Password));
        }

        private static byte[] LegacyCbc(byte[] plain, bool sha1, byte[] iv = null)
        {
            iv ??= RandomNumberGenerator.GetBytes(16);
            var salt = Encoding.UTF8.GetBytes("NethereumWallet16");
            byte[] key;
            if (sha1)
            {
                var generator = new Pkcs5S2ParametersGenerator();
                generator.Init(Encoding.UTF8.GetBytes(Password), salt, 10000);
                key = ((KeyParameter)generator.GenerateDerivedParameters("AES", 256)).GetKey();
            }
            else
            {
                key = new Rfc2898DeriveBytes(Password, salt, 10000, HashAlgorithmName.SHA256).GetBytes(32);
            }
            var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new AesEngine()));
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
            var output = new byte[cipher.GetOutputSize(plain.Length)];
            var length = cipher.ProcessBytes(plain, 0, plain.Length, output, 0);
            length += cipher.DoFinal(output, length);
            return iv.Concat(output.Take(length)).ToArray();
        }

        private static byte[] LegacyKeyStore(byte[] plain) => new KeyStoreEncryptionStrategy().Encrypt(plain, Password);

        private async Task<string> WriteBlob(byte[] blob)
        {
            var path = NewPath();
            await File.WriteAllTextAsync(path, Convert.ToBase64String(blob));
            return path;
        }

        private static byte[] FileBlob(string path) => Convert.FromBase64String(File.ReadAllText(path));

        private static byte[] SevenZeroSeed(string mnemonic, string password)
        {
            var normalized = mnemonic.Normalize(NormalizationForm.FormKD);
            var salt = ("mnemonic" + password).Normalize(NormalizationForm.FormKD);
            using var derive = new Rfc2898DeriveBytes(normalized, Encoding.UTF8.GetBytes(salt), 2048, HashAlgorithmName.SHA512);
            return derive.GetBytes(64);
        }

        private static byte[] NwvHeader(int iterations)
        {
            var header = new byte[37];
            header[0] = 0x4E; header[1] = 0x57; header[2] = 0x56; header[3] = 1; header[4] = 1;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(5), iterations);
            RandomNumberGenerator.Fill(header.AsSpan(9));
            return header;
        }

        [Trait("Rule", "S9-R3-1")]
        [Fact]
        public void Given_crafted_nwv_prefixed_random_blobs_When_decrypting_Then_none_is_accepted_through_the_legacy_fallback()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var accepted = 0;
            var rng = new Random(12345);
            for (var i = 0; i < 600; i++)
            {
                var body = new byte[16 + rng.Next(0, 300)];
                RandomNumberGenerator.Fill(body);
                var blob = NwvHeader(1).Concat(body).ToArray();
                try { strategy.Decrypt(blob, Password); accepted++; } catch (Exception) { }
            }
            Assert.Equal(0, accepted);
        }

        [Trait("Rule", "S9-R3-1")]
        [Fact]
        public void Given_a_nwv_prefixed_blob_that_is_a_valid_legacy_vault_under_another_password_When_decrypting_Then_it_is_rejected()
        {
            var plain = PlainVaultJson("attacker");
            var iv = RandomNumberGenerator.GetBytes(16);
            iv[0] = 0x4E; iv[1] = 0x57; iv[2] = 0x56; iv[3] = 1;
            var attackerBlob = LegacyCbc(plain, false, iv);
            Assert.Equal(plain, new Aes256GcmEncryptionStrategy().Decrypt(attackerBlob, Password));
            Assert.ThrowsAny<Exception>(() => new Aes256GcmEncryptionStrategy().Decrypt(attackerBlob, "someone else"));
        }

        [Trait("Rule", "S9-R3-2")]
        [Fact]
        public void Given_failures_of_every_kind_on_a_current_format_blob_When_decrypting_Then_exception_type_and_message_do_not_differ()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var blob = strategy.Encrypt(Encoding.UTF8.GetBytes("{\"a\":1}"), Password);
            var outcomes = new Dictionary<string, string>();
            void Record(string name, byte[] data, string password)
            {
                try { strategy.Decrypt(data, password); outcomes[name] = "accepted"; }
                catch (Exception ex) { outcomes[name] = ex.GetType().FullName + "|" + ex.Message; }
            }
            Record("wrong password", blob, "nope");
            var tag = (byte[])blob.Clone(); tag[^1] ^= 1; Record("tag", tag, Password);
            var body = (byte[])blob.Clone(); body[40] ^= 1; Record("body", body, Password);
            var salt = (byte[])blob.Clone(); salt[10] ^= 1; Record("salt", salt, Password);
            var nonce = (byte[])blob.Clone(); nonce[30] ^= 1; Record("nonce", nonce, Password);
            var iterations = (byte[])blob.Clone(); iterations[8] ^= 1; Record("iterations", iterations, Password);
            Assert.True(outcomes.Values.Distinct().Count() == 1, string.Join("; ", outcomes.Select(o => o.Key + " => " + o.Value)));
        }

        [Trait("Rule", "S9-R3-3")]
        [Fact]
        public async Task Given_every_iv_bit_and_every_ciphertext_byte_flipped_in_a_legacy_cbc_vault_When_unlocking_Then_none_is_accepted()
        {
            var plain = PlainVaultJson();
            var blob = LegacyCbc(plain, false);
            var accepted = new List<string>();
            for (var bit = 0; bit < 128; bit++)
            {
                var copy = (byte[])blob.Clone();
                copy[bit / 8] ^= (byte)(1 << (bit % 8));
                var path = await WriteBlob(copy);
                if (await new FileWalletVaultService(path).UnlockAsync(Password)) accepted.Add("iv bit " + bit);
            }
            for (var index = 16; index < blob.Length; index++)
            {
                var copy = (byte[])blob.Clone();
                copy[index] ^= 0x01;
                var path = await WriteBlob(copy);
                if (await new FileWalletVaultService(path).UnlockAsync(Password)) accepted.Add("ct byte " + index);
            }
            Assert.True(accepted.Count == 0, string.Join(", ", accepted));
        }

        [Trait("Rule", "S9-R3-3")]
        [Fact]
        public async Task Given_a_legacy_vault_with_a_view_only_address_altered_by_the_cbc_flip_trick_When_unlocking_Then_it_is_rejected()
        {
            var address = "0x" + new string('a', 40);
            var vault = new WalletVault(new IdentityStrategy());
            vault.Accounts.Add(new ViewOnlyWalletAccount(address, "w"));
            var plain = Convert.FromBase64String(vault.Encrypt(Password));
            var text = Encoding.UTF8.GetString(plain);
            var addressStart = text.IndexOf(address, StringComparison.Ordinal);
            var targetBlock = (addressStart + 30) / 16;
            var offset = 16 + (targetBlock - 1) * 16 + 2;
            var accepted = 0;
            for (var attempt = 0; attempt < 3000; attempt++)
            {
                var blob = LegacyCbc(plain, false);
                blob[offset] ^= 0x01;
                var path = await WriteBlob(blob);
                if (await new FileWalletVaultService(path).UnlockAsync(Password)) accepted++;
            }
            Assert.Equal(0, accepted);
        }

        [Trait("Rule", "S9-R3-4")]
        [Fact]
        public async Task Given_non_ascii_labels_in_every_legacy_format_When_unlocking_and_migrating_Then_the_labels_survive_exactly()
        {
            var label = "😀 Ünï 漢字 é́ ‍x";
            var failures = new List<string>();
            foreach (var raw in new[] { false, true })
            {
                byte[] plain;
                if (raw)
                {
                    var vault = new WalletVault(new IdentityStrategy());
                    vault.AddOrUpdateHardwareDevice("d1", "ledger", label);
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(vault.Encrypt(Password)));
                    plain = Encoding.UTF8.GetBytes(System.Text.RegularExpressions.Regex.Replace(json, @"\\u([0-9A-Fa-f]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString()));
                    if (!Encoding.UTF8.GetString(plain).Contains("漢字")) failures.Add("raw utf8 fixture not raw");
                }
                else plain = PlainVaultJson(label, label);
                foreach (var format in new Dictionary<string, byte[]> { ["sha256"] = LegacyCbc(plain, false), ["sha1"] = LegacyCbc(plain, true), ["keystore"] = LegacyKeyStore(plain) })
                {
                    var path = await WriteBlob(format.Value);
                    var service = new FileWalletVaultService(path);
                    if (!await service.UnlockAsync(Password)) { failures.Add(raw + format.Key + " unlock"); continue; }
                    if (service.GetCurrentVault().HardwareDevices.Single().Label != label) failures.Add(raw + format.Key + " label changed");
                    var again = new FileWalletVaultService(path);
                    if (!await again.UnlockAsync(Password) || again.GetCurrentVault().HardwareDevices.Single().Label != label) failures.Add(raw + format.Key + " second unlock");
                }
            }
            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [Trait("Rule", "S9-R3-5")]
        [Fact]
        public async Task Given_a_legit_legacy_vault_with_index_3_uppercase_key_and_view_only_account_When_unlocking_Then_it_is_judged_consistent_and_migrated()
        {
            var plain = PlainVaultJson();
            var path = await WriteBlob(LegacyCbc(plain, false));
            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            Assert.True(await service.GetCurrentVault().IsConsistentAsync());
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(FileBlob(path)));
        }

        [Trait("Rule", "S9-R3-6")]
        [Fact]
        public async Task Given_a_legacy_vault_with_unknown_top_level_properties_or_mismatched_addresses_When_unlocking_Then_it_is_not_migrated()
        {
            var good = Encoding.UTF8.GetString(PlainVaultJson());
            var extra = good.TrimEnd('}') + ",\"injected\":1}";
            var doc = JsonDocument.Parse(good);
            var mismatched = good.Replace(KeyAddress(PrivateKeyHex), "0x2222222222222222222222222222222222222222", StringComparison.OrdinalIgnoreCase);
            var failures = new List<string>();
            foreach (var variant in new Dictionary<string, string> { ["extra property"] = extra, ["mismatched address"] = mismatched })
            {
                var blob = LegacyCbc(Encoding.UTF8.GetBytes(variant.Value), false);
                var path = await WriteBlob(blob);
                await new FileWalletVaultService(path).UnlockAsync(Password);
                if (!FileBlob(path).SequenceEqual(blob)) failures.Add(variant.Key + " was migrated");
            }
            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [Trait("Rule", "S9-R3-7")]
        [Fact]
        public async Task Given_an_inconsistent_legacy_vault_that_was_not_migrated_When_the_user_saves_Then_it_is_not_sealed_under_gcm()
        {
            var good = Encoding.UTF8.GetString(PlainVaultJson());
            var mismatched = good.Replace(KeyAddress(PrivateKeyHex), "0x2222222222222222222222222222222222222222", StringComparison.OrdinalIgnoreCase);
            var blob = LegacyCbc(Encoding.UTF8.GetBytes(mismatched), false);
            var path = await WriteBlob(blob);
            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            await service.SaveAsync();
            Assert.False(Aes256GcmEncryptionStrategy.IsCurrentFormat(FileBlob(path)),
                "a vault judged inconsistent at unlock was authenticated under GCM by the next save");
        }

        private static IEnumerable<(string, Func<string, IEncryptionStrategy>)> Services()
        {
#pragma warning disable CS0618
            yield return ("gcm", p => new Aes256GcmEncryptionStrategy());
            yield return ("default-aes", p => new DefaultAes256EncryptionStrategy());
            yield return ("bc-aes", p => new BouncyCastleAes256EncryptionStrategy());
            yield return ("keystore", p => new KeyStoreEncryptionStrategy());
#pragma warning restore CS0618
        }

        [Trait("Rule", "S9-R3-8")]
        [Fact]
        public async Task Given_every_service_strategy_and_every_blob_format_7_0_could_read_When_unlocking_twice_Then_both_unlocks_work_and_first_party_services_end_in_gcm()
        {
            var plain = PlainVaultJson();
            var sevenZeroReadable = new Dictionary<string, string>
            {
                ["gcm"] = "gcm", ["default-aes"] = "sha256", ["bc-aes"] = "sha1", ["keystore"] = "keystore"
            };
            var failures = new List<string>();
            foreach (var service in Services())
            {
                foreach (var format in new[] { "sha256", "sha1", "keystore", "gcm" })
                {
                    byte[] blob = format switch
                    {
                        "sha256" => LegacyCbc(plain, false),
                        "sha1" => LegacyCbc(plain, true),
                        "keystore" => LegacyKeyStore(plain),
                        _ => new Aes256GcmEncryptionStrategy().Encrypt(plain, Password)
                    };
                    var expectReadable = service.Item1 == "gcm" || format == sevenZeroReadable[service.Item1] || (format == "gcm" && service.Item1 != "keystore");
                    var path = await WriteBlob(blob);
                    var first = await new FileWalletVaultService(path, service.Item2(Password)).UnlockAsync(Password);
                    if (first != expectReadable) { failures.Add(service.Item1 + "/" + format + " first=" + first); continue; }
                    if (!first) continue;
                    var second = await new FileWalletVaultService(path, service.Item2(Password)).UnlockAsync(Password);
                    if (!second) failures.Add(service.Item1 + "/" + format + " second unlock failed");
                    var migrated = Aes256GcmEncryptionStrategy.IsCurrentFormat(FileBlob(path));
                    var shouldMigrate = service.Item1 != "keystore";
                    if (migrated != shouldMigrate) failures.Add(service.Item1 + "/" + format + " migrated=" + migrated);
                }
            }
            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [Trait("Rule", "S9-R3-9")]
        [Fact]
        public async Task Given_a_migrated_vault_When_the_password_is_changed_through_save_Then_the_new_password_unlocks_and_the_old_one_does_not()
        {
            foreach (var service in Services())
            {
                var path = await WriteBlob(service.Item1 == "keystore" ? LegacyKeyStore(PlainVaultJson()) : LegacyCbc(PlainVaultJson(), service.Item1 == "bc-aes"));
                var unlocked = new FileWalletVaultService(path, service.Item2(Password));
                Assert.True(await unlocked.UnlockAsync(Password), service.Item1);
                await unlocked.SaveAsync("new password");
                Assert.False(await new FileWalletVaultService(path, service.Item2(Password)).UnlockAsync(Password), service.Item1 + " old password still works");
                Assert.True(await new FileWalletVaultService(path, service.Item2(Password)).UnlockAsync("new password"), service.Item1 + " new password");
                Assert.False(File.Exists(path + ".tmp"));
            }
        }

        [Trait("Rule", "S9-R3-10")]
        [Fact]
        public void Given_the_english_word_list_When_compared_with_nbitcoin_Then_all_2048_words_are_identical_and_lowercase()
        {
            var nbitcoin = Wordlist.English.GetWords();
            Assert.Equal(2048, nbitcoin.Count);
            Assert.True(nbitcoin.SequenceEqual(Bip39.WordList), "word lists differ");
            Assert.All(Bip39.WordList, w => Assert.Equal(w.ToLowerInvariant(), w));
        }

        [Trait("Rule", "S9-R3-11")]
        [Fact]
        public void Given_random_7_0_style_generation_When_counting_phrases_containing_true_or_false_Then_the_rate_matches_two_in_2048_per_word()
        {
            int Rate(int words, int trials)
            {
                var hits = 0;
                for (var i = 0; i < trials; i++)
                {
                    var size = words == 12 ? 16 : 32;
                    var mnemonic = Bip39.EntropyToMnemonic(RandomNumberGenerator.GetBytes(size)).Split(' ');
                    if (mnemonic.Contains("true") || mnemonic.Contains("false")) hits++;
                }
                return hits;
            }
            var trials = 100000;
            var twelve = Rate(12, trials) / (double)trials;
            var twentyFour = Rate(24, trials) / (double)trials;
            Console.Error.WriteLine("S9-R3-11 12-word rate " + twelve + " 24-word rate " + twentyFour);
            Assert.InRange(twelve, 0.0095, 0.0140);
            Assert.InRange(twentyFour, 0.0190, 0.0275);
        }

        private static string SevenZeroPhrase()
        {
            for (var i = 0; i < 100000; i++)
            {
                var words = Bip39.EntropyToMnemonic(RandomNumberGenerator.GetBytes(16)).Split(' ');
                var index = Array.FindIndex(words, w => w == "true" || w == "false");
                if (index < 0) continue;
                words[index] = words[index].ToUpperInvariant();
                return string.Join(" ", words);
            }
            throw new InvalidOperationException("no sample");
        }

        [Trait("Rule", "S9-R3-12")]
        [Fact]
        public async Task Given_a_7_0_generated_phrase_with_uppercase_true_or_false_When_loaded_imported_and_migrated_Then_each_path_derives_the_documented_wallet()
        {
            var phrase = SevenZeroPhrase();
            var lower = phrase.ToLowerInvariant();
            var oldAddress = new MinimalHDWallet(SevenZeroSeed(phrase, "")).GetEthereumAddress(0);
            var standardAddress = new MinimalHDWallet(SevenZeroSeed(lower, "")).GetEthereumAddress(0);
            Assert.NotEqual(oldAddress, standardAddress);

            Assert.Equal(oldAddress, new MinimalHDWallet(phrase, "", false).GetEthereumAddress(0));
            Assert.Equal(standardAddress, new MinimalHDWallet(phrase, "").GetEthereumAddress(0));
            Assert.Equal(standardAddress, new MinimalHDWallet(lower, "").GetEthereumAddress(0));
            Assert.Equal(standardAddress, new MinimalHDWallet(lower, "", false).GetEthereumAddress(0));

            var vault = new WalletVault(new IdentityStrategy());
            vault.AddMnemonic(new MnemonicInfo("m", phrase, "") { Id = "m1" });
            var hd = new MinimalHDWallet(phrase, "", false);
            vault.Accounts.Add(new MnemonicWalletAccount(oldAddress, "a", 0, "m1", hd));
            var path = await WriteBlob(LegacyCbc(Convert.FromBase64String(vault.Encrypt(Password)), false));
            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            Assert.Equal(oldAddress, (await service.GetCurrentVault().Accounts.Single().GetAccountAsync()).Address, ignoreCase: true);
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(FileBlob(path)));
            var again = new FileWalletVaultService(path);
            Assert.True(await again.UnlockAsync(Password));
            Assert.Equal(oldAddress, (await again.GetCurrentVault().Accounts.Single().GetAccountAsync()).Address, ignoreCase: true);
            Assert.Equal(phrase, again.GetCurrentVault().Mnemonics.Single().Mnemonic);

            var strictFactoryJson = JsonDocument.Parse("{\"type\":\"mnemonic\",\"address\":\"" + oldAddress + "\",\"label\":\"a\",\"index\":0,\"mnemonicId\":\"m1\"}").RootElement;
            Assert.ThrowsAny<ArgumentException>(() => new MnemonicWalletAccountFactory(true).FromJson(strictFactoryJson, again.GetCurrentVault()));
        }

        [Trait("Rule", "S9-R3-13")]
        [Fact]
        public async Task Given_messy_input_When_creating_a_mnemonic_account_through_the_core_service_Then_the_stored_mnemonic_is_canonical_and_the_address_derives_from_it()
        {
            var vault = new WalletVault(new Aes256GcmEncryptionStrategy());
            var service = new CoreWalletAccountService(vault);
            var messy = "  " + Abandon.ToUpperInvariant().Replace(" ", "  ") + " \n";
            var account = await service.CreateMnemonicAccountAsync(messy, "pass", "label");
            var stored = vault.Mnemonics.Single();
            Assert.Equal(Abandon, stored.Mnemonic);
            var canonicalAddress = new MinimalHDWallet(Abandon, "pass").GetEthereumAddress(0);
            Assert.Equal(canonicalAddress, account.Address, ignoreCase: true);
            Assert.Equal(canonicalAddress, (await account.GetAccountAsync()).Address, ignoreCase: true);
            vault.Accounts.Add(account);
            var blob = vault.Encrypt(Password);
            var reloaded = new WalletVault(new Aes256GcmEncryptionStrategy());
            reloaded.Decrypt(blob, Password);
            Assert.Equal(canonicalAddress, (await reloaded.Accounts.Single().GetAccountAsync()).Address, ignoreCase: true);
            Assert.True(await reloaded.IsConsistentAsync());
        }

        [Trait("Rule", "S9-R3-14")]
        [Fact]
        public void Given_messy_but_valid_phrases_When_importing_strictly_Then_seed_equals_the_nbitcoin_seed_of_the_canonical_phrase()
        {
            var expected = new Mnemonic(Abandon).DeriveSeed("pw");
            foreach (var messy in new[] { Abandon.ToUpperInvariant(), "  " + Abandon + "\n", Abandon.Replace(" ", "\t"), Abandon.Replace(" ", "   "), "Abandon " + Abandon.Substring(8) })
                Assert.Equal(expected.ToHex(), new MinimalHDWallet(messy, "pw").Seed.ToHex());
        }
    }
}
