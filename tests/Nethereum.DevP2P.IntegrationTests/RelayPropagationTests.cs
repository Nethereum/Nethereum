using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class RelayPropagationTests : IAsyncLifetime
    {
        private const ulong NetworkId = 9988;
        private const int ChainId = 1;
        private const string PrivateKey = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

        private static readonly LegacyTransactionSigner LegacySigner = new();
        private static string SenderAddress => new EthECKey(PrivateKey).GetPublicAddress().ToLowerInvariant();

        private readonly byte[] _genesisHash = new byte[32];
        private readonly EthECKey _serverKey = EthECKey.GenerateKey();
        private readonly DevP2PConfig _config = new DevP2PConfig
        {
            NetworkId = NetworkId,
            ConnectTimeoutMs = 5000,
            HandshakeTimeoutMs = 5000,
            RequestTimeoutMs = 10000
        };

        private RlpxListener _listener;
        private readonly Eth68PeerPool _pool = new Eth68PeerPool();
        private readonly List<Guid> _registered = new List<Guid>();

        public Task InitializeAsync()
        {
            for (int i = 0; i < 32; i++) _genesisHash[i] = (byte)i;

            _listener = new RlpxListener(_serverKey, _config);
            _listener.PeerAccepted += async (_, conn) =>
            {
                var ethOffset = conn.GetCapabilityOffset("eth");
                await conn.SendMessageAsync(
                    ethOffset + Eth68MessageIds.Status, Eth68StatusMessageEncoder.Encode(Status()));
                var (_, payload) = await conn.ReceiveMessageAsync();
                var session = _pool.Add(conn, ethOffset, Eth68StatusMessageEncoder.Decode(payload));
                lock (_registered) _registered.Add(session.Id);
            };
            _listener.Start(port: 0, bindAddress: IPAddress.Loopback);
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            if (_listener != null) await _listener.StopAsync();
        }

        [Fact]
        public async Task Given_RelayOurs_When_APeerTransactionIsAdmitted_Then_NothingIsPropagated()
        {
            var peer = await ConnectPeerAsync();
            var mempool = await MempoolAsync(MempoolRetention.Full, MempoolRelay.Ours);

            await mempool.AdmitFromTrustedPeerAsync(new List<ISignedTransaction> { ATransaction(0) }, sourcePeer: null);

            Assert.True(await NothingArrivesAsync(peer, TimeSpan.FromSeconds(2)),
                "a peer transaction was propagated while relaying only our own");

            await mempool.SubmitAsync(ATransaction(1));
            Assert.True(await PropagationArrivesAsync(peer, TimeSpan.FromSeconds(5)),
                "the node stayed silent about its OWN transaction, so the silence above proves nothing");
        }

        [Fact]
        public async Task Given_RelayAll_When_APeerTransactionIsAdmitted_Then_ItReachesEveryPeerExceptItsSource()
        {
            var source = await ConnectPeerAsync();
            var other = await ConnectPeerAsync();
            var third = await ConnectPeerAsync();
            await Helpers.Wait.UntilAsync(() => _pool.Count == 3, TimeSpan.FromSeconds(5));

            var mempool = await MempoolAsync(MempoolRetention.Full, MempoolRelay.All);
            var sourceId = SessionIdFor(0);

            await mempool.AdmitFromTrustedPeerAsync(
                new List<ISignedTransaction> { ATransaction(0) }, sourcePeer: sourceId);

            Assert.True(await PropagationArrivesAsync(other, TimeSpan.FromSeconds(5)),
                "a peer that was not the source was neither sent the body nor told the hash");
            Assert.True(await PropagationArrivesAsync(third, TimeSpan.FromSeconds(5)),
                "a second non-source peer missed it");
            Assert.True(await NothingArrivesAsync(source, TimeSpan.FromSeconds(1)),
                "the transaction was sent back to the peer it came from");
        }

        [Fact]
        public async Task Given_RelayAll_When_TheSameTransactionArrivesTwice_Then_ItIsPropagatedOnce()
        {
            var source = await ConnectPeerAsync();
            var other = await ConnectPeerAsync();
            await Helpers.Wait.UntilAsync(() => _pool.Count == 2, TimeSpan.FromSeconds(5));

            var mempool = await MempoolAsync(MempoolRetention.Full, MempoolRelay.All);
            var sourceId = SessionIdFor(0);
            var tx = ATransaction(0);

            await mempool.AdmitFromTrustedPeerAsync(new List<ISignedTransaction> { tx }, sourcePeer: sourceId);
            Assert.True(await PropagationArrivesAsync(other, TimeSpan.FromSeconds(5)),
                "the first arrival was not propagated");

            await mempool.AdmitFromTrustedPeerAsync(new List<ISignedTransaction> { tx }, sourcePeer: sourceId);
            Assert.True(await NothingArrivesAsync(other, TimeSpan.FromSeconds(2)),
                "the same transaction was propagated twice - two such nodes would trade it forever");
        }

        private Guid SessionIdFor(int index)
        {
            lock (_registered) return _registered[index];
        }

        private async Task<RelayMempool> MempoolAsync(MempoolRetention retention, MempoolRelay relay)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.State.SaveAccountAsync(SenderAddress, new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)BigInteger.Parse("1000000000000000000")
            });

            return new RelayMempool(
                new TxPool(), _pool, bundle, new MempoolAdmissionValidator(ChainId),
                logger: null, retention: retention, relay: relay);
        }

        private static ISignedTransaction ATransaction(BigInteger nonce) =>
            TransactionFactory.CreateTransaction(LegacySigner.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, Recipient,
                amount: 1000, nonce: nonce, gasPrice: 1, gasLimit: 21_000, data: ""));

        private Eth68StatusMessage Status() => new Eth68StatusMessage
        {
            ProtocolVersion = 68,
            NetworkId = NetworkId,
            TotalDifficulty = BigInteger.One,
            BestHash = _genesisHash,
            GenesisHash = _genesisHash,
            ForkHash = ForkId.ComputeHash(_genesisHash, Array.Empty<ulong>()),
            ForkNext = 0
        };

        private sealed class Peer
        {
            public RlpxConnection Connection;
            public int EthOffset;
            public readonly System.Collections.Concurrent.ConcurrentQueue<int> Received =
                new System.Collections.Concurrent.ConcurrentQueue<int>();

            public bool SawPropagation()
            {
                while (Received.TryDequeue(out var msgId))
                {
                    if (msgId == EthOffset + Eth68MessageIds.NewPooledTransactionHashes) return true;
                    if (msgId == EthOffset + Eth68MessageIds.Transactions) return true;
                }
                return false;
            }
        }

        private async Task<Peer> ConnectPeerAsync()
        {
            var before = _pool.Count;
            var connection = new RlpxConnection(EthECKey.GenerateKey(), _config);
            await connection.ConnectAsync("127.0.0.1", _listener.Port, _serverKey.GetPubKeyNoPrefix());
            var ethOffset = connection.GetCapabilityOffset("eth");
            await connection.SendMessageAsync(
                ethOffset + Eth68MessageIds.Status, Eth68StatusMessageEncoder.Encode(Status()));
            await connection.ReceiveMessageAsync();
            await Helpers.Wait.UntilAsync(() => _pool.Count == before + 1, TimeSpan.FromSeconds(5));

            var peer = new Peer { Connection = connection, EthOffset = ethOffset };
            _ = Task.Run(async () =>
            {
                try
                {
                    while (connection.IsConnected)
                    {
                        var (msgId, _) = await connection.ReceiveMessageAsync();
                        peer.Received.Enqueue(msgId);
                    }
                }
                catch { }
            });
            return peer;
        }

        private static async Task<bool> PropagationArrivesAsync(Peer peer, TimeSpan within)
        {
            var deadline = DateTime.UtcNow + within;
            while (DateTime.UtcNow < deadline)
            {
                if (peer.SawPropagation()) return true;
                await Task.Delay(25);
            }
            return false;
        }

        private static async Task<bool> NothingArrivesAsync(Peer peer, TimeSpan window)
        {
            await Task.Delay(window);
            return !peer.SawPropagation();
        }

    }
}
