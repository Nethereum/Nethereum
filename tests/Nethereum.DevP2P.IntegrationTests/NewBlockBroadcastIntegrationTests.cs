using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Strategies;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class NewBlockBroadcastIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public NewBlockBroadcastIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task SequencerPublishesNewBlock_FollowerReceivesPushedBlock()
        {
            var genesisHash = new byte[32];
            for (int i = 0; i < 32; i++) genesisHash[i] = (byte)i;

            var sequencerKey = EthECKey.GenerateKey();
            var followerKey = EthECKey.GenerateKey();
            ulong networkId = 9999;

            var devP2PConfig = new DevP2PConfig
            {
                NetworkId = networkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000
            };

            var pool = new Eth68PeerPool();
            var publisher = new DevP2PBlockPublisher(pool);

            var sequencerListener = new RlpxListener(sequencerKey, devP2PConfig);
            sequencerListener.PeerAccepted += async (_, conn) =>
            {
                var status = new Eth68StatusMessage
                {
                    ProtocolVersion = 68,
                    NetworkId = networkId,
                    TotalDifficulty = BigInteger.One,
                    BestHash = genesisHash,
                    GenesisHash = genesisHash,
                    ForkHash = ForkId.ComputeHash(genesisHash, Array.Empty<ulong>()),
                    ForkNext = 0
                };
                var ethOffset = conn.GetCapabilityOffset("eth");
                await conn.SendMessageAsync(ethOffset + Eth68MessageIds.Status, Eth68StatusMessageEncoder.Encode(status));
                var (msgId, payload) = await conn.ReceiveMessageAsync();
                var remoteStatus = Eth68StatusMessageEncoder.Decode(payload);
                pool.Add(conn, ethOffset, remoteStatus);
                _output.WriteLine($"Sequencer accepted peer, pool count = {pool.Count}");
            };

            sequencerListener.Start(port: 0, bindAddress: IPAddress.Loopback);
            var sequencerEnode = $"enode://{sequencerKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{sequencerListener.Port}";

            var followerConn = new RlpxConnection(followerKey, devP2PConfig);
            await followerConn.ConnectAsync("127.0.0.1", sequencerListener.Port, sequencerKey.GetPubKeyNoPrefix());
            var followerEthOffset = followerConn.GetCapabilityOffset("eth");

            var followerStatus = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = networkId,
                TotalDifficulty = BigInteger.One,
                BestHash = genesisHash,
                GenesisHash = genesisHash,
                ForkHash = ForkId.ComputeHash(genesisHash, Array.Empty<ulong>()),
                ForkNext = 0
            };
            await followerConn.SendMessageAsync(followerEthOffset + Eth68MessageIds.Status, Eth68StatusMessageEncoder.Encode(followerStatus));
            await followerConn.ReceiveMessageAsync();
            _output.WriteLine("Follower handshake complete, awaiting NewBlock push");

            await WaitForAsync(() => pool.Count == 1, TimeSpan.FromSeconds(3));
            Assert.Equal(1, publisher.ConnectedPeerCount);

            var blockHeader = BuildSampleHeader(blockNumber: 1, parentHash: genesisHash);

            var pushedTcs = new TaskCompletionSource<NewBlockMessage>();
            var pushReader = Task.Run(async () =>
            {
                try
                {
                    while (!pushedTcs.Task.IsCompleted)
                    {
                        var (msgId, payload) = await followerConn.ReceiveMessageAsync();
                        if (msgId == followerEthOffset + Eth68MessageIds.NewBlock)
                        {
                            pushedTcs.TrySetResult(NewBlockMessageEncoder.Decode(payload));
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    pushedTcs.TrySetException(ex);
                }
            });

            await publisher.BroadcastNewBlockAsync(
                blockHeader,
                new List<ISignedTransaction>(),
                new List<BlockHeader>(),
                withdrawals: null,
                totalDifficulty: BigInteger.Parse("2"));

            var pushed = await pushedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(BigInteger.One, (BigInteger)pushed.Header.BlockNumber);
            var computed = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(pushed.Header);
            var expected = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(blockHeader);
            Assert.Equal(expected.ToHex(true), computed.ToHex(true));
            Assert.Equal(BigInteger.Parse("2"), pushed.TotalDifficulty);

            _output.WriteLine($"Follower received NewBlock {computed.ToHex(true)} TD={pushed.TotalDifficulty}");

            await followerConn.DisconnectAsync();
            await sequencerListener.StopAsync();
        }

        private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (predicate()) return;
                await Task.Delay(50);
            }
            throw new TimeoutException("WaitForAsync predicate not satisfied");
        }

        private static BlockHeader BuildSampleHeader(long blockNumber, byte[] parentHash) => new BlockHeader
        {
            ParentHash = parentHash,
            UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
            ReceiptHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
            LogsBloom = new byte[256],
            Difficulty = Nethereum.Util.EvmUInt256.One,
            BlockNumber = blockNumber,
            GasLimit = 30_000_000,
            GasUsed = 0,
            Timestamp = 1700000000,
            ExtraData = new byte[0],
            MixHash = new byte[32],
            Nonce = new byte[8]
        };
    }
}
