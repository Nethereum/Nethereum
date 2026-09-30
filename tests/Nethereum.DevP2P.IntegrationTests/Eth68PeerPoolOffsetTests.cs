using System;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class Eth68PeerPoolOffsetTests
    {
        private const ulong NetworkId = 9977;

        [Fact]
        public async Task Given_TwoPeersRegisteredAtDifferentEthOffsets_When_AMessageIsBroadcast_Then_EachReceivesItAtItsOwnOffset()
        {
            var genesisHash = new byte[32];
            for (int i = 0; i < 32; i++) genesisHash[i] = (byte)i;

            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                NetworkId = NetworkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000
            };

            var pool = new Eth68PeerPool();

            var accepted = 0;
            var listener = new RlpxListener(serverKey, config);
            listener.PeerAccepted += async (_, conn) =>
            {
                var ethOffset = conn.GetCapabilityOffset("eth");
                await conn.SendMessageAsync(
                    ethOffset + Eth68MessageIds.Status,
                    Eth68StatusMessageEncoder.Encode(Status(genesisHash)));
                var (_, payload) = await conn.ReceiveMessageAsync();
                var remoteStatus = Eth68StatusMessageEncoder.Decode(payload);

                var registeredOffset = System.Threading.Interlocked.Increment(ref accepted) == 1
                    ? ethOffset
                    : ethOffset + 18;
                pool.Add(conn, registeredOffset, remoteStatus);
            };

            listener.Start(port: 0, bindAddress: IPAddress.Loopback);
            try
            {
                var first = await ConnectAsync(serverKey, listener.Port, config, genesisHash);
                await Helpers.Wait.UntilAsync(() => pool.Count == 1, TimeSpan.FromSeconds(5));
                var second = await ConnectAsync(serverKey, listener.Port, config, genesisHash);
                await Helpers.Wait.UntilAsync(() => pool.Count == 2, TimeSpan.FromSeconds(5));

                var firstMsgId = NextMessageIdAsync(first.Connection);
                var secondMsgId = NextMessageIdAsync(second.Connection);

                await pool.BroadcastAsync(Eth68MessageIds.NewPooledTransactionHashes, new byte[] { 0xc0 });

                var firstSeen = await firstMsgId.WaitAsync(TimeSpan.FromSeconds(5));
                var secondSeen = await secondMsgId.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(first.EthOffset + Eth68MessageIds.NewPooledTransactionHashes, firstSeen);
                Assert.Equal(second.EthOffset + 18 + Eth68MessageIds.NewPooledTransactionHashes, secondSeen);
                Assert.NotEqual(firstSeen, secondSeen);
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        private static Eth68StatusMessage Status(byte[] genesisHash) => new Eth68StatusMessage
        {
            ProtocolVersion = 68,
            NetworkId = NetworkId,
            TotalDifficulty = BigInteger.One,
            BestHash = genesisHash,
            GenesisHash = genesisHash,
            ForkHash = ForkId.ComputeHash(genesisHash, Array.Empty<ulong>()),
            ForkNext = 0
        };

        private static async Task<(RlpxConnection Connection, int EthOffset)> ConnectAsync(
            EthECKey serverKey, int port, DevP2PConfig config, byte[] genesisHash)
        {
            var connection = new RlpxConnection(EthECKey.GenerateKey(), config);
            await connection.ConnectAsync("127.0.0.1", port, serverKey.GetPubKeyNoPrefix());
            var ethOffset = connection.GetCapabilityOffset("eth");
            await connection.SendMessageAsync(
                ethOffset + Eth68MessageIds.Status,
                Eth68StatusMessageEncoder.Encode(Status(genesisHash)));
            await connection.ReceiveMessageAsync();
            return (connection, ethOffset);
        }

        private static Task<int> NextMessageIdAsync(RlpxConnection connection) =>
            Task.Run(async () =>
            {
                var (msgId, _) = await connection.ReceiveMessageAsync();
                return msgId;
            });

    }
}
