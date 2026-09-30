using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    [Collection(DevP2PGethFixture.COLLECTION_NAME)]
    public class Eth68SyncIntegrationTests
    {
        private readonly DevP2PGethFixture _fixture;
        private readonly ITestOutputHelper _output;

        public Eth68SyncIntegrationTests(
            DevP2PGethFixture fixture,
            ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task SyncGenesisHeader_ComputedHashMatchesGethRpc()
        {
            var conn = await OpenEthSessionAsync();
            try
            {
                var ethOffset = conn.GetCapabilityOffset("eth");
                var headers = await RequestHeadersAsync(conn, ethOffset, startBlock: 0, count: 1);
                Assert.Single(headers);

                var computedHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(headers[0]);
                Assert.Equal(_fixture.GenesisHash.ToHex(true), computedHash.ToHex(true));
                _output.WriteLine($"Genesis hash matches: {computedHash.ToHex(true)}");
            }
            finally
            {
                await conn.DisconnectAsync();
            }
        }

        [Fact]
        public async Task SyncHeaderChain_ParentHashLinksValid()
        {
            const int count = 5;
            await WaitForBlockHeightAsync((ulong)(count - 1), TimeSpan.FromSeconds(15));

            var conn = await OpenEthSessionAsync();
            try
            {
                var ethOffset = conn.GetCapabilityOffset("eth");
                var headers = await RequestHeadersAsync(conn, ethOffset, startBlock: 0, count: count);
                Assert.Equal(count, headers.Count);

                var hashProvider = RlpKeccakBlockHashProvider.Instance;
                for (int i = 0; i < headers.Count; i++)
                {
                    var current = headers[i];
                    var computedHash = hashProvider.ComputeBlockHash(current);
                    _output.WriteLine($"Block {current.BlockNumber}: {computedHash.ToHex(true).Substring(0, 18)}...");

                    if (i > 0)
                    {
                        var expectedParent = hashProvider.ComputeBlockHash(headers[i - 1]);
                        Assert.Equal(expectedParent.ToHex(), current.ParentHash.ToHex());
                    }
                }

                Assert.True(headers[0].BlockNumber.IsZero, "First header must be genesis");
                _output.WriteLine($"PASS: parentHash chain valid across {count} blocks");
            }
            finally
            {
                await conn.DisconnectAsync();
            }
        }

        [Fact]
        public async Task SyncBlockBody_TransactionsRootMatchesHeader()
        {
            var recipient = "0x27Ef5cDBe01777D62438AfFeb695e33fC2335979";
            var receipt = await _fixture.SendEtherFromSealerAsync(
                recipient, BigInteger.Parse("100000000000000000"));
            var includedBlock = (ulong)receipt.BlockNumber.Value;
            _output.WriteLine($"Tx {receipt.TransactionHash} included in block #{includedBlock}");

            var conn = await OpenEthSessionAsync();
            try
            {
                var ethOffset = conn.GetCapabilityOffset("eth");

                var headers = await RequestHeadersAsync(conn, ethOffset, includedBlock, 1);
                Assert.Single(headers);
                var header = headers[0];
                var blockHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);
                Assert.Equal(receipt.BlockHash, blockHash.ToHex(true));

                var bodies = await RequestBodiesAsync(conn, ethOffset, new[] { blockHash });
                Assert.Single(bodies);
                var body = bodies[0];
                Assert.True(body.Transactions.Count >= 1,
                    $"Expected at least 1 tx, got {body.Transactions.Count}");

                var encodedTxs = body.Transactions.Select(t => t.GetRLPEncoded()).ToList();
                var computedRoot = new RootCalculator().CalculateTransactionsRoot(encodedTxs);
                Assert.Equal(header.TransactionsHash.ToHex(), computedRoot.ToHex());

                _output.WriteLine(
                    $"PASS: block #{header.BlockNumber} has {body.Transactions.Count} tx(s), " +
                    $"txRoot {computedRoot.ToHex().Substring(0, 18)}... matches header");
            }
            finally
            {
                await conn.DisconnectAsync();
            }
        }

        private static async Task<List<BlockBody>> RequestBodiesAsync(
            RlpxConnection conn, int ethOffset, byte[][] blockHashes)
        {
            var request = new GetBlockBodiesMessage
            {
                RequestId = conn.NextRequestId(),
                BlockHashes = blockHashes
            };

            var (_, payload) = await conn.RequestAsync(
                ethOffset + Eth68MessageIds.GetBlockBodies,
                GetBlockBodiesMessageEncoder.Encode(request),
                ethOffset + Eth68MessageIds.BlockBodies);

            return BlockBodiesMessageEncoder.Decode(payload).Bodies;
        }

        [Fact]
        public async Task SyncBlockReceipts_ReceiptsRootMatchesHeader()
        {
            var recipient = "0xE65B318b9dECf504d1cb6Ea5C367Ca657a070Db1";
            var receipt = await _fixture.SendEtherFromSealerAsync(
                recipient, BigInteger.Parse("50000000000000000"));
            var includedBlock = (ulong)receipt.BlockNumber.Value;
            _output.WriteLine($"Tx {receipt.TransactionHash} included in block #{includedBlock}");

            var conn = await OpenEthSessionAsync();
            try
            {
                var ethOffset = conn.GetCapabilityOffset("eth");

                var headers = await RequestHeadersAsync(conn, ethOffset, includedBlock, 1);
                var header = headers[0];
                var blockHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);

                var receiptsByBlock = await RequestReceiptsAsync(conn, ethOffset, new[] { blockHash });
                Assert.Single(receiptsByBlock);
                var blockReceipts = receiptsByBlock[0];
                Assert.True(blockReceipts.Count >= 1,
                    $"Expected at least 1 receipt, got {blockReceipts.Count}");

                var computedRoot = new RootCalculator().CalculateReceiptsRoot(blockReceipts);
                Assert.Equal(header.ReceiptHash.ToHex(), computedRoot.ToHex());

                _output.WriteLine(
                    $"PASS: block #{header.BlockNumber} has {blockReceipts.Count} receipt(s), " +
                    $"receiptsRoot {computedRoot.ToHex().Substring(0, 18)}... matches header");
            }
            finally
            {
                await conn.DisconnectAsync();
            }
        }

        private static async Task<List<List<Receipt>>> RequestReceiptsAsync(
            RlpxConnection conn, int ethOffset, byte[][] blockHashes)
        {
            var request = new GetReceiptsMessage
            {
                RequestId = conn.NextRequestId(),
                BlockHashes = blockHashes
            };

            var (_, payload) = await conn.RequestAsync(
                ethOffset + Eth68MessageIds.GetReceipts,
                GetReceiptsMessageEncoder.Encode(request),
                ethOffset + Eth68MessageIds.Receipts);

            return ReceiptsMessageEncoder.Decode(payload).ReceiptsByBlock;
        }

        private async Task WaitForBlockHeightAsync(ulong minHeight, TimeSpan timeout)
        {
            var web3 = _fixture.GetWeb3();
            var deadline = DateTime.UtcNow + timeout;
            HexBigInteger current = null;
            while (DateTime.UtcNow < deadline)
            {
                current = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                if ((ulong)current.Value >= minHeight) return;
                await Task.Delay(250);
            }
            throw new TimeoutException(
                $"Geth did not reach block {minHeight} within {timeout.TotalSeconds}s (last: {current?.Value})");
        }

        private async Task<RlpxConnection> OpenEthSessionAsync()
        {
            var config = new DevP2PConfig
            {
                NetworkId = _fixture.NetworkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000
            };

            var connector = new StaticPeerConnector(config: config);
            var conn = await connector.ConnectAsync(_fixture.Enode);
            var ethOffset = conn.GetCapabilityOffset("eth");

            var status = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = _fixture.NetworkId,
                TotalDifficulty = BigInteger.One,
                BestHash = _fixture.GenesisHash,
                GenesisHash = _fixture.GenesisHash,
                ForkHash = ForkId.ComputeHash(_fixture.GenesisHash, Array.Empty<ulong>()),
                ForkNext = 0
            };

            await conn.SendMessageAsync(
                ethOffset + Eth68MessageIds.Status,
                Eth68StatusMessageEncoder.Encode(status));
            await conn.ReceiveMessageAsync();

            return conn;
        }

        private static async Task<List<BlockHeader>> RequestHeadersAsync(
            RlpxConnection conn, int ethOffset, ulong startBlock, int count)
        {
            var request = new GetBlockHeadersMessage
            {
                RequestId = conn.NextRequestId(),
                StartBlock = startBlock,
                Limit = (ulong)count,
                Skip = 0,
                Reverse = false
            };

            var (_, payload) = await conn.RequestAsync(
                ethOffset + Eth68MessageIds.GetBlockHeaders,
                GetBlockHeadersMessageEncoder.Encode(request),
                ethOffset + Eth68MessageIds.BlockHeaders);

            return BlockHeadersMessageEncoder.Decode(payload).Headers;
        }
    }
}
