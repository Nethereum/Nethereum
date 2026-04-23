using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class BinaryLayoutCodeHashTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;

        public BinaryLayoutCodeHashTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_binlayout_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, recursive: true); } catch { }
            }
        }

        [Fact]
        public async Task CodeHash_RoundTrips_ThroughBinaryPackedLayout()
        {
            var store = new RocksDbStateStore(_manager,
                accountLayout: BinaryPackedAccountLayout.Instance);

            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            var address = "0x1234567890123456789012345678901234567890";

            var original = new Account
            {
                Nonce = 7,
                Balance = new EvmUInt256(0xDEADBEEF),
                CodeHash = codeHash
            };

            await store.SaveAccountAsync(address, original);
            var retrieved = await store.GetAccountAsync(address);

            Assert.NotNull(retrieved);
            Assert.Equal((BigInteger)original.Nonce, (BigInteger)retrieved.Nonce);
            Assert.Equal((BigInteger)original.Balance, (BigInteger)retrieved.Balance);
            Assert.Equal(original.CodeHash, retrieved.CodeHash);
        }

        [Fact]
        public async Task CodeHash_IncludedInGetAllAccounts()
        {
            var store = new RocksDbStateStore(_manager,
                accountLayout: BinaryPackedAccountLayout.Instance);

            var codeHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0x60, 0x00 });
            var address = "0xAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

            await store.SaveAccountAsync(address, new Account
            {
                Nonce = 1,
                Balance = 100,
                CodeHash = codeHash
            });

            var all = await store.GetAllAccountsAsync();
            Assert.Single(all);

            var entry = all.Values.GetEnumerator();
            entry.MoveNext();
            Assert.Equal(codeHash, entry.Current.CodeHash);
        }

        [Fact]
        public async Task DeleteAccount_RemovesCodeHashSlot()
        {
            var store = new RocksDbStateStore(_manager,
                accountLayout: BinaryPackedAccountLayout.Instance);

            var codeHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0x60, 0x00 });
            var address = "0xBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

            await store.SaveAccountAsync(address, new Account
            {
                Nonce = 1,
                Balance = 100,
                CodeHash = codeHash
            });

            await store.DeleteAccountAsync(address);
            var retrieved = await store.GetAccountAsync(address);
            Assert.Null(retrieved);
        }

        // Validates that a contract account persisted through RocksDB with
        // BinaryPackedAccountLayout + dual-slot code_hash produces the same
        // state root as the jsign-validated fixture (acc1f843...).
        // Uses the same canonical state as the jsign cross-check:
        // address=0x1000..., balance=1000, nonce=1, code=[PUSH1 01 PUSH1 00 SSTORE].
        [Fact]
        public async Task StateRoot_MatchesJsignVector_AfterRocksDbRoundTrip()
        {
            var hashProvider = new Blake3HashProvider();
            var store = new RocksDbStateStore(_manager,
                accountLayout: BinaryPackedAccountLayout.Instance);

            var address = "0x1000000000000000000000000000000000000000";
            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            var balance = new EvmUInt256(1000);
            ulong nonce = 1;

            await store.SaveCodeAsync(codeHash, code);
            await store.SaveAccountAsync(address, new Account
            {
                Nonce = nonce,
                Balance = balance,
                CodeHash = codeHash
            });
            await store.ClearDirtyTrackingAsync();

            var retrieved = await store.GetAccountAsync(address);
            Assert.NotNull(retrieved);
            Assert.Equal((BigInteger)nonce, (BigInteger)retrieved.Nonce);
            Assert.Equal((BigInteger)balance, (BigInteger)retrieved.Balance);
            Assert.Equal(codeHash, retrieved.CodeHash);

            // Same state in InMemory for head-to-head comparison
            var memStore = new InMemoryStateStore();
            await memStore.SaveCodeAsync(codeHash, code);
            await memStore.SaveAccountAsync(address, new Account
            {
                Nonce = nonce, Balance = balance, CodeHash = codeHash
            });
            await memStore.ClearDirtyTrackingAsync();

            var memCalc = new BinaryIncrementalStateRootCalculator(memStore, hashProvider);
            var memRoot = await memCalc.ComputeFullStateRootAsync();

            var dbCalc = new BinaryIncrementalStateRootCalculator(store, hashProvider);
            var dbRoot = await dbCalc.ComputeFullStateRootAsync();

            // InMemory matches jsign (cross-checked 2026-04-21)
            Assert.Equal(
                "acc1f843250ebabbc9c2aa5392741656da98ffb3ec5246b9a64f79ef16048a83",
                memRoot.ToHex());

            // RocksDb with BinaryPackedAccountLayout + dual-slot code_hash
            // must produce the same root
            Assert.Equal(memRoot.ToHex(), dbRoot.ToHex());
        }
    }
}
