using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.Chain.TestData.UnitTests
{
    internal sealed class Snap2BootstrapBed : IDisposable
    {
        private readonly string _dir;

        private Snap2BootstrapBed(BalChainFixture fixture, string dir, RocksDbManager rocks, RocksDbChainStoreBundle bundle)
        {
            Fixture = fixture;
            _dir = dir;
            Rocks = rocks;
            Bundle = bundle;
            Bals = fixture.CreateBlockAccessListPeerSource();
            Scheduler = fixture.CreateBlockingBackfillScheduler();
        }

        public static byte[] SecondHalfStart { get; } = Enumerable.Range(0, 32).Select(i => i == 0 ? (byte)0x80 : (byte)0).ToArray();

        public static byte[] FirstHalfEnd { get; } = Enumerable.Range(0, 32).Select(i => i == 0 ? (byte)0x7f : (byte)0xff).ToArray();

        public BalChainFixture Fixture { get; }

        public RocksDbManager Rocks { get; }

        public RocksDbChainStoreBundle Bundle { get; }

        public ScriptedBlockAccessListPeerSource Bals { get; }

        public BlockingBackfillScheduler Scheduler { get; }

        public RecordingLogger Log { get; } = new();

        public SnapBootstrapper.RollingPivot Rolling { get; private set; }

        public ISnapFlatStateWriter Flat => (ISnapFlatStateWriter)Bundle.State;

        public static async Task<Snap2BootstrapBed> OpenAsync(BalChainFixture fixture, ulong? missingCanonical = null)
        {
            var dir = Path.Combine(Path.GetTempPath(), $"snap2boot_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            var rocks = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            var bed = new Snap2BootstrapBed(fixture, dir, rocks, RocksDbChainStoreBundle.FromManager(rocks, dir));
            for (ulong n = 0; n <= fixture.Tip; n++)
                if (n != missingCanonical)
                    await bed.Bundle.Blocks.SaveAsync(fixture.HeaderAt(n), fixture.HashAt(n));
            return bed;
        }

        public static SnapSyncAccountTask Task(byte[] next, byte[] last) => BalCatchUpBed.Chunk(next, last);

        public static IReadOnlyList<SnapSyncAccountTask> BothHalvesUnfetched()
            => new[] { Task(new byte[32], FirstHalfEnd), Task(SecondHalfStart, BalCatchUpBed.Filled(0xff)) };

        public static IReadOnlyList<SnapSyncAccountTask> SecondHalfUnfetched()
            => new[] { Task(SecondHalfStart, BalCatchUpBed.Filled(0xff)) };

        public void MoveTipTo(ulong number) => Volatile.Write(ref _tipBox, new TipBox(Fixture.HeaderAt(number), Fixture.HashAt(number)));

        private TipBox _tipBox;

        private sealed record TipBox(BlockHeader Header, byte[] Hash);

        public async Task DownloadFlatAtAsync(ulong number)
        {
            var root = Fixture.HeaderAt(number).StateRoot;
            var client = new SnapSyncClient(Fixture.CreateSnapPeer(), new FlatSnapSyncSink(Flat, Bundle.State))
            {
                AccountConcurrency = 1,
                PivotCatchUp = (tasks, ct) => System.Threading.Tasks.Task.FromResult(root),
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await client.SyncStateWithCheckpointAsync(root, resumeFrom: null, checkpointSink: _ => { }, ct: cts.Token);
        }

        public void SaveState(SnapPhase phase, ulong pivot, IReadOnlyList<SnapSyncAccountTask> tasks, byte[] pivotHash = null)
            => Bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = phase,
                PivotBlockNumber = pivot,
                PivotBlockHash = pivotHash ?? Fixture.HashAt(pivot),
                HealTargetRoot = phase == SnapPhase.Phase2Running ? new byte[32] : Fixture.HeaderAt(pivot).StateRoot,
                Tasks = tasks,
                Counters = SnapSyncCounters.Zero,
            });

        public Task<SnapBootstrapper.Result> RunAsync(
            ulong bootPivot,
            ISnapPeer peer,
            CancellationToken ct,
            bool balHealEnabled = true,
            bool runBackfill = false,
            IChainActivations activations = null,
            IBlockAccessListPeerSource bals = null,
            IBlockAccessListApplier applier = null,
            int accountConcurrency = 1,
            int rootRefreshIntervalMs = 20)
        {
            Rolling = new SnapBootstrapper.RollingPivot(Fixture.HeaderAt(bootPivot), Fixture.HashAt(bootPivot));
            return SnapBootstrapper.RunAsync(
                Bundle, peer, Fixture.HeaderAt(bootPivot), Fixture.HashAt(bootPivot), Log,
                new SnapRunOptions
                {
                    Scheduler = Scheduler,
                    Pool = new IdlePeerPool(),
                    Activations = activations ?? new FixedChainActivations(HardforkName.Amsterdam),
                    RunBackfill = runBackfill,
                    UseBackwardSkeleton = false,
                    ExternalHeaderFollow = true,
                    BalHealEnabled = balHealEnabled,
                    RootRefreshIntervalMs = rootRefreshIntervalMs,
                    AccountConcurrency = accountConcurrency,
                    PivotRefresher = RefreshAsync,
                    RollingPivot = Rolling,
                    BlockAccessListPeers = bals ?? Bals,
                    BlockAccessListApplier = applier,
                },
                ct);
        }

        private Task<(BlockHeader Header, byte[] Hash)?> RefreshAsync(bool forceFresh, CancellationToken ct)
        {
            var tip = Volatile.Read(ref _tipBox);
            return System.Threading.Tasks.Task.FromResult<(BlockHeader Header, byte[] Hash)?>(
                tip == null ? null : (tip.Header, tip.Hash));
        }

        public HoldingSnapPeer Peer() => new(Fixture.CreateSnapHandler());

        public HoldingSnapPeer Peer(int softResponseLimit) => new(Fixture.CreateSnapHandler(softResponseLimit));

        public HoldingSnapPeer PeerHoldingSecondHalfAt(ulong block)
        {
            var root = Fixture.HeaderAt(block).StateRoot;
            return new HoldingSnapPeer(Fixture.CreateSnapHandler())
            {
                Hold = request => ByteUtil.AreEqual(request.RootHash, root) && ByteUtil.AreEqual(request.StartingHash, SecondHalfStart),
            };
        }

        public async Task CorruptFlatAccountAsync(string address)
        {
            var account = await FlatAccountAsync(address);
            account.Balance = (EvmUInt256)999_999;
            await Flat.SaveAccountByHashAsync(Sha3Keccack.Current.CalculateHash(address.HexToByteArray()), account);
        }

        public Task WriteGhostAccountAsync(byte[] accountHash)
            => Flat.SaveAccountByHashAsync(accountHash, new Account
            {
                Nonce = (EvmUInt256)7,
                Balance = (EvmUInt256)7,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });

        public IReadOnlyList<string> RequestedBalBlocks()
            => Bals.RequestedBlockHashes.Select(h => h.ToHex()).ToList();

        public IReadOnlyList<string> Hashes(ulong from, ulong to)
        {
            var hashes = new List<string>();
            for (var n = from; n <= to; n++) hashes.Add(Fixture.HashAt(n).ToHex());
            return hashes;
        }

        public long CountRows(string columnFamily)
        {
            using var iterator = Rocks.CreateIterator(columnFamily);
            long rows = 0;
            for (iterator.SeekToFirst(); iterator.Valid(); iterator.Next()) rows++;
            return rows;
        }

        public long CountTrieRows()
            => CountRows(RocksDbManager.CF_TRIE_NODES)
               + CountRows(RocksDbManager.CF_STATE_TRIE_ACCOUNT)
               + CountRows(RocksDbManager.CF_STATE_TRIE_STORAGE);

        public Task<Account> FlatAccountAsync(string address)
            => Flat.GetAccountByHashAsync(Sha3Keccack.Current.CalculateHash(address.HexToByteArray()));

        public void Dispose()
        {
            Bundle.Dispose();
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }
    }

    internal sealed class RecordingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public Action<string> OnMessage { get; set; }

        public IReadOnlyList<string> Messages => _messages.ToList();

        public bool Contains(string fragment) => _messages.Any(m => m.Contains(fragment, StringComparison.Ordinal));

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            _messages.Enqueue(message);
            OnMessage?.Invoke(message);
        }
    }

    internal sealed class HoldingSnapPeer : ISnapPeer
    {
        private readonly ISnapRequestHandler _handler;
        private readonly ConcurrentQueue<(string Root, string Start)> _accountRanges = new();
        private int _requests;
        private int _inFlight;

        public HoldingSnapPeer(ISnapRequestHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public Func<GetAccountRangeMessage, bool> Hold { get; set; } = _ => false;

        public Action OnHeld { get; set; }

        public Action OnReleased { get; set; }

        public TimeSpan DrainDelay { get; set; } = TimeSpan.Zero;

        public Action<int> AfterRequest { get; set; }

        public Action<GetAccountRangeMessage> OnAccountRange { get; set; }

        public int Requests => Volatile.Read(ref _requests);

        public int InFlight => Volatile.Read(ref _inFlight);

        public IReadOnlyList<(string Root, string Start)> AccountRanges => _accountRanges.ToList();

        public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
        {
            _accountRanges.Enqueue((request.RootHash.ToHex(), request.StartingHash.ToHex()));
            OnAccountRange?.Invoke(request);
            Interlocked.Increment(ref _inFlight);
            try
            {
                if (Hold(request))
                {
                    OnHeld?.Invoke();
                    try
                    {
                        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        OnReleased?.Invoke();
                        if (DrainDelay > TimeSpan.Zero)
                            await Task.Delay(DrainDelay, CancellationToken.None).ConfigureAwait(false);
                        throw;
                    }
                }
                return await ServeAsync(() => _handler.GetAccountRangeAsync(request, ct)).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        public Func<GetStorageRangesMessage, bool> HoldStorage { get; set; } = _ => false;

        public Action<GetStorageRangesMessage> AfterStorage { get; set; }

        public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
        {
            if (HoldStorage(request))
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            var response = await ServeAsync(() => _handler.GetStorageRangesAsync(request, ct)).ConfigureAwait(false);
            AfterStorage?.Invoke(request);
            return response;
        }

        public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
            => ServeAsync(() => _handler.GetByteCodesAsync(request, ct));

        public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
            => ServeAsync(() => _handler.GetTrieNodesAsync(request, ct));

        private async Task<T> ServeAsync<T>(Func<Task<T>> serve)
        {
            var response = await serve().ConfigureAwait(false);
            AfterRequest?.Invoke(Interlocked.Increment(ref _requests));
            return response;
        }
    }

    internal sealed class SilentBlockAccessListPeerSource : IBlockAccessListPeerSource
    {
        private readonly SilentPeer _peer = new();

        public IReadOnlyList<IBlockAccessListPeer> GetServiceablePeers() => new IBlockAccessListPeer[] { _peer };

        private sealed class SilentPeer : IBlockAccessListPeer
        {
            public string Id => "silent-bal-peer";

            public async Task<IReadOnlyList<byte[]>> RequestBlockAccessListsAsync(
                IReadOnlyList<byte[]> blockHashes, ulong responseBytes, CancellationToken ct)
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return Array.Empty<byte[]>();
            }
        }
    }

    internal sealed class NoOpApplier : IBlockAccessListApplier
    {
        public Task ApplyAsync(IReadOnlyList<AccountChanges> blockAccessList, ISnapTaskFrontier frontier, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    internal sealed class ForkFromBlock : IChainActivations
    {
        private readonly long _amsterdamFrom;

        public ForkFromBlock(long amsterdamFrom) => _amsterdamFrom = amsterdamFrom;

        public HardforkName ResolveAt(long blockNumber, ulong timestamp)
            => blockNumber >= _amsterdamFrom ? HardforkName.Amsterdam : HardforkName.Osaka;
    }

    internal static class Snap2Chain
    {
        public static string LowA { get; } = FindAddress(low: true, skip: 0);
        public static string LowB { get; } = FindAddress(low: true, skip: 1);
        public static string HighA { get; } = FindAddress(low: false, skip: 0);
        public static string HighB { get; } = FindAddress(low: false, skip: 1);

        public static string Whale { get; } = FindAddress(low: true, skip: 2);

        public const int WhaleSlots = 40;

        public static ulong BalanceAt(ulong block) => 100 + block;

        public static BalChainFixture BuildWithWhale(int blocks, ulong rewriteWhaleFrom)
        {
            var genesis = new List<AccountChanges>
            {
                Balance(LowA, 100, 1), Balance(HighA, 100, 1), Balance(Whale, 1, 1), WhaleStorage(0),
            };
            var chain = new List<IReadOnlyList<AccountChanges>>();
            for (ulong block = 1; block <= (ulong)blocks; block++)
            {
                var changes = new List<AccountChanges> { Balance(LowA, BalanceAt(block)), Balance(HighA, BalanceAt(block)) };
                if (block >= rewriteWhaleFrom) changes.Add(WhaleStorage(block));
                chain.Add(changes);
            }
            return BalChainFixture.Build(genesis, chain);
        }

        private static AccountChanges WhaleStorage(ulong block)
        {
            var change = new AccountChanges(Whale);
            for (ulong slot = 1; slot <= WhaleSlots; slot++)
            {
                var slotChanges = new SlotChanges(slot);
                slotChanges.Changes.Add(new StorageChange(0, block * 1000 + slot));
                change.StorageChanges.Add(slotChanges);
            }
            return change;
        }

        public static BalChainFixture Build(int blocks, ulong? emptyHighBAt = null)
        {
            var genesis = new List<AccountChanges>
            {
                Balance(LowA, 100, 1), Balance(LowB, 200, 1), Balance(HighA, 100, 1), Balance(HighB, 200, 1),
            };
            var chain = new List<IReadOnlyList<AccountChanges>>();
            for (ulong block = 1; block <= (ulong)blocks; block++)
            {
                var changes = new List<AccountChanges> { Balance(LowA, BalanceAt(block)), Balance(HighA, BalanceAt(block)) };
                if (emptyHighBAt == block) changes.Add(Balance(HighB, 0, 0));
                chain.Add(changes);
            }
            return BalChainFixture.Build(genesis, chain);
        }

        private static AccountChanges Balance(string address, ulong balance, ulong? nonce = null)
        {
            var change = new AccountChanges(address);
            change.BalanceChanges.Add(new BalanceChange(0, balance));
            if (nonce.HasValue) change.NonceChanges.Add(new NonceChange(0, nonce.Value));
            return change;
        }

        private static string FindAddress(bool low, int skip)
        {
            var found = 0;
            for (var i = 1; ; i++)
            {
                var address = "0x3" + i.ToString("x").PadLeft(39, '0');
                var hash = Sha3Keccack.Current.CalculateHash(address.HexToByteArray());
                if ((hash[0] < 0x80) == low && found++ == skip) return address;
            }
        }
    }
}
