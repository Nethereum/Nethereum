using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.Chain.TestData.UnitTests
{
    internal sealed class BalCatchUpBed : IDisposable
    {
        private readonly string _dir;

        private BalCatchUpBed(BalChainFixture fixture, string dir, RocksDbChainStoreBundle bundle)
        {
            Fixture = fixture;
            _dir = dir;
            Bundle = bundle;
            Bals = fixture.CreateBlockAccessListPeerSource();
        }

        public BalChainFixture Fixture { get; }

        public RocksDbChainStoreBundle Bundle { get; }

        public ScriptedBlockAccessListPeerSource Bals { get; }

        public ISnapFlatStateWriter Flat => (ISnapFlatStateWriter)Bundle.State;

        public static IReadOnlyList<SnapSyncAccountTask> EverythingFetched { get; } = new[] { Chunk(Filled(0xff), Filled(0xff)) };

        public static byte[] Filled(byte value) => Enumerable.Repeat(value, 32).ToArray();

        public static SnapSyncAccountTask Chunk(byte[] next, byte[] last) => new SnapSyncAccountTask
        {
            Next = next,
            Last = last,
            StorageCompleted = Array.Empty<byte[]>(),
            SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(ByteArrayComparer.Current),
        };

        public static async Task<BalCatchUpBed> OpenAsync(BalChainFixture fixture, ulong flatAt, ulong? missingCanonical = null)
        {
            var dir = Path.Combine(Path.GetTempPath(), $"balcatchup_{Guid.NewGuid():N}");
            var bed = new BalCatchUpBed(fixture, dir, RocksDbChainStoreBundle.Open(dir));
            for (ulong n = 0; n <= fixture.Tip; n++)
                if (n != missingCanonical)
                    await bed.Bundle.Blocks.SaveAsync(fixture.HeaderAt(n), fixture.HashAt(n));
            await bed.DownloadFlatAsync(fixture.HeaderAt(flatAt).StateRoot);
            bed.Bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = flatAt,
                PivotBlockHash = fixture.HashAt(flatAt),
                HealTargetRoot = new byte[32],
                Tasks = EverythingFetched,
                Counters = SnapSyncCounters.Zero,
            });
            return bed;
        }

        private async Task DownloadFlatAsync(byte[] root)
        {
            var client = new SnapSyncClient(Fixture.CreateSnapPeer(), new FlatSnapSyncSink(Flat, Bundle.State))
            {
                AccountConcurrency = 1,
                PivotCatchUp = (tasks, ct) => Task.FromResult(root),
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await client.SyncStateWithCheckpointAsync(root, resumeFrom: null, checkpointSink: _ => { }, ct: cts.Token);
        }

        public SnapBootstrapper.PivotState Pivot(ulong number) => new(Fixture.HeaderAt(number), Fixture.HashAt(number));

        public BalCatchUp CatchUp(SnapBootstrapper.PivotState applied, SnapBootstrapper.PivotState target, IBlockAccessListApplier applier = null)
        {
            var rolling = new SnapBootstrapper.RollingPivot(target.Header, target.Hash);
            return applier == null
                ? BalCatchUp.Create(Bundle, Bals, rolling, applied, null, NullLogger.Instance)
                : new BalCatchUp(Bundle, rolling, applied,
                    new BlockAccessListFetcher(Bals, new BlockAccessListVerifier()), applier, null, NullLogger.Instance);
        }

        public BlockAccessListApplier DurableApplier() => new BlockAccessListApplier(Flat, Bundle.State);

        public Task<FlatTrieGenerationResult> GenerateAsync(ulong number)
            => ((IFlatStateTrieGenerator)Bundle).GenerateTrieFromFlatAsync(
                Fixture.HeaderAt(number).StateRoot, _ => { }, CancellationToken.None);

        public async Task<SnapBootstrapper.PivotState> SavedPivotAsync()
        {
            var saved = Bundle.Metadata.GetSnapSyncState();
            var header = await Bundle.Blocks.GetByHashAsync(saved.PivotBlockHash);
            return new SnapBootstrapper.PivotState(header, saved.PivotBlockHash);
        }

        public void Dispose()
        {
            Bundle.Dispose();
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }
    }

    internal sealed class CountingApplier : IBlockAccessListApplier
    {
        private readonly IBlockAccessListApplier _inner;
        private readonly Func<int, bool> _throwBeforeApplying;
        private readonly Func<int, bool> _throwAfterApplying;
        private int _calls;

        public CountingApplier(IBlockAccessListApplier inner, Func<int, bool> throwBeforeApplying = null, Func<int, bool> throwAfterApplying = null)
        {
            _inner = inner;
            _throwBeforeApplying = throwBeforeApplying ?? (_ => false);
            _throwAfterApplying = throwAfterApplying ?? (_ => false);
        }

        public int Calls => Volatile.Read(ref _calls);

        public async Task ApplyAsync(IReadOnlyList<AccountChanges> blockAccessList, ISnapTaskFrontier frontier, CancellationToken ct = default)
        {
            var call = Interlocked.Increment(ref _calls);
            if (_throwBeforeApplying(call)) throw new OperationCanceledException("process stopped before applying");
            await _inner.ApplyAsync(blockAccessList, frontier, ct).ConfigureAwait(false);
            if (_throwAfterApplying(call)) throw new OperationCanceledException("process stopped before persisting the pivot");
        }
    }
}
