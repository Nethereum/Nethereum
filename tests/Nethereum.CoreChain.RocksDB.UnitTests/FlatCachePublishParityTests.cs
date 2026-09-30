using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FlatCachePublishParityTests : IDisposable
    {
        private readonly string _dir;

        public FlatCachePublishParityTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-cacheparity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string AddrB = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string AddrC = "0xcccccccccccccccccccccccccccccccccccccc";
        private const string AddrD = "0xdddddddddddddddddddddddddddddddddddddd";
        private const string AddrE = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

        private static byte[] CodeHash(byte b)
        {
            var h = new byte[32];
            h[0] = b;
            return h;
        }

        private static async Task AssertAccountParityAsync(FlatStateCache cache, RocksDbStateStore store, string address, string label)
        {
            var disk = await store.GetAccountAsync(address).ConfigureAwait(false);
            var hit = cache.TryGetAccount(address, out var cached);
            Assert.True(hit, $"[{label}] cache miss for account {address} after flush — every committed key must be published");

            if (disk == null)
            {
                Assert.True(ReferenceEquals(cached, FlatStateCache.AccountTombstone),
                    $"[{label}] account {address} is absent on disk but the cache did not return the AccountTombstone sentinel");
            }
            else
            {
                Assert.False(ReferenceEquals(cached, FlatStateCache.AccountTombstone),
                    $"[{label}] account {address} exists on disk but the cache reports the deleted-account tombstone (stale hit)");
                Assert.Equal(disk.Balance, cached.Balance);
                Assert.Equal(disk.Nonce, cached.Nonce);
                Assert.Equal(disk.CodeHash ?? Array.Empty<byte>(), cached.CodeHash ?? Array.Empty<byte>());
            }
        }

        private static async Task AssertStorageParityAsync(FlatStateCache cache, RocksDbStateStore store, string address, BigInteger slot, string label)
        {
            var disk = await store.GetStorageAsync(address, slot).ConfigureAwait(false);
            var hit = cache.TryGetStorage(address, slot, out var cached);
            Assert.True(hit, $"[{label}] cache miss for storage {address}:{slot} after flush — every committed key must be published");

            bool diskAbsent = disk == null || disk.Length == 0;
            if (diskAbsent)
            {
                Assert.True(ReferenceEquals(cached, FlatStateCache.Tombstone),
                    $"[{label}] slot {address}:{slot} is absent/zero on disk but the cache did not return the Tombstone sentinel");
            }
            else
            {
                Assert.False(ReferenceEquals(cached, FlatStateCache.Tombstone),
                    $"[{label}] slot {address}:{slot} has a real value on disk but the cache reports the tombstone (stale hit)");
                Assert.Equal(disk, cached);
            }
        }

        private static async Task AssertClearedStorageNeverStalePositiveAsync(FlatStateCache cache, RocksDbStateStore store, string address, BigInteger slot, string label)
        {
            var disk = await store.GetStorageAsync(address, slot).ConfigureAwait(false);
            Assert.True(disk == null || disk.Length == 0, $"[{label}] test-setup error: {address}:{slot} expected absent on disk after a clear");

            if (cache.TryGetStorage(address, slot, out var cached))
            {
                Assert.True(ReferenceEquals(cached, FlatStateCache.Tombstone),
                    $"[{label}] slot {address}:{slot} was address-cleared but the cache still returns a REAL (stale) value — R3 bug");
            }
        }

        private static async Task AssertCodeParityAsync(FlatStateCache cache, RocksDbStateStore store, byte[] codeHash, string label)
        {
            var disk = await store.GetCodeAsync(codeHash).ConfigureAwait(false);
            var hit = cache.TryGetCode(codeHash, out var cached);
            Assert.True(hit, $"[{label}] cache miss for code {Convert.ToHexString(codeHash)} after flush");
            Assert.Equal(disk, cached);
        }

        [Fact]
        public async Task CacheMirrorsCommittedDisk_AcrossFullWorkload_IncludingTombstonesAndClears()
        {
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            var raw = new RocksDbStateStore(mgr, accountLayout: BinaryPackedAccountLayout.Instance);
            var cache = new FlatStateCache(budgetBytes: 4 * 1024 * 1024);
            var store = new BufferedFlatStateStore(raw, cache);

            const string addrG = "0x2222222222222222222222222222222222222222";
            var ch1 = CodeHash(0x11);
            var ch2 = CodeHash(0x22);
            var code1 = new byte[] { 0x60, 0x00, 0x60, 0x00 };
            var code2 = new byte[] { 0x60, 0x01 };

            store.BeginBuffering();
            await store.SaveAccountAsync(AddrA, new Account { Balance = 100, Nonce = 1, CodeHash = ch1 });
            await store.SaveAccountAsync(AddrB, new Account { Balance = 200, Nonce = 1, CodeHash = null });
            await store.SaveAccountAsync(AddrC, new Account { Balance = 300, Nonce = 1, CodeHash = null });
            await store.SaveAccountAsync(AddrD, new Account { Balance = 400, Nonce = 1, CodeHash = null });
            await store.SaveAccountAsync(AddrE, new Account { Balance = 500, Nonce = 1, CodeHash = null });
            await store.SaveAccountAsync(addrG, new Account { Balance = 600, Nonce = 1, CodeHash = ch1 });
            await store.SaveCodeAsync(ch1, code1);
            await store.SaveStorageAsync(AddrA, 1, new byte[] { 0xA1 });
            await store.SaveStorageAsync(AddrB, 1, new byte[] { 0xB1 });
            await store.SaveStorageAsync(AddrC, 1, new byte[] { 0xC1 });
            await store.SaveStorageAsync(AddrC, 2, new byte[] { 0xC2 });
            await store.SaveStorageAsync(addrG, 1, new byte[] { 0xD1 });
            await store.FlushBufferAsync();

            await AssertAccountParityAsync(cache, raw, AddrA, "block1");
            await AssertAccountParityAsync(cache, raw, AddrB, "block1");
            await AssertAccountParityAsync(cache, raw, AddrC, "block1");
            await AssertAccountParityAsync(cache, raw, AddrD, "block1");
            await AssertAccountParityAsync(cache, raw, AddrE, "block1");
            await AssertAccountParityAsync(cache, raw, addrG, "block1");
            await AssertCodeParityAsync(cache, raw, ch1, "block1");
            await AssertStorageParityAsync(cache, raw, AddrA, 1, "block1");
            await AssertStorageParityAsync(cache, raw, AddrB, 1, "block1");
            await AssertStorageParityAsync(cache, raw, AddrC, 1, "block1");
            await AssertStorageParityAsync(cache, raw, AddrC, 2, "block1");
            await AssertStorageParityAsync(cache, raw, addrG, 1, "block1");

            store.BeginBuffering();
            await store.SaveAccountAsync(AddrA, new Account { Balance = 150, Nonce = 2, CodeHash = ch1 });
            await store.SaveStorageAsync(AddrA, 1, new byte[] { 0xA9 });
            await store.SaveStorageAsync(AddrB, 1, Array.Empty<byte>());
            await store.ClearStorageAsync(AddrC);
            await store.DeleteAccountAsync(AddrD);
            await store.DeleteAccountAsync(AddrE);
            await store.SaveAccountAsync(AddrE, new Account { Balance = 999, Nonce = 1, CodeHash = ch2 });
            await store.SaveStorageAsync(AddrE, 7, new byte[] { 0xE7 });
            await store.ClearStorageAsync(addrG);
            await store.SaveStorageAsync(addrG, 2, new byte[] { 0xD2 });
            await store.SaveCodeAsync(ch2, code2);
            await store.FlushBufferAsync();

            await AssertAccountParityAsync(cache, raw, AddrA, "block2");
            await AssertAccountParityAsync(cache, raw, AddrD, "block2");
            await AssertAccountParityAsync(cache, raw, AddrE, "block2");
            await AssertCodeParityAsync(cache, raw, ch2, "block2");
            await AssertStorageParityAsync(cache, raw, AddrA, 1, "block2");
            await AssertStorageParityAsync(cache, raw, AddrB, 1, "block2");
            await AssertClearedStorageNeverStalePositiveAsync(cache, raw, AddrC, 1, "block2");
            await AssertClearedStorageNeverStalePositiveAsync(cache, raw, AddrC, 2, "block2");
            await AssertStorageParityAsync(cache, raw, AddrE, 7, "block2");
            await AssertClearedStorageNeverStalePositiveAsync(cache, raw, addrG, 1, "block2");
            await AssertStorageParityAsync(cache, raw, addrG, 2, "block2");

            Assert.Null(await raw.GetAccountAsync(AddrD));
            Assert.Null(await raw.GetStorageAsync(addrG, 1));
            Assert.NotNull(await raw.GetStorageAsync(addrG, 2));

            store.BeginBuffering();
            await store.SaveAccountAsync(AddrD, new Account { Balance = 1, Nonce = 1, CodeHash = null });
            await store.SaveStorageAsync(AddrD, 1, new byte[] { 0xD1 });
            await store.DeleteAccountAsync(addrG);
            await store.FlushBufferAsync();

            await AssertAccountParityAsync(cache, raw, AddrD, "block3");
            await AssertAccountParityAsync(cache, raw, addrG, "block3");
            await AssertStorageParityAsync(cache, raw, AddrD, 1, "block3");
            await AssertClearedStorageNeverStalePositiveAsync(cache, raw, addrG, 2, "block3");

            Assert.NotNull(await raw.GetAccountAsync(AddrD));
            Assert.Null(await raw.GetAccountAsync(addrG));
            Assert.Null(await raw.GetStorageAsync(addrG, 2));
        }

        [Fact]
        public async Task NullCache_BehavesLikeTodayWithNoCacheAttached()
        {
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            var raw = new RocksDbStateStore(mgr);
            var store = new BufferedFlatStateStore(raw);

            store.BeginBuffering();
            await store.SaveAccountAsync(AddrA, new Account { Balance = 42, Nonce = 1 });
            await store.SaveStorageAsync(AddrA, 1, new byte[] { 0x01 });
            await store.FlushBufferAsync();

            var account = await raw.GetAccountAsync(AddrA);
            Assert.NotNull(account);
            Assert.Equal((Nethereum.Util.EvmUInt256)42, account.Balance);
        }
    }
}
