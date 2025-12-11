using System;
using System.Numerics;
using Nethereum.ChainStateVerification.Caching;
using Nethereum.Model;
using Xunit;

namespace Nethereum.ChainStateVerification.Tests.Caching
{
    public class VerifiedStateCacheTests : IDisposable
    {
        private readonly VerifiedStateCache _cache;

        public VerifiedStateCacheTests()
        {
            _cache = new VerifiedStateCache();
        }

        public void Dispose()
        {
            _cache?.Dispose();
        }

        [Fact]
        public void SetBlock_StoresBlockInformation()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };

            _cache.SetBlock(12345, stateRoot);

            Assert.Equal(12345UL, _cache.BlockNumber);
            Assert.Equal(stateRoot, _cache.StateRoot);
        }

        [Fact]
        public void SetBlock_ClearsAccountsOnBlockChange()
        {
            var stateRoot1 = new byte[] { 1, 2, 3, 4 };
            var stateRoot2 = new byte[] { 5, 6, 7, 8 };
            var account = new Account { Balance = BigInteger.One, Nonce = BigInteger.One };

            _cache.SetBlock(12345, stateRoot1);
            _cache.SetAccount("0x1234", account);

            Assert.True(_cache.TryGetAccount("0x1234", out _));

            _cache.SetBlock(12346, stateRoot2);

            Assert.False(_cache.TryGetAccount("0x1234", out _));
        }

        [Fact]
        public void SetBlock_SameBlockDoesNotClearCache()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            var account = new Account { Balance = BigInteger.One, Nonce = BigInteger.One };

            _cache.SetBlock(12345, stateRoot);
            _cache.SetAccount("0x1234", account);

            _cache.SetBlock(12345, stateRoot);

            Assert.True(_cache.TryGetAccount("0x1234", out _));
        }

        [Fact]
        public void TryGetAccount_ReturnsFalseForMissingAddress()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            _cache.SetBlock(12345, stateRoot);

            var result = _cache.TryGetAccount("0x1234", out var state);

            Assert.False(result);
            Assert.Null(state);
        }

        [Fact]
        public void TryGetAccount_ReturnsFalseForNullOrEmptyAddress()
        {
            Assert.False(_cache.TryGetAccount(null, out _));
            Assert.False(_cache.TryGetAccount("", out _));
            Assert.False(_cache.TryGetAccount("  ", out _));
        }

        [Fact]
        public void SetAccount_StoresAndRetrievesAccount()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            var account = new Account
            {
                Balance = new BigInteger(1000),
                Nonce = new BigInteger(5),
                CodeHash = new byte[] { 10, 20, 30 },
                StateRoot = new byte[] { 40, 50, 60 }
            };

            _cache.SetBlock(12345, stateRoot);
            _cache.SetAccount("0x1234", account);

            Assert.True(_cache.TryGetAccount("0x1234", out var retrieved));
            Assert.Equal(account.Balance, retrieved.Account.Balance);
            Assert.Equal(account.Nonce, retrieved.Account.Nonce);
        }

        [Fact]
        public void SetAccount_IsCaseInsensitive()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            var account = new Account { Balance = BigInteger.One };

            _cache.SetBlock(12345, stateRoot);
            _cache.SetAccount("0xABCD", account);

            Assert.True(_cache.TryGetAccount("0xabcd", out _));
            Assert.True(_cache.TryGetAccount("0xABCD", out _));
        }

        [Fact]
        public void TryGetCode_ReturnsFalseForUncachedCode()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            _cache.SetBlock(12345, stateRoot);

            var result = _cache.TryGetCode("0x1234", out var code);

            Assert.False(result);
            Assert.Null(code);
        }

        [Fact]
        public void SetCode_StoresAndRetrievesCode()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            var code = new byte[] { 0x60, 0x80, 0x60, 0x40, 0x52 };

            _cache.SetBlock(12345, stateRoot);
            _cache.SetCode("0x1234", code);

            Assert.True(_cache.TryGetCode("0x1234", out var retrieved));
            Assert.Equal(code, retrieved);
        }

        [Fact]
        public void SetCode_PersistsAcrossBlockChanges()
        {
            var stateRoot1 = new byte[] { 1, 2, 3, 4 };
            var stateRoot2 = new byte[] { 5, 6, 7, 8 };
            var code = new byte[] { 0x60, 0x80 };

            _cache.SetBlock(12345, stateRoot1);
            _cache.SetCode("0x1234", code);

            _cache.SetBlock(12346, stateRoot2);

            Assert.True(_cache.TryGetCode("0x1234", out var retrieved));
            Assert.Equal(code, retrieved);
        }

        [Fact]
        public void TryGetStorage_ReturnsFalseForUncachedSlot()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            var account = new Account { Balance = BigInteger.One };

            _cache.SetBlock(12345, stateRoot);
            _cache.SetAccount("0x1234", account);

            var result = _cache.TryGetStorage("0x1234", "0x0", out var value);

            Assert.False(result);
            Assert.Null(value);
        }

        [Fact]
        public void SetStorage_StoresAndRetrievesStorageValue()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            var storageValue = new byte[] { 0, 0, 0, 1 };

            _cache.SetBlock(12345, stateRoot);
            _cache.SetStorage("0x1234", "0x0", storageValue);

            Assert.True(_cache.TryGetStorage("0x1234", "0x0", out var retrieved));
            Assert.Equal(storageValue, retrieved);
        }

        [Fact]
        public void SetStorage_IsClearedOnBlockChange()
        {
            var stateRoot1 = new byte[] { 1, 2, 3, 4 };
            var stateRoot2 = new byte[] { 5, 6, 7, 8 };
            var storageValue = new byte[] { 0, 0, 0, 1 };

            _cache.SetBlock(12345, stateRoot1);
            _cache.SetStorage("0x1234", "0x0", storageValue);

            Assert.True(_cache.TryGetStorage("0x1234", "0x0", out _));

            _cache.SetBlock(12346, stateRoot2);

            Assert.False(_cache.TryGetStorage("0x1234", "0x0", out _));
        }

        [Fact]
        public void SetStorage_SlotIsCaseInsensitive()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            var storageValue = new byte[] { 0, 0, 0, 1 };

            _cache.SetBlock(12345, stateRoot);
            _cache.SetStorage("0x1234", "0xABC", storageValue);

            Assert.True(_cache.TryGetStorage("0x1234", "0xabc", out _));
            Assert.True(_cache.TryGetStorage("0x1234", "0xABC", out _));
        }

        [Fact]
        public void Clear_RemovesAllCachedData()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            var account = new Account { Balance = BigInteger.One };
            var code = new byte[] { 0x60, 0x80 };
            var storage = new byte[] { 0, 0, 0, 1 };

            _cache.SetBlock(12345, stateRoot);
            _cache.SetAccount("0x1234", account);
            _cache.SetStorage("0x1234", "0x0", storage);

            _cache.Clear();

            Assert.Equal(0UL, _cache.BlockNumber);
            Assert.Null(_cache.StateRoot);
            Assert.False(_cache.TryGetAccount("0x1234", out _));
            Assert.False(_cache.TryGetStorage("0x1234", "0x0", out _));
        }

        [Fact]
        public void SetBlock_ThrowsOnNullStateRoot()
        {
            Assert.Throws<ArgumentNullException>(() => _cache.SetBlock(12345, null));
        }

        [Fact]
        public void SetAccount_ThrowsOnInvalidInput()
        {
            Assert.Throws<ArgumentException>(() => _cache.SetAccount(null, new Account()));
            Assert.Throws<ArgumentException>(() => _cache.SetAccount("", new Account()));
            Assert.Throws<ArgumentNullException>(() => _cache.SetAccount("0x1234", null));
        }

        [Fact]
        public void SetStorage_ThrowsOnInvalidInput()
        {
            Assert.Throws<ArgumentException>(() => _cache.SetStorage(null, "0x0", new byte[] { 1 }));
            Assert.Throws<ArgumentException>(() => _cache.SetStorage("0x1234", null, new byte[] { 1 }));
            Assert.Throws<ArgumentException>(() => _cache.SetStorage("0x1234", "", new byte[] { 1 }));
        }

        [Fact]
        public void MultipleStorageSlots_IndependentlyRetrieved()
        {
            var stateRoot = new byte[] { 1, 2, 3, 4 };
            var value0 = new byte[] { 0, 0, 0, 1 };
            var value1 = new byte[] { 0, 0, 0, 2 };
            var value2 = new byte[] { 0, 0, 0, 3 };

            _cache.SetBlock(12345, stateRoot);
            _cache.SetStorage("0x1234", "0x0", value0);
            _cache.SetStorage("0x1234", "0x1", value1);
            _cache.SetStorage("0x1234", "0x2", value2);

            Assert.True(_cache.TryGetStorage("0x1234", "0x0", out var r0));
            Assert.True(_cache.TryGetStorage("0x1234", "0x1", out var r1));
            Assert.True(_cache.TryGetStorage("0x1234", "0x2", out var r2));

            Assert.Equal(value0, r0);
            Assert.Equal(value1, r1);
            Assert.Equal(value2, r2);
        }
    }
}
