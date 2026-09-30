using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FlatStatePruneTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"flatprune_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static byte[] Hash(byte leading, byte fill = 0x00)
        {
            var hash = Enumerable.Repeat(fill, 32).ToArray();
            hash[0] = leading;
            return hash;
        }

        private static byte[] Ff32 => Enumerable.Repeat((byte)0xff, 32).ToArray();

        private static byte[] StorageRowKey(byte[] owner, byte[] slot) => owner.Concat(slot).ToArray();

        private static byte[] CodeHashRowKey(byte[] accountHash) => accountHash.Concat(new byte[] { 0x01 }).ToArray();

        private static SnapSyncStorageSubTask SubTask(byte[] owner, byte[] next, byte[] last) => new SnapSyncStorageSubTask
        {
            AccountHash = owner,
            Next = next,
            Last = last,
            StorageRoot = DefaultValues.EMPTY_TRIE_HASH,
        };

        [Fact]
        public async Task Given_FlatRowsInsideAndOutsideEachTasksUncoveredRange_When_PruneFlatStateBeyondRuns_Then_OnlyUncoveredRowsAreDeleted()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dir);
            var flat = new RocksDbStateStore(bundle.Rocks);

            var belowNext = Hash(0x10);
            var atNext = Hash(0x20);
            var inside = Hash(0x25);
            var storageCompleted = Hash(0x30);
            var whale = Hash(0x50);
            var atLastOfA = Hash(0x7f, 0xff);
            var betweenTasks = Hash(0x80);
            var insideB = Hash(0xc5);
            var atLastOfB = Ff32;

            var taskA = new SnapSyncAccountTask
            {
                Next = atNext,
                Last = atLastOfA,
                StorageCompleted = new List<byte[]> { storageCompleted, belowNext },
                SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(ByteArrayComparer.Current)
                {
                    [whale] = new[]
                    {
                        SubTask(whale, Hash(0x60), Hash(0x7f, 0xff)),
                        SubTask(whale, Hash(0xa0), Ff32),
                    },
                },
            };
            var taskB = new SnapSyncAccountTask
            {
                Next = Hash(0xc0),
                Last = atLastOfB,
                StorageCompleted = new List<byte[]>(),
                SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(ByteArrayComparer.Current),
            };

            var survivingAccounts = new[] { belowNext, betweenTasks };
            var prunedAccounts = new[] { atNext, inside, storageCompleted, whale, atLastOfA, insideB, atLastOfB };
            foreach (var account in survivingAccounts.Concat(prunedAccounts))
            {
                await flat.SaveAccountByHashAsync(account, new Account { Nonce = 1, Balance = 1 });
                bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, CodeHashRowKey(account), DefaultValues.EMPTY_DATA_HASH);
            }

            var survivingSlots = new List<byte[]>
            {
                StorageRowKey(belowNext, Hash(0x01)),
                StorageRowKey(storageCompleted, Hash(0x01)),
                StorageRowKey(storageCompleted, Ff32),
                StorageRowKey(whale, Hash(0x10)),
                StorageRowKey(whale, Hash(0x90)),
                StorageRowKey(betweenTasks, Hash(0x01)),
            };
            var prunedSlots = new List<byte[]>
            {
                StorageRowKey(atNext, Hash(0x01)),
                StorageRowKey(inside, Hash(0x01)),
                StorageRowKey(whale, Hash(0x60)),
                StorageRowKey(whale, Hash(0x70)),
                StorageRowKey(whale, Hash(0x7f, 0xff)),
                StorageRowKey(whale, Hash(0xa0)),
                StorageRowKey(whale, Ff32),
                StorageRowKey(atLastOfA, Ff32),
                StorageRowKey(insideB, Hash(0x01)),
                StorageRowKey(atLastOfB, Ff32),
            };
            foreach (var row in survivingSlots.Concat(prunedSlots))
                await flat.SaveStorageByHashAsync(row.Take(32).ToArray(), row.Skip(32).ToArray(), new byte[] { 0x01 });

            bundle.PruneFlatStateBeyond(new[] { taskA, taskB });

            foreach (var account in survivingAccounts)
            {
                Assert.NotNull(bundle.Rocks.Get(RocksDbManager.CF_STATE_ACCOUNTS, account));
                Assert.NotNull(bundle.Rocks.Get(RocksDbManager.CF_STATE_ACCOUNTS, CodeHashRowKey(account)));
            }
            foreach (var account in prunedAccounts)
            {
                Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_ACCOUNTS, account));
                Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_ACCOUNTS, CodeHashRowKey(account)));
            }
            foreach (var row in survivingSlots)
                Assert.NotNull(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, row));
            foreach (var row in prunedSlots)
                Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, row));
        }
    }
}
