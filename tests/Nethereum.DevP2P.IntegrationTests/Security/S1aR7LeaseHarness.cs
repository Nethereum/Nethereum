using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public enum LeaseLoop
    {
        Eth68ServerSession,
        MultiProtocolRlpxSession,
        PeerListener
    }

    internal sealed class LeaseLoopHarness : IAsyncDisposable
    {
        public const int PaddedHashes = 120000;
        private const byte SlowMarker = 0xAA;
        private const byte ProbeMarker = 0x01;

        private readonly LeaseLoop _loop;
        private readonly EthECKey _serverKey = EthECKey.GenerateKey();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly List<Task> _sessions = new List<Task>();
        private readonly List<RawRlpxPeer> _peers = new List<RawRlpxPeer>();
        private readonly List<RlpxConnection> _accepted = new List<RlpxConnection>();
        private readonly TaskCompletionSource<bool> _gate =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _probeServed =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private RlpxListener _rlpx;
        private PeerListener _peerListener;
        private InMemoryChainStoreBundle _bundle;
        private int _slowServed;

        public LeaseLoopHarness(LeaseLoop loop, long capacityBytes)
        {
            _loop = loop;
            Limiter = new InFlightDecodeLimiter(capacityBytes);
        }

        public InFlightDecodeLimiter Limiter { get; }

        public int SlowRequestsInHandlers => Volatile.Read(ref _slowServed);

        public Task ProbeServed => _probeServed.Task;

        public static int PaddedRequestLength(LeaseLoop loop) => PaddedRequest(loop).Length;

        public async Task StartAsync()
        {
            if (_loop == LeaseLoop.PeerListener)
            {
                _bundle = InMemoryChainStoreBundle.Open();
                var options = new PeerListenerOptions
                {
                    ListenPort = 0,
                    BindAddress = IPAddress.Loopback,
                    MaxInboundPeers = 20,
                    MaxInboundPerIP = 20,
                    ServeSnap = false,
                    HandshakeTimeoutMs = 5000,
                    TxPool = new BlockingTxPool(this),
                    DecodeLimiter = Limiter
                };
                _peerListener = new PeerListener(_serverKey, _bundle, options);
                await _peerListener.StartAsync();
                return;
            }

            var config = new DevP2PConfig { ClientId = "Nethereum/lease", HandshakeTimeoutMs = 5000, DecodeLimiter = Limiter };
            _rlpx = new RlpxListener(_serverKey, config);
            _rlpx.PeerAccepted += OnAccepted;
            _rlpx.Start(0, IPAddress.Loopback);
        }

        private int Port => _loop == LeaseLoop.PeerListener ? _peerListener.Port : _rlpx.Port;

        private static Eth68StatusMessage Status() => new Eth68StatusMessage
        {
            ProtocolVersion = 68, NetworkId = 1, TotalDifficulty = BigInteger.One,
            BestHash = new byte[32], GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0
        };

        private void OnAccepted(object sender, RlpxConnection connection)
        {
            lock (_accepted) _accepted.Add(connection);
            var status = Status();
            var eth = new Eth68ServerSession(connection, new BlockingEthHandler(this), status);
            eth.BindCapabilityOffset(connection.GetCapabilityOffset("eth"));
            eth.BindRemoteStatus(status);
            Task run;
            if (_loop == LeaseLoop.MultiProtocolRlpxSession)
            {
                var snap = new Snap1Handler(connection, new BlockingSnapHandler(this));
                var multi = new MultiProtocolRlpxSession(connection, eth, snap);
                run = Task.Run(async () =>
                {
                    try { await multi.RunAsync(default, _cts.Token); } catch { }
                });
            }
            else
            {
                run = Task.Run(async () =>
                {
                    try { await eth.RunAsync(cancellationToken: _cts.Token); } catch { }
                });
            }
            lock (_sessions) _sessions.Add(run);
        }

        private static byte[] HashList(byte marker, int count)
        {
            var hash = Enumerable.Repeat(marker, 32).ToArray();
            return RLP.RLP.EncodeList(Enumerable.Repeat(RLP.RLP.EncodeElement(hash), count).ToArray());
        }

        private static List<byte[]> Hashes(byte marker, int count)
        {
            var hash = Enumerable.Repeat(marker, 32).ToArray();
            return Enumerable.Repeat(hash, count).ToList();
        }

        private static byte[] Request(LeaseLoop loop, byte marker, int count)
        {
            switch (loop)
            {
                case LeaseLoop.MultiProtocolRlpxSession:
                    return GetByteCodesMessageEncoder.Encode(new GetByteCodesMessage
                    {
                        RequestId = 1, Hashes = Hashes(marker, count), ResponseBytes = 1
                    });
                case LeaseLoop.PeerListener:
                    return GetPooledTransactionsMessageEncoder.Encode(new GetPooledTransactionsMessage
                    {
                        RequestId = 1, Hashes = Hashes(marker, count)
                    });
                default:
                    return GetBlockBodiesMessageEncoder.Encode(new GetBlockBodiesMessage
                    {
                        RequestId = 1, BlockHashes = Hashes(marker, count).ToArray()
                    });
            }
        }

        private static byte[] PaddedRequest(LeaseLoop loop) => Request(loop, SlowMarker, PaddedHashes);

        public async Task<RawRlpxPeer> ConnectAsync()
        {
            var peer = await RawRlpxPeer.ConnectAsync(null, Port, _serverKey.GetPubKeyNoPrefix());
            var hello = RawRlpxPeer.HelloFor(peer.Key);
            if (_loop == LeaseLoop.MultiProtocolRlpxSession)
                hello.Capabilities.Add(new P2PCapability { Name = "snap", Version = 1 });
            await peer.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
            await peer.ReadFrameAsync();
            lock (_peers) _peers.Add(peer);
            if (_loop == LeaseLoop.PeerListener)
            {
                await peer.SendFrameAsync(EthOffset + Eth68MessageIds.Status, Eth68StatusMessageEncoder.Encode(Status()));
                await peer.ReadFrameAsync();
            }
            return peer;
        }

        private int EthOffset => 0x10;

        private int RequestMessageId()
        {
            switch (_loop)
            {
                case LeaseLoop.MultiProtocolRlpxSession:
                    int snapOffset;
                    lock (_accepted) snapOffset = _accepted[0].GetCapabilityOffset("snap");
                    return snapOffset + SnapMessageIds.GetByteCodes;
                case LeaseLoop.PeerListener:
                    return EthOffset + EthMessageIds.GetPooledTransactions;
                default:
                    int ethOffset;
                    lock (_accepted) ethOffset = _accepted[0].GetCapabilityOffset("eth");
                    return ethOffset + EthMessageIds.GetBlockBodies;
            }
        }

        public async Task SendPaddedRequestAsync(RawRlpxPeer peer)
        {
            await WaitForAcceptedAsync();
            await peer.SendFrameAsync(RequestMessageId(), PaddedRequest(_loop));
        }

        public async Task SendProbeRequestAsync(RawRlpxPeer peer)
        {
            await WaitForAcceptedAsync();
            await peer.SendFrameAsync(RequestMessageId(), Request(_loop, ProbeMarker, 1));
        }

        private async Task WaitForAcceptedAsync()
        {
            if (_loop == LeaseLoop.PeerListener) return;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                lock (_accepted) if (_accepted.Count > 0) return;
                await Task.Delay(10);
            }
            throw new TimeoutException("the listener accepted no connection");
        }

        public async Task<bool> WaitForSlowRequestsAsync(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (SlowRequestsInHandlers >= count) return true;
                await Task.Delay(20);
            }
            return false;
        }

        public void OpenGate() => _gate.TrySetResult(true);

        private async Task Serve(byte[] firstHash, CancellationToken ct)
        {
            if (firstHash != null && firstHash.Length > 0 && firstHash[0] == SlowMarker)
            {
                Interlocked.Increment(ref _slowServed);
                await _gate.Task.WaitAsync(ct);
                return;
            }
            _probeServed.TrySetResult(true);
        }

        public async ValueTask DisposeAsync()
        {
            OpenGate();
            try { _cts.Cancel(); } catch { }
            lock (_peers) foreach (var peer in _peers) peer.Dispose();
            Task[] sessions;
            lock (_sessions) sessions = _sessions.ToArray();
            try { await Task.WhenAll(sessions).WaitAsync(TimeSpan.FromSeconds(10)); } catch { }
            if (_rlpx != null) await _rlpx.StopAsync();
            if (_peerListener != null) await _peerListener.DisposeAsync();
            if (_bundle != null) await _bundle.DisposeAsync();
        }

        private sealed class BlockingEthHandler : IEth68RequestHandler
        {
            private readonly LeaseLoopHarness _owner;
            public BlockingEthHandler(LeaseLoopHarness owner) => _owner = owner;

            public Task<IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());

            public async Task<IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
            {
                await _owner.Serve(blockHashes.Length > 0 ? blockHashes[0] : null, cancellationToken);
                return new List<BlockBody>();
            }

            public Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult(new List<List<Receipt>>());

            public Task<Receipts70Result> GetReceipts70Async(byte[][] blockHashes, ulong firstBlockReceiptIndex, ulong sizeCap, CancellationToken cancellationToken = default)
                => Task.FromResult(new Receipts70Result(new List<List<Receipt>>(), false));

            public Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<ISignedTransaction>>(new List<ISignedTransaction>());

            public Task<List<byte[]>> GetBlockAccessListsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult(new List<byte[]>());
        }

        private sealed class BlockingSnapHandler : ISnapRequestHandler
        {
            private readonly LeaseLoopHarness _owner;
            public BlockingSnapHandler(LeaseLoopHarness owner) => _owner = owner;

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => Task.FromResult(new AccountRangeMessage());

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
                => Task.FromResult(new StorageRangesMessage());

            public async Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
            {
                await _owner.Serve(request.Hashes.Count > 0 ? request.Hashes[0] : null, ct);
                return new ByteCodesMessage();
            }

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => Task.FromResult(new TrieNodesMessage());

            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => Task.FromResult(new Nethereum.Model.P2P.Snap.BlockAccessListsMessage());
        }

        private sealed class BlockingTxPool : ITxPool
        {
            private readonly LeaseLoopHarness _owner;
            public BlockingTxPool(LeaseLoopHarness owner) => _owner = owner;

            public int PendingCount => 0;
            public int MaxTxsPerSender => 0;

            public async Task<ISignedTransaction> GetByHashAsync(byte[] txHash)
            {
                await _owner.Serve(txHash, CancellationToken.None);
                return null;
            }

            public Task<int> GetPendingCountAsync() => Task.FromResult(0);
            public Task<byte[]> AddAsync(ISignedTransaction transaction) => Task.FromResult(new byte[32]);
            public Task<bool> RemoveAsync(byte[] txHash) => Task.FromResult(false);
            public Task<int> RemoveBatchAsync(IEnumerable<byte[]> txHashes) => Task.FromResult(0);
            public Task<bool> ContainsAsync(byte[] txHash) => Task.FromResult(false);
            public Task<IReadOnlyList<ISignedTransaction>> GetPendingAsync(int maxCount)
                => Task.FromResult<IReadOnlyList<ISignedTransaction>>(new List<ISignedTransaction>());
            public Task ClearAsync() => Task.CompletedTask;
            public Task<BigInteger> GetPendingNonceAsync(string senderAddress, BigInteger confirmedNonce) => Task.FromResult(confirmedNonce);
            public void TrackPendingNonce(string senderAddress, BigInteger nonce) { }
            public void ResetPendingNonces() { }
            public int GetSenderTxCount(string senderAddress) => 0;
            public void IncrementSenderTxCount(string senderAddress) { }
        }
    }
}
