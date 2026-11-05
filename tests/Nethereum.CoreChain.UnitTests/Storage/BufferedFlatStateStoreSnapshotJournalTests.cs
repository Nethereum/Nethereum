using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class BufferedFlatStateStoreSnapshotJournalTests
    {
        private static readonly string[] Addresses =
        {
            "0x1111111111111111111111111111111111111111",
            "0x2222222222222222222222222222222222222222",
            "0x3333333333333333333333333333333333333333",
            "0x4444444444444444444444444444444444444444"
        };

        private const int SlotCount = 6;
        private const int CodeCount = 3;

        private enum OpKind { SaveAccount, DeleteAccount, SaveStorage, ClearStorage, SaveCode }

        private sealed class Op
        {
            public OpKind Kind;
            public string Address;
            public Account Account;
            public BigInteger Slot;
            public byte[] Value;
            public byte[] CodeHash;
        }

        private static byte[] Filled(byte b)
        {
            var bytes = new byte[32];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = b;
            return bytes;
        }

        private static async Task Apply(IStateStore store, Op op)
        {
            switch (op.Kind)
            {
                case OpKind.SaveAccount: await store.SaveAccountAsync(op.Address, op.Account); break;
                case OpKind.DeleteAccount: await store.DeleteAccountAsync(op.Address); break;
                case OpKind.SaveStorage: await store.SaveStorageAsync(op.Address, op.Slot, op.Value); break;
                case OpKind.ClearStorage: await store.ClearStorageAsync(op.Address); break;
                case OpKind.SaveCode: await store.SaveCodeAsync(op.CodeHash, op.Value); break;
            }
        }

        private static Op RandomWrite(Random random)
        {
            var address = Addresses[random.Next(Addresses.Length)];
            switch (random.Next(6))
            {
                case 0:
                    return new Op
                    {
                        Kind = OpKind.SaveAccount,
                        Address = address,
                        Account = new Account
                        {
                            Nonce = random.Next(1, 1000),
                            Balance = random.Next(1, 100000),
                            StateRoot = Filled((byte)random.Next(1, 255)),
                            CodeHash = Filled((byte)random.Next(1, 255))
                        }
                    };
                case 1:
                    return new Op { Kind = OpKind.DeleteAccount, Address = address };
                case 2:
                case 3:
                    return new Op
                    {
                        Kind = OpKind.SaveStorage,
                        Address = address,
                        Slot = random.Next(SlotCount),
                        Value = new[] { (byte)random.Next(1, 255), (byte)random.Next(256) }
                    };
                case 4:
                    return new Op
                    {
                        Kind = random.Next(3) == 0 ? OpKind.ClearStorage : OpKind.SaveStorage,
                        Address = address,
                        Slot = random.Next(SlotCount),
                        Value = random.Next(2) == 0 ? Array.Empty<byte>() : new byte[32]
                    };
                default:
                    return new Op
                    {
                        Kind = OpKind.SaveCode,
                        Address = address,
                        CodeHash = Filled((byte)(random.Next(CodeCount) + 1)),
                        Value = new[] { (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256) }
                    };
            }
        }

        private static string Hex(byte[] bytes) => bytes == null ? "null" : bytes.ToHex();

        private static string SlotValue(byte[] value)
        {
            if (value == null || value.All(b => b == 0)) return "";
            return value.SkipWhile(b => b == 0).ToArray().ToHex();
        }

        private static string Describe(Account account) => account == null
            ? "null"
            : account.Nonce + "|" + account.Balance + "|" + Hex(account.StateRoot) + "|" + Hex(account.CodeHash);

        private static async Task<string> Observe(IStateStore store, HashSet<string> clearedAddresses)
        {
            var lines = new List<string>();
            foreach (var address in Addresses)
            {
                lines.Add("account " + address + " " + Describe(await store.GetAccountAsync(address)));
                lines.Add("exists " + address + " " + await store.AccountExistsAsync(address));
                for (var slot = 0; slot < SlotCount; slot++)
                    lines.Add("slot " + address + " " + slot + " " + SlotValue(await store.GetStorageAsync(address, slot)));
                var all = await store.GetAllStorageAsync(address);
                lines.Add("all " + address + " " + string.Join(",",
                    all.Where(kv => kv.Value != null && kv.Value.Length > 0)
                       .Select(kv => kv.Key.ToHex() + ":" + kv.Value.ToHex()).OrderBy(x => x, StringComparer.Ordinal)));
                var dirtySlots = await store.GetDirtyStorageSlotsAsync(address);
                lines.Add("dirtyslots " + address + " " + string.Join(",", dirtySlots.OrderBy(x => x)));
            }
            for (var code = 1; code <= CodeCount; code++)
                lines.Add("code " + code + " " + Hex(await store.GetCodeAsync(Filled((byte)code))));
            var dirty = await store.GetDirtyAccountAddressesAsync();
            lines.Add("dirty " + string.Join(",", dirty.Select(a => a.ToLowerInvariant()).OrderBy(x => x, StringComparer.Ordinal)));
            lines.Add("cleared " + string.Join(",", clearedAddresses.OrderBy(x => x, StringComparer.Ordinal)));
            return string.Join("\n", lines);
        }

        private static HashSet<string> ClearedBy(IEnumerable<Op> log) => new HashSet<string>(
            log.Where(o => o.Kind == OpKind.ClearStorage || o.Kind == OpKind.DeleteAccount).Select(o => o.Address.ToLowerInvariant()));

        [Fact]
        public async Task Given_RandomWritesWithNestedSnapshots_When_RevertedAndCommitted_Then_EveryReadMatchesTheInMemoryStore()
        {
            var random = new Random(20261016);
            for (var sequence = 0; sequence < 200; sequence++)
            {
                var buffered = new BufferedFlatStateStore(new InMemoryStateStore());
                buffered.BeginBuffering();
                var log = new List<Op>();
                var snapshots = new Stack<(IStateSnapshot Snapshot, int LogLength)>();
                (IStateSnapshot Snapshot, int LogLength)? committedOutermost = null;
                var writtenSinceCommit = false;

                for (var step = 0; step < 60; step++)
                {
                    var choice = random.Next(12);
                    if (choice == 0 && snapshots.Count < 4)
                    {
                        if (snapshots.Count == 0) committedOutermost = null;
                        snapshots.Push((await buffered.CreateSnapshotAsync(), log.Count));
                    }
                    else if (choice == 1 && snapshots.Count > 0)
                    {
                        var (snapshot, logLength) = snapshots.Pop();
                        await buffered.RevertSnapshotAsync(snapshot);
                        log.RemoveRange(logLength, log.Count - logLength);
                    }
                    else if (choice == 2 && snapshots.Count > 0)
                    {
                        var committed = snapshots.Pop();
                        await buffered.CommitSnapshotAsync(committed.Snapshot);
                        if (snapshots.Count == 0) { committedOutermost = committed; writtenSinceCommit = false; }
                    }
                    else if (choice == 3 && snapshots.Count == 0 && committedOutermost.HasValue)
                    {
                        var (snapshot, logLength) = committedOutermost.Value;
                        if (writtenSinceCommit)
                        {
                            await Assert.ThrowsAsync<InvalidOperationException>(() => buffered.RevertSnapshotAsync(snapshot));
                        }
                        else
                        {
                            await buffered.RevertSnapshotAsync(snapshot);
                            log.RemoveRange(logLength, log.Count - logLength);
                        }
                        committedOutermost = null;
                    }
                    else
                    {
                        var op = RandomWrite(random);
                        await Apply(buffered, op);
                        log.Add(op);
                        if (snapshots.Count == 0 && committedOutermost.HasValue) writtenSinceCommit = true;
                    }

                    var reference = new InMemoryStateStore();
                    foreach (var op in log) await Apply(reference, op);

                    var expected = await Observe(reference, ClearedBy(log));
                    var actual = await Observe(buffered, new HashSet<string>(
                        (await buffered.GetStorageClearedAddressesAsync()).Select(a => a.ToLowerInvariant())));
                    Assert.True(expected == actual, "sequence " + sequence + " step " + step + "\nexpected:\n" + expected + "\nactual:\n" + actual);
                }
            }
        }

        [Fact]
        public async Task Given_AWindowHoldingManyBufferedWrites_When_ATransactionSnapshotIsTakenAndCommitted_Then_ItAllocatesTheSameAsOnAnEmptyWindow()
        {
            var buffered = new BufferedFlatStateStore(new InMemoryStateStore());
            buffered.BeginBuffering();

            await TakeAndCommitSnapshot(buffered);
            var emptyWindow = AllocatedByTakingAndCommittingASnapshot(buffered);

            for (var a = 0; a < 1000; a++)
            {
                var address = "0x" + a.ToString("x40");
                await buffered.SaveAccountAsync(address, new Account { Nonce = a + 1, Balance = 1 });
                for (var slot = 1; slot <= 99; slot++)
                    await buffered.SaveStorageAsync(address, slot, new byte[] { (byte)slot });
            }

            var fullWindow = AllocatedByTakingAndCommittingASnapshot(buffered);

            Assert.True(fullWindow - emptyWindow < 4096,
                "empty window " + emptyWindow + " bytes, window with 100k writes " + fullWindow + " bytes");
        }

        private static async Task TakeAndCommitSnapshot(IStateStore store)
        {
            var snapshot = await store.CreateSnapshotAsync();
            await store.CommitSnapshotAsync(snapshot);
        }

        private static long AllocatedByTakingAndCommittingASnapshot(IStateStore store)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            TakeAndCommitSnapshot(store).GetAwaiter().GetResult();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        public async Task Given_AnAccountDeletedThenRewritten_When_TheSnapshotReverts_Then_ItsSlotsAndClearMarkAreBack()
        {
            var address = Addresses[0];
            var inner = new InMemoryStateStore();
            await inner.SaveStorageAsync(address, 7, new byte[] { 0x77 });
            var buffered = new BufferedFlatStateStore(inner);
            buffered.BeginBuffering();
            await buffered.SaveAccountAsync(address, new Account { Nonce = 3, Balance = 30 });
            await buffered.SaveStorageAsync(address, 1, new byte[] { 0x11 });

            var snapshot = await buffered.CreateSnapshotAsync();
            await buffered.DeleteAccountAsync(address);
            await buffered.SaveAccountAsync(address, new Account { Nonce = 9, Balance = 90 });
            await buffered.SaveStorageAsync(address, 9, new byte[] { 0x99 });
            Assert.Contains(address, await buffered.GetStorageClearedAddressesAsync(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal("", SlotValue(await buffered.GetStorageAsync(address, 7)));

            await buffered.RevertSnapshotAsync(snapshot);

            var account = await buffered.GetAccountAsync(address);
            Assert.Equal(3, (int)account.Nonce);
            Assert.Equal("11", SlotValue(await buffered.GetStorageAsync(address, 1)));
            Assert.Equal("77", SlotValue(await buffered.GetStorageAsync(address, 7)));
            Assert.Equal("", SlotValue(await buffered.GetStorageAsync(address, 9)));
            Assert.DoesNotContain(address, await buffered.GetStorageClearedAddressesAsync(), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Given_ACommittedSnapshotWithNoFurtherWrite_When_ItIsReverted_Then_ThePreTransactionStateIsBack()
        {
            var address = Addresses[0];
            var buffered = new BufferedFlatStateStore(new InMemoryStateStore());
            buffered.BeginBuffering();
            await buffered.SaveAccountAsync(address, new Account { Nonce = 1, Balance = 10 });

            var snapshot = await buffered.CreateSnapshotAsync();
            await buffered.SaveAccountAsync(address, new Account { Nonce = 2, Balance = 20 });
            await buffered.SaveStorageAsync(address, 5, new byte[] { 0x55 });
            await buffered.CommitSnapshotAsync(snapshot);

            await buffered.RevertSnapshotAsync(snapshot);

            Assert.Equal(1, (int)(await buffered.GetAccountAsync(address)).Nonce);
            Assert.Equal("", SlotValue(await buffered.GetStorageAsync(address, 5)));
        }

        [Fact]
        public async Task Given_ACommittedSnapshotFollowedByAWriteOutsideAnySnapshot_When_ItIsReverted_Then_ItThrows()
        {
            var address = Addresses[0];
            var buffered = new BufferedFlatStateStore(new InMemoryStateStore());
            buffered.BeginBuffering();
            var snapshot = await buffered.CreateSnapshotAsync();
            await buffered.SaveStorageAsync(address, 5, new byte[] { 0x55 });
            await buffered.CommitSnapshotAsync(snapshot);
            await buffered.SaveAccountAsync(address, new Account { Nonce = 2, Balance = 20 });

            await Assert.ThrowsAsync<InvalidOperationException>(() => buffered.RevertSnapshotAsync(snapshot));
            Assert.Equal(2, (int)(await buffered.GetAccountAsync(address)).Nonce);
        }

        [Fact]
        public async Task Given_ACommittedSnapshot_When_ManyWritesFollowOutsideAnySnapshot_Then_TheyAllocateNoMoreThanWithNoSnapshotEverTaken()
        {
            var withoutSnapshot = new BufferedFlatStateStore(new InMemoryStateStore());
            withoutSnapshot.BeginBuffering();
            var withCommitted = new BufferedFlatStateStore(new InMemoryStateStore());
            withCommitted.BeginBuffering();
            await TakeAndCommitSnapshot(withCommitted);

            AllocatedByWriting(withoutSnapshot, 0, 10);
            AllocatedByWriting(withCommitted, 0, 10);

            var baseline = AllocatedByWriting(withoutSnapshot, 500, 1000);
            var afterCommit = AllocatedByWriting(withCommitted, 500, 1000);

            Assert.True(afterCommit - baseline < 4096,
                "no snapshot ever " + baseline + " bytes, after a committed snapshot " + afterCommit + " bytes");
        }

        private static long AllocatedByWriting(IStateStore store, int first, int count)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var a = first; a < first + count; a++)
                store.SaveAccountAsync("0x" + a.ToString("x40"), new Account { Nonce = a + 1, Balance = 1 }).GetAwaiter().GetResult();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        public async Task Given_ACommittedSnapshotFollowedByANewSnapshot_When_TheOldOneIsReverted_Then_ItThrows()
        {
            var buffered = new BufferedFlatStateStore(new InMemoryStateStore());
            buffered.BeginBuffering();
            var first = await buffered.CreateSnapshotAsync();
            await buffered.SaveStorageAsync(Addresses[0], 1, new byte[] { 1 });
            await buffered.CommitSnapshotAsync(first);
            await buffered.CreateSnapshotAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => buffered.RevertSnapshotAsync(first));
        }

        [Fact]
        public async Task Given_AnOpenSnapshot_When_WritesAreCleared_Then_ItThrows()
        {
            var buffered = new BufferedFlatStateStore(new InMemoryStateStore());
            buffered.BeginBuffering();
            await buffered.SaveStorageAsync(Addresses[0], 1, new byte[] { 1 });

            var snapshot = await buffered.CreateSnapshotAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => buffered.ClearDirtyTrackingAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => buffered.DiscardBufferAsync());

            await buffered.CommitSnapshotAsync(snapshot);
            await buffered.ClearDirtyTrackingAsync();
            await buffered.DiscardBufferAsync();
        }
    }
}
