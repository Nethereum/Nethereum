using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FlatCacheExecParityTests : IDisposable
    {
        private readonly string _dir;

        public FlatCacheExecParityTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-execparity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private string SubDir(string name)
        {
            var path = Path.Combine(_dir, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string AddrB = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string AddrC = "0xcccccccccccccccccccccccccccccccccccccc";
        private const string AddrD = "0xdddddddddddddddddddddddddddddddddddddd";
        private const string AddrE = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        private const string AddrF = "0x1111111111111111111111111111111111111111";
        private const string AddrG = "0x2222222222222222222222222222222222222222";
        private const string AddrH = "0x3333333333333333333333333333333333333333"; // EIP-161 empty account (exists, all-zero)
        private const string AddrI = "0x4444444444444444444444444444444444444444";

        private static byte[] CodeHash(byte b)
        {
            var h = new byte[32];
            h[0] = b;
            return h;
        }

        private static async Task AssertAccountIdenticalAsync(BufferedFlatStateStore on, BufferedFlatStateStore off, string address, string label)
        {
            var a = await on.GetAccountAsync(address).ConfigureAwait(false);
            var b = await off.GetAccountAsync(address).ConfigureAwait(false);
            if (a == null || b == null)
            {
                Assert.True(a == null && b == null,
                    $"[{label}] account presence differs for {address}: cache-ON={(a == null ? "absent" : "present")} cache-OFF={(b == null ? "absent" : "present")}");
                return;
            }
            Assert.Equal(b.Balance, a.Balance);
            Assert.Equal(b.Nonce, a.Nonce);
            Assert.Equal(b.CodeHash ?? Array.Empty<byte>(), a.CodeHash ?? Array.Empty<byte>());
            Assert.Equal(b.StateRoot ?? Array.Empty<byte>(), a.StateRoot ?? Array.Empty<byte>());
        }

        private static async Task AssertStorageIdenticalAsync(BufferedFlatStateStore on, BufferedFlatStateStore off, string address, BigInteger slot, string label)
        {
            var a = await on.GetStorageAsync(address, slot).ConfigureAwait(false);
            var b = await off.GetStorageAsync(address, slot).ConfigureAwait(false);
            bool aAbsent = a == null || a.Length == 0;
            bool bAbsent = b == null || b.Length == 0;
            Assert.True(aAbsent == bAbsent,
                $"[{label}] storage presence differs for {address}:{slot}: cache-ON={(aAbsent ? "absent" : "present")} cache-OFF={(bAbsent ? "absent" : "present")}");
            if (!aAbsent) Assert.Equal(b, a);
        }

        private static async Task AssertCodeIdenticalAsync(BufferedFlatStateStore on, BufferedFlatStateStore off, byte[] codeHash, string label)
        {
            var a = await on.GetCodeAsync(codeHash).ConfigureAwait(false);
            var b = await off.GetCodeAsync(codeHash).ConfigureAwait(false);
            Assert.Equal(b, a);
        }

        private static async Task RunShadowOracleWorkloadAsync(BufferedFlatStateStore on, BufferedFlatStateStore off)
        {
            var ch1 = CodeHash(0x11);
            var ch2 = CodeHash(0x22);
            var chEmpty = Nethereum.Model.DefaultValues.EMPTY_DATA_HASH;
            var code1 = new byte[] { 0x60, 0x00, 0x60, 0x00 };
            var code2 = new byte[] { 0x60, 0x01 };

            async Task DriveBothAsync(Func<BufferedFlatStateStore, Task> action)
            {
                await action(on).ConfigureAwait(false);
                await action(off).ConfigureAwait(false);
            }

            on.BeginBuffering(); off.BeginBuffering();
            await DriveBothAsync(s => s.SaveAccountAsync(AddrA, new Account { Balance = 100, Nonce = 1, CodeHash = ch1 }));
            await DriveBothAsync(s => s.SaveAccountAsync(AddrB, new Account { Balance = 200, Nonce = 1, CodeHash = ch1 }));
            await DriveBothAsync(s => s.SaveAccountAsync(AddrC, new Account { Balance = 300, Nonce = 1, CodeHash = ch1 }));
            await DriveBothAsync(s => s.SaveAccountAsync(AddrD, new Account { Balance = 400, Nonce = 1, CodeHash = ch1 }));
            await DriveBothAsync(s => s.SaveAccountAsync(AddrE, new Account { Balance = 500, Nonce = 1, CodeHash = ch1 }));
            await DriveBothAsync(s => s.SaveAccountAsync(AddrF, new Account { Balance = 600, Nonce = 1, CodeHash = ch1 }));
            await DriveBothAsync(s => s.SaveAccountAsync(AddrG, new Account { Balance = 700, Nonce = 1, CodeHash = ch1 }));
            await DriveBothAsync(s => s.SaveAccountAsync(AddrH, new Account { Balance = 0, Nonce = 0, CodeHash = Nethereum.Model.DefaultValues.EMPTY_DATA_HASH })); // EIP-161 empty
            await DriveBothAsync(s => s.SaveCodeAsync(ch1, code1));
            await DriveBothAsync(s => s.SaveCodeAsync(chEmpty, Array.Empty<byte>()));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrA, 1, new byte[] { 0xA1 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrA, 2, new byte[] { 0xA2 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrB, 1, new byte[] { 0xB1 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrB, 2, new byte[] { 0xB2 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrC, 1, new byte[] { 0xC1 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrC, 2, new byte[] { 0xC2 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrD, 9, new byte[] { 0xD9 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrE, 3, new byte[] { 0xE3 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrF, 4, new byte[] { 0xF4 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrG, 1, new byte[] { 0xD1 }));
            await on.FlushBufferAsync().ConfigureAwait(false);
            await off.FlushBufferAsync().ConfigureAwait(false);

            foreach (var addr in new[] { AddrA, AddrB, AddrC, AddrD, AddrE, AddrF, AddrG, AddrH })
                await AssertAccountIdenticalAsync(on, off, addr, "block1");
            await AssertAccountIdenticalAsync(on, off, AddrI, "block1");
            await AssertCodeIdenticalAsync(on, off, ch1, "block1");
            await AssertCodeIdenticalAsync(on, off, chEmpty, "block1");
            await AssertStorageIdenticalAsync(on, off, AddrA, 1, "block1");
            await AssertStorageIdenticalAsync(on, off, AddrA, 2, "block1");
            await AssertStorageIdenticalAsync(on, off, AddrB, 1, "block1");
            await AssertStorageIdenticalAsync(on, off, AddrB, 2, "block1");
            await AssertStorageIdenticalAsync(on, off, AddrC, 1, "block1");
            await AssertStorageIdenticalAsync(on, off, AddrC, 2, "block1");
            await AssertStorageIdenticalAsync(on, off, AddrD, 9, "block1");
            await AssertStorageIdenticalAsync(on, off, AddrE, 3, "block1");
            await AssertStorageIdenticalAsync(on, off, AddrF, 4, "block1");
            await AssertStorageIdenticalAsync(on, off, AddrG, 1, "block1");

            on.BeginBuffering(); off.BeginBuffering();
            await DriveBothAsync(s => s.SaveAccountAsync(AddrA, new Account { Balance = 150, Nonce = 2, CodeHash = ch1 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrA, 1, new byte[] { 0xA9 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrB, 1, Array.Empty<byte>()));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrB, 2, new byte[32]));
            await DriveBothAsync(s => s.ClearStorageAsync(AddrC));
            await DriveBothAsync(s => s.DeleteAccountAsync(AddrD));
            await DriveBothAsync(s => s.DeleteAccountAsync(AddrE));
            await DriveBothAsync(s => s.SaveAccountAsync(AddrE, new Account { Balance = 999, Nonce = 1, CodeHash = ch2 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrE, 7, new byte[] { 0xE7 }));
            await DriveBothAsync(s => s.DeleteAccountAsync(AddrF));
            await DriveBothAsync(s => s.ClearStorageAsync(AddrG));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrG, 2, new byte[] { 0xD2 }));
            await DriveBothAsync(s => s.SaveCodeAsync(ch2, code2));
            await on.FlushBufferAsync().ConfigureAwait(false);
            await off.FlushBufferAsync().ConfigureAwait(false);

            await AssertAccountIdenticalAsync(on, off, AddrA, "block2");
            await AssertAccountIdenticalAsync(on, off, AddrD, "block2");
            await AssertAccountIdenticalAsync(on, off, AddrE, "block2");
            await AssertAccountIdenticalAsync(on, off, AddrF, "block2");
            await AssertAccountIdenticalAsync(on, off, AddrH, "block2");
            await AssertCodeIdenticalAsync(on, off, ch2, "block2");
            await AssertStorageIdenticalAsync(on, off, AddrA, 1, "block2");
            await AssertStorageIdenticalAsync(on, off, AddrB, 1, "block2");
            await AssertStorageIdenticalAsync(on, off, AddrB, 2, "block2");
            await AssertStorageIdenticalAsync(on, off, AddrC, 1, "block2");
            await AssertStorageIdenticalAsync(on, off, AddrC, 2, "block2");
            await AssertStorageIdenticalAsync(on, off, AddrE, 7, "block2");
            await AssertStorageIdenticalAsync(on, off, AddrF, 4, "block2");
            await AssertStorageIdenticalAsync(on, off, AddrG, 1, "block2");
            await AssertStorageIdenticalAsync(on, off, AddrG, 2, "block2");

            Assert.Null(await off.GetAccountAsync(AddrD).ConfigureAwait(false));
            Assert.Null(await off.GetStorageAsync(AddrG, 1).ConfigureAwait(false));
            Assert.NotNull(await off.GetStorageAsync(AddrG, 2).ConfigureAwait(false));

            on.BeginBuffering(); off.BeginBuffering();
            await DriveBothAsync(s => s.SaveAccountAsync(AddrD, new Account { Balance = 1, Nonce = 1, CodeHash = ch1 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrD, 1, new byte[] { 0xD1 }));
            await DriveBothAsync(s => s.SaveAccountAsync(AddrF, new Account { Balance = 1234, Nonce = 1, CodeHash = ch2 }));
            await DriveBothAsync(s => s.SaveStorageAsync(AddrF, 5, new byte[] { 0xF5 }));
            await DriveBothAsync(s => s.DeleteAccountAsync(AddrG));
            await on.FlushBufferAsync().ConfigureAwait(false);
            await off.FlushBufferAsync().ConfigureAwait(false);

            await AssertAccountIdenticalAsync(on, off, AddrD, "block3");
            await AssertAccountIdenticalAsync(on, off, AddrF, "block3");
            await AssertAccountIdenticalAsync(on, off, AddrG, "block3");
            await AssertStorageIdenticalAsync(on, off, AddrD, 1, "block3");
            await AssertStorageIdenticalAsync(on, off, AddrF, 4, "block3");
            await AssertStorageIdenticalAsync(on, off, AddrF, 5, "block3");
            await AssertStorageIdenticalAsync(on, off, AddrG, 2, "block3");

            Assert.NotNull(await off.GetAccountAsync(AddrD).ConfigureAwait(false));
            Assert.NotNull(await off.GetAccountAsync(AddrF).ConfigureAwait(false));
            Assert.Null(await off.GetAccountAsync(AddrG).ConfigureAwait(false));
        }

        [Fact]
        public async Task CacheOn_Vs_CacheOff_ByteIdenticalReads_AcrossR3EdgeCaseWorkload()
        {
            var dirOn = SubDir("shadow-on");
            var dirOff = SubDir("shadow-off");

            using var mgrOn = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirOn });
            using var mgrOff = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirOff });

            var rawOn = new RocksDbStateStore(mgrOn);
            var rawOff = new RocksDbStateStore(mgrOff);

            var cache = new FlatStateCache(budgetBytes: 4 * 1024 * 1024);
            var on = new BufferedFlatStateStore(rawOn, cache);
            var off = new BufferedFlatStateStore(rawOff);

            await RunShadowOracleWorkloadAsync(on, off).ConfigureAwait(false);

            Assert.True(cache.Count > 0);
        }

        [Fact]
        public async Task CacheOn_Vs_CacheOff_ByteIdenticalReads_WithForcedTinyBudgetEviction()
        {
            var dirOn = SubDir("shadow-tiny-on");
            var dirOff = SubDir("shadow-tiny-off");

            using var mgrOn = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirOn });
            using var mgrOff = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirOff });

            var rawOn = new RocksDbStateStore(mgrOn);
            var rawOff = new RocksDbStateStore(mgrOff);

            var cache = new FlatStateCache(budgetBytes: 300);
            var on = new BufferedFlatStateStore(rawOn, cache);
            var off = new BufferedFlatStateStore(rawOff);

            await RunShadowOracleWorkloadAsync(on, off).ConfigureAwait(false);

            Assert.True(cache.ApproxBytes <= 300, $"cache exceeded its budget: {cache.ApproxBytes} bytes");
            Assert.True(cache.Misses > 0, "expected the tiny budget to force at least one eviction-induced miss");
        }

        [Fact]
        public async Task NullCache_Store_MatchesExplicitCacheOffStore()
        {
            var dirNull = SubDir("nullcache");
            var dirOff = SubDir("explicit-off");

            using var mgrNull = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirNull });
            using var mgrOff = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirOff });

            var rawNull = new RocksDbStateStore(mgrNull);
            var rawOff = new RocksDbStateStore(mgrOff);

            var nullCacheStore = new BufferedFlatStateStore(rawNull);
            var explicitOffStore = new BufferedFlatStateStore(rawOff);

            await RunShadowOracleWorkloadAsync(nullCacheStore, explicitOffStore).ConfigureAwait(false);
        }

        [Fact]
        public async Task ReadYourWrites_OverlayShadowsStaleCommittedCacheEntry()
        {
            var dir = SubDir("ryw");
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            var raw = new RocksDbStateStore(mgr);
            var cache = new FlatStateCache(budgetBytes: 1024 * 1024);
            var store = new BufferedFlatStateStore(raw, cache);

            const string address = "0x5555555555555555555555555555555555555555";
            const int slot = 11;

            var committedAccount = new Account { Balance = 100, Nonce = 1, CodeHash = CodeHash(0x11) };
            var inFlightAccount = new Account { Balance = 999, Nonce = 2, CodeHash = CodeHash(0x22) };
            var committedStorage = new byte[] { 0xC0, 0x11 };
            var inFlightStorage = new byte[] { 0xF1, 0x99 };

            store.BeginBuffering();
            await store.SaveAccountAsync(address, committedAccount);
            await store.SaveStorageAsync(address, slot, committedStorage);
            await store.FlushBufferAsync();

            Assert.True(cache.TryGetAccount(address, out var cachedCommitted));
            Assert.Equal(committedAccount.Balance, cachedCommitted.Balance);
            Assert.True(cache.TryGetStorage(address, slot, out var cachedStorageCommitted));
            Assert.Equal(committedStorage, cachedStorageCommitted);

            store.BeginBuffering();
            await store.SaveAccountAsync(address, inFlightAccount);
            await store.SaveStorageAsync(address, slot, inFlightStorage);

            var readAccount = await store.GetAccountAsync(address);
            var readStorage = await store.GetStorageAsync(address, slot);

            Assert.Equal(inFlightAccount.Balance, readAccount.Balance);
            Assert.Equal(inFlightAccount.Nonce, readAccount.Nonce);
            Assert.Equal(inFlightStorage, readStorage);

            Assert.True(cache.TryGetAccount(address, out var cacheAfterRead));
            Assert.Equal(committedAccount.Balance, cacheAfterRead.Balance);
            Assert.True(cache.TryGetStorage(address, slot, out var cacheStorageAfterRead));
            Assert.Equal(committedStorage, cacheStorageAfterRead);
        }

        private static byte[] EncodeRound(int round)
        {
            var bytes = new byte[32];
            bytes[28] = (byte)(round >> 24);
            bytes[29] = (byte)(round >> 16);
            bytes[30] = (byte)(round >> 8);
            bytes[31] = (byte)round;
            return bytes;
        }

        // Flat storage values are canonically TRIMMED (leading zero bytes stripped) — a round number
        // under 256 now durably occupies just 1 byte, not the 32-byte-padded shape this test's
        // EncodeRound produces before the write. Pad back to a fixed width before decoding, exactly
        // as every real consumer (SLOAD, eth_getStorageAt, EIP-2935/4788) must.
        private static int DecodeRound(byte[] value)
        {
            if (value == null || value.Length == 0) return -1;
            var padded = value.Length < 4 ? value.PadTo32Bytes() : value;
            int result = 0;
            for (int i = padded.Length - 4; i < padded.Length; i++)
                result = (result << 8) | padded[i];
            return result;
        }

        private static string DecoyAddress(int i) => "0x" + i.ToString("x40");

        [Fact]
        public async Task ConcurrentReads_ViaSameStore_NeverObserveARoundOlderThanTheLastFlush()
        {
            var dir = SubDir("r7-concurrency");
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            var raw = new RocksDbStateStore(mgr);
            var cache = new FlatStateCache(budgetBytes: 4 * 1024 * 1024);
            var store = new BufferedFlatStateStore(raw, cache);

            const string sentinel = "0xffffffffffffffffffffffffffffffffffffffff";
            const int sentinelSlot = 42;
            const int totalRounds = 40;
            const int decoyCount = 200;

            store.BeginBuffering();
            await store.SaveAccountAsync(sentinel, new Account { Balance = 1, Nonce = 1 });
            await store.SaveStorageAsync(sentinel, sentinelSlot, EncodeRound(1));
            await store.FlushBufferAsync();

            int lastFlushedRound = 1;
            var running = true;
            var violations = new System.Collections.Concurrent.ConcurrentBag<string>();

            async Task ReaderLoopAsync(int readerId)
            {
                var iterations = 0;
                while (running)
                {
                    iterations++;
                    var floor = Volatile.Read(ref lastFlushedRound);
                    var account = await store.GetAccountAsync(sentinel).ConfigureAwait(false);
                    var storage = await store.GetStorageAsync(sentinel, sentinelSlot).ConfigureAwait(false);
                    var ceiling = Volatile.Read(ref lastFlushedRound);

                    if (account == null || storage == null)
                    {
                        violations.Add($"reader{readerId}: unexpected absent read after pre-seed (account null={account == null}, storage null={storage == null})");
                        continue;
                    }

                    var acctRound = (int)(ulong)account.Nonce;
                    var slotRound = DecodeRound(storage);

                    if (acctRound < floor || acctRound > ceiling + 1)
                        violations.Add($"reader{readerId} iter{iterations}: account round {acctRound} outside [{floor},{ceiling + 1}]");
                    if (slotRound < floor || slotRound > ceiling + 1)
                        violations.Add($"reader{readerId} iter{iterations}: slot round {slotRound} outside [{floor},{ceiling + 1}]");
                }
            }

            var readerTasks = new List<Task>();
            for (int r = 0; r < 4; r++)
                readerTasks.Add(Task.Run(() => ReaderLoopAsync(r)));

            for (int round = 2; round <= totalRounds; round++)
            {
                store.BeginBuffering();
                for (int i = 0; i < decoyCount; i++)
                    await store.SaveAccountAsync(DecoyAddress(round * 100000 + i + 1), new Account { Balance = i, Nonce = 1 });
                await store.SaveAccountAsync(sentinel, new Account { Balance = 1, Nonce = (ulong)round });
                await store.SaveStorageAsync(sentinel, sentinelSlot, EncodeRound(round));
                await store.FlushBufferAsync();
                Volatile.Write(ref lastFlushedRound, round);
            }

            running = false;
            await Task.WhenAll(readerTasks).ConfigureAwait(false);

            Assert.True(violations.IsEmpty, "R7 self-consistency violated:\n" + string.Join("\n", violations));
            Assert.Equal(totalRounds, lastFlushedRound);
        }
    }
}
