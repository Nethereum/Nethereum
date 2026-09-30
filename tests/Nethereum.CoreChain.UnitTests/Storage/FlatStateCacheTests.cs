using System;
using System.Numerics;
using System.Threading;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class FlatStateCacheTests
    {
        private static string Addr(char c) => "0x" + new string(c, 40);

        private static byte[] Hash(byte fill) => Repeat(fill, 32);

        private static byte[] Repeat(byte fill, int length)
        {
            var b = new byte[length];
            for (int i = 0; i < length; i++) b[i] = fill;
            return b;
        }

        private static void AssertAccountValueEqual(Account expected, Account actual)
        {
            Assert.NotSame(expected, actual);
            Assert.Equal(expected.Nonce, actual.Nonce);
            Assert.Equal(expected.Balance, actual.Balance);
            Assert.Equal(expected.StateRoot, actual.StateRoot);
            Assert.Equal(expected.CodeHash, actual.CodeHash);
        }


        [Fact]
        public void Constructor_RejectsNonPositiveBudget()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlatStateCache(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlatStateCache(-1));
        }


        [Fact]
        public void KeyLayout_AccountStorageCode_HaveDistinctLengthsAndPrefixes()
        {
            var address = Addr('a');
            var accountKey = FlatStateCache.AccountKey(address);
            var storageKey = FlatStateCache.StorageKey(address, BigInteger.Zero);
            var codeKey = FlatStateCache.CodeKey(Hash(0xaa));

            Assert.Equal(21, accountKey.Length);
            Assert.Equal(53, storageKey.Length);
            Assert.Equal(33, codeKey.Length);

            Assert.Equal(0x00, accountKey[0]);
            Assert.Equal(0x01, storageKey[0]);
            Assert.Equal(0x02, codeKey[0]);
        }

        [Fact]
        public void KeyLayout_AdversarialByteOverlap_NeverCollides()
        {
            var cache = new FlatStateCache(1_000_000);
            var address = Addr('7');
            var addressBytes = new byte[20];
            for (int i = 0; i < 20; i++) addressBytes[i] = 0x77;
            var codeHash = new byte[32];
            Buffer.BlockCopy(addressBytes, 0, codeHash, 0, 20);

            var account = new Account { Balance = 1, Nonce = 1 };
            cache.PutAccount(address, account);
            cache.PutStorage(address, BigInteger.Zero, new byte[] { 0x01 });
            cache.PutCode(codeHash, new byte[] { 0x02 });

            Assert.True(cache.TryGetAccount(address, out var gotAccount));
            AssertAccountValueEqual(account, gotAccount);

            Assert.True(cache.TryGetStorage(address, BigInteger.Zero, out var gotStorage));
            Assert.Equal(new byte[] { 0x01 }, gotStorage);

            Assert.True(cache.TryGetCode(codeHash, out var gotCode));
            Assert.Equal(new byte[] { 0x02 }, gotCode);

            Assert.Equal(3, cache.Count);
        }


        [Fact]
        public void Account_PutTryGet_RoundTrips()
        {
            var cache = new FlatStateCache(1_000_000);
            var address = Addr('1');
            var account = new Account { Balance = 42, Nonce = 3, StateRoot = Hash(0x11), CodeHash = Hash(0x22) };

            Assert.False(cache.TryGetAccount(address, out _));
            cache.PutAccount(address, account);

            Assert.True(cache.TryGetAccount(address, out var got));
            AssertAccountValueEqual(account, got);
        }

        [Fact]
        public void Storage_PutTryGet_RoundTrips()
        {
            var cache = new FlatStateCache(1_000_000);
            var address = Addr('2');
            var slot = new BigInteger(7);
            var value = new byte[] { 0x0a, 0x0b, 0x0c };

            Assert.False(cache.TryGetStorage(address, slot, out _));
            cache.PutStorage(address, slot, value);

            Assert.True(cache.TryGetStorage(address, slot, out var got));
            Assert.Equal(value, got);
        }

        [Fact]
        public void Code_PutTryGet_RoundTrips()
        {
            var cache = new FlatStateCache(1_000_000);
            var codeHash = Hash(0x33);
            var code = new byte[] { 0xde, 0xad, 0xbe, 0xef };

            Assert.False(cache.TryGetCode(codeHash, out _));
            cache.PutCode(codeHash, code);

            Assert.True(cache.TryGetCode(codeHash, out var got));
            Assert.Equal(code, got);
        }


        [Fact]
        public void AccountTombstone_DistinguishableFromRealValueAndFromMiss()
        {
            var cache = new FlatStateCache(1_000_000);
            var deleted = Addr('3');
            var untouched = Addr('4');

            cache.PutAccountAbsent(deleted);

            Assert.True(cache.TryGetAccount(deleted, out var tombstone));
            Assert.Same(FlatStateCache.AccountTombstone, tombstone);

            var realEmptyAccount = new Account();
            Assert.NotSame(FlatStateCache.AccountTombstone, realEmptyAccount);

            Assert.False(cache.TryGetAccount(untouched, out var missed));
            Assert.Null(missed);
        }

        [Fact]
        public void StorageTombstone_DistinguishableFromRealValueAndFromMiss()
        {
            var cache = new FlatStateCache(1_000_000);
            var address = Addr('5');
            var clearedSlot = BigInteger.One;
            var untouchedSlot = new BigInteger(2);

            cache.PutStorageAbsent(address, clearedSlot);

            Assert.True(cache.TryGetStorage(address, clearedSlot, out var tombstone));
            Assert.Same(FlatStateCache.Tombstone, tombstone);

            var realEmptyValue = new byte[0];
            Assert.NotSame(FlatStateCache.Tombstone, realEmptyValue);

            Assert.False(cache.TryGetStorage(address, untouchedSlot, out var missed));
            Assert.Null(missed);
        }


        [Fact]
        public void RemoveOwnerStorage_DropsExactlyOneAddressSlots_NothingElse()
        {
            var cache = new FlatStateCache(1_000_000);
            var owner = Addr('6');
            var other = Addr('8');

            var ownerAccount = new Account { Balance = 5 };
            cache.PutAccount(owner, ownerAccount);
            cache.PutStorage(owner, BigInteger.Zero, new byte[] { 1 });
            cache.PutStorage(owner, BigInteger.One, new byte[] { 2 });
            cache.PutStorage(other, BigInteger.Zero, new byte[] { 3 });

            cache.RemoveOwnerStorage(owner);

            Assert.False(cache.TryGetStorage(owner, BigInteger.Zero, out _));
            Assert.False(cache.TryGetStorage(owner, BigInteger.One, out _));

            Assert.True(cache.TryGetStorage(other, BigInteger.Zero, out var otherValue));
            Assert.Equal(new byte[] { 3 }, otherValue);

            Assert.True(cache.TryGetAccount(owner, out var stillThere));
            AssertAccountValueEqual(ownerAccount, stillThere);
        }


        [Fact]
        public void Eviction_TinyBudget_EvictsLruTail_SurvivorsCorrect_ByteBudgetRespected()
        {
            var cache = new FlatStateCache(400);
            var addr1 = Addr('1');
            var addr2 = Addr('2');
            var addr3 = Addr('3');
            var slot = BigInteger.Zero;

            cache.PutStorage(addr1, slot, new byte[8]);
            cache.PutStorage(addr2, slot, new byte[8]);
            cache.PutStorage(addr3, slot, new byte[8]);

            Assert.True(cache.ApproxBytes <= 400);
            Assert.False(cache.TryGetStorage(addr1, slot, out _));
            Assert.True(cache.TryGetStorage(addr2, slot, out var v2));
            Assert.Equal(new byte[8], v2);
            Assert.True(cache.TryGetStorage(addr3, slot, out var v3));
            Assert.Equal(new byte[8], v3);
        }

        [Fact]
        public void Eviction_LargeCodeEntry_ByteBudgetAccountsForCodeSize()
        {
            var cache = new FlatStateCache(10_000_000);
            var before = cache.ApproxBytes;

            var codeHash = Hash(0x55);
            var code = new byte[24 * 1024];
            new Random(1).NextBytes(code);
            cache.PutCode(codeHash, code);

            var after = cache.ApproxBytes;
            var delta = after - before;

            Assert.True(delta >= code.Length, $"expected byte accounting to be at least the code length ({code.Length}), was {delta}");
            Assert.True(delta <= code.Length + 512, $"expected byte accounting to be close to the code length ({code.Length}), was {delta} (overhead too large)");
        }

        [Fact]
        public void Eviction_TinyBudget_ForcesEvictionEvenWithOneLargeCodeEntry()
        {
            var addr = Addr('9');
            var codeHash = Hash(0x66);
            var code = new byte[24 * 1024];

            var probe = new FlatStateCache(50_000_000);
            probe.PutStorage(addr, BigInteger.Zero, new byte[8]);
            var storageCost = probe.ApproxBytes;
            probe.PutCode(codeHash, code);
            var codeCost = probe.ApproxBytes - storageCost;
            var budget = codeCost + (storageCost / 2);

            var cache = new FlatStateCache(budget);
            cache.PutStorage(addr, BigInteger.Zero, new byte[8]);
            Assert.True(cache.TryGetStorage(addr, BigInteger.Zero, out _));

            cache.PutCode(codeHash, code);

            Assert.True(cache.ApproxBytes <= budget);
            Assert.False(cache.TryGetStorage(addr, BigInteger.Zero, out _));
            Assert.True(cache.TryGetCode(codeHash, out _));
        }

        [Fact]
        public void Eviction_RecentlyTouchedKeySurvivesOverUntouchedKey()
        {
            var cache = new FlatStateCache(310);
            var addr1 = Addr('1');
            var addr2 = Addr('2');
            var addr3 = Addr('3');
            var slot = BigInteger.Zero;

            cache.PutStorage(addr1, slot, new byte[8]);
            cache.PutStorage(addr2, slot, new byte[8]);

            Assert.True(cache.TryGetStorage(addr1, slot, out _));

            cache.PutStorage(addr3, slot, new byte[8]);

            Assert.True(cache.TryGetStorage(addr1, slot, out _));
            Assert.False(cache.TryGetStorage(addr2, slot, out _));
            Assert.True(cache.TryGetStorage(addr3, slot, out _));
        }


        [Fact]
        public void HitsAndMisses_IncrementCorrectly()
        {
            var cache = new FlatStateCache(1_000_000);
            var address = Addr('c');

            Assert.False(cache.TryGetAccount(address, out _));
            cache.PutAccount(address, new Account());
            Assert.True(cache.TryGetAccount(address, out _));
            Assert.True(cache.TryGetAccount(address, out _));
            Assert.False(cache.TryGetAccount(Addr('d'), out _));

            Assert.Equal(2, cache.Hits);
            Assert.Equal(2, cache.Misses);
        }

        [Fact]
        public void Clear_ResetsCountAndBytesAndCausesMisses()
        {
            var cache = new FlatStateCache(1_000_000);
            var address = Addr('e');
            cache.PutAccount(address, new Account());
            Assert.Equal(1, cache.Count);

            cache.Clear();

            Assert.Equal(0, cache.Count);
            Assert.Equal(0, cache.ApproxBytes);
            Assert.False(cache.TryGetAccount(address, out _));
        }


        [Fact]
        public void Concurrent_PutTryGetRemove_NoExceptions_FinalConsistency()
        {
            var cache = new FlatStateCache(50_000_000);
            const int threadCount = 8;
            const int iterations = 300;
            const string hexDigits = "0123456789abcdef";
            var sharedAddress = Addr('e');
            Exception captured = null;
            var threads = new Thread[threadCount];

            for (int t = 0; t < threadCount; t++)
            {
                int id = t;
                threads[t] = new Thread(() =>
                {
                    try
                    {
                        var ownAddress = Addr(hexDigits[id]);
                        for (int i = 0; i < iterations; i++)
                        {
                            cache.PutStorage(ownAddress, BigInteger.One, BitConverter.GetBytes(i));
                            cache.TryGetStorage(ownAddress, BigInteger.One, out _);

                            cache.PutStorage(sharedAddress, BigInteger.One, new[] { (byte)id });
                            cache.TryGetStorage(sharedAddress, BigInteger.One, out _);

                            if (i % 50 == 0)
                            {
                                cache.RemoveOwnerStorage(ownAddress);
                                cache.PutAccount(ownAddress, new Account { Nonce = i });
                                cache.TryGetAccount(ownAddress, out _);
                            }
                        }

                        cache.PutStorage(ownAddress, BigInteger.One, new byte[] { 0xFF, (byte)id });
                    }
                    catch (Exception ex)
                    {
                        captured = ex;
                    }
                });
            }

            foreach (var th in threads) th.Start();
            foreach (var th in threads) th.Join();

            Assert.Null(captured);

            for (int id = 0; id < threadCount; id++)
            {
                var ownAddress = Addr(hexDigits[id]);
                Assert.True(cache.TryGetStorage(ownAddress, BigInteger.One, out var value));
                Assert.Equal(new byte[] { 0xFF, (byte)id }, value);
            }
        }


        [Fact]
        public void TryGetAccount_ReturnsClone_MutatingItDoesNotPoisonTheCache()
        {
            var cache = new FlatStateCache(1 << 20);
            var addr = Addr('c');
            cache.PutAccount(addr, new Account
            {
                Nonce = new EvmUInt256(1),
                Balance = new EvmUInt256(100),
                StateRoot = Hash(0x11),
                CodeHash = Hash(0x22),
            });

            Assert.True(cache.TryGetAccount(addr, out var first));
            first.Nonce = new EvmUInt256(999);
            first.Balance = new EvmUInt256(999999);
            first.CodeHash = Hash(0xEE);
            first.StateRoot[0] = 0xFF;

            Assert.True(cache.TryGetAccount(addr, out var second));
            Assert.Equal(new EvmUInt256(1), second.Nonce);
            Assert.Equal(new EvmUInt256(100), second.Balance);
            Assert.Equal(Hash(0x22), second.CodeHash);
            Assert.Equal(Hash(0x11), second.StateRoot);
            Assert.NotSame(first, second);
        }


        [Fact]
        public void TryGetStorage_ReturnsClone_MutatingItDoesNotPoisonTheCache()
        {
            var cache = new FlatStateCache(1 << 20);
            var addr = Addr('f');
            var slot = BigInteger.Zero;
            var original = new byte[] { 0x01, 0x02, 0x03 };
            cache.PutStorage(addr, slot, original);

            Assert.True(cache.TryGetStorage(addr, slot, out var first));
            first[0] ^= 0xFF;

            Assert.True(cache.TryGetStorage(addr, slot, out var second));
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, second);
            Assert.NotEqual(first, second);
            Assert.NotSame(first, second);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, original);
        }

        [Fact]
        public void TryGetStorage_TombstoneStillRoundTripsByIdentity_NotCloned()
        {
            var cache = new FlatStateCache(1 << 20);
            var addr = Addr('0');
            var slot = BigInteger.Zero;
            cache.PutStorageAbsent(addr, slot);

            Assert.True(cache.TryGetStorage(addr, slot, out var got));
            Assert.Same(FlatStateCache.Tombstone, got);
        }
    }
}
