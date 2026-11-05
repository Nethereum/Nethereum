using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.Builder;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class MessageBatchRetryTests
    {
        private const string OperatorKey = "0x12345678901234567890123456789012345678901234567890123456789012ab";
        private const string Target = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public MessageBatchRetryTests(ITestOutputHelper output) { _out = output; }

        private sealed class FailingResultStore : IMessageResultStore
        {
            public Task StoreAsync(MessageResult result) => throw new IOException("result store unavailable");
            public Task<MessageResult?> GetByMessageIdAsync(ulong sourceChainId, ulong messageId) => Task.FromResult<MessageResult?>(null);
            public Task<IReadOnlyList<MessageResult>> GetAllBySourceChainOrderedByLeafIndexAsync(ulong sourceChainId) => Task.FromResult<IReadOnlyList<MessageResult>>(Array.Empty<MessageResult>());
            public Task<IReadOnlyList<MessageResult>> GetBySourceChainAsync(ulong sourceChainId, int offset, int count) => Task.FromResult<IReadOnlyList<MessageResult>>(Array.Empty<MessageResult>());
            public Task<IReadOnlyList<ulong>> GetSourceChainIdsAsync() => Task.FromResult<IReadOnlyList<ulong>>(Array.Empty<ulong>());
            public Task<int> GetCountAsync(ulong sourceChainId) => Task.FromResult(0);
        }

        [Fact]
        [Trait("Finding", "S6-K3")]
        public async Task Given_AResultStoreThatFailsForEveryMessage_When_ABlockCarryingThreeMessagesIsProduced_Then_NoMessageIsRequeuedAndEachIsAccumulatedOnce()
        {
            await using var chain = await new AppChainBuilder("S6Messages", 421702)
                .WithOperator(OperatorKey)
                .WithBlockTime(3_600_000)
                .BuildAsync();
            var accumulator = new MessageMerkleAccumulator();
            var queue = new MessageQueue();
            var processor = new MessageProcessor(accumulator, new FailingResultStore());
            await using var sequencer = new Nethereum.AppChain.Sequencer.Sequencer(
                chain.AppChain, new SequencerConfig { BlockTimeMs = 0, AllowEmptyBlocks = true },
                messageQueue: queue, messageProcessor: processor);
            await sequencer.StartAsync();
            for (ulong id = 1; id <= 3; id++)
                queue.Enqueue(new MessageInfo { MessageId = id, SourceChainId = 5, Target = Target, Sender = Target, Data = new byte[] { 1 } });

            await sequencer.ProduceBlockAsync();

            _out.WriteLine($"S6 queued={queue.Count} leaves={accumulator.GetLeafCount(5)}");
            Assert.Equal(0, queue.Count);
            Assert.Equal(3, accumulator.GetLeafCount(5));
        }
    }
}
