using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class RetainedDrainParityTests : IDisposable
    {
        private readonly string _dir;

        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string AddrB = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string AddrC = "0xcccccccccccccccccccccccccccccccccccccc";

        public RetainedDrainParityTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-retaineddrain-" + Guid.NewGuid().ToString("N"));
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

        private static byte[] CodeHash(byte b) { var h = new byte[32]; h[0] = b; return h; }

        private static async Task WriteBlockAsync(IStateStore store, int blockOffset)
        {
            await store.SaveAccountAsync(AddrA, new Account { Balance = 1000 + blockOffset, Nonce = (ulong)(1 + blockOffset) });
            await store.SaveStorageAsync(AddrA, 1, new byte[] { (byte)(0x10 + blockOffset) });
            await store.SaveAccountAsync(AddrB, new Account { Balance = 5 * blockOffset, Nonce = 1 });

            if (blockOffset == 1)
            {
                await store.SaveAccountAsync(AddrC, new Account { Balance = 50, Nonce = 1, CodeHash = CodeHash(0x01) });
                await store.SaveCodeAsync(CodeHash(0x01), new byte[] { 0x60, 0x01 });
            }

            if (blockOffset == 2)
            {
                await store.ClearStorageAsync(AddrA);
                await store.SaveStorageAsync(AddrA, 2, new byte[] { 0x99 });
            }

            if (blockOffset == 3)
            {
                await store.DeleteAccountAsync(AddrC);
                await store.SaveStorageAsync(AddrB, 5, new byte[] { 0x55 });
            }
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

        private static void AssertCfIdentical(RocksDbManager baseline, RocksDbManager retained, string cf)
        {
            var dumpBaseline = DumpCf(baseline, cf);
            var dumpRetained = DumpCf(retained, cf);
            Assert.True(dumpBaseline.Count == dumpRetained.Count,
                $"[{cf}] row count differs: per-block-drain={dumpBaseline.Count} retained-drain={dumpRetained.Count}");
            foreach (var kv in dumpBaseline)
            {
                Assert.True(dumpRetained.TryGetValue(kv.Key, out var retainedVal),
                    $"[{cf}] key {kv.Key} present under per-block drain, missing under retained drain");
                Assert.Equal(kv.Value, retainedVal);
            }
        }

        private static async Task AssertCoalescedEndStateAsync(IStateStore raw)
        {
            var a = await raw.GetAccountAsync(AddrA);
            Assert.NotNull(a);
            Assert.Equal((EvmUInt256)1003, a.Balance);
            Assert.Equal(new byte[] { 0x13 }, await raw.GetStorageAsync(AddrA, 1));
            Assert.Equal(new byte[] { 0x99 }, await raw.GetStorageAsync(AddrA, 2));

            var b = await raw.GetAccountAsync(AddrB);
            Assert.NotNull(b);
            Assert.NotNull(await raw.GetStorageAsync(AddrB, 5));

            Assert.Null(await raw.GetAccountAsync(AddrC));
        }

        [Fact]
        public async Task AccumulateAcrossBlocksThenDrainOnce_IsByteIdenticalToPerBlockDrain()
        {
            var dirBaseline = SubDir("per-block");
            var dirRetained = SubDir("retained");

            using var mgrBaseline = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirBaseline });
            using var mgrRetained = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirRetained });

            var rawBaseline = new RocksDbStateStore(mgrBaseline);
            var rawRetained = new RocksDbStateStore(mgrRetained);

            var bufferedBaseline = new BufferedFlatStateStore(rawBaseline);
            var bufferedRetained = new BufferedFlatStateStore(rawRetained);

            for (int b = 1; b <= 3; b++)
            {
                bufferedBaseline.BeginBuffering();
                await WriteBlockAsync(bufferedBaseline, b);
                await bufferedBaseline.FlushBufferAsync();
            }

            bufferedRetained.BeginBuffering();
            Assert.Equal(0, bufferedRetained.WindowApproxBytes);
            for (int b = 1; b <= 3; b++)
            {
                await WriteBlockAsync(bufferedRetained, b);
            }
            Assert.True(bufferedRetained.WindowApproxBytes > 0, "overlay should report a non-zero buffered size before the window drain");
            await bufferedRetained.FlushBufferAsync();
            Assert.Equal(0, bufferedRetained.WindowApproxBytes);

            AssertCfIdentical(mgrBaseline, mgrRetained, RocksDbManager.CF_STATE_ACCOUNTS);
            AssertCfIdentical(mgrBaseline, mgrRetained, RocksDbManager.CF_STATE_STORAGE);
            AssertCfIdentical(mgrBaseline, mgrRetained, RocksDbManager.CF_STATE_CODE);

            Assert.True(DumpCf(mgrBaseline, RocksDbManager.CF_STATE_ACCOUNTS).Count > 0);
            Assert.True(DumpCf(mgrBaseline, RocksDbManager.CF_STATE_STORAGE).Count > 0);
            Assert.True(DumpCf(mgrBaseline, RocksDbManager.CF_STATE_CODE).Count > 0);

            await AssertCoalescedEndStateAsync(rawBaseline);
            await AssertCoalescedEndStateAsync(rawRetained);
        }

        [Fact]
        public async Task HistoricalStateStore_DrainBufferToDiskAsync_AtWindowBoundary_MatchesPerBlockClear()
        {
            var dirBaseline = SubDir("hist-per-block");
            var dirRetained = SubDir("hist-retained");

            using var mgrBaseline = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirBaseline });
            using var mgrRetained = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirRetained });

            var rawBaseline = new RocksDbStateStore(mgrBaseline);
            var rawRetained = new RocksDbStateStore(mgrRetained);

            var diffsBaseline = new RocksDbStateDiffStore(mgrBaseline);
            var diffsRetained = new RocksDbStateDiffStore(mgrRetained);

            var histBaseline = new HistoricalStateStore(new BufferedFlatStateStore(rawBaseline), diffsBaseline, HistoricalStateOptions.FullArchive);
            var histRetained = new HistoricalStateStore(new BufferedFlatStateStore(rawRetained), diffsRetained, HistoricalStateOptions.FullArchive);

            for (int b = 1; b <= 3; b++)
            {
                histBaseline.SetCurrentBlockNumber(b);
                await WriteBlockAsync(histBaseline, b);
                await histBaseline.ClearCurrentBlockNumberAsync();
            }

            histRetained.SetCurrentBlockNumber(1);
            for (int b = 1; b <= 3; b++)
            {
                await WriteBlockAsync(histRetained, b);
            }
            await histRetained.DrainBufferToDiskAsync();

            AssertCfIdentical(mgrBaseline, mgrRetained, RocksDbManager.CF_STATE_ACCOUNTS);
            AssertCfIdentical(mgrBaseline, mgrRetained, RocksDbManager.CF_STATE_STORAGE);
            AssertCfIdentical(mgrBaseline, mgrRetained, RocksDbManager.CF_STATE_CODE);

            await AssertCoalescedEndStateAsync(rawBaseline);
            await AssertCoalescedEndStateAsync(rawRetained);
        }
    }
}
