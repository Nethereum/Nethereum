using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class CollapseAfterPersistCrossBlockReproTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly List<string> _dirs = new List<string>();

        public CollapseAfterPersistCrossBlockReproTests(ITestOutputHelper output) { _out = output; }

        private static string Addr(int i) => "0x" + i.ToString("x").PadLeft(40, '0');
        private static string Contract(int i) => "0x" + ("c0" + i.ToString("x")).PadLeft(40, '0');

        private (RocksDbManager mgr, string dir) NewDb(bool pathKeyed = true)
        {
            var dir = Path.Combine(Path.GetTempPath(), "necc-collapse-repro-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            _dirs.Add(dir);
            var options = pathKeyed
                ? new RocksDbStorageOptions { DatabasePath = dir, PathKeyedState = true, TrieNodeHistoryBlocks = 256, TrieNodeHistoryIndex = true }
                : new RocksDbStorageOptions { DatabasePath = dir, PathKeyedState = false };
            var mgr = new RocksDbManager(options);
            return (mgr, dir);
        }

        private static IncrementalStateRootCalculator NewCalc(RocksDbChainStoreBundle bundle)
            => new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));

        private static async Task<byte[]> CommitBlockAsync(
            RocksDbChainStoreBundle bundle, HistoricalStateStore hist, IncrementalStateRootCalculator calc,
            ulong blockNumber, byte[] prevRoot, Func<Task> mutate)
        {
            bundle.NodeCommitBlockSource?.Arm(blockNumber);
            hist.SetCurrentBlockNumber(blockNumber);
            await mutate();
            var root = await calc.ComputeStateRootWithoutPersistAsync(prevRoot);
            await calc.PersistPendingStateAsync();
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource?.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, blockNumber, new byte[32]);
            await ((IAtomicBlockFlush)bundle).DrainAsync();
            return root;
        }

        private static async Task MutateBlockAsync(HistoricalStateStore hist, int b)
        {
            const int nContracts = 4;
            if (b == 1)
            {
                for (int c = 0; c < nContracts; c++)
                {
                    for (int s = 1; s <= 8; s++)
                        await hist.SaveStorageAsync(Contract(c), s, new byte[] { (byte)(0x10 + s), (byte)(c + 1) });
                    await hist.SaveAccountAsync(Contract(c), new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { (byte)(0xC0 + c) } });
                }
                for (int i = 0; i < 30; i++)
                    await hist.SaveAccountAsync(Addr(i), new Account { Balance = 1000 + i, Nonce = 1 });
                return;
            }

            for (int c = 0; c < nContracts; c++)
            {
                var addr = Contract(c);
                await hist.SaveStorageAsync(addr, 100 + b * 4 + c, new byte[] { (byte)b, (byte)c, 0xaa });
                await hist.SaveStorageAsync(addr, 200 + b * 7 + c, new byte[] { (byte)b, 0xbb });
                await hist.SaveStorageAsync(addr, 1 + (c % 8), new byte[] { (byte)(b & 0xff), 0xcc, 0xdd });
                await hist.SaveStorageAsync(addr, 1 + ((b + c) % 8), new byte[0]);
                await hist.SaveAccountAsync(addr, new Account { Nonce = (uint)b, Balance = b * 10, CodeHash = new byte[] { (byte)(0xC0 + c) } });
            }

            for (int i = (b % 6) * 5; i < (b % 6) * 5 + 5 && i < 30; i++)
                await hist.SaveAccountAsync(Addr(i), new Account { Balance = 5000 + b * 100 + i, Nonce = (uint)(b + 1) });
        }

        private async Task<(byte[] finalRoot, int firstDivergentBlock, byte[] divergentActual, byte[] divergentOracle)>
            RunScenarioAsync(bool pathKeyed, int blocks)
        {
            var (mgr, dir) = NewDb(pathKeyed);
            using (mgr)
            using (var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, HistoricalStateOptions.FullArchive, ownsManager: false))
            {
                var hist = (HistoricalStateStore)bundle.State;
                var calc = NewCalc(bundle);

                byte[] prev = null;
                int firstDivergent = -1;
                byte[] divActual = null, divOracle = null;
                for (int b = 1; b <= blocks; b++)
                {
                    byte[] root;
                    try
                    {
                        root = await CommitBlockAsync(bundle, hist, calc, (ulong)b, prev, () => MutateBlockAsync(hist, b));
                    }
                    catch (Exception ex)
                    {
                        _out.WriteLine($"THREW at block {b}: {ex}");
                        firstDivergent = b; divActual = null; divOracle = null;
                        return (prev, firstDivergent, divActual, divOracle);
                    }
                    prev = root;

                    var oracle = await new IncrementalStateRootCalculator(bundle.State, new InMemoryContentNodeStore())
                        .ComputeFullStateRootAsync();
                    if (firstDivergent < 0 && !ByteEq(root, oracle))
                    {
                        firstDivergent = b;
                        divActual = root; divOracle = oracle;
                        _out.WriteLine($"ROOT DIVERGED at block {b}: actual={Hex(root)} oracle={Hex(oracle)}");
                    }
                }
                return (prev, firstDivergent, divActual, divOracle);
            }
        }

        [Fact]
        public async Task Control_HashKeyed_WarmCalculatorMatchesOracleEveryBlock()
        {
            var r = await RunScenarioAsync(pathKeyed: false, blocks: 15);
            Assert.True(r.firstDivergentBlock < 0,
                $"CONTROL (hash-keyed) must match the oracle every block, but diverged at block {r.firstDivergentBlock}: " +
                $"actual={Hex(r.divergentActual)} oracle={Hex(r.divergentOracle)}");
        }

        [Fact]
        public async Task PathKeyed_Collapse_WarmCalculatorMatchesOracleEveryBlock()
        {
            var r = await RunScenarioAsync(pathKeyed: true, blocks: 15);
            if (r.firstDivergentBlock >= 0)
                _out.WriteLine($"DIVERGED at block {r.firstDivergentBlock}: " +
                               $"actual={Hex(r.divergentActual)} oracle={Hex(r.divergentOracle)}");
            Assert.True(r.firstDivergentBlock < 0,
                $"Path-keyed collapse diverged from the oracle at block {r.firstDivergentBlock}: " +
                $"actual={Hex(r.divergentActual)} oracle={Hex(r.divergentOracle)}");
        }

        private async Task<(int throwBlock, int divBlock)> RunMinimalAsync(bool pathKeyed, string mode, int blocks)
        {
            const string C = "0x00000000000000000000000000000000000000ca";
            var (mgr, dir) = NewDb(pathKeyed);
            using (mgr)
            using (var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, HistoricalStateOptions.FullArchive, ownsManager: false))
            {
                var hist = (HistoricalStateStore)bundle.State;
                var calc = NewCalc(bundle);
                byte[] prev = null;
                for (int b = 1; b <= blocks; b++)
                {
                    try
                    {
                        var root = await CommitBlockAsync(bundle, hist, calc, (ulong)b, prev, async () =>
                        {
                            if (b == 1)
                            {
                                for (int s = 1; s <= 16; s++)
                                    await hist.SaveStorageAsync(C, s, new byte[] { (byte)(0x10 + s) });
                                await hist.SaveAccountAsync(C, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 9 } });
                                return;
                            }
                            if (mode == "put" || mode == "putdel")
                                await hist.SaveStorageAsync(C, 100 + b, new byte[] { (byte)b, 0xaa });
                            if (mode == "overwrite" || mode == "putdel")
                                await hist.SaveStorageAsync(C, 1 + (b % 16), new byte[] { (byte)b, 0xcc });
                            if (mode == "delete" || mode == "putdel")
                                await hist.SaveStorageAsync(C, 1 + (b % 16), new byte[0]);
                            await hist.SaveAccountAsync(C, new Account { Nonce = (uint)b, Balance = b, CodeHash = new byte[] { 9 } });
                        });
                        prev = root;
                        var oracle = await new IncrementalStateRootCalculator(bundle.State, new InMemoryContentNodeStore())
                            .ComputeFullStateRootAsync();
                        if (!ByteEq(root, oracle))
                        {
                            _out.WriteLine($"[{mode} pathKeyed={pathKeyed}] ROOT DIVERGED at block {b}");
                            return (-1, b);
                        }
                    }
                    catch (Exception ex)
                    {
                        _out.WriteLine($"[{mode} pathKeyed={pathKeyed}] THREW at block {b}: {ex.Message}");
                        return (b, -1);
                    }
                }
                return (-1, -1);
            }
        }

        [Theory]
        [InlineData("put")]
        [InlineData("overwrite")]
        [InlineData("delete")]
        [InlineData("putdel")]
        public async Task Minimal_Collapse_SingleContract_Isolate(string mode)
        {
            var control = await RunMinimalAsync(pathKeyed: false, mode, blocks: 20);
            Assert.True(control.throwBlock < 0 && control.divBlock < 0,
                $"[{mode}] CONTROL (hash-keyed) diverged/threw (throw={control.throwBlock} div={control.divBlock})");
            var pathKeyed = await RunMinimalAsync(pathKeyed: true, mode, blocks: 20);
            _out.WriteLine($"[{mode}] path-keyed collapse: throwBlock={pathKeyed.throwBlock} divBlock={pathKeyed.divBlock}");
            Assert.True(pathKeyed.throwBlock < 0 && pathKeyed.divBlock < 0,
                $"[{mode}] path-keyed collapse threw@{pathKeyed.throwBlock} / diverged@{pathKeyed.divBlock}");
        }

        private static bool ByteEq(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        private static string Hex(byte[] b) => b == null ? "<null>" : BitConverter.ToString(b);

        public void Dispose()
        {
            foreach (var d in _dirs)
                try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
        }
    }
}
