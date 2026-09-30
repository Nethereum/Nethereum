using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Serving.Strategies;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.SpecTests.Eth71
{
    public class Eth71SessionLoopbackTests
    {
        private const ulong NetworkId = 7777ul;

        private static byte[] Make32(byte fill)
        {
            var bytes = new byte[32];
            for (int i = 0; i < 32; i++) bytes[i] = (byte)(fill ^ i);
            return bytes;
        }

        private static DevP2PConfig BuildConfig(string clientId) => new DevP2PConfig
        {
            ClientId = clientId,
            HandshakeTimeoutMs = 10_000,
            ConnectTimeoutMs = 10_000,
            RequestTimeoutMs = 10_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000
        };

        private static async Task<(SyncPeerSession client, RlpxListener listener, InMemoryChainStoreBundle bundle,
            Eth68ServerSession serverSession, Task serverLoop, CancellationTokenSource serverCts)>
            ConnectEth71LoopbackAsync()
        {
            var serverKey = EthECKey.GenerateKey();
            var genesis = Make32(0xC0);
            var bundle = InMemoryChainStoreBundle.Open();
            var handler = new StorageBackedEth68Handler(bundle.Blocks, bundle.Transactions, bundle.Receipts, bundle.Withdrawals);

            var listener = new RlpxListener(serverKey, BuildConfig("Nethereum.Spec.Tests/eth71-server"));
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => acceptedTcs.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var enode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
            var clientConnectTask = SyncPeerSession.ConnectAsync(
                enode, TimeSpan.FromSeconds(10), CancellationToken.None, genesis, NetworkId);

            var serverConn = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var serverEthOffset = serverConn.GetCapabilityOffset("eth");
            var ethCap = serverConn.SharedCapabilities.Find(c => c.Name == "eth");
            Assert.NotNull(ethCap);
            Assert.Equal(71, ethCap!.Version);

            await serverConn.SendMessageAsync(
                serverEthOffset + EthMessageIds.Status,
                Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = 71,
                    NetworkId = NetworkId,
                    GenesisHash = genesis,
                    ForkHash = 0,
                    ForkNext = 0,
                    EarliestBlock = 0,
                    LatestBlock = 0,
                    LatestBlockHash = genesis
                }));

            var (clientStatusMsgId, clientStatusPayload) = await serverConn.ReceiveMessageAsync();
            Assert.Equal(serverEthOffset + EthMessageIds.Status, clientStatusMsgId);
            var clientStatus = Eth69StatusMessageEncoder.Decode(clientStatusPayload);

            var localStatus = new Eth68StatusMessage
            {
                ProtocolVersion = 71,
                NetworkId = NetworkId,
                GenesisHash = genesis,
                BestHash = genesis,
                TotalDifficulty = System.Numerics.BigInteger.One,
                ForkHash = 0,
                ForkNext = 0
            };
            var serverSession = new Eth68ServerSession(serverConn, handler, localStatus);
            serverSession.BindCapabilityOffset(serverEthOffset);
            typeof(Eth68ServerSession)
                .GetProperty(nameof(Eth68ServerSession.RemoteStatus))!
                .SetValue(serverSession, new Eth68StatusMessage
                {
                    ProtocolVersion = 71,
                    NetworkId = clientStatus.NetworkId,
                    GenesisHash = clientStatus.GenesisHash,
                    BestHash = clientStatus.LatestBlockHash,
                    ForkHash = clientStatus.ForkHash,
                    ForkNext = clientStatus.ForkNext
                });

            var serverCts = new CancellationTokenSource();
            var serverLoop = Task.Run(async () =>
            {
                try { await serverSession.RunAsync(cancellationToken: serverCts.Token); }
                catch { }
            });

            var client = await clientConnectTask;
            Assert.Equal(71, client.EthVersion);

            return (client, listener, bundle, serverSession, serverLoop, serverCts);
        }

        [Fact]
        public async Task Eth71Session_ServesEmptyBlockAccessLists()
        {
            var (client, listener, bundle, _, serverLoop, serverCts) = await ConnectEth71LoopbackAsync();
            try
            {
                var hashes = new List<byte[]> { Make32(0xAA), Make32(0xBB) };

                var response = await client.GetBlockAccessListsAsync(hashes, CancellationToken.None);

                Assert.NotNull(response);
                Assert.Empty(response);
            }
            finally
            {
                serverCts.Cancel();
                try { await serverLoop; } catch { }
                await client.DisposeAsync();
                await bundle.DisposeAsync();
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Eth71_ClientRequestsAndDecodes_Loopback()
        {
            var (client, listener, bundle, _, serverLoop, serverCts) = await ConnectEth71LoopbackAsync();
            try
            {
                var hashes = new List<byte[]> { Make32(0x01), Make32(0x02), Make32(0x03) };

                List<List<AccountChanges>> decoded = null;
                var ex = await Record.ExceptionAsync(async () =>
                {
                    decoded = await client.GetBlockAccessListsAsync(hashes, CancellationToken.None);
                });

                Assert.Null(ex);
                Assert.NotNull(decoded);
                Assert.IsType<List<List<AccountChanges>>>(decoded);
                Assert.Empty(decoded);
            }
            finally
            {
                serverCts.Cancel();
                try { await serverLoop; } catch { }
                await client.DisposeAsync();
                await bundle.DisposeAsync();
                await listener.StopAsync();
            }
        }
    }
}
