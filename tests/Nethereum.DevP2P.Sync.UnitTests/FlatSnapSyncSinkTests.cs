using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FlatSnapSyncSinkTests
    {
        private const int WhaleSlots = 40;
        private static readonly byte[] Whale = Hash(0x30, 0x02);

        private static byte[] Hash(byte first, byte last)
        {
            var h = new byte[32];
            h[0] = first;
            h[31] = last;
            return h;
        }

        [Fact]
        public async Task Given_AFlatSnapSyncSink_When_AnAccountAndAVerifiedStoragePageAreWritten_Then_OnlyFlatRowsAreWrittenAndFinaliseRootThrows()
        {
            var flat = new RecordingFlatWriter();
            var codes = new InMemoryStateStore();
            var sink = new FlatSnapSyncSink(flat, codes);
            var account = new Account
            {
                Nonce = (EvmUInt256)3,
                Balance = (EvmUInt256)77,
                StateRoot = Hash(0x5e, 0x01),
                CodeHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0x60, 0x00 }),
            };
            var owner = Hash(0x11, 0x01);
            var slot = Hash(0x22, 0x02);

            await sink.BeginAsync(new byte[32], CancellationToken.None);
            await sink.WriteAccountAsync(owner, SlimAccountEncoder.ToSlim(new AccountEncoder().Encode(account)), CancellationToken.None);
            var scope = await sink.BeginAccountStorageAsync(owner, account.StateRoot, CancellationToken.None);
            await scope.WriteSlotAsync(slot, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x0a, 0x0b }), CancellationToken.None);
            await scope.EndAsync(CancellationToken.None);
            await sink.WriteBytecodeAsync(account.CodeHash, new byte[] { 0x60, 0x00 }, CancellationToken.None);

            Assert.Equal(new[] { "account:" + owner.ToHex(), "storage:" + owner.ToHex() + ":" + slot.ToHex() }, flat.Operations);
            var written = flat.Accounts[owner.ToHex()];
            Assert.Equal(account.Balance, written.Balance);
            Assert.Equal(account.Nonce, written.Nonce);
            Assert.Equal(account.StateRoot.ToHex(), written.StateRoot.ToHex());
            Assert.Equal(account.CodeHash.ToHex(), written.CodeHash.ToHex());
            Assert.Equal("0a0b", flat.StorageOf(owner, slot).ToHex());
            Assert.Equal("6000", (await codes.GetCodeAsync(account.CodeHash)).ToHex());
            Assert.Equal((1, 1, 1), (sink.AccountCount, sink.SlotCount, sink.BytecodeCount));
            Assert.Same(flat, sink.FlatWriter);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await sink.FinaliseRootAsync(CancellationToken.None));
        }

        [Fact]
        public async Task Given_AWhaleLargerThanOnePage_When_DownloadedThroughTheFlatSnapSyncSink_Then_ItIsChunkedIntoCursoredSubtasksEverySlotReachesFlatAndNoDebtIsRecorded()
        {
            var (store, root, whaleSlots) = BuildState();
            var flat = new RecordingFlatWriter();
            var run = await DownloadAsync(store, root, new FlatSnapSyncSink(flat, new InMemoryStateStore()), withPivotCatchUp: true);

            Assert.True(run.WhaleHadCursoredSubtasks, "the whale was not chunked into cursored subtasks");
            Assert.Equal(0, run.CatchUps);
            Assert.Empty(run.Result.AccountsNeedingHeal);
            Assert.Equal(0, run.DeferredStorageDebts);
            Assert.Equal(WhaleSlots, flat.StorageCountOf(Whale));
            foreach (var (slotHash, raw) in whaleSlots)
                Assert.Equal(raw.ToHex(), flat.StorageOf(Whale, slotHash).ToHex());
            Assert.Equal(3, flat.Accounts.Count);
        }

        [Fact]
        public async Task Given_AWhaleLargerThanOnePage_When_DownloadedThroughTheInMemorySnapSyncSink_Then_ItIsPulledWithoutCursoredSubtasks()
        {
            var (store, root, _) = BuildState();
            var run = await DownloadAsync(store, root, new InMemorySnapSyncSink(), withPivotCatchUp: false);

            Assert.False(run.WhaleHadCursoredSubtasks);
            Assert.True(run.Result.RootMatchesTarget);
            Assert.Empty(run.Result.AccountsNeedingHeal);
        }

        private static (InMemoryContentNodeStore Store, byte[] Root, List<(byte[] SlotHash, byte[] Raw)> WhaleSlots) BuildState()
        {
            var store = new InMemoryContentNodeStore();
            var keccak = new Sha3Keccack();
            var whaleSlots = new List<(byte[] SlotHash, byte[] Raw)>();
            var storageTrie = new PatriciaTrie(store, Whale);
            for (int i = 0; i < WhaleSlots; i++)
            {
                var slotHash = keccak.CalculateHash(new[] { (byte)0x77, (byte)i });
                var raw = new byte[] { (byte)(i + 1), 0xCD };
                storageTrie.Put(slotHash, Nethereum.RLP.RLP.EncodeElement(raw));
                whaleSlots.Add((slotHash, raw));
            }
            storageTrie.SaveDirtyNodesToStorage();

            var stateTrie = new PatriciaTrie(store);
            void Put(byte[] hash, byte[] storageRoot) => stateTrie.Put(hash, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)5,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            Put(Hash(0x10, 0x01), DefaultValues.EMPTY_TRIE_HASH);
            Put(Whale, storageTrie.Root.GetHash());
            Put(Hash(0x90, 0x03), DefaultValues.EMPTY_TRIE_HASH);
            stateTrie.SaveDirtyNodesToStorage();
            return (store, stateTrie.Root.GetHash(), whaleSlots);
        }

        private sealed record DownloadRun(SnapSyncClient.SyncResult Result, bool WhaleHadCursoredSubtasks, int DeferredStorageDebts, int CatchUps);

        private static async Task<DownloadRun> DownloadAsync(
            InMemoryContentNodeStore store, byte[] root, ISnapSyncSink sink, bool withPivotCatchUp)
        {
            var peer = new SmallStoragePagesPeer(new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NoBytecodes())));
            var catchUps = 0;
            var whaleSubtasks = false;
            var debts = 0;
            var client = new SnapSyncClient(peer, sink)
            {
                AccountConcurrency = 1,
                LargeContractConcurrency = 2,
                CheckpointBytesThreshold = 1,
            };
            if (withPivotCatchUp)
                client.PivotCatchUp = (tasks, ct) => { Interlocked.Increment(ref catchUps); return Task.FromResult(root); };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await client.SyncStateWithCheckpointAsync(root, resumeFrom: null,
                checkpointSink: cp =>
                {
                    if (cp.State.Tasks.Any(t => t.SubTasks.ContainsKey(Whale))) whaleSubtasks = true;
                    debts += cp.DeferredStorageDebts.Count;
                },
                ct: cts.Token);
            return new DownloadRun(result, whaleSubtasks, debts, catchUps);
        }

        private sealed class NoBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class SmallStoragePagesPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;

            public SmallStoragePagesPeer(ISnapPeer inner) => _inner = inner;

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(r, ct);

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => _inner.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = r.RequestId,
                    RootHash = r.RootHash,
                    AccountHashes = r.AccountHashes,
                    StartingHash = r.StartingHash,
                    LimitHash = r.LimitHash,
                    ResponseBytes = 200,
                }, ct);

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        private sealed class RecordingFlatWriter : ISnapFlatStateWriter
        {
            private readonly object _gate = new();
            public readonly Dictionary<string, Account> Accounts = new();
            public readonly Dictionary<string, byte[]> Storage = new();
            public readonly List<string> Operations = new();

            public byte[] StorageOf(byte[] accountHash, byte[] slotHash)
            {
                lock (_gate) return Storage.TryGetValue(accountHash.ToHex() + ":" + slotHash.ToHex(), out var v) ? v : null;
            }

            public int StorageCountOf(byte[] accountHash)
            {
                lock (_gate) return Storage.Keys.Count(k => k.StartsWith(accountHash.ToHex() + ":"));
            }

            public Task<Account> GetAccountByHashAsync(byte[] accountHash)
            {
                lock (_gate) return Task.FromResult(Accounts.TryGetValue(accountHash.ToHex(), out var a) ? a : null);
            }

            public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
            {
                lock (_gate)
                {
                    Accounts[accountHash.ToHex()] = account;
                    Operations.Add("account:" + accountHash.ToHex());
                }
                return Task.CompletedTask;
            }

            public Task DeleteAccountByHashAsync(byte[] accountHash)
            {
                lock (_gate)
                {
                    Accounts.Remove(accountHash.ToHex());
                    Operations.Add("delete:" + accountHash.ToHex());
                }
                return Task.CompletedTask;
            }

            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
            {
                lock (_gate)
                {
                    Storage[accountHash.ToHex() + ":" + slotKeccak.ToHex()] = value;
                    Operations.Add("storage:" + accountHash.ToHex() + ":" + slotKeccak.ToHex());
                }
                return Task.CompletedTask;
            }
        }
    }
}
