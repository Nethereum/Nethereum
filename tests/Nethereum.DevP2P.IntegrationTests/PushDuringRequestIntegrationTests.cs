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
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Verifies that an unsolicited push message arriving while RequestAsync is
    /// waiting for a response fires PushMessageReceived instead of being silently
    /// dropped, then RequestAsync still returns the response cleanly.
    /// </summary>
    public class PushDuringRequestIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public PushDuringRequestIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task NewBlockPush_DuringPendingRequest_FiresEventAndRequestStillResolves()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/push-test",
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 8000
            };
            byte[] fakeGenesis = new byte[32];
            for (int i = 0; i < 32; i++) fakeGenesis[i] = (byte)i;

            using var sessionCts = new CancellationTokenSource();
            var serverConnReadyTcs = new TaskCompletionSource<RlpxConnection>();

            var listener = new RlpxListener(serverKey, config);
            listener.PeerAccepted += async (_, conn) =>
            {
                try
                {
                    var ethOffset = conn.GetCapabilityOffset("eth");
                    var status = new Eth68StatusMessage
                    {
                        ProtocolVersion = 68,
                        NetworkId = 12345,
                        TotalDifficulty = BigInteger.One,
                        BestHash = fakeGenesis,
                        GenesisHash = fakeGenesis,
                        ForkHash = ForkId.ComputeHash(fakeGenesis, Array.Empty<ulong>()),
                        ForkNext = 0
                    };
                    await conn.SendMessageAsync(ethOffset + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(status));
                    await conn.ReceiveMessageAsync();
                    serverConnReadyTcs.TrySetResult(conn);
                }
                catch { }
            };

            try
            {
                listener.Start(port: 0, bindAddress: IPAddress.Loopback);
                var serverEnode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
                _output.WriteLine($"Server listening at {serverEnode}");

                var clientConn = new RlpxConnection(clientKey, config);
                await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                var clientEthOffset = clientConn.GetCapabilityOffset("eth");

                var clientStatus = new Eth68StatusMessage
                {
                    ProtocolVersion = 68,
                    NetworkId = 12345,
                    TotalDifficulty = BigInteger.One,
                    BestHash = fakeGenesis,
                    GenesisHash = fakeGenesis,
                    ForkHash = ForkId.ComputeHash(fakeGenesis, Array.Empty<ulong>()),
                    ForkNext = 0
                };
                await clientConn.SendMessageAsync(clientEthOffset + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(clientStatus));
                await clientConn.ReceiveMessageAsync();

                var serverConn = await serverConnReadyTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
                var serverEthOffset = serverConn.GetCapabilityOffset("eth");

                var pushTcs = new TaskCompletionSource<NewBlockMessage>();
                clientConn.PushMessageReceived += (_, e) =>
                {
                    if (e.MessageId == clientEthOffset + EthMessageIds.NewBlock)
                    {
                        try { pushTcs.TrySetResult(NewBlockMessageEncoder.Decode(e.Payload)); }
                        catch (Exception ex) { pushTcs.TrySetException(ex); }
                    }
                };

                var blockHeader = BuildSampleHeader(blockNumber: 99, parentHash: fakeGenesis);
                var newBlock = new NewBlockMessage
                {
                    Header = blockHeader,
                    Transactions = new List<ISignedTransaction>(),
                    Uncles = new List<BlockHeader>(),
                    Withdrawals = null,
                    TotalDifficulty = BigInteger.Parse("2")
                };

                var requestTask = clientConn.RequestAsync(
                    clientEthOffset + EthMessageIds.GetBlockHeaders,
                    GetBlockHeadersMessageEncoder.Encode(new GetBlockHeadersMessage
                    {
                        RequestId = 42,
                        StartBlock = 0,
                        Limit = 1,
                        Skip = 0,
                        Reverse = false
                    }),
                    clientEthOffset + EthMessageIds.BlockHeaders);

                await Task.Delay(150);

                await serverConn.SendMessageAsync(
                    serverEthOffset + EthMessageIds.NewBlock,
                    NewBlockMessageEncoder.Encode(newBlock));

                await Task.Delay(150);

                await serverConn.SendMessageAsync(
                    serverEthOffset + EthMessageIds.BlockHeaders,
                    BlockHeadersMessageEncoder.Encode(new BlockHeadersMessage
                    {
                        RequestId = 42,
                        Headers = new List<BlockHeader> { blockHeader }
                    }));

                var pushed = await pushTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.NotNull(pushed);
                Assert.Equal(99L, (long)pushed.Header.BlockNumber);
                _output.WriteLine($"Push captured: block #{pushed.Header.BlockNumber} TD={pushed.TotalDifficulty}");

                var (responseMsgId, responsePayload) = await requestTask;
                Assert.Equal(clientEthOffset + EthMessageIds.BlockHeaders, responseMsgId);
                var decoded = BlockHeadersMessageEncoder.Decode(responsePayload);
                Assert.Equal(42ul, decoded.RequestId);
                Assert.Single(decoded.Headers);
                _output.WriteLine("Request also resolved correctly after the push event");

                await clientConn.DisconnectAsync();
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
