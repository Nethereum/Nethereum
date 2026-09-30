using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SyncNodeFacadeTests
    {
        [Fact]
        public void Given_Resources_When_Constructed_Then_ExposesThemAsProperties()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var activations = new FixedActivations();
            var peers = new EmptyFakePeerPool();
            var scheduler = new UnusedScheduler();

            var node = new SyncNode(bundle, activations, NullLogger.Instance, peers, scheduler, serving: null);

            Assert.Same(peers, node.Peers);
            Assert.Same(scheduler, node.Scheduler);
            Assert.Null(node.Serving);
        }

        [Fact]
        public void Given_NullBundle_When_Constructed_Then_Throws()
        {
            var activations = new FixedActivations();

            Assert.Throws<ArgumentNullException>(() =>
                new SyncNode(bundle: null, activations, NullLogger.Instance));
        }

        [Fact]
        public void Given_NullActivations_When_Constructed_Then_Throws()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            Assert.Throws<ArgumentNullException>(() =>
                new SyncNode(bundle, activations: null, NullLogger.Instance));
        }

        [Fact]
        public async Task Given_NoServingListener_When_StartServingAsync_Then_CompletesWithoutError()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var activations = new FixedActivations();

            var node = new SyncNode(bundle, activations, NullLogger.Instance, serving: null);

            var task = node.StartServingAsync();
            await task;

            Assert.True(task.IsCompletedSuccessfully);
        }

        private sealed class FixedActivations : IChainActivations
        {
            public HardforkName ResolveAt(long blockNumber, ulong timestamp) => HardforkName.Cancun;
        }

        private sealed class EmptyFakePeerPool : IPeerPool
        {
            public IReadOnlyCollection<IEthPeer> ActivePeers { get; } = Array.Empty<IEthPeer>();
            public int TargetPeerCount => 0;
            public event EventHandler<IEthPeer> PeerAdded;
            public event EventHandler<IEthPeer> PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }

        private sealed class UnusedScheduler : IFetchRequestScheduler
        {
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }
    }
}
