using Nethereum.AppChain.Anchoring.Messaging;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class MessagingRestartTests
    {
        private const ulong SourceChain = 91;
        private const string Target = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public MessagingRestartTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        [Trait("Finding", "S6-K2")]
        public async Task Given_ThreeMessagesRecordedBeforeARestartAndFiveIndexedAfterIt_When_TheServiceRebuiltFromTheStoreEnqueues_Then_OnlyTheTwoNewOnesAreEnqueued()
        {
            var resultStore = new InMemoryMessageResultStore();
            var before = new MessageMerkleAccumulator();
            var processor = new MessageProcessor(before, resultStore);
            await processor.ProcessBatchAsync(Enumerable.Range(1, 3)
                .Select(i => new MessageInfo { MessageId = (ulong)i, SourceChainId = SourceChain, Target = Target, Sender = Target, Data = new byte[] { 1 } })
                .ToList());

            var after = new MessageMerkleAccumulator();
            await after.RebuildFromStoreAsync(resultStore);
            var index = new InMemoryMessageIndexStore();
            await index.StoreBatchAsync(Enumerable.Range(1, 5)
                .Select(i => new MessageInfo { MessageId = (ulong)i, SourceChainId = SourceChain, Target = Target, Sender = Target, Data = new byte[] { 1 } }));
            var queue = new MessageQueue();
            var config = new MessagingConfig { Enabled = true, MaxMessagesPerPoll = 100 };
            config.SourceChains.Add(new SourceChainConfig { ChainId = SourceChain });
            var service = new MessagingService(1, config, index, queue, accumulator: after);

            var polled = await service.PollAllSourcesAsync();

            _out.WriteLine($"S6 polled={string.Join(",", polled.Select(m => m.MessageId))}");
            Assert.Equal(new ulong[] { 4, 5 }, polled.Select(m => m.MessageId).ToArray());
            Assert.Equal(2, queue.Count);
        }
    }
}
