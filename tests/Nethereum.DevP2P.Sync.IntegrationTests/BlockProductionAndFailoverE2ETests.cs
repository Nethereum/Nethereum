using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class BlockProductionAndFailoverE2ETests
    {
        private const string SenderKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private static readonly BigInteger TenEther = BigInteger.Parse("10000000000000000000");

        private readonly ITestOutputHelper _output;
        public BlockProductionAndFailoverE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_ASealingNodeWithAConnectedPeer_When_ItProducesABlock_Then_ThePeerReceivesIt()
        {
            var (producerNode, peer) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                var producer = await DevChainProducer.AttachAsync(producerNode);

                var sealed1 = await producer.ProduceAsync();

                Assert.NotNull(sealed1?.Header);
                Assert.Equal(new BigInteger(1), (BigInteger)sealed1.Header.BlockNumber);

                var received = await DevChainNetwork.WaitUntilAsync(
                    () => peer.ObservedBlocks.Any(b => (BigInteger)b.Header.BlockNumber == 1),
                    TimeSpan.FromSeconds(15));

                Assert.True(received, "the peer never received the sealed block");
                _output.WriteLine($"peer received block 1 0x{sealed1.BlockHash.ToHex()}");
            }
            finally
            {
                await producerNode.DisposeAsync();
                await peer.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AFundedSender_When_ItsTransactionIsSealedIntoABlock_Then_ThePeerReceivesTheBlockCarryingIt()
        {
            var (producerNode, peer) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                var producer = await DevChainProducer.AttachAsync(producerNode);
                await producerNode.FundAccountAsync(DevChainTransactions.AddressOf(SenderKey), TenEther);

                var tx = DevChainTransactions.SignEip1559(SenderKey, producerNode.ChainId, Recipient, 0, 1000);
                var submitted = await producerNode.Mempool.SubmitAsync(tx);
                Assert.True(submitted.Accepted, submitted.RejectMessage);

                var sealedBlock = await producer.ProduceAsync();
                Assert.NotNull(sealedBlock?.Header);

                var carried = await DevChainNetwork.WaitUntilAsync(
                    () => peer.ObservedBlocks.Any(b =>
                        b.Transactions != null &&
                        b.Transactions.Any(t => ByteUtil.AreEqual(t.Hash, submitted.TransactionHash))),
                    TimeSpan.FromSeconds(15));

                Assert.True(carried, "the peer never received a block carrying the submitted transaction");
                _output.WriteLine("peer received the block carrying the submitted transaction");
            }
            finally
            {
                await producerNode.DisposeAsync();
                await peer.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ThePrimarySealerStops_When_TheSecondSealerProduces_Then_ThePeerKeepsReceivingBlocks()
        {
            var primary = DevChainNode.Create();
            var secondary = DevChainNode.Create();
            var observer = DevChainNode.Create();
            try
            {
                await primary.StartAsync();
                await secondary.StartAsync();
                await observer.StartAsync();

                await observer.ConnectToAsync(primary);
                await observer.ConnectToAsync(secondary);

                var primaryProducer = await DevChainProducer.AttachAsync(primary);
                var secondaryProducer = await DevChainProducer.AttachAsync(secondary);

                await primaryProducer.ProduceAsync();
                var sawPrimary = await DevChainNetwork.WaitUntilAsync(
                    () => observer.ObservedBlocks.Count >= 1, TimeSpan.FromSeconds(15));
                Assert.True(sawPrimary, "the observer never saw the primary's block");

                var afterPrimary = observer.ObservedBlocks.Count;
                await secondaryProducer.ProduceAsync();

                var sawSecondary = await DevChainNetwork.WaitUntilAsync(
                    () => observer.ObservedBlocks.Count > afterPrimary, TimeSpan.FromSeconds(15));

                Assert.True(sawSecondary, "the observer stopped receiving blocks when the primary stopped");
                _output.WriteLine($"observer kept receiving after failover: {observer.ObservedBlocks.Count} blocks");
            }
            finally
            {
                await primary.DisposeAsync();
                await secondary.DisposeAsync();
                await observer.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AChainAlreadyProducing_When_ANewNodeEnrols_Then_ItCanPullEveryHeaderFromGenesis()
        {
            var producerNode = DevChainNode.Create();
            try
            {
                await producerNode.StartAsync();
                var producer = await DevChainProducer.AttachAsync(producerNode);

                for (var i = 0; i < 3; i++) await producer.ProduceAsync();
                Assert.Equal(new BigInteger(3), await producer.HeightAsync());

                var joiner = DevChainNode.Create();
                try
                {
                    await joiner.StartAsync();
                    await joiner.ConnectToAsync(producerNode);

                    var session = joiner.OutboundSessionTo(producerNode);
                    var headers = await session.GetHeadersAsync(0, 4, default);

                    Assert.NotNull(headers);
                    Assert.Equal(4, headers.Count);
                    Assert.Equal(new BigInteger(0), (BigInteger)headers[0].BlockNumber);
                    Assert.Equal(new BigInteger(3), (BigInteger)headers[3].BlockNumber);

                    for (var i = 1; i < headers.Count; i++)
                    {
                        Assert.True(
                            ByteUtil.AreEqual(headers[i].ParentHash, BlockHashOf(headers[i - 1])),
                            $"header {i} does not chain back to {i - 1}");
                    }

                    _output.WriteLine("joiner pulled genesis..tip and the chain links");
                }
                finally
                {
                    await joiner.DisposeAsync();
                }
            }
            finally
            {
                await producerNode.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ANewlyEnrolledNode_When_ItAsksForTheGenesisBlock_Then_ItMatchesTheProducersGenesis()
        {
            var producerNode = DevChainNode.Create();
            try
            {
                await producerNode.StartAsync();
                var producer = await DevChainProducer.AttachAsync(producerNode);
                await producer.ProduceAsync();

                var producerGenesis = await producerNode.Bundle.Blocks.GetByNumberAsync(0);

                var joiner = DevChainNode.Create();
                try
                {
                    await joiner.StartAsync();
                    await joiner.ConnectToAsync(producerNode);

                    var headers = await joiner.OutboundSessionTo(producerNode).GetHeadersAsync(0, 1, default);

                    Assert.Single(headers);
                    Assert.Equal(BlockHashOf(producerGenesis).ToHex(), BlockHashOf(headers[0]).ToHex());
                    _output.WriteLine($"joiner agrees on genesis 0x{BlockHashOf(headers[0]).ToHex()}");
                }
                finally
                {
                    await joiner.DisposeAsync();
                }
            }
            finally
            {
                await producerNode.DisposeAsync();
            }
        }

        private static byte[] BlockHashOf(Nethereum.Model.BlockHeader header)
            => Nethereum.CoreChain.BlockHashCalculator.ForHeader(header);
    }
}
