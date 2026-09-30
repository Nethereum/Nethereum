using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class AtomicFlushTests : IDisposable
    {
        private readonly string _dir;

        public AtomicFlushTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-atomicflush-" + Guid.NewGuid().ToString("N"));
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

        private sealed class PassThroughNonBatchStateStore : IStateStore, ISnapFlatStateWriter
        {
            private readonly RocksDbStateStore _inner;
            public PassThroughNonBatchStateStore(RocksDbStateStore inner) => _inner = inner;

            public Task<Account> GetAccountAsync(string address) => _inner.GetAccountAsync(address);
            public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();
            public Task<Account> GetAccountByHashAsync(byte[] accountHash) => _inner.GetAccountByHashAsync(accountHash);
            public Task SaveAccountByHashAsync(byte[] accountHash, Account account) => _inner.SaveAccountByHashAsync(accountHash, account);
            public Task DeleteAccountByHashAsync(byte[] accountHash) => _inner.DeleteAccountByHashAsync(accountHash);
            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByHashAsync(accountHash, slotKeccak, value);
        }

        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string AddrB = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string AddrC = "0xcccccccccccccccccccccccccccccccccccccc";
        private const string AddrD = "0xdddddddddddddddddddddddddddddddddddddd";

        private static byte[] CodeHash(byte b)
        {
            var h = new byte[32];
            h[0] = b;
            return h;
        }

        private static async Task DriveWorkloadAsync(BufferedFlatStateStore store)
        {
            var ch1 = CodeHash(0x11);
            var ch2 = CodeHash(0x22);
            var code1 = new byte[] { 0x60, 0x00, 0x60, 0x00 };
            var code2 = new byte[] { 0x60, 0x01 };

            store.BeginBuffering();
            await store.SaveAccountAsync(AddrA, new Account { Balance = 100, Nonce = 1, CodeHash = ch1 });
            await store.SaveAccountAsync(AddrB, new Account { Balance = 200, Nonce = 1, CodeHash = ch1 });
            await store.SaveAccountAsync(AddrC, new Account { Balance = 300, Nonce = 1, CodeHash = null });
            await store.SaveAccountAsync(AddrD, new Account { Balance = 400, Nonce = 1, CodeHash = ch1 });
            await store.SaveStorageAsync(AddrA, 1, new byte[] { 0xA1 });
            await store.SaveStorageAsync(AddrA, 2, new byte[] { 0xA2 });
            await store.SaveStorageAsync(AddrB, 1, new byte[] { 0xB1 });
            await store.SaveStorageAsync(AddrC, 1, new byte[] { 0xC1 });
            await store.SaveStorageAsync(AddrD, 9, new byte[] { 0xD9 });
            await store.SaveCodeAsync(ch1, code1);
            await store.FlushBufferAsync();

            store.BeginBuffering();
            await store.SaveAccountAsync(AddrA, new Account { Balance = 150, Nonce = 2, CodeHash = ch1 });
            await store.DeleteAccountAsync(AddrB);
            await store.ClearStorageAsync(AddrC);
            await store.SaveStorageAsync(AddrA, 1, Array.Empty<byte>());
            await store.SaveCodeAsync(ch2, code2);
            await store.FlushBufferAsync();

            store.BeginBuffering();
            await store.DeleteAccountAsync(AddrD);
            await store.SaveAccountAsync(AddrD, new Account { Balance = 999, Nonce = 1, CodeHash = ch2 });
            await store.SaveStorageAsync(AddrD, 5, new byte[] { 0xD5 });
            await store.FlushBufferAsync();
        }

        private static Dictionary<string, byte[]> DumpCf(RocksDbManager mgr, string cf)
        {
            var result = new Dictionary<string, byte[]>();
            using var it = mgr.CreateIterator(cf);
            it.SeekToFirst();
            while (it.Valid())
            {
                result[Convert.ToHexString(it.Key())] = it.Value();
                it.Next();
            }
            return result;
        }

        private static void AssertCfIdentical(RocksDbManager a, RocksDbManager b, string cf)
        {
            var dumpA = DumpCf(a, cf);
            var dumpB = DumpCf(b, cf);
            Assert.True(dumpA.Count == dumpB.Count,
                $"[{cf}] row count differs: batch-path={dumpA.Count} per-op-path={dumpB.Count}");
            foreach (var kv in dumpA)
            {
                Assert.True(dumpB.TryGetValue(kv.Key, out var bVal), $"[{cf}] key {kv.Key} present in batch path, missing in per-op path");
                Assert.Equal(kv.Value, bVal);
            }
        }

        [Fact]
        public async Task ByteIdentity_BatchPathVsPerOpFallbackPath()
        {
            var dirBatch = SubDir("batch");
            var dirPerOp = SubDir("perop");

            using var mgrBatch = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirBatch });
            using var mgrPerOp = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirPerOp });

            var rawBatch = new RocksDbStateStore(mgrBatch, accountLayout: BinaryPackedAccountLayout.Instance);
            var rawPerOp = new RocksDbStateStore(mgrPerOp, accountLayout: BinaryPackedAccountLayout.Instance);

            var batchStore = new BufferedFlatStateStore(rawBatch);
            var perOpStore = new BufferedFlatStateStore(new PassThroughNonBatchStateStore(rawPerOp));

            await DriveWorkloadAsync(batchStore);
            await DriveWorkloadAsync(perOpStore);

            AssertCfIdentical(mgrBatch, mgrPerOp, RocksDbManager.CF_STATE_ACCOUNTS);
            AssertCfIdentical(mgrBatch, mgrPerOp, RocksDbManager.CF_STATE_STORAGE);
            AssertCfIdentical(mgrBatch, mgrPerOp, RocksDbManager.CF_STATE_CODE);

            Assert.True(DumpCf(mgrBatch, RocksDbManager.CF_STATE_ACCOUNTS).Count > 0);
            Assert.True(DumpCf(mgrBatch, RocksDbManager.CF_STATE_STORAGE).Count > 0);
            Assert.True(DumpCf(mgrBatch, RocksDbManager.CF_STATE_CODE).Count > 0);

            var a = await rawBatch.GetAccountAsync(AddrA);
            Assert.NotNull(a);
            Assert.Equal(CodeHash(0x11), a.CodeHash);
            Assert.Null(await rawBatch.GetAccountAsync(AddrB));
            Assert.Null(await rawBatch.GetStorageAsync(AddrB, 1));
            Assert.Null(await rawBatch.GetStorageAsync(AddrC, 1));
            Assert.Null(await rawBatch.GetStorageAsync(AddrA, 1));
            Assert.NotNull(await rawBatch.GetStorageAsync(AddrA, 2));
            var d = await rawBatch.GetAccountAsync(AddrD);
            Assert.NotNull(d);
            Assert.Equal((Nethereum.Util.EvmUInt256)999, d.Balance);
            Assert.Null(await rawBatch.GetStorageAsync(AddrD, 9));
            Assert.NotNull(await rawBatch.GetStorageAsync(AddrD, 5));
        }

        [Fact]
        public async Task SingleWriteCall_PerFlush_OnBatchPath_MultipleOnPerOpFallback()
        {
            var dirBatch = SubDir("batch-writecount");
            var dirPerOp = SubDir("perop-writecount");

            using var mgrBatch = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirBatch });
            using var mgrPerOp = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirPerOp });

            var rawBatch = new RocksDbStateStore(mgrBatch);
            var rawPerOp = new RocksDbStateStore(mgrPerOp);

            var batchStore = new BufferedFlatStateStore(rawBatch);
            var perOpStore = new BufferedFlatStateStore(new PassThroughNonBatchStateStore(rawPerOp));

            var writesBefore = mgrBatch.WriteCallCount;
            var commitsBefore = mgrBatch.NativeCommitCount;
            batchStore.BeginBuffering();
            await batchStore.SaveAccountAsync(AddrA, new Account { Balance = 1, Nonce = 1 });
            await batchStore.SaveStorageAsync(AddrA, 1, new byte[] { 0x01 });
            await batchStore.SaveCodeAsync(CodeHash(0x01), new byte[] { 0x60 });
            await batchStore.FlushBufferAsync();
            Assert.Equal(writesBefore + 1, mgrBatch.WriteCallCount);
            Assert.Equal(commitsBefore + 1, mgrBatch.NativeCommitCount);

            var commitsBeforePerOp = mgrPerOp.NativeCommitCount;
            perOpStore.BeginBuffering();
            await perOpStore.SaveAccountAsync(AddrA, new Account { Balance = 1, Nonce = 1 });
            await perOpStore.SaveStorageAsync(AddrA, 1, new byte[] { 0x01 });
            await perOpStore.SaveCodeAsync(CodeHash(0x01), new byte[] { 0x60 });
            await perOpStore.FlushBufferAsync();
            Assert.True(mgrPerOp.NativeCommitCount > commitsBeforePerOp + 1,
                $"expected the per-op fallback to issue multiple native commits (got {mgrPerOp.NativeCommitCount - commitsBeforePerOp}) — " +
                "if this fails, the NativeCommitCount probe itself is not discriminating.");

            var dirBatch2 = SubDir("batch-writecount-full");
            using var mgrBatch2 = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirBatch2 });
            var rawBatch2 = new RocksDbStateStore(mgrBatch2, accountLayout: BinaryPackedAccountLayout.Instance);
            var batchStore2 = new BufferedFlatStateStore(rawBatch2);
            var before2 = mgrBatch2.WriteCallCount;
            var commitsBefore2 = mgrBatch2.NativeCommitCount;
            await DriveWorkloadAsync(batchStore2);
            Assert.Equal(before2 + 3, mgrBatch2.WriteCallCount);
            Assert.Equal(commitsBefore2 + 3, mgrBatch2.NativeCommitCount);
        }

        private static string DecoyAddress(int i) => "0x" + i.ToString("x40");

        private async Task<bool> RunNoTearRaceAsync(RocksDbManager mgr, BufferedFlatStateStore store, int decoyCount, int toggleCycles)
        {
            const string sentinel = "0xffffffffffffffffffffffffffffffffffffffff";
            const int sentinelSlot = 42;
            var oldAccount = new Account { Balance = 1000, Nonce = 1 };
            var newAccount = new Account { Balance = 2000, Nonce = 2 };
            var oldValue = new byte[] { 0x0d, 0x0d };
            var newValue = new byte[] { 0xbe, 0xef };

            var acctKey = StateKeys.AccountKey(sentinel);
            var slotKey = StorageKeyFor(sentinel, sentinelSlot);

            byte[] oldAccountRaw, newAccountRaw;
            var probeDir = SubDir("notear-probe-" + Guid.NewGuid().ToString("N"));
            using (var probeMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = probeDir }))
            {
                var probeStore = new RocksDbStateStore(probeMgr);
                await probeStore.SaveAccountAsync(sentinel, oldAccount);
                oldAccountRaw = probeMgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, acctKey);
                await probeStore.SaveAccountAsync(sentinel, newAccount);
                newAccountRaw = probeMgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, acctKey);
            }

            store.BeginBuffering();
            await store.SaveAccountAsync(sentinel, oldAccount);
            await store.SaveStorageAsync(sentinel, sentinelSlot, oldValue);
            await store.FlushBufferAsync();

            var running = true;
            var tornObserved = false;

            var readerTask = Task.Run(() =>
            {
                while (running)
                {
                    using var snapshot = mgr.CreateSnapshot();
                    var readOptions = new RocksDbSharp.ReadOptions().SetSnapshot(snapshot);
                    var acctData = mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, acctKey, readOptions);
                    var slotData = mgr.Get(RocksDbManager.CF_STATE_STORAGE, slotKey, readOptions);
                    bool acctIsNew = acctData != null && acctData.SequenceEqual(newAccountRaw);
                    bool slotIsNew = slotData != null && slotData.SequenceEqual(newValue);
                    if (acctIsNew != slotIsNew)
                    {
                        tornObserved = true;
                        running = false;
                    }
                }
            });

            for (int cycle = 0; cycle < toggleCycles && running; cycle++)
            {
                var toNew = cycle % 2 == 0;
                store.BeginBuffering();
                for (int i = 0; i < decoyCount; i++)
                    await store.SaveAccountAsync(DecoyAddress(i + 1), new Account { Balance = i, Nonce = 1 });
                await store.SaveAccountAsync(sentinel, toNew ? newAccount : oldAccount);
                await store.SaveStorageAsync(sentinel, sentinelSlot, toNew ? newValue : oldValue);
                await store.FlushBufferAsync();
            }

            running = false;
            await readerTask.ConfigureAwait(false);
            return tornObserved;
        }

        private static byte[] StorageKeyFor(string address, BigInteger slot)
        {
            var addrKey = StateKeys.AccountKey(address);
            var slotHash = StateKeys.StorageSlotKey(slot);
            var key = new byte[addrKey.Length + slotHash.Length];
            Buffer.BlockCopy(addrKey, 0, key, 0, addrKey.Length);
            Buffer.BlockCopy(slotHash, 0, key, addrKey.Length, slotHash.Length);
            return key;
        }

        [Fact]
        public async Task NoMidDrainTear_BatchPath_NeverObservesAccountStorageMismatch()
        {
            var dir = SubDir("notear-batch");
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            var raw = new RocksDbStateStore(mgr);
            var store = new BufferedFlatStateStore(raw);

            var torn = await RunNoTearRaceAsync(mgr, store, decoyCount: 1500, toggleCycles: 20);

            Assert.False(torn, "batch path: reader observed a torn (mid-drain) account/storage state");
        }

        [Fact]
        public async Task NoMidDrainTear_PerOpFallbackPath_DoesObserveTear()
        {
            var dir = SubDir("notear-perop");
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            var raw = new RocksDbStateStore(mgr);
            var store = new BufferedFlatStateStore(new PassThroughNonBatchStateStore(raw));

            var torn = await RunNoTearRaceAsync(mgr, store, decoyCount: 1500, toggleCycles: 20);

            Assert.True(torn, "per-op fallback path: expected a torn read to prove the race has discriminating power");
        }
    }
}
