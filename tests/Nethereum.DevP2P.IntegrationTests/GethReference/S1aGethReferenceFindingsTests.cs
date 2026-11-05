using System;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.DevP2P.IntegrationTests.Security;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    [Collection("GethReference")]
    public class S1aGethReferenceFindingsTests : IDisposable
    {
        private readonly ObjectGenerator _generator = new ObjectGenerator(new Random(7));
        private readonly Lazy<GethReferenceProcess> _geth = new Lazy<GethReferenceProcess>(() => new GethReferenceProcess(TimeSpan.FromMinutes(2)));

        public void Dispose()
        {
            if (_geth.IsValueCreated) _geth.Value.Dispose();
        }

        private GethAnswer Geth(string kind, byte[] input) =>
            _geth.Value.Query(new[] { new System.Collections.Generic.KeyValuePair<string, byte[]>(kind, input) })[0];

        private static bool GethRefuses(GethAnswer answer) => !answer.Ok || !answer.ItemsOk || answer.PolicyRefusal != null;

        [Theory]
        [InlineData(false, 5, 9, true)]
        [InlineData(true, 5, 9, false)]
        [InlineData(false, 9, 5, false)]
        public async Task S1aGR01_Given_ABlockRangeUpdateFromAPeer_When_ItsRangeIsInvalid_Then_ThePeerIsDisconnected(bool zeroHash, ulong earliest, ulong latest, bool valid)
        {
            var disconnected = await SendBlockRangeUpdateAsync(earliest, latest, zeroHash ? new byte[32] : Enumerable.Repeat((byte)7, 32).ToArray());

            Assert.Equal(!valid, disconnected);
        }

        private static async Task<bool> SendBlockRangeUpdateAsync(ulong earliest, ulong latest, byte[] latestHash)
        {
            var config = new DevP2PConfig { ClientId = "Nethereum/gr01", HandshakeTimeoutMs = 5000 };
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            using var cts = new CancellationTokenSource();
            RawRlpxPeer attacker = null;
            Task loop = Task.CompletedTask;
            try
            {
                attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
                var hello = RawRlpxPeer.HelloFor(attacker.Key);
                hello.Capabilities[0].Version = 71;
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
                await attacker.ReadFrameAsync();
                var connection = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var status = new Eth68StatusMessage
                {
                    ProtocolVersion = 68, NetworkId = 1, TotalDifficulty = BigInteger.One,
                    BestHash = new byte[32], GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0
                };
                var session = new Eth68ServerSession(connection, new NoopHandler(), status);
                var ethOffset = connection.GetCapabilityOffset("eth");
                session.BindCapabilityOffset(ethOffset);
                session.BindRemoteStatus(new Eth68StatusMessage
                {
                    ProtocolVersion = 71, NetworkId = 1, TotalDifficulty = BigInteger.One,
                    BestHash = new byte[32], GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0
                });
                loop = Task.Run(async () =>
                {
                    try { await session.RunAsync(cancellationToken: cts.Token); } catch { }
                });

                await attacker.SendFrameAsync(ethOffset + EthMessageIds.BlockRangeUpdate, BlockRangeUpdateMessageEncoder.Encode(
                    new BlockRangeUpdateMessage { EarliestBlock = earliest, LatestBlock = latest, LatestBlockHash = latestHash }));

                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline && !connection.IsDisconnected) await Task.Delay(25);
                return connection.IsDisconnected;
            }
            finally
            {
                cts.Cancel();
                attacker?.Dispose();
                try { await loop.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
                await listener.StopAsync();
            }
        }

        [Theory]
        [InlineData(false, 5, 9, true)]
        [InlineData(true, 5, 9, false)]
        [InlineData(false, 9, 5, false)]
        public async Task S1aGR02_Given_AnEth69StatusFromAPeer_When_ItsBlockRangeIsInvalid_Then_TheHandshakeIsRefused(bool zeroHash, ulong earliest, ulong latest, bool valid)
        {
            await using var bundle = InMemoryChainStoreBundle.Open();
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 4,
                MaxInboundPerIP = 4,
                ServeSnap = false,
                HandshakeTimeoutMs = 5000
            };
            var serverKey = EthECKey.GenerateKey();
            await using var listener = new PeerListener(serverKey, bundle, options);
            await listener.StartAsync();
            using var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            var hello = RawRlpxPeer.HelloFor(attacker.Key);
            hello.Capabilities[0].Version = 69;
            await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
            await attacker.ReadFrameAsync();

            var status = Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
            {
                ProtocolVersion = 69, NetworkId = 1, GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0,
                EarliestBlock = earliest, LatestBlock = latest,
                LatestBlockHash = zeroHash ? new byte[32] : Enumerable.Repeat((byte)7, 32).ToArray()
            });
            await attacker.SendFrameAsync(0x10 + EthMessageIds.Status, status);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var answeredWithStatus = false;
            try
            {
                var (msgId, _) = await attacker.ReadFrameAsync(cts.Token);
                answeredWithStatus = msgId == 0x10 + EthMessageIds.Status;
            }
            catch (Exception)
            {
            }

            Assert.Equal(valid, answeredWithStatus);
        }

        private static byte[] EthBalMessage(RlpNode entry) => RlpNode.List(RlpNode.Int(1), RlpNode.List(entry)).Encode();

        [Fact]
        public void S1aGR03_Given_AnEthBlockAccessListsEntryThatIsANonEmptyString_When_Decoded_Then_ItIsRefused()
        {
            var message = EthBalMessage(RlpNode.Str(new byte[] { 0xaa, 0xbb }));
            Assert.True(GethRefuses(Geth("eth_BlockAccessLists", message)));

            Assert.ThrowsAny<Exception>(() => Nethereum.Model.P2P.BlockAccessListsMessageEncoder.Decode(message));
        }

        [Fact]
        public void S1aGR03_Given_AnEthBlockAccessListsEntryWhoseChildHasANonCanonicalLength_When_Decoded_Then_ItIsRefused()
        {
            var child = RlpNode.Str(new byte[] { 0x81 });
            child.Form = LengthForm.LongFormLeadingZero;
            var message = EthBalMessage(RlpNode.List(child));
            Assert.True(GethRefuses(Geth("eth_BlockAccessLists", message)));

            Assert.ThrowsAny<Exception>(() => Nethereum.Model.P2P.BlockAccessListsMessageEncoder.Decode(message));
        }

        [Theory]
        [InlineData("nonce wider than uint64")]
        [InlineData("nonce with a leading zero")]
        [InlineData("balance wider than 256 bits")]
        [InlineData("balance with a leading zero")]
        public void S1aGR04_Given_AnAccountRangeWhoseSlimAccountIsNotAValidSlimAccount_When_Decoded_Then_ItIsRefused(string defect)
        {
            RlpNode nonce = RlpNode.Int(1), balance = RlpNode.Int(2);
            switch (defect)
            {
                case "nonce wider than uint64": nonce = RlpNode.Str(Enumerable.Repeat((byte)1, 9).ToArray()); break;
                case "nonce with a leading zero": nonce = RlpNode.Str(new byte[] { 0, 1 }); break;
                case "balance wider than 256 bits": balance = RlpNode.Str(Enumerable.Repeat((byte)1, 33).ToArray()); break;
                default: balance = RlpNode.Str(new byte[] { 0, 2 }); break;
            }
            var account = RlpNode.List(nonce, balance, RlpNode.Str(Array.Empty<byte>()), RlpNode.Str(Array.Empty<byte>()));
            var message = RlpNode.List(RlpNode.Int(1), RlpNode.List(RlpNode.List(_generator.Hash(), account)), RlpNode.List()).Encode();
            Assert.True(GethRefuses(Geth("snap_AccountRange", message)));

            Assert.ThrowsAny<Exception>(() => AccountRangeMessageEncoder.Decode(message));
        }

        [Fact]
        public void S1aGR05_Given_ATransactionsMessageCarryingTheSameTransactionTwice_When_Decoded_Then_ItIsRefused()
        {
            var tx = _generator.Legacy();
            var message = RlpNode.List(tx, tx.Clone()).Encode();
            Assert.True(GethRefuses(Geth("eth_Transactions", message)));

            Assert.ThrowsAny<Exception>(() => TransactionsMessageEncoder.Decode(message));
        }

        [Fact]
        public void S1aGR05_Given_APooledTransactionsMessageCarryingTheSameTransactionTwice_When_Decoded_Then_ItIsRefused()
        {
            var tx = _generator.Legacy();
            var message = RlpNode.List(RlpNode.Int(1), RlpNode.List(tx, tx.Clone())).Encode();
            Assert.True(GethRefuses(Geth("eth_PooledTransactions", message)));

            Assert.ThrowsAny<Exception>(() => PooledTransactionsMessageEncoder.Decode(message));
        }

        [Fact]
        public void S1aGR06_Given_AnEth69StatusWhoseProtocolVersionIsAUint32AboveInt32_When_Decoded_Then_ItIsAccepted()
        {
            var status = RlpNode.List(RlpNode.Int(0xFFFFFFFF), RlpNode.Int(1), _generator.Hash(), _generator.ForkId(),
                RlpNode.Int(1), RlpNode.Int(2), _generator.Hash()).Encode();

            Assert.False(GethRefuses(Geth("eth_Status", status)));

            var decoded = Eth69StatusMessageEncoder.Decode(status);

            Assert.Equal(1UL, decoded.NetworkId);
        }

        [Fact]
        public void S1aGR08_Given_ALegacyTransactionWithVOf5AndEmptyRAndS_When_Decoded_Then_TheDecoderFormatExceptionIsThrown()
        {
            var transaction = "df030182c73894b94f5374fce5edbc8e2a8697c15331677e6ebf0b0a80058080".HexToByteArray();
            Assert.False(GethRefuses(Geth("tx_binary", transaction)));

            var exception = Assert.ThrowsAny<Exception>(() => TransactionFactory.CreateTransaction(transaction));

            Assert.True(exception is TransactionFieldFormatException || exception is InvalidOperationException, exception.GetType().Name);
        }

        private sealed class NoopHandler : IEth68RequestHandler
        {
            public System.Threading.Tasks.Task<System.Collections.Generic.IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken cancellationToken = default)
                => Task.FromResult<System.Collections.Generic.IList<BlockHeader>>(new System.Collections.Generic.List<BlockHeader>());

            public Task<System.Collections.Generic.IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<System.Collections.Generic.IList<BlockBody>>(new System.Collections.Generic.List<BlockBody>());

            public Task<System.Collections.Generic.List<System.Collections.Generic.List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult(new System.Collections.Generic.List<System.Collections.Generic.List<Receipt>>());

            public Task<Receipts70Result> GetReceipts70Async(byte[][] blockHashes, ulong firstBlockReceiptIndex, ulong sizeCap, CancellationToken cancellationToken = default)
                => Task.FromResult(new Receipts70Result(new System.Collections.Generic.List<System.Collections.Generic.List<Receipt>>(), false));

            public Task<System.Collections.Generic.IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<System.Collections.Generic.IList<ISignedTransaction>>(new System.Collections.Generic.List<ISignedTransaction>());

            public Task<System.Collections.Generic.List<byte[]>> GetBlockAccessListsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult(new System.Collections.Generic.List<byte[]>());
        }
    }
}
