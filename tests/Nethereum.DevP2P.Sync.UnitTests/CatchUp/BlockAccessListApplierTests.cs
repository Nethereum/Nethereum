using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.CatchUp
{
    public class BlockAccessListApplierTests
    {
        private readonly InMemoryStateStore _codeStore = new InMemoryStateStore();

        private BlockAccessListApplier Applier(FakeFlatWriter flat) => new BlockAccessListApplier(flat, _codeStore);

        private static byte[] AccountHash(string address) => Sha3Keccack.Current.CalculateHash(address.HexToByteArray());
        private static string Addr(char c) => "0x" + new string(c, 40);

        [Fact]
        public async Task Given_BalanceAndNonceChanges_When_Applied_Then_PostValuesAreLastWriteByIndexNotByListOrder()
        {
            var target = new FakeFlatWriter();
            var account = new AccountChanges(Addr('1'));
            account.BalanceChanges.Add(new BalanceChange(3, new EvmUInt256(500)));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(100)));
            account.NonceChanges.Add(new NonceChange(5, 7));
            account.NonceChanges.Add(new NonceChange(2, 4));

            await Applier(target).ApplyAsync(new[] { account }, FakeFrontier.All);

            var saved = target.Accounts[AccountHash(Addr('1')).ToHex()];
            Assert.Equal(new EvmUInt256(500), saved.Balance);
            Assert.Equal(new EvmUInt256(7), saved.Nonce);
        }

        [Fact]
        public async Task Given_MultipleCodeChanges_When_Applied_Then_HighestIndexCodeHashedAndPersisted()
        {
            var target = new FakeFlatWriter();
            var winningCode = new byte[] { 0x60, 0x00, 0x60, 0x00 };
            var earlierCode = new byte[] { 0xFF };
            var account = new AccountChanges(Addr('a'));
            account.NonceChanges.Add(new NonceChange(0, 1));
            account.CodeChanges.Add(new CodeChange(2, winningCode));
            account.CodeChanges.Add(new CodeChange(0, earlierCode));

            await Applier(target).ApplyAsync(new[] { account }, FakeFrontier.All);

            var codeHash = Sha3Keccack.Current.CalculateHash(winningCode);
            var saved = target.Accounts[AccountHash(Addr('a')).ToHex()];
            Assert.Equal(codeHash.ToHex(), saved.CodeHash.ToHex());
            Assert.Equal(winningCode.ToHex(), (await _codeStore.GetCodeAsync(codeHash)).ToHex());
            Assert.Null(await _codeStore.GetCodeAsync(Sha3Keccack.Current.CalculateHash(earlierCode)));
        }

        [Fact]
        public async Task Given_ExistingAccountWithStorageRoot_When_BalanceChanged_Then_StorageRootLeftStale()
        {
            var target = new FakeFlatWriter();
            var hash = AccountHash(Addr('8'));
            var storageRoot = Sha3Keccack.Current.CalculateHash(new byte[] { 0xAB });
            target.Accounts[hash.ToHex()] = new Account { Balance = new EvmUInt256(1), StateRoot = storageRoot };
            var account = new AccountChanges(Addr('8'));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(777)));

            await Applier(target).ApplyAsync(new[] { account }, FakeFrontier.All);

            var saved = target.Accounts[hash.ToHex()];
            Assert.Equal(new EvmUInt256(777), saved.Balance);
            Assert.Equal(storageRoot.ToHex(), saved.StateRoot.ToHex());
        }

        [Fact]
        public async Task Given_AccountUnfetchedButSlotFetched_When_Applied_Then_SlotWritten_AndAccountSkipped()
        {
            var target = new FakeFlatWriter();
            var account = new AccountChanges(Addr('7'));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(100)));
            var slot = new SlotChanges(new EvmUInt256(1));
            slot.Changes.Add(new StorageChange(0, new EvmUInt256(5)));
            account.StorageChanges.Add(slot);

            await Applier(target).ApplyAsync(
                new[] { account }, new FakeFrontier { Account = false, Storage = true });

            Assert.Empty(target.Accounts);
            Assert.Single(target.SavedStorage);
        }

        [Fact]
        public async Task Given_EmptyChangesForNewAccount_When_Applied_Then_NothingWritten()
        {
            var target = new FakeFlatWriter();
            var account = new AccountChanges(Addr('2'));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(0)));

            await Applier(target).ApplyAsync(new[] { account }, FakeFrontier.All);

            Assert.Empty(target.Accounts);
            Assert.Empty(target.DeletedAccounts);
        }

        [Fact]
        public async Task Given_ExistingAccountDrainedToEmpty_When_Applied_Then_AccountDeleted()
        {
            var target = new FakeFlatWriter();
            var hash = AccountHash(Addr('3'));
            target.Accounts[hash.ToHex()] = new Account { Balance = new EvmUInt256(999), Nonce = new EvmUInt256(2) };
            var account = new AccountChanges(Addr('3'));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(0)));
            account.NonceChanges.Add(new NonceChange(1, 0));

            await Applier(target).ApplyAsync(new[] { account }, FakeFrontier.All);

            Assert.DoesNotContain(hash.ToHex(), target.Accounts.Keys);
            Assert.Contains(hash.ToHex(), target.DeletedAccounts);
        }

        [Fact]
        public async Task Given_AccountNotInTaskFrontier_When_Applied_Then_AccountSkipped()
        {
            var target = new FakeFlatWriter();
            var account = new AccountChanges(Addr('4'));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(100)));

            await Applier(target).ApplyAsync(new[] { account }, new FakeFrontier { Account = false });

            Assert.Empty(target.Accounts);
        }

        [Fact]
        public async Task Given_StorageWrites_When_Applied_Then_NonZeroSavedTrimmed_AndZeroDeleted()
        {
            var target = new FakeFlatWriter();
            var account = new AccountChanges(Addr('5'));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(1)));
            var nonZero = new SlotChanges(new EvmUInt256(11));
            nonZero.Changes.Add(new StorageChange(3, new EvmUInt256(42)));
            nonZero.Changes.Add(new StorageChange(0, new EvmUInt256(9)));
            account.StorageChanges.Add(nonZero);
            var zeroed = new SlotChanges(new EvmUInt256(12));
            zeroed.Changes.Add(new StorageChange(0, new EvmUInt256(0)));
            account.StorageChanges.Add(zeroed);

            await Applier(target).ApplyAsync(new[] { account }, FakeFrontier.All);

            Assert.Single(target.SavedStorage);
            Assert.Equal("2a", Assert.Single(target.SavedStorage.Values).ToHex());
            Assert.Single(target.DeletedStorage);
        }

        [Fact]
        public async Task Given_SlotNotInStorageFrontier_When_Applied_Then_SlotSkipped()
        {
            var target = new FakeFlatWriter();
            var account = new AccountChanges(Addr('6'));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(1)));
            var slot = new SlotChanges(new EvmUInt256(1));
            slot.Changes.Add(new StorageChange(0, new EvmUInt256(7)));
            account.StorageChanges.Add(slot);

            await Applier(target).ApplyAsync(new[] { account }, new FakeFrontier { Storage = false });

            Assert.Empty(target.SavedStorage);
            Assert.Empty(target.DeletedStorage);
        }

        [Fact]
        public async Task Given_ASlotThatAlreadyHoldsANonZeroRow_When_TheBalClearsIt_Then_TheRowIsRemovedNotLeftStale()
        {
            var target = new FakeFlatWriter();
            var hash = AccountHash(Addr('9'));
            var slotHash = Sha3Keccack.Current.CalculateHash(new EvmUInt256(21).ToBigEndian());
            var key = hash.ToHex() + ":" + slotHash.ToHex();
            target.SavedStorage[key] = new byte[] { 0x7B };

            var account = new AccountChanges(Addr('9'));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(1)));
            var cleared = new SlotChanges(new EvmUInt256(21));
            cleared.Changes.Add(new StorageChange(0, new EvmUInt256(0)));
            account.StorageChanges.Add(cleared);

            await Applier(target).ApplyAsync(new[] { account }, FakeFrontier.All);

            Assert.DoesNotContain(key, target.SavedStorage.Keys);
            Assert.Contains(key, target.DeletedStorage);
        }

        [Fact]
        public async Task Given_ASlotThatAlreadyHoldsANonZeroRow_When_TheBalSetsANewNonZeroValue_Then_TheRowIsOverwrittenNotRemoved()
        {
            var target = new FakeFlatWriter();
            var hash = AccountHash(Addr('9'));
            var slotHash = Sha3Keccack.Current.CalculateHash(new EvmUInt256(21).ToBigEndian());
            var key = hash.ToHex() + ":" + slotHash.ToHex();
            target.SavedStorage[key] = new byte[] { 0x7B };

            var account = new AccountChanges(Addr('9'));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(1)));
            var changed = new SlotChanges(new EvmUInt256(21));
            changed.Changes.Add(new StorageChange(0, new EvmUInt256(0x2A)));
            account.StorageChanges.Add(changed);

            await Applier(target).ApplyAsync(new[] { account }, FakeFrontier.All);

            Assert.Equal("2a", target.SavedStorage[key].ToHex());
            Assert.Empty(target.DeletedStorage);
        }

        private sealed class FakeFrontier : ISnapTaskFrontier
        {
            public static readonly FakeFrontier All = new FakeFrontier();
            public bool Account = true;
            public bool Storage = true;
            public bool IsAccountFetched(byte[] accountHash) => Account;
            public bool IsStorageFetched(byte[] accountHash, byte[] slotHash) => Storage;
        }

        private sealed class FakeFlatWriter : ISnapFlatStateWriter
        {
            public readonly Dictionary<string, Account> Accounts = new();
            public readonly HashSet<string> DeletedAccounts = new();
            public readonly Dictionary<string, byte[]> SavedStorage = new();
            public readonly HashSet<string> DeletedStorage = new();

            public Task<Account> GetAccountByHashAsync(byte[] accountHash)
                => Task.FromResult(Accounts.TryGetValue(accountHash.ToHex(), out var a) ? a : null);

            public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
            {
                Accounts[accountHash.ToHex()] = account;
                return Task.CompletedTask;
            }

            public Task DeleteAccountByHashAsync(byte[] accountHash)
            {
                Accounts.Remove(accountHash.ToHex());
                DeletedAccounts.Add(accountHash.ToHex());
                return Task.CompletedTask;
            }

            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotHash, byte[] value)
            {
                var key = accountHash.ToHex() + ":" + slotHash.ToHex();
                if (SnapFlatStorageValue.ClearsSlot(value))
                {
                    SavedStorage.Remove(key);
                    DeletedStorage.Add(key);
                }
                else
                {
                    SavedStorage[key] = value;
                }
                return Task.CompletedTask;
            }
        }
    }
}
