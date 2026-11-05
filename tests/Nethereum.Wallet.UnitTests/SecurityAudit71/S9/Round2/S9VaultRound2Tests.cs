using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Wallet.WalletAccounts;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9.Round2
{
    public class S9VaultRound2Tests : IDisposable
    {
        private const string Password = "vault password";
        private const string PrivateKeyHex = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d";
        private const string Address = "0x008aeeda4d805471df9b2a5b0f38a0c3bcba786b";
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "s9r2-" + Guid.NewGuid().ToString("N"));

        public S9VaultRound2Tests()
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

        private static byte[] PlainVaultJson(string deviceLabel = "d")
        {
            var vault = new WalletVault(new IdentityStrategy());
            vault.AddMnemonic(new MnemonicInfo("main", "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", "pp") { Id = "m1" });
            vault.Accounts.Add(new PrivateKeyWalletAccount(Address, "cold", PrivateKeyHex));
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

        private static byte[] LegacyKeyStore(byte[] plain)
        {
            return new KeyStoreEncryptionStrategy().Encrypt(plain, Password);
        }

        private async Task<string> WriteLegacy(byte[] blob)
        {
            var path = NewPath();
            await File.WriteAllTextAsync(path, Convert.ToBase64String(blob));
            return path;
        }

        private static byte[] FileBlob(string path) => Convert.FromBase64String(File.ReadAllText(path));

        [Trait("Rule", "S9-R2-20")]
        [Fact]
        public void Given_a_blob_from_each_implementation_When_decrypting_with_the_other_Then_native_and_bouncycastle_interoperate()
        {
            var plain = Encoding.UTF8.GetBytes("{\"k\":\"v\"}");
            var native = new Aes256GcmEncryptionStrategy(false);
            var bc = new Aes256GcmEncryptionStrategy(true);
            Assert.Equal(plain, bc.Decrypt(native.Encrypt(plain, Password), Password));
            Assert.Equal(plain, native.Decrypt(bc.Encrypt(plain, Password), Password));
#pragma warning disable CS0618
            Assert.Equal(plain, new BouncyCastleAes256EncryptionStrategy().Decrypt(native.Encrypt(plain, Password), Password));
            Assert.Equal(plain, new DefaultAes256EncryptionStrategy().Decrypt(bc.Encrypt(plain, Password), Password));
#pragma warning restore CS0618
        }

        [Trait("Rule", "S9-R2-21")]
        [Fact]
        public void Given_a_gcm_blob_When_any_single_byte_is_flipped_Then_it_is_never_accepted()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var plain = Encoding.UTF8.GetBytes("{\"k\":\"value value value value\"}");
            var blob = strategy.Encrypt(plain, Password);
            var accepted = new List<int>();
            for (var i = 0; i < blob.Length; i++)
            {
                var copy = (byte[])blob.Clone();
                copy[i] ^= 0x01;
                try
                {
                    var result = strategy.Decrypt(copy, Password);
                    accepted.Add(i);
                }
                catch (Exception) { }
            }
            Assert.True(accepted.Count == 0, "accepted flips at offsets " + string.Join(",", accepted));
        }

        [Trait("Rule", "S9-R2-22")]
        [Fact]
        public void Given_truncated_extended_or_degenerate_blobs_When_decrypting_Then_each_fails_with_a_controlled_exception()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var blob = strategy.Encrypt(Encoding.UTF8.GetBytes("{\"k\":\"v\"}"), Password);
            var uncontrolled = new Type[]
            {
                typeof(NullReferenceException), typeof(IndexOutOfRangeException), typeof(OverflowException),
                typeof(OutOfMemoryException), typeof(InvalidCastException), typeof(InvalidOperationException)
            };
            var offenders = new List<string>();
            var cases = new List<(string, byte[])>();
            for (var length = 0; length < blob.Length; length++) cases.Add(("truncate " + length, blob.Take(length).ToArray()));
            cases.Add(("extended", blob.Concat(new byte[] { 0 }).ToArray()));
            cases.Add(("extended 1MiB", blob.Concat(new byte[1 << 20]).ToArray()));
            cases.Add(("magic only", new byte[] { 0x4E, 0x57, 0x56, 1 }));
            foreach (var c in cases)
            {
                try
                {
                    strategy.Decrypt(c.Item2, Password);
                    offenders.Add(c.Item1 + " accepted");
                }
                catch (Exception ex)
                {
                    if (uncontrolled.Any(t => t.IsInstanceOfType(ex))) offenders.Add(c.Item1 + " => " + ex.GetType().Name);
                }
            }
            Assert.True(offenders.Count == 0, string.Join("; ", offenders));
        }

        [Trait("Rule", "S9-R2-23")]
        [Fact]
        public void Given_a_header_with_out_of_range_iterations_or_an_unknown_kdf_When_decrypting_Then_it_is_rejected_without_deriving()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var blob = strategy.Encrypt(Encoding.UTF8.GetBytes("{}"), Password);
            var offenders = new List<string>();
            foreach (var iterations in new[] { 0, -1, int.MinValue, 10_000_001, int.MaxValue })
            {
                var copy = (byte[])blob.Clone();
                System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(copy.AsSpan(5), iterations);
                try { strategy.Decrypt(copy, Password); offenders.Add(iterations + " accepted"); }
                catch (System.Security.Cryptography.CryptographicException ex) when (ex.Message.Contains("out of range")) { }
                catch (Exception ex) { offenders.Add(iterations + " reached derivation: " + ex.GetType().Name); }
            }
            var unknownKdf = (byte[])blob.Clone();
            unknownKdf[4] = 2;
            Assert.ThrowsAny<Exception>(() => strategy.Decrypt(unknownKdf, Password));
            Assert.True(offenders.Count == 0, string.Join("; ", offenders));
        }

        [Trait("Rule", "S9-R2-24")]
        [Fact]
        public void Given_a_header_claiming_exactly_the_maximum_iteration_count_When_decrypting_Then_it_is_accepted_for_derivation_and_one_more_is_rejected_before_it()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var blob = strategy.Encrypt(Encoding.UTF8.GetBytes("{}"), Password);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(blob.AsSpan(5), Aes256GcmEncryptionStrategy.MaxIterations);
            var atMax = Assert.ThrowsAny<Exception>(() => strategy.Decrypt(blob, Password));
            Assert.DoesNotContain("out of range", atMax.Message);

            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(blob.AsSpan(5), Aes256GcmEncryptionStrategy.MaxIterations + 1);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var aboveMax = Assert.ThrowsAny<Exception>(() => strategy.Decrypt(blob, Password));
            Assert.Contains("out of range", aboveMax.Message);
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 1024);
        }

        [Trait("Rule", "S9-R2-25")]
        [Fact]
        public void Given_repeated_encryptions_When_comparing_headers_Then_salt_and_nonce_never_repeat()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var salts = new HashSet<string>();
            var nonces = new HashSet<string>();
            for (var i = 0; i < 12; i++)
            {
                var blob = strategy.Encrypt(new byte[] { 1 }, Password);
                Assert.True(salts.Add(Convert.ToHexString(blob, 9, 16)));
                Assert.True(nonces.Add(Convert.ToHexString(blob, 25, 12)));
            }
        }

        [Trait("Rule", "S9-R2-26")]
        [Fact]
        public void Given_a_legacy_cbc_blob_whose_iv_starts_with_the_nwv_magic_When_decrypting_Then_it_is_still_read()
        {
            var plain = PlainVaultJson();
            var iv = new byte[16];
            RandomNumberGenerator.Fill(iv);
            iv[0] = 0x4E; iv[1] = 0x57; iv[2] = 0x56; iv[3] = 0x01;
            foreach (var sha1 in new[] { false, true })
            {
                var blob = LegacyCbc(plain, sha1, iv);
                Assert.Equal(plain, new Aes256GcmEncryptionStrategy().Decrypt(blob, Password));
            }
        }

        [Trait("Rule", "S9-R2-27")]
        [Fact]
        public async Task Given_a_gcm_vault_file_replaced_by_an_attacker_keystore_blob_When_unlocking_Then_it_is_refused_and_the_file_is_not_rewritten()
        {
            var service = new FileWalletVaultService(NewPath());
            await service.CreateNewAsync(Password);
            var path = NewPath();
            var attacker = new KeyStoreEncryptionStrategy().Encrypt(PlainVaultJson("attacker"), "attacker password");
            await File.WriteAllTextAsync(path, Convert.ToBase64String(attacker));
            var victim = new FileWalletVaultService(path);
            Assert.False(await victim.UnlockAsync(Password));
            Assert.Equal(attacker, FileBlob(path));
        }

        [Trait("Rule", "S9-R2-28")]
        [Fact]
        public async Task Given_each_legacy_vault_format_When_unlocking_Then_it_loads_migrates_to_gcm_and_unlocks_again()
        {
            var plain = PlainVaultJson();
            var formats = new Dictionary<string, byte[]>
            {
                ["cbc-sha256"] = LegacyCbc(plain, false),
                ["cbc-sha1-bc"] = LegacyCbc(plain, true),
                ["keystore"] = LegacyKeyStore(plain)
            };
            var failures = new List<string>();
            foreach (var format in formats)
            {
                var path = await WriteLegacy(format.Value);
                try
                {
                    var first = new FileWalletVaultService(path);
                    if (!await first.UnlockAsync(Password)) { failures.Add(format.Key + " first unlock failed"); continue; }
                    if (!Aes256GcmEncryptionStrategy.IsCurrentFormat(FileBlob(path))) failures.Add(format.Key + " not migrated");
                    if (File.Exists(path + ".tmp")) failures.Add(format.Key + " tmp left");
                    var second = new FileWalletVaultService(path);
                    if (!await second.UnlockAsync(Password)) failures.Add(format.Key + " second unlock failed");
                    var vault = second.GetCurrentVault();
                    if (vault == null || vault.Mnemonics.Count != 1 || vault.Accounts.Count != 1 || vault.HardwareDevices.Count != 1)
                        failures.Add(format.Key + " content lost");
                }
                catch (Exception ex) { failures.Add(format.Key + " threw " + ex.GetType().Name); }
            }
            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [Trait("Rule", "S9-R2-29")]
        [Fact]
        public async Task Given_a_legacy_vault_and_a_wrong_password_When_unlocking_Then_the_file_is_byte_identical_afterwards()
        {
            var blob = LegacyCbc(PlainVaultJson(), false);
            var path = await WriteLegacy(blob);
            var service = new FileWalletVaultService(path);
            Assert.False(await service.UnlockAsync("wrong"));
            Assert.Equal(blob, FileBlob(path));
            Assert.False(File.Exists(path + ".tmp"));
        }

        [Trait("Rule", "S9-R2-30")]
        [Fact]
        public async Task Given_a_service_configured_with_the_keystore_strategy_When_unlocking_twice_Then_the_second_unlock_still_works()
        {
            var path = NewPath();
            var creator = new FileWalletVaultService(path, new KeyStoreEncryptionStrategy());
            await creator.CreateNewAsync(Password);
            Assert.True(await new FileWalletVaultService(path, new KeyStoreEncryptionStrategy()).UnlockAsync(Password));
            Assert.True(await new FileWalletVaultService(path, new KeyStoreEncryptionStrategy()).UnlockAsync(Password),
                "vault locked out after the first unlock migrated it to a format KeyStoreEncryptionStrategy cannot read");
        }

        [Trait("Rule", "S9-R2-31")]
        [Fact]
        public async Task Given_a_service_configured_with_an_obsolete_aes_strategy_When_unlocking_a_legacy_vault_twice_Then_both_unlocks_work()
        {
#pragma warning disable CS0618
            var blob = LegacyCbc(PlainVaultJson(), false);
            var path = await WriteLegacy(blob);
            Assert.True(await new FileWalletVaultService(path, new DefaultAes256EncryptionStrategy()).UnlockAsync(Password));
            Assert.True(await new FileWalletVaultService(path, new DefaultAes256EncryptionStrategy()).UnlockAsync(Password));
            var bcBlob = LegacyCbc(PlainVaultJson(), true);
            var bcPath = await WriteLegacy(bcBlob);
            Assert.True(await new FileWalletVaultService(bcPath, new BouncyCastleAes256EncryptionStrategy()).UnlockAsync(Password));
            Assert.True(await new FileWalletVaultService(bcPath, new BouncyCastleAes256EncryptionStrategy()).UnlockAsync(Password));
#pragma warning restore CS0618
        }

        [Trait("Rule", "S9-R2-32")]
        [Fact]
        public async Task Given_concurrent_unlocks_of_one_legacy_vault_When_all_migrate_Then_the_file_is_a_valid_gcm_vault_and_no_tmp_remains()
        {
            var problems = new List<string>();
            for (var round = 0; round < 6; round++)
            {
                var path = await WriteLegacy(LegacyCbc(PlainVaultJson(), false));
                var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
                {
                    try { return await new FileWalletVaultService(path).UnlockAsync(Password); }
                    catch (Exception ex) { return ex.GetType().Name == null; }
                })));
                if (results.Any(r => !r)) problems.Add("round " + round + " a concurrent unlock failed");
                var blob = FileBlob(path);
                if (!Aes256GcmEncryptionStrategy.IsCurrentFormat(blob)) problems.Add("round " + round + " not migrated");
                if (File.Exists(path + ".tmp")) problems.Add("round " + round + " tmp left");
                if (!await new FileWalletVaultService(path).UnlockAsync(Password)) problems.Add("round " + round + " final unlock failed");
            }
            Assert.True(problems.Count == 0, string.Join("; ", problems));
        }

        [Trait("Rule", "S9-R2-33")]
        [Fact]
        public async Task Given_an_unwritable_tmp_path_When_unlocking_a_legacy_vault_Then_unlock_succeeds_and_the_original_is_untouched()
        {
            var blob = LegacyCbc(PlainVaultJson(), false);
            var path = await WriteLegacy(blob);
            Directory.CreateDirectory(path + ".tmp");
            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            Assert.Equal(blob, FileBlob(path));
        }

        [Trait("Rule", "S9-R2-34")]
        [Fact]
        public async Task Given_a_failed_unlock_When_it_happens_Then_nothing_is_written_to_console_or_the_working_directory()
        {
            var service = new FileWalletVaultService(NewPath());
            await service.CreateNewAsync(Password);
            var before = Directory.GetFiles(Directory.GetCurrentDirectory()).Length;
            var originalOut = Console.Out;
            var originalError = Console.Error;
            var captured = new StringWriter();
            Console.SetOut(captured);
            Console.SetError(captured);
            try
            {
                Assert.False(await service.UnlockAsync("wrong"));
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
            Assert.Equal(string.Empty, captured.ToString());
            Assert.Equal(before, Directory.GetFiles(Directory.GetCurrentDirectory()).Length);
        }

        [Trait("Rule", "S9-R2-35")]
        [Fact]
        public void Given_a_private_key_account_When_serialising_its_settings_Then_the_private_key_is_not_in_the_output()
        {
            var account = new PrivateKeyWalletAccount(Address, "cold", PrivateKeyHex);
            Assert.DoesNotContain(PrivateKeyHex, JsonSerializer.Serialize(account.Settings), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(PrivateKeyHex, Newtonsoft.Json.JsonConvert.SerializeObject(account.Settings), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(PrivateKeyHex, account.Settings.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Trait("Rule", "S9-R2-36")]
        [Fact]
        public async Task Given_a_legacy_cbc_vault_with_a_bit_flipped_in_ciphertext_When_unlocking_Then_the_flip_is_rejected()
        {
            var label = new string('A', 96);
            var plain = PlainVaultJson(label);
            var text = Encoding.UTF8.GetString(plain);
            var labelStart = text.IndexOf(label, StringComparison.Ordinal);
            var block = (labelStart / 16) + 1;
            var offset = 16 + block * 16 + 3;
            string persistedLabel = null;
            for (var attempt = 0; attempt < 600 && persistedLabel == null; attempt++)
            {
                var blob = LegacyCbc(plain, false);
                blob[offset] ^= 0x01;
                var path = await WriteLegacy(blob);
                var service = new FileWalletVaultService(path);
                if (await service.UnlockAsync(Password))
                {
                    var second = new FileWalletVaultService(path);
                    Assert.True(await second.UnlockAsync(Password));
                    persistedLabel = second.GetCurrentVault().HardwareDevices.Single().Label;
                }
            }
            Assert.True(persistedLabel == null, "a tampered legacy vault was accepted and then sealed under GCM; label now " + persistedLabel);
        }
    }
}
