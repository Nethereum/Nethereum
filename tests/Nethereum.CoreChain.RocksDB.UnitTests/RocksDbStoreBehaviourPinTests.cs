using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbStoreBehaviourPinTests : IDisposable
    {
        private const string Address = "0x1111111111111111111111111111111111111111";
        private static readonly byte[] BlockHash = Enumerable.Repeat((byte)0xBB, 32).ToArray();
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"storepins_{Guid.NewGuid():N}");
        private readonly List<IDisposable> _owned = new();

        public RocksDbStoreBehaviourPinTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            foreach (var owned in _owned) owned.Dispose();
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        private RocksDbManager NewManager(string name)
        {
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = Path.Combine(_root, name) });
            _owned.Add(manager);
            return manager;
        }

        private static List<(string Cf, string Key, string Value)> Dump(RocksDbManager manager, params string[] cfs)
        {
            var rows = new List<(string, string, string)>();
            foreach (var cf in cfs)
            {
                using var it = manager.CreateIterator(cf);
                for (it.SeekToFirst(); it.Valid(); it.Next())
                    rows.Add((cf, Convert.ToHexString(it.Key()), Convert.ToHexString(it.Value())));
            }
            return rows;
        }

        private static List<Log> Logs(int count) => Enumerable.Range(0, count).Select(i => new Log
        {
            Address = "0x" + new string('a', 40),
            Topics = new List<byte[]> { Enumerable.Repeat((byte)(0x10 + i), 32).ToArray() },
            Data = new byte[] { (byte)i }
        }).ToList();

        private static BlockStateDiff AccountDiff(BigInteger block, ulong balance) => new BlockStateDiff
        {
            BlockNumber = block,
            AccountDiffs = new List<AccountDiffEntry>
            {
                new AccountDiffEntry
                {
                    Address = Address,
                    PreValue = new Account { Balance = balance, Nonce = 1, CodeHash = new byte[32], StateRoot = new byte[32] }
                }
            }
        };

        private static BlockStateDiff StorageDiff(BigInteger block, BigInteger slot, byte value) => new BlockStateDiff
        {
            BlockNumber = block,
            StorageDiffs = new List<StorageDiffEntry>
            {
                new StorageDiffEntry { Address = Address, SlotKey = StateKeys.StorageSlotKey(slot), PreValue = new[] { value } }
            }
        };

        [Fact]
        public async Task Save_And_StageInto_Produce_Identical_BlockMeta_Withdrawals()
        {
            var saved = NewManager("saved");
            var staged = NewManager("staged");
            var withdrawals = new List<Withdrawal>
            {
                new Withdrawal { Index = 1, ValidatorIndex = 2, Address = Enumerable.Repeat((byte)3, 20).ToArray(), AmountInGwei = 4 },
                new Withdrawal { Index = 5, ValidatorIndex = 6, Address = Enumerable.Repeat((byte)7, 20).ToArray(), AmountInGwei = 8 }
            };
            foreach (var manager in new[] { saved, staged })
                manager.Put(HistoryColumnFamilies.BlockHashIndex, BlockHash, HistoryKeys.BlockKey(12));

            await new RocksDbWithdrawalStore(saved).SaveAsync(BlockHash, withdrawals);
            var stagedStore = new RocksDbWithdrawalStore(staged);
            using (var batch = staged.CreateWriteBatch())
            {
                stagedStore.StageInto(batch, BlockHash, withdrawals);
                staged.Write(batch);
            }

            Assert.Equal(Dump(saved, HistoryColumnFamilies.BlockMeta), Dump(staged, HistoryColumnFamilies.BlockMeta));
            var read = await stagedStore.GetByBlockNumberAsync(12);
            Assert.Equal(2, read.Count);
            Assert.Equal(8UL, read[1].AmountInGwei);
        }

        [Fact]
        public async Task SaveLogs_Writes_Same_Keys_As_SaveManyLogs_For_One_Tx()
        {
            var single = NewManager("single");
            var many = NewManager("many");
            var txHash = Enumerable.Repeat((byte)0xCC, 32).ToArray();
            var cfs = new[]
            {
                RocksDbManager.CF_LOGS, RocksDbManager.CF_LOG_BY_BLOCK,
                RocksDbManager.CF_LOG_BY_ADDRESS, RocksDbManager.CF_LOG_BY_TX
            };

            await new RocksDbLogStore(single).SaveLogsAsync(Logs(3), txHash, BlockHash, 21, 4);
            await new RocksDbLogStore(many).SaveManyLogsAsync(
                new List<(List<Log> Logs, byte[] TxHash, int TxIndex)> { (Logs(3), txHash, 4) }, BlockHash, 21);

            var singleRows = Dump(single, cfs);
            Assert.NotEmpty(singleRows);
            Assert.Equal(singleRows, Dump(many, cfs));
        }

        [Fact]
        public async Task GetLogsByBlockHash_And_ByTxHash_Return_Same_Logs_As_Before()
        {
            var store = new RocksDbLogStore(NewManager("byhash"));
            var txHash = Enumerable.Repeat((byte)0xCC, 32).ToArray();
            await store.SaveLogsAsync(Logs(3), txHash, BlockHash, 21, 4);

            var byTx = await store.GetLogsByTxHashAsync(txHash);
            var byBlock = await store.GetLogsByBlockHashAsync(BlockHash);

            Assert.Equal(new[] { 0, 1, 2 }, byTx.Select(l => l.LogIndex).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, byBlock.Select(l => l.LogIndex).ToArray());
            Assert.All(byTx.Concat(byBlock), log => Assert.Equal(21, log.BlockNumber));
            Assert.Empty(await store.GetLogsByTxHashAsync(Enumerable.Repeat((byte)0xDD, 32).ToArray()));
            Assert.Empty(await store.GetLogsByBlockHashAsync(Enumerable.Repeat((byte)0xDD, 32).ToArray()));
        }

        [Fact]
        public async Task SaveBlockDiff_Twice_Leaves_Only_Latest_Rows()
        {
            var store = new RocksDbStateDiffStore(NewManager("twice"));
            await store.SaveBlockDiffAsync(AccountDiff(10, 1));
            await store.SaveBlockDiffAsync(StorageDiff(10, 5, 0x09));

            var diff = await store.GetBlockDiffAsync(10);

            Assert.Empty(diff.AccountDiffs);
            Assert.Single(diff.StorageDiffs);
            var (foundAccount, _) = await store.GetFirstAccountPreValueAfterBlockAsync(Address, 9);
            Assert.False(foundAccount);
        }

        [Fact]
        public async Task DeleteDiffsBelow_Removes_Data_And_Index_Rows()
        {
            var manager = NewManager("below");
            var store = new RocksDbStateDiffStore(manager);
            foreach (var block in new[] { 5, 10, 15 })
                await store.SaveBlockDiffAsync(AccountDiff(block, (ulong)block));
            var before = Dump(manager, RocksDbManager.CF_STATE_HISTORY_ACCOUNTS, RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX).Count;

            await store.DeleteDiffsBelowBlockAsync(10);

            var after = Dump(manager, RocksDbManager.CF_STATE_HISTORY_ACCOUNTS, RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX).Count;
            Assert.Equal(before - 2, after);
            Assert.Equal((BigInteger)10, await store.GetOldestDiffBlockAsync());
            Assert.Equal((BigInteger)15, await store.GetNewestDiffBlockAsync());
            Assert.Null(await store.GetBlockDiffAsync(5));
        }

        [Fact]
        public async Task GetBlockDiff_Roundtrips_Account_And_Storage()
        {
            var store = new RocksDbStateDiffStore(NewManager("roundtrip"));
            var diff = AccountDiff(20, 77);
            diff.StorageDiffs.Add(new StorageDiffEntry
            {
                Address = Address, SlotKey = StateKeys.StorageSlotKey(3), PreValue = new byte[] { 0x42 }
            });
            await store.SaveBlockDiffAsync(diff);

            var read = await store.GetBlockDiffAsync(20);

            Assert.Equal(20, read.BlockNumber);
            Assert.Equal(77, Assert.Single(read.AccountDiffs).PreValue.Balance);
            var storage = Assert.Single(read.StorageDiffs);
            Assert.Equal(new byte[] { 0x42 }, storage.PreValue);
            Assert.Equal(StateKeys.StorageSlotKey(3), storage.SlotKey);
        }

        [Fact]
        public async Task StreamAccounts_And_GetAllAccounts_Return_Same_Set()
        {
            var store = new RocksDbStateStore(NewManager("accounts"));
            for (int i = 1; i <= 5; i++)
                await store.SaveAccountAsync(
                    "0x" + new string((char)('0' + i), 40),
                    new Account { Balance = (ulong)i, Nonce = (ulong)i, CodeHash = DefaultValues.EMPTY_DATA_HASH, StateRoot = DefaultValues.EMPTY_TRIE_HASH });

            var all = await store.GetAllAccountsAsync();
            var streamed = new Dictionary<string, Account>();
            await foreach (var kv in store.StreamAccountsAsync()) streamed[kv.Key] = kv.Value;

            Assert.Equal(5, all.Count);
            Assert.Equal(all.Keys.OrderBy(k => k), streamed.Keys.OrderBy(k => k));
            Assert.All(all, kv => Assert.Equal(kv.Value.Balance, streamed[kv.Key].Balance));
        }

        [Fact]
        public async Task SaveStorage_Tracks_Dirty_Slots_Same_For_String_And_EvmAddress()
        {
            const string mixedCase = "0xAbCdEf0123456789aBcDeF0123456789AbCdEf01";
            var byString = new RocksDbStateStore(NewManager("dirty-string"));
            var byAddress = new RocksDbStateStore(NewManager("dirty-evm"));

            foreach (var slot in new[] { 7, 9, 7 })
            {
                await byString.SaveStorageAsync(mixedCase, slot, new byte[] { 1 });
                await byAddress.SaveStorageAsync(EvmAddress.FromHex(mixedCase), slot, new byte[] { 1 });
            }

            var accountsByString = (await byString.GetDirtyAccountAddressesAsync()).OrderBy(a => a).ToArray();
            var accountsByAddress = (await byAddress.GetDirtyAccountAddressesAsync()).OrderBy(a => a).ToArray();
            var slotsByString = (await byString.GetDirtyStorageSlotsAsync(mixedCase)).OrderBy(x => x).ToArray();
            var slotsByAddress = (await byAddress.GetDirtyStorageSlotsAsync(mixedCase)).OrderBy(x => x).ToArray();

            Assert.Single(accountsByString);
            Assert.Equal(accountsByString, accountsByAddress);
            Assert.Equal(new BigInteger[] { 7, 9 }, slotsByString);
            Assert.Equal(slotsByString, slotsByAddress);
        }

        [Fact]
        public async Task SaveCheckpoint_Failure_Cleans_Staging_Dirs()
        {
            var manager = NewManager("checkpoint-db");
            var dataDir = Path.Combine(_root, "checkpoint-data");
            var checkpoints = new RocksDbCheckpointManager(manager, dataDir, new RocksDbChainMetadataStore(manager));

            await Assert.ThrowsAsync<ArgumentException>(
                () => checkpoints.SaveCheckpointAsync(5, new byte[31], new byte[32]));

            Assert.False(Directory.Exists(checkpoints.ResolveCheckpointSnapshotPath(5)));
            Assert.Empty(Directory.GetDirectories(Path.Combine(dataDir, ".cp")));
        }

        [Fact]
        public async Task SaveCheckpoint_Success_Keeps_The_Snapshot_Dir()
        {
            var manager = NewManager("checkpoint-ok-db");
            var dataDir = Path.Combine(_root, "checkpoint-ok-data");
            var checkpoints = new RocksDbCheckpointManager(manager, dataDir, new RocksDbChainMetadataStore(manager));

            var saved = await checkpoints.SaveCheckpointAsync(5, new byte[32], new byte[32]);

            Assert.Equal(5UL, saved.BlockNumber);
            Assert.True(Directory.Exists(checkpoints.ResolveCheckpointSnapshotPath(5)));
        }
    }
}
