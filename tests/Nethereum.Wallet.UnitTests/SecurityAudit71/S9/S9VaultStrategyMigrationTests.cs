using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Wallet.WalletAccounts;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9
{
    public class S9VaultStrategyMigrationTests : IDisposable
    {
        private const string Password = "vault password";
        private const string NewPassword = "changed password";
        private const string PrivateKeyHex = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d";
        private const string Address = "0x008aeeda4d805471df9b2a5b0f38a0c3bcba786b";
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "s9mig-" + Guid.NewGuid().ToString("N"));

        public S9VaultStrategyMigrationTests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        public static IEnumerable<object[]> Strategies()
        {
#pragma warning disable CS0618
            yield return new object[] { "gcm", (Func<IEncryptionStrategy>)(() => new Aes256GcmEncryptionStrategy()) };
            yield return new object[] { "default-aes", (Func<IEncryptionStrategy>)(() => new DefaultAes256EncryptionStrategy()) };
            yield return new object[] { "bouncycastle-aes", (Func<IEncryptionStrategy>)(() => new BouncyCastleAes256EncryptionStrategy()) };
#pragma warning restore CS0618
            yield return new object[] { "keystore", (Func<IEncryptionStrategy>)(() => new KeyStoreEncryptionStrategy()) };
        }

        private static async Task Exercise(Func<WalletVaultServiceBase> open)
        {
            var creator = open();
            await creator.CreateNewVaultWithAccountAsync(Password, new PrivateKeyWalletAccount(Address, "cold", PrivateKeyHex));

            var first = open();
            Assert.True(await first.UnlockAsync(Password));
            var second = open();
            Assert.True(await second.UnlockAsync(Password));
            Assert.Single(await second.GetAccountsAsync());

            await second.SaveAsync(NewPassword);
            var changed = open();
            Assert.False(await changed.UnlockAsync(Password));
            Assert.True(await changed.UnlockAsync(NewPassword));
            Assert.Single(await changed.GetAccountsAsync());
        }

        [Trait("Rule", "S9-R2-C")]
        [Theory]
        [MemberData(nameof(Strategies))]
        public async Task Given_each_first_party_strategy_on_a_file_vault_When_unlocking_twice_and_changing_the_password_Then_the_vault_stays_readable(string name, Func<IEncryptionStrategy> create)
        {
            var path = Path.Combine(_dir, name + ".vault");
            await Exercise(() => new FileWalletVaultService(path, create()));
        }

        [Trait("Rule", "S9-R2-C")]
        [Theory]
        [MemberData(nameof(Strategies))]
        public async Task Given_each_first_party_strategy_on_an_injected_strategy_storage_vault_When_unlocking_twice_and_changing_the_password_Then_the_vault_stays_readable(string name, Func<IEncryptionStrategy> create)
        {
            var storage = new Dictionary<string, string>();
            await Exercise(() => new InjectedStrategyVaultService(storage, create()));
        }

        [Trait("Rule", "S9-R2-C")]
        [Fact]
        public async Task Given_the_keystore_strategy_When_unlocking_Then_the_vault_stays_in_the_keystore_format()
        {
            var path = Path.Combine(_dir, "keystore-format.vault");
            await new FileWalletVaultService(path, new KeyStoreEncryptionStrategy()).CreateNewAsync(Password);
            var before = File.ReadAllText(path);
            Assert.True(await new FileWalletVaultService(path, new KeyStoreEncryptionStrategy()).UnlockAsync(Password));
            Assert.Equal(before, File.ReadAllText(path));
        }

        private sealed class InjectedStrategyVaultService : WalletVaultServiceBase
        {
            private readonly Dictionary<string, string> _storage;
            private readonly IEncryptionStrategy _strategy;

            public InjectedStrategyVaultService(Dictionary<string, string> storage, IEncryptionStrategy strategy)
            {
                _storage = storage;
                _strategy = strategy;
            }

            protected override IEncryptionStrategy GetEncryptionStrategy() => _strategy;
            public override Task<bool> VaultExistsAsync() => Task.FromResult(_storage.ContainsKey("vault"));
            protected override Task ResetStorageAsync() { _storage.Remove("vault"); return Task.CompletedTask; }
            protected override Task<string> GetEncryptedAsync() => Task.FromResult(_storage.TryGetValue("vault", out var v) ? v : null);
            protected override Task SaveEncryptedAsync(string encrypted) { _storage["vault"] = encrypted; return Task.CompletedTask; }
        }
    }
}
