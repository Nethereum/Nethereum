using Nethereum.AppChain.Anchoring.Messaging;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class MessagingTests
    {
        private const ulong SourceChain = 77;
        private const string Target = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public MessagingTests(ITestOutputHelper output) { _out = output; }

        private static Task<S6ProducerHost> StartAsync() =>
            S6ProducerHost.StartAsync(config => config.Messaging.Enabled = true);

        private static MessageInfo Message(ulong id, byte[]? data = null) => new MessageInfo
        {
            MessageId = id,
            SourceChainId = SourceChain,
            TargetChainId = 0,
            Sender = Target,
            Target = Target,
            Data = data!
        };

        [Fact]
        [Trait("Finding", "S6-K3")]
        public async Task Given_MoreQueuedMessagesThanMaxMessagesPerBlock_When_ABlockIsProduced_Then_OnlyTheMaximumAreProcessedAndTheRestStayQueued()
        {
            await using var host = await StartAsync();
            var max = host.Node.Sequencer!.Config.MaxMessagesPerBlock;
            for (ulong id = 1; id <= (ulong)max + 20; id++) host.Node.MessageQueue!.Enqueue(Message(id, new byte[] { 1 }));

            await host.Node.Sequencer!.ProduceBlockAsync();

            var leaves = host.Node.MessageAccumulator.GetLeafCount(SourceChain);
            _out.WriteLine($"S6 max={max} leaves={leaves} queued={host.Node.MessageQueue!.Count}");
            Assert.Equal(max, leaves);
            Assert.Equal(20, host.Node.MessageQueue!.Count);
        }

        [Fact]
        [Trait("Finding", "S6-05")]
        public async Task Given_HubMessagesWhoseIdsAreContiguousPerTargetButInterleaveTwoSourceChainIds_When_ABlockIsProduced_Then_EveryMessageIsProcessedOnce()
        {
            await using var host = await StartAsync();
            host.Node.MessageQueue!.Enqueue(Message(1, new byte[] { 1 }));
            var other = Message(2, new byte[] { 2 });
            other.SourceChainId = SourceChain + 1;
            host.Node.MessageQueue!.Enqueue(other);
            host.Node.MessageQueue!.Enqueue(Message(3, new byte[] { 3 }));
            host.Node.MessageQueue!.Enqueue(Message(4, new byte[] { 4 }));

            await host.Node.Sequencer!.ProduceBlockAsync();

            var first = await host.Node.MessageResultStore.GetAllBySourceChainOrderedByLeafIndexAsync(SourceChain);
            var second = await host.Node.MessageResultStore.GetAllBySourceChainOrderedByLeafIndexAsync(SourceChain + 1);
            _out.WriteLine($"S6 queued={host.Node.MessageQueue!.Count} bucketA={string.Join(",", first.Select(r => r.MessageId))} bucketB={string.Join(",", second.Select(r => r.MessageId))}");
            Assert.Equal(new ulong[] { 1, 3, 4 }, first.Select(r => r.MessageId).ToArray());
            Assert.Equal(new ulong[] { 2 }, second.Select(r => r.MessageId).ToArray());
        }

        [Fact]
        [Trait("Finding", "S6-K2")]
        public async Task Given_TheSameMessageQueuedTwice_When_BlocksAreProduced_Then_ItIsAccumulatedOnce()
        {
            await using var host = await StartAsync();
            host.Node.MessageQueue!.Enqueue(Message(1, new byte[] { 1 }));
            host.Node.MessageQueue!.Enqueue(Message(1, new byte[] { 1 }));

            await host.Node.Sequencer!.ProduceBlockAsync();

            var leaves = host.Node.MessageAccumulator.GetLeafCount(SourceChain);
            var count = await host.Node.MessageResultStore.GetCountAsync(SourceChain);
            _out.WriteLine($"S6 leaves={leaves} stored={count}");
            Assert.Equal(1, leaves);
            Assert.Equal(1, count);
        }

        [Fact]
        [Trait("Finding", "S6-04")]
        public async Task Given_AHubMessageAndACompositionWithNoMessageExecutor_When_ABlockIsProduced_Then_ItIsRecordedAsNotExecutedAndNeverAsASuccess()
        {
            await using var host = await StartAsync();
            host.Node.MessageQueue!.Enqueue(Message(1, new byte[] { 1, 2, 3 }));

            await host.Node.Sequencer!.ProduceBlockAsync();

            var result = await host.Node.MessageResultStore.GetByMessageIdAsync(SourceChain, 1);
            var transaction = result == null ? null : await host.Node.Bundle.Transactions.GetByHashAsync(result.TxHash);
            _out.WriteLine($"S6 recorded={result != null} success={result?.Success} txFound={transaction != null}");
            Assert.NotNull(result);
            Assert.False(result!.Success);
            Assert.Null(transaction);
        }
    }
}
