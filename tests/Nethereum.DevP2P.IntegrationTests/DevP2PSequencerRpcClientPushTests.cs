using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Verifies typed push events on DevP2PSequencerRpcClient. The follower uses
    /// the standard pull API (GetBlockHeaderAsync etc.) and ALSO subscribes to
    /// the NewBlockReceived event. When an unsolicited NewBlock arrives during a
    /// pending pull request, the event fires with the decoded message.
    /// </summary>
    public class DevP2PSequencerRpcClientPushTests
    {
        private readonly ITestOutputHelper _output;

        public DevP2PSequencerRpcClientPushTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GetBlockHeaderAsync_NewBlockArrivesMidRequest_TypedEventFires()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/typed-push-test",
                NetworkId = 31337,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 8000
            };

            var genesisHash = new byte[32];
            for (int i = 0; i < 32; i++) genesisHash[i] = (byte)(0xA0 ^ i);
            var sampleHeader = BuildSampleHeader(blockNumber: 7, parentHash: genesisHash);
            var genesisHeader = BuildSampleHeader(0, new byte[32]);

            using var sessionCts = new CancellationTokenSource();

            var listener = new RlpxListener(serverKey, config);
            listener.PeerAccepted += async (_, conn) =>
            {
                try
                {
                    var ethOffset = conn.GetCapabilityOffset("eth");
                    var localStatus = new Eth68StatusMessage
                    {
                        ProtocolVersion = 68,
                        NetworkId = config.NetworkId,
                        TotalDifficulty = BigInteger.One,
                        BestHash = genesisHash,
                        GenesisHash = genesisHash,
                        ForkHash = ForkId.ComputeHash(genesisHash, Array.Empty<ulong>()),
                        ForkNext = 0
                    };
                    await conn.SendMessageAsync(ethOffset + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(localStatus));
                    await conn.ReceiveMessageAsync(sessionCts.Token);

                    while (!sessionCts.IsCancellationRequested && conn.IsConnected)
                    {
                        var (msgId, payload) = await conn.ReceiveMessageAsync(sessionCts.Token);
                        var localId = msgId - ethOffset;

                        if (localId != EthMessageIds.GetBlockHeaders) continue;

                        var newBlock = new NewBlockMessage
                        {
                            Header = sampleHeader,
                            Transactions = new List<ISignedTransaction>(),
                            Uncles = new List<BlockHeader>(),
                            Withdrawals = null,
                            TotalDifficulty = BigInteger.Parse("99")
                        };
                        await conn.SendMessageAsync(
                            ethOffset + EthMessageIds.NewBlock,
                            NewBlockMessageEncoder.Encode(newBlock));

                        var request = GetBlockHeadersMessageEncoder.Decode(payload);
                        var headers = new List<BlockHeader>();
                        if (request.StartBlock == 0) headers.Add(genesisHeader);
                        var response = new BlockHeadersMessage
                        {
                            RequestId = request.RequestId,
                            Headers = headers
                        };
                        await conn.SendMessageAsync(
                            ethOffset + EthMessageIds.BlockHeaders,
                            BlockHeadersMessageEncoder.Encode(response));
                    }
                }
                catch
                {
                    // background loop on test teardown
                }
            };

            try
            {
                listener.Start(port: 0, bindAddress: IPAddress.Loopback);
                var serverEnode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";

                await using var client = new DevP2PSequencerRpcClient(serverEnode, config, genesisHash);

                var pushTcs = new TaskCompletionSource<NewBlockMessage>();
                client.NewBlockReceived += (_, msg) =>
                {
                    if (msg.Header.BlockNumber == 7)
                        pushTcs.TrySetResult(msg);
                };

                _ = await client.GetBlockHeaderAsync(BigInteger.Zero);

                var pushed = await pushTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.NotNull(pushed);
                Assert.Equal(7L, (long)pushed.Header.BlockNumber);
                Assert.Equal(BigInteger.Parse("99"), pushed.TotalDifficulty);
                _output.WriteLine($"Typed NewBlockReceived: block #{pushed.Header.BlockNumber}, TD={pushed.TotalDifficulty}");
            }
            finally
            {
                sessionCts.Cancel();
                await listener.StopAsync();
            }
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
