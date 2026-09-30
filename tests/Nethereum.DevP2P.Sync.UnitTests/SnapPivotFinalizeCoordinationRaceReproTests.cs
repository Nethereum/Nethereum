using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapPivotFinalizeCoordinationRaceReproTests
    {
        private readonly ITestOutputHelper _out;

        public SnapPivotFinalizeCoordinationRaceReproTests(ITestOutputHelper output) => _out = output;

        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private static byte[] BuildSingleOwnerStateRoot(InMemoryContentNodeStore store, byte[] owner, int seed)
        {
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            var key = keccak.CalculateHash(new[] { (byte)seed });
            storageTrie.Put(key, new byte[] { (byte)seed, 0xCD });
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var account = new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)seed,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var stateTrie = new PatriciaTrie(store);
            stateTrie.Put(owner, new AccountEncoder().Encode(account));
            stateTrie.SaveDirtyNodesToStorage();
            return stateTrie.Root.GetHash();
        }

        private sealed class RaceForcingSink : ISnapSyncSink
        {
            private readonly ISnapSyncSink _inner;
            private readonly TaskCompletionSource<bool> _driverGate;
            private readonly SnapBootstrapper.RollingPivot _rollingPivot;
            private readonly byte[] _expectedRacedRoot;

            public bool ObservedRaceLandedBeforeFinalise { get; private set; }

            public RaceForcingSink(
                ISnapSyncSink inner,
                TaskCompletionSource<bool> driverGate,
                SnapBootstrapper.RollingPivot rollingPivot,
                byte[] expectedRacedRoot)
            {
                _inner = inner;
                _driverGate = driverGate;
                _rollingPivot = rollingPivot;
                _expectedRacedRoot = expectedRacedRoot;
            }

            public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => _inner.BeginAsync(targetRoot, ct);
            public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct)
                => _inner.WriteAccountAsync(accountHash, slimRlp, ct);
            public ValueTask<IStorageScope> BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
                => _inner.BeginAccountStorageAsync(accountHash, expectedStorageRoot, ct);
            public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
                => _inner.WriteBytecodeAsync(codeHash, code, ct);

            public async ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct)
            {
                _driverGate.TrySetResult(true);

                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!ByteUtil.AreEqual(_rollingPivot.Current.Header.StateRoot, _expectedRacedRoot))
                {
                    if (DateTime.UtcNow > deadline)
                        throw new TimeoutException("driver never adopted the raced pivot — harness assumption broken");
                    await Task.Delay(5, CancellationToken.None).ConfigureAwait(false);
                }
                ObservedRaceLandedBeforeFinalise = true;

                return await _inner.FinaliseRootAsync(ct).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task Given_Phase2ConvergesCleanlyWithNoMidAttemptPivotMove_When_TheBackgroundDriverAdoptsAFresherPivotBetweenTheFinalNoPendingMoveCheckAndFinaliseRootAsync_Then_RollingPivotCurrentDivergesFromWhatWasActuallyVerified_AndTheClientTrieStoreHasNoDataForIt()
        {
            var store = new InMemoryContentNodeStore();
            var owner = new byte[32];
            owner[0] = 0x77;
            owner[31] = 0x01;

            var rootA = BuildSingleOwnerStateRoot(store, owner, seed: 11);
            var rootB = BuildSingleOwnerStateRoot(store, owner, seed: 22);
            Assert.False(ByteArrayComparer.Current.Equals(rootA, rootB));

            var headerA = new BlockHeader { BlockNumber = new EvmUInt256(1000), StateRoot = rootA };
            var hashA = new byte[32]; hashA[0] = 0xAA;
            var headerB = new BlockHeader { BlockNumber = new EvmUInt256(1005), StateRoot = rootB };
            var hashB = new byte[32]; hashB[0] = 0xBB;

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var peer = new InProcessSnapPeer(handler);

            var rollingPivot = new SnapBootstrapper.RollingPivot(headerA, hashA);

            var driverGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int refresherCalls = 0;
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> rawPivotRefresher =
                async (forceFresh, ct) =>
                {
                    Interlocked.Increment(ref refresherCalls);
                    await driverGate.Task.ConfigureAwait(false);
                    return (headerB, hashB);
                };

            var clientPivotRefresher = SnapBootstrapper.BuildClientPivotRefresher(
                rawPivotRefresher, rollingPivot, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            var clientNodeStore = new InMemoryContentNodeStore();
            var clientStateStore = new InMemoryStateStore();
            var realSink = new TrieSnapSyncSink(clientNodeStore, clientStateStore, flatWriter: null);
            var raceSink = new RaceForcingSink(realSink, driverGate, rollingPivot, rootB);

            var client = new SnapSyncClient(peer, raceSink)
            {
                AccountConcurrency = 1,
                RootRefreshIntervalMs = 10,
                PivotRefresher = clientPivotRefresher,
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await client.SyncStateWithCheckpointAsync(
                rootA, resumeFrom: null, checkpointSink: null, ct: cts.Token);

            Assert.True(result.RootMatchesTarget);
            Assert.Equal(rootA, result.ComputedRoot, ByteArrayComparer.Current);
            Assert.Equal(rootA, result.FinalTargetRoot, ByteArrayComparer.Current);
            Assert.True(raceSink.ObservedRaceLandedBeforeFinalise);
            Assert.True(refresherCalls >= 1);

            Assert.Equal(rootB, rollingPivot.Current.Header.StateRoot, ByteArrayComparer.Current);
            Assert.NotEqual(rollingPivot.Current.Header.StateRoot, result.FinalTargetRoot, ByteArrayComparer.Current);

            var rootBNode = clientNodeStore.Get(rootB);
            _out.WriteLine($"rootA=0x{rootA.ToHex()} rootB(raced, would-be-persisted)=0x{rootB.ToHex()} " +
                            $"clientStore has node for rootB: {rootBNode != null}");
            Assert.Null(rootBNode);

            Assert.NotNull(clientNodeStore.Get(rootA));
        }
    }
}
