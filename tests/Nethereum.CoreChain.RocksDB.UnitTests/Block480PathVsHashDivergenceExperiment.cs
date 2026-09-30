using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class Block480PathVsHashDivergenceExperiment : IDisposable
    {
        private readonly ITestOutputHelper _out;

        private readonly string _pathDir, _hashDir, _oracleDir;
        private readonly RocksDbManager _pathMgr, _hashMgr, _oracleMgr;
        private readonly RocksDbChainStoreBundle _pathBundle, _hashBundle;
        private readonly RocksDbStateStore _oracleState;
        private readonly RocksDbPathTrieNodeStore _oracleTrie;

        private const string CA = "0x00000000000000000000000000000000000000ca";
        private const string CB = "0x00000000000000000000000000000000000000cb";
        private const string CC = "0x00000000000000000000000000000000000000cc";
        private const string EOA7702 = "0x0000000000000000000000000000000000007702";
        private const string CN = "0x00000000000000000000000000000000000000c0";

        public Block480PathVsHashDivergenceExperiment(ITestOutputHelper output)
        {
            _out = output;
            _pathDir = NewDir("b480-path");
            _hashDir = NewDir("b480-hash");
            _oracleDir = NewDir("b480-oracle");

            _pathMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _pathDir, PathKeyedState = true });
            _hashMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _hashDir, PathKeyedState = false });
            _oracleMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _oracleDir, PathKeyedState = true });

            _pathBundle = RocksDbChainStoreBundle.FromManager(_pathMgr, _pathDir, HistoricalStateOptions.Default, ownsManager: false);
            _hashBundle = RocksDbChainStoreBundle.FromManager(_hashMgr, _hashDir, HistoricalStateOptions.Default, ownsManager: false);

            _oracleState = new RocksDbStateStore(_oracleMgr);
            _oracleTrie = new RocksDbPathTrieNodeStore(_oracleMgr);
        }

        [Fact]
        public Task ColdCache_StorageWriteToPreExistingContract()
            => RunScenario(async s =>
            {
                await s.SaveStorageAsync(CA, 999, ValueBytes(0xABCD));
            });

        [Fact]
        public Task ColdCache_OverwriteExistingSlotOnPreExistingContract()
            => RunScenario(async s =>
            {
                await s.SaveStorageAsync(CA, 3, ValueBytes(0x5555));
            });

        [Fact]
        public Task ColdCache_PartialStorageDeleteOnPreExistingContract()
            => RunScenario(async s =>
            {
                for (int i = 0; i < 10; i++)
                    await s.SaveStorageAsync(CB, i, Array.Empty<byte>());
            });

        [Fact]
        public Task ColdCache_SelfDestructPreExistingContract()
            => RunScenario(async s =>
            {
                await s.ClearStorageAsync(CC);
                await s.DeleteAccountAsync(CC);
            });

        [Fact]
        public Task ColdCache_SetCode7702OnPreExistingEOA()
            => RunScenario(async s =>
            {
                // EIP-7702 type-4 setcode: an EOA gets a delegation indicator (0xef0100 || address). Only the
                // account's CodeHash changes; storage is untouched.
                var code = "0xef0100".HexToByteArray().ConcatArrays(
                    AddressUtil.Current.ConvertToValid20ByteAddress(CA).HexToByteArray());
                var codeHash = new Sha3Keccack().CalculateHash(code);
                await s.SaveCodeAsync(codeHash, code);
                var acct = await s.GetAccountAsync(EOA7702);
                acct.CodeHash = codeHash;
                acct.Nonce = acct.Nonce + 1;
                await s.SaveAccountAsync(EOA7702, acct);
            });

        [Fact]
        public Task ColdCache_ContractCreationWithStorage()
            => RunScenario(async s =>
            {
                var code = "0x60016000556002600155".HexToByteArray();
                var codeHash = new Sha3Keccack().CalculateHash(code);
                await s.SaveCodeAsync(codeHash, code);
                await s.SaveAccountAsync(CN, new Account { Balance = 7, Nonce = 1, CodeHash = codeHash });
                for (int i = 0; i < 8; i++)
                    await s.SaveStorageAsync(CN, i, ValueBytes(i + 500));
            });

        [Fact]
        public Task ColdCache_FullBlock480OperationMix()
            => RunScenario(async s =>
            {
                var code = "0x60016000556002600155".HexToByteArray();
                var codeHash = new Sha3Keccack().CalculateHash(code);
                await s.SaveCodeAsync(codeHash, code);
                await s.SaveAccountAsync(CN, new Account { Balance = 7, Nonce = 1, CodeHash = codeHash });
                for (int i = 0; i < 8; i++)
                    await s.SaveStorageAsync(CN, i, ValueBytes(i + 500));

                await s.SaveStorageAsync(CA, 999, ValueBytes(0xABCD));
                await s.SaveStorageAsync(CA, 3, ValueBytes(0x5555));

                for (int i = 0; i < 10; i++)
                    await s.SaveStorageAsync(CB, i, Array.Empty<byte>());

                await s.ClearStorageAsync(CC);
                await s.DeleteAccountAsync(CC);

                var deleg = "0xef0100".HexToByteArray().ConcatArrays(
                    AddressUtil.Current.ConvertToValid20ByteAddress(CA).HexToByteArray());
                var delegHash = new Sha3Keccack().CalculateHash(deleg);
                await s.SaveCodeAsync(delegHash, deleg);
                var e = await s.GetAccountAsync(EOA7702);
                e.CodeHash = delegHash; e.Nonce = e.Nonce + 1;
                await s.SaveAccountAsync(EOA7702, e);

                for (int i = 0; i < 16; i += 2)
                {
                    var a = await s.GetAccountAsync(Addr(i));
                    a.Balance = a.Balance + 1_000_000; a.Nonce = a.Nonce + 1;
                    await s.SaveAccountAsync(Addr(i), a);
                }
            });

        [Fact]
        public Task ColdCache_LargeTrie_ColdDeletesTriggerStructuralCollapse()
            => RunScenarioLarge(
                accountCount: 6000, contractCount: 0, slotsPerContract: 0,
                block480: async s =>
                {
                    for (int i = 0; i < 6000; i += 3)
                        await s.DeleteAccountAsync(AddrN(i));
                    for (int i = 1; i < 6000; i += 7)
                    {
                        var a = await s.GetAccountAsync(AddrN(i));
                        if (a != null) { a.Balance = a.Balance + 123; a.Nonce = a.Nonce + 1; await s.SaveAccountAsync(AddrN(i), a); }
                    }
                });

        [Fact]
        public Task ColdCache_LargeStorageTrie_ColdPartialDeleteCollapse()
            => RunScenarioLarge(
                accountCount: 200, contractCount: 3, slotsPerContract: 2500,
                block480: async s =>
                {
                    foreach (var c in new[] { LC(0), LC(1), LC(2) })
                    {
                        for (int i = 0; i < 2500; i += 3)
                            await s.SaveStorageAsync(c, i, Array.Empty<byte>());
                        for (int i = 100000; i < 100050; i++)
                            await s.SaveStorageAsync(c, i, ValueBytes(i));
                    }
                });

        private const string LargeContractPrefix = "0x00000000000000000000000000000000000d";
        private static string LC(int i) => LargeContractPrefix + i.ToString("x4");
        private static string AddrN(int i) => "0x" + i.ToString("x").PadLeft(40, '0');

        private async Task RunScenarioLarge(int accountCount, int contractCount, int slotsPerContract, Func<IStateStore, Task> block480)
        {
            await ApplyToAll(async s =>
            {
                for (int i = 0; i < accountCount; i++)
                    await s.SaveAccountAsync(AddrN(i), new Account { Balance = 1000 + i, Nonce = 1 });
                for (int c = 0; c < contractCount; c++)
                {
                    await s.SaveAccountAsync(LC(c), new Account { Balance = 0, Nonce = 1 });
                    for (int i = 0; i < slotsPerContract; i++)
                        await s.SaveStorageAsync(LC(c), i, ValueBytes(i + 1));
                }
            });

            var checkpointPath = await BuildAndPersist(_pathBundle);
            var checkpointHash = await BuildAndPersist(_hashBundle);
            Assert.Equal(checkpointHash.ToHex(), checkpointPath.ToHex());

            await ApplyToAll(block480);

            var root480Path = await FollowWithFreshCalculator(_pathBundle, checkpointPath);
            var root480Hash = await FollowWithFreshCalculator(_hashBundle, checkpointHash);
            var oracle = await OracleFullRebuildRoot();

            _out.WriteLine($"checkpoint root : {checkpointPath.ToHex()}");
            _out.WriteLine($"oracle   (480)  : {oracle.ToHex()}");
            _out.WriteLine($"hash-keyed (480): {root480Hash.ToHex()}");
            _out.WriteLine($"path-keyed (480): {root480Path.ToHex()}");

            Assert.Equal(oracle.ToHex(), root480Hash.ToHex());
            Assert.Equal(oracle.ToHex(), root480Path.ToHex());
            Assert.Equal(root480Hash.ToHex(), root480Path.ToHex());
        }

        [Fact]
        public async Task ColdCache_MultiBlockWarmCarry_476to480_ThenColdContractTouch()
        {
            await ApplyToAll(async s =>
            {
                for (int i = 0; i < 400; i++)
                    await s.SaveAccountAsync(AddrN(i), new Account { Balance = 1000 + i, Nonce = 1 });
                foreach (var c in new[] { CA, CB, CC })
                {
                    await s.SaveAccountAsync(c, new Account { Balance = 0, Nonce = 1 });
                    for (int i = 0; i < 48; i++)
                        await s.SaveStorageAsync(c, i, ValueBytes(i + 1));
                }
            });
            var cpPath = await BuildAndPersist(_pathBundle);
            var cpHash = await BuildAndPersist(_hashBundle);
            Assert.Equal(cpHash.ToHex(), cpPath.ToHex());

            var pathCalc = new IncrementalStateRootCalculator(_pathBundle.State, _pathBundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(_pathBundle.StateTrieNodes, _pathBundle.TrieNodes));
            var hashCalc = new IncrementalStateRootCalculator(_hashBundle.State, _hashBundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(_hashBundle.StateTrieNodes, _hashBundle.TrieNodes));

            byte[] prevPath = cpPath, prevHash = cpHash;
            for (int blk = 0; blk < 4; blk++)
            {
                await ApplyToAll(async s =>
                {
                    for (int i = blk; i < 400; i += 11)
                    {
                        var a = await s.GetAccountAsync(AddrN(i));
                        a.Balance = a.Balance + 7; a.Nonce = a.Nonce + 1;
                        await s.SaveAccountAsync(AddrN(i), a);
                    }
                });
                prevPath = await CommitOneBlock(pathCalc, prevPath);
                prevHash = await CommitOneBlock(hashCalc, prevHash);
                Assert.Equal(prevHash.ToHex(), prevPath.ToHex());
            }

            await ApplyToAll(async s =>
            {
                await s.SaveStorageAsync(CA, 999, ValueBytes(0xABCD));
                await s.SaveStorageAsync(CA, 3, ValueBytes(0x5555));
                for (int i = 0; i < 12; i++) await s.SaveStorageAsync(CB, i, Array.Empty<byte>());
                await s.ClearStorageAsync(CC);
                await s.DeleteAccountAsync(CC);
                var deleg = "0xef0100".HexToByteArray().ConcatArrays(AddressUtil.Current.ConvertToValid20ByteAddress(CA).HexToByteArray());
                var dh = new Sha3Keccack().CalculateHash(deleg);
                await s.SaveCodeAsync(dh, deleg);
                await s.SaveAccountAsync(EOA7702, new Account { Balance = 9, Nonce = 2, CodeHash = dh });
            });
            var root480Path = await CommitOneBlock(pathCalc, prevPath);
            var root480Hash = await CommitOneBlock(hashCalc, prevHash);
            var oracle = await OracleFullRebuildRoot();

            _out.WriteLine($"oracle   (480)  : {oracle.ToHex()}");
            _out.WriteLine($"hash-keyed (480): {root480Hash.ToHex()}");
            _out.WriteLine($"path-keyed (480): {root480Path.ToHex()}");
            Assert.Equal(oracle.ToHex(), root480Hash.ToHex());
            Assert.Equal(oracle.ToHex(), root480Path.ToHex());
        }

        private static async Task<byte[]> CommitOneBlock(IncrementalStateRootCalculator calc, byte[] prevRoot)
        {
            var root = await calc.ComputeStateRootWithoutPersistAsync(prevRoot);
            await calc.PersistPendingStateAsync();
            return root;
        }

        private async Task RunScenario(Func<IStateStore, Task> block480Ops)
        {
            await ApplyToAll(async s =>
            {
                for (int i = 0; i < 40; i++)
                    await s.SaveAccountAsync(Addr(i), new Account { Balance = 1000 + i, Nonce = 1 });
                await s.SaveAccountAsync(EOA7702, new Account { Balance = 5, Nonce = 1 });
                foreach (var c in new[] { CA, CB, CC })
                {
                    await s.SaveAccountAsync(c, new Account { Balance = 0, Nonce = 1 });
                    for (int i = 0; i < 32; i++)
                        await s.SaveStorageAsync(c, i, ValueBytes(i + 1));
                }
            });

            var checkpointPath = await BuildAndPersist(_pathBundle);
            var checkpointHash = await BuildAndPersist(_hashBundle);
            Assert.Equal(checkpointHash.ToHex(), checkpointPath.ToHex());

            await ApplyToAll(block480Ops);

            var root480Path = await FollowWithFreshCalculator(_pathBundle, checkpointPath);
            var root480Hash = await FollowWithFreshCalculator(_hashBundle, checkpointHash);

            var oracle = await OracleFullRebuildRoot();

            _out.WriteLine($"checkpoint root : {checkpointPath.ToHex()}");
            _out.WriteLine($"oracle   (480)  : {oracle.ToHex()}");
            _out.WriteLine($"hash-keyed (480): {root480Hash.ToHex()}");
            _out.WriteLine($"path-keyed (480): {root480Path.ToHex()}");

            Assert.Equal(oracle.ToHex(), root480Hash.ToHex());
            Assert.Equal(oracle.ToHex(), root480Path.ToHex());
            Assert.Equal(root480Hash.ToHex(), root480Path.ToHex());
        }

        private async Task<byte[]> BuildAndPersist(RocksDbChainStoreBundle bundle)
        {
            var builder = new IncrementalStateRootCalculator(
                bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));
            var root = await builder.ComputeStateRootAsync();
            return root;
        }

        private async Task<byte[]> FollowWithFreshCalculator(RocksDbChainStoreBundle bundle, byte[] checkpointRoot)
        {
            var calc = new IncrementalStateRootCalculator(
                bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));
            var root = await calc.ComputeStateRootWithoutPersistAsync(checkpointRoot);
            await calc.PersistPendingStateAsync();
            return root;
        }

        private async Task<byte[]> OracleFullRebuildRoot()
        {
            _oracleTrie.Clear();
            var rebuild = new IncrementalStateRootCalculator(_oracleState, _oracleTrie);
            var root = await rebuild.ComputeFullStateRootAsync();
            _oracleTrie.Flush();
            return root;
        }

        private async Task ApplyToAll(Func<IStateStore, Task> ops)
        {
            await ops(_pathBundle.State);
            await ops(_hashBundle.State);
            await ops(_oracleState);
        }

        private static string Addr(int i) => "0x" + i.ToString("x").PadLeft(40, '0');

        private static byte[] ValueBytes(int v)
        {
            var b = new BigInteger(v).ToByteArray(isUnsigned: true, isBigEndian: true);
            return b.Length == 0 ? new byte[] { 0 } : b;
        }

        private static string NewDir(string prefix)
        {
            var d = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        public void Dispose()
        {
            _pathBundle?.Dispose();
            _hashBundle?.Dispose();
            _pathMgr?.Dispose();
            _hashMgr?.Dispose();
            _oracleMgr?.Dispose();
            foreach (var d in new[] { _pathDir, _hashDir, _oracleDir })
            {
                try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { }
            }
        }
    }
}
