using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class PendingFlushFlatOverlayTests
    {
        private const string AddressA = "0x1111111111111111111111111111111111111111";
        private const string AddressB = "0x2222222222222222222222222222222222222222";

        private static Account MakeAccount(byte tag) => new Account
        {
            Nonce = new EvmUInt256((ulong)tag),
            Balance = new EvmUInt256((ulong)tag * 2),
            StateRoot = new byte[] { tag, tag, tag },
            CodeHash = new byte[] { tag, tag, tag, tag },
        };

        private static FlatStateBatch EmptyBatch() => new FlatStateBatch(null, null, null, null, null, null);

        private static byte[] CodeHash32(byte tag)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = tag;
            return h;
        }

        [Fact]
        public void Populate_NullOrEmptyBatch_IsNoOp_EverythingStillMisses()
        {
            var overlay = new PendingFlushFlatOverlay();
            overlay.Populate(1, null);
            overlay.Populate(1, EmptyBatch());

            Assert.False(overlay.TryGetAccount(AddressA, out _, out _));
            Assert.False(overlay.TryGetStorage(AddressA, 0, out _, out _));
            Assert.False(overlay.TryGetCode(CodeHash32(0x01), out _));
        }

        [Fact]
        public void Populate_Account_ThenRead_HitsWithClonedInstance_ThenClear_Misses()
        {
            var overlay = new PendingFlushFlatOverlay();
            var account = MakeAccount(0x42);
            var batch = new FlatStateBatch(
                deletedAccountAddresses: null,
                clearedStorageAddresses: null,
                nonZeroStorage: null,
                deletedSlots: null,
                accounts: new List<(string, Account)> { (AddressA, account) },
                code: null);

            overlay.Populate(block: 5, batch);

            Assert.True(overlay.TryGetAccount(AddressA, out var read, out var isDeleted));
            Assert.False(isDeleted);
            Assert.Equal(account.Nonce, read.Nonce);
            Assert.Equal(account.Balance, read.Balance);
            Assert.Equal(account.StateRoot, read.StateRoot);
            Assert.Equal(account.CodeHash, read.CodeHash);

            Assert.NotSame(account, read);
            read.StateRoot[0] = 0xFF;
            Assert.True(overlay.TryGetAccount(AddressA, out var readAgain, out _));
            Assert.NotEqual((byte)0xFF, readAgain.StateRoot[0]);

            Assert.False(overlay.TryGetAccount(AddressB, out _, out _));

            overlay.Clear(block: 4);
            Assert.True(overlay.TryGetAccount(AddressA, out _, out _));

            overlay.Clear(block: 5);
            Assert.False(overlay.TryGetAccount(AddressA, out var afterClear, out var afterClearDeleted));
            Assert.Null(afterClear);
            Assert.False(afterClearDeleted);
        }

        [Fact]
        public void DeletedAccount_ReportsDeleted_ButLiveWriteInTheSameBatchWins()
        {
            var overlay = new PendingFlushFlatOverlay();

            var recreated = MakeAccount(0x07);
            var batch = new FlatStateBatch(
                deletedAccountAddresses: new List<string> { AddressA, AddressB },
                clearedStorageAddresses: null,
                nonZeroStorage: null,
                deletedSlots: null,
                accounts: new List<(string, Account)> { (AddressA, recreated) },
                code: null);

            overlay.Populate(1, batch);

            Assert.True(overlay.TryGetAccount(AddressA, out var accountA, out var deletedA));
            Assert.False(deletedA);
            Assert.Equal(recreated.CodeHash, accountA.CodeHash);

            Assert.True(overlay.TryGetAccount(AddressB, out var accountB, out var deletedB));
            Assert.Null(accountB);
            Assert.True(deletedB);
        }

        [Fact]
        public void Storage_SpecificSlotWrite_BeatsWholeAddressClear()
        {
            var overlay = new PendingFlushFlatOverlay();
            var value = new byte[] { 0x01, 0x02, 0x03 };

            var batch = new FlatStateBatch(
                deletedAccountAddresses: null,
                clearedStorageAddresses: new List<string> { AddressA },
                nonZeroStorage: new List<(string, BigInteger, byte[])> { (AddressA, 7, value) },
                deletedSlots: null,
                accounts: null,
                code: null);

            overlay.Populate(2, batch);

            Assert.True(overlay.TryGetStorage(AddressA, 7, out var readValue, out var isCleared7));
            Assert.False(isCleared7);
            Assert.Equal(value, readValue);

            Assert.True(overlay.TryGetStorage(AddressA, 9, out var readValue9, out var isCleared9));
            Assert.Null(readValue9);
            Assert.True(isCleared9);

            Assert.False(overlay.TryGetStorage(AddressB, 7, out _, out _));
        }

        [Fact]
        public void Storage_DeletedSlot_ReportsCleared_MissForOtherSlots()
        {
            var overlay = new PendingFlushFlatOverlay();
            var batch = new FlatStateBatch(
                deletedAccountAddresses: null,
                clearedStorageAddresses: null,
                nonZeroStorage: null,
                deletedSlots: new List<(string, BigInteger)> { (AddressA, 3) },
                accounts: null,
                code: null);

            overlay.Populate(3, batch);

            Assert.True(overlay.TryGetStorage(AddressA, 3, out var value, out var isCleared));
            Assert.Null(value);
            Assert.True(isCleared);

            Assert.False(overlay.TryGetStorage(AddressA, 4, out _, out _));
        }

        [Fact]
        public void Code_HitReturnsExactBytes_MissForUnknownOrMalformedHash()
        {
            var overlay = new PendingFlushFlatOverlay();
            var hash = CodeHash32(0x9A);
            var code = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            var batch = new FlatStateBatch(
                deletedAccountAddresses: null,
                clearedStorageAddresses: null,
                nonZeroStorage: null,
                deletedSlots: null,
                accounts: null,
                code: new List<(byte[], byte[])> { (hash, code) });

            overlay.Populate(4, batch);

            Assert.True(overlay.TryGetCode(hash, out var readCode));
            Assert.Equal(code, readCode);

            Assert.False(overlay.TryGetCode(CodeHash32(0x01), out _));
            Assert.False(overlay.TryGetCode(null, out _));
            Assert.False(overlay.TryGetCode(new byte[] { 0x01 }, out _));
        }

        [Fact]
        public void Clear_DropsEveryCategory_NotJustTheOneExercised()
        {
            var overlay = new PendingFlushFlatOverlay();
            var account = MakeAccount(0x11);
            var hash = CodeHash32(0x55);
            var batch = new FlatStateBatch(
                deletedAccountAddresses: new List<string> { AddressB },
                clearedStorageAddresses: new List<string> { AddressA },
                nonZeroStorage: new List<(string, BigInteger, byte[])> { (AddressA, 1, new byte[] { 0x01 }) },
                deletedSlots: new List<(string, BigInteger)> { (AddressA, 2) },
                accounts: new List<(string, Account)> { (AddressA, account) },
                code: new List<(byte[], byte[])> { (hash, new byte[] { 0xAA }) });

            overlay.Populate(9, batch);

            Assert.True(overlay.TryGetAccount(AddressA, out _, out _));
            Assert.True(overlay.TryGetAccount(AddressB, out _, out _));
            Assert.True(overlay.TryGetStorage(AddressA, 1, out _, out _));
            Assert.True(overlay.TryGetStorage(AddressA, 2, out _, out _));
            Assert.True(overlay.TryGetCode(hash, out _));

            overlay.Clear(9);

            Assert.False(overlay.TryGetAccount(AddressA, out _, out _));
            Assert.False(overlay.TryGetAccount(AddressB, out _, out _));
            Assert.False(overlay.TryGetStorage(AddressA, 1, out _, out _));
            Assert.False(overlay.TryGetStorage(AddressA, 2, out _, out _));
            Assert.False(overlay.TryGetCode(hash, out _));
        }
    }
}
