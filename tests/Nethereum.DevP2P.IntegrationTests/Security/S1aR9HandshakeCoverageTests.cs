using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.EVM.ForkId;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1aR9HandshakeCoverageTests
    {
        private const ulong LocalNetwork = 1337;
        private static readonly byte[] LocalGenesis = Enumerable.Repeat((byte)0xAA, 32).ToArray();
        private static readonly ulong[] ForkBlocks = { 100UL };

        private static Eth68StatusMessage LocalTemplate()
        {
            var forkId = Eip2124ForkIdCalculator.NewId(LocalGenesis, ForkBlocks, Array.Empty<ulong>(), 0, 0);
            return new Eth68StatusMessage
            {
                ProtocolVersion = 68, NetworkId = LocalNetwork, TotalDifficulty = BigInteger.One,
                BestHash = LocalGenesis, GenesisHash = LocalGenesis, ForkHash = forkId.Hash, ForkNext = forkId.Next
            };
        }

        private static async Task<bool> WaitDisconnectedAsync(RawRlpxPeer peer, TimeSpan window)
        {
            using var cts = new CancellationTokenSource(window);
            try
            {
                while (true)
                {
                    var (msgId, _) = await peer.ReadFrameAsync(cts.Token);
                    if (msgId == P2PMessageIds.Disconnect) return true;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static async Task<RawRlpxPeer> AdmittedPeerAsync(PeerListener listener, EthECKey serverKey, int version)
        {
            var peer = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            var hello = RawRlpxPeer.HelloFor(peer.Key);
            hello.Capabilities[0].Version = version;
            await peer.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
            await peer.ReadFrameAsync();
            var status = version >= 69
                ? Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = version, NetworkId = 1, GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0,
                    EarliestBlock = 0, LatestBlock = 1, LatestBlockHash = Enumerable.Repeat((byte)7, 32).ToArray()
                })
                : Eth68StatusMessageEncoder.Encode(new Eth68StatusMessage
                {
                    ProtocolVersion = version, NetworkId = 1, TotalDifficulty = BigInteger.One,
                    BestHash = new byte[32], GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0
                });
            await peer.SendFrameAsync(0x10 + EthMessageIds.Status, status);
            var (msgId, _) = await peer.ReadFrameAsync();
            Assert.Equal(0x10 + EthMessageIds.Status, msgId);
            return peer;
        }

        private static async Task<PeerListener> StartMirroringListenerAsync(EthECKey serverKey, InMemoryChainStoreBundle bundle)
        {
            var listener = new PeerListener(serverKey, bundle, new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 4,
                MaxInboundPerIP = 4,
                ServeSnap = false,
                HandshakeTimeoutMs = 5000
            });
            await listener.StartAsync();
            return listener;
        }

        private static BlockHeader WellFormedHeader(long number) => new BlockHeader
        {
            BlockNumber = number,
            ParentHash = Enumerable.Repeat((byte)1, 32).ToArray(),
            UnclesHash = Enumerable.Repeat((byte)2, 32).ToArray(),
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = Enumerable.Repeat((byte)3, 32).ToArray(),
            TransactionsHash = Enumerable.Repeat((byte)4, 32).ToArray(),
            ReceiptHash = Enumerable.Repeat((byte)5, 32).ToArray(),
            LogsBloom = new byte[256],
            MixHash = new byte[32],
            ExtraData = new byte[0],
            Nonce = new byte[8]
        };

        private static byte[] WellFormedNewBlock() => NewBlockMessageEncoder.Encode(new NewBlockMessage
        {
            Header = WellFormedHeader(1),
            Transactions = new List<ISignedTransaction>(),
            Uncles = new List<BlockHeader>(),
            TotalDifficulty = BigInteger.One
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task S1aR9_04_Given_AListenerWithAForkSchedule_When_APeerOnTheSameNetworkAndGenesisAnswersWithAnUnrelatedForkId_Then_ItIsNotAdmitted(bool matchingForkId)
        {
            await using var bundle = InMemoryChainStoreBundle.Open();
            var admitted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 4,
                MaxInboundPerIP = 4,
                ServeSnap = false,
                MirrorRemoteStatus = false,
                HandshakeTimeoutMs = 5000,
                ForkBlockNumbers = ForkBlocks,
                ForkTimestamps = Array.Empty<ulong>(),
                OnInboundPeerAdded = _ => admitted.TrySetResult(true)
            };
            var serverKey = EthECKey.GenerateKey();
            await using var listener = new PeerListener(serverKey, bundle, options, LocalTemplate());
            await listener.StartAsync();
            using var peer = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            var hello = RawRlpxPeer.HelloFor(peer.Key);
            hello.Capabilities[0].Version = 68;
            await peer.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
            await peer.ReadFrameAsync();

            var local = LocalTemplate();
            await peer.SendFrameAsync(0x10 + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(new Eth68StatusMessage
            {
                ProtocolVersion = 68, NetworkId = LocalNetwork, TotalDifficulty = BigInteger.One,
                BestHash = LocalGenesis, GenesisHash = LocalGenesis,
                ForkHash = matchingForkId ? local.ForkHash : 0xDEADBEEFu,
                ForkNext = matchingForkId ? local.ForkNext : 0
            }));

            var completed = await Task.WhenAny(admitted.Task, Task.Delay(TimeSpan.FromSeconds(3)));

            Assert.Equal(matchingForkId, ReferenceEquals(admitted.Task, completed));
        }

        [Theory]
        [InlineData(69, true)]
        [InlineData(68, false)]
        public async Task S1aR9_05_Given_AnAdmittedPeer_When_ItSendsAWellFormedNewBlock_Then_ItIsDisconnectedOnlyOnEth69Plus(int version, bool disconnected)
        {
            await using var bundle = InMemoryChainStoreBundle.Open();
            var serverKey = EthECKey.GenerateKey();
            await using var listener = await StartMirroringListenerAsync(serverKey, bundle);
            using var peer = await AdmittedPeerAsync(listener, serverKey, version);

            await peer.SendFrameAsync(0x10 + EthMessageIds.NewBlock, WellFormedNewBlock());

            Assert.Equal(disconnected, await WaitDisconnectedAsync(peer, TimeSpan.FromSeconds(3)));
        }

    }
}
