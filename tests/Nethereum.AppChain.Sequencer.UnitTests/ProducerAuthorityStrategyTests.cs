using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sequencer.ProducerAuthority;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.Sequencer.UnitTests
{
    public class ProducerAuthorityStrategyTests : IDisposable
    {
        private const string ThisNodeId = "0xthisnode";
        private const string OtherNodeId = "0xothernode";

        private readonly List<string> _tempFiles = new();

        private string NewTempFile()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".authority");
            _tempFiles.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var path in _tempFiles)
            {
                try { File.Delete(path); } catch (Exception) { }
            }
        }

        private sealed class StaticProducerAuthority : IProducerAuthority
        {
            private readonly string? _answer;
            public StaticProducerAuthority(string? answer) => _answer = answer;
            public string? CurrentProducer() => _answer;
        }

        private sealed class SequenceProducerAuthority : IProducerAuthority
        {
            private readonly Queue<string?> _answers;
            public SequenceProducerAuthority(params string?[] answers) => _answers = new Queue<string?>(answers);
            public string? CurrentProducer() => _answers.Count > 0 ? _answers.Dequeue() : _answers.Peek();
        }

        private sealed class RecordingInnerStrategy : IBlockProductionStrategy
        {
            public int CanProduceBlockCallCount;
            public long? PrepareBlockOptionsCalledWithBlockNumber;
            public BlockHeader? FinalizeCalledWithHeader;
            public readonly BlockProductionOptions OptionsToReturn = new BlockProductionOptions();
            public readonly TimeSpan DelayToReturn = TimeSpan.FromSeconds(5);

            public bool AnswerToCanProduceBlock = true;

            public bool CanProduceBlock(long blockNumber)
            {
                CanProduceBlockCallCount++;
                return AnswerToCanProduceBlock;
            }

            public Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken cancellationToken = default)
                => Task.FromResult(DelayToReturn);

            public BlockProductionOptions PrepareBlockOptions(long blockNumber, BlockHeader? parentHeader)
            {
                PrepareBlockOptionsCalledWithBlockNumber = blockNumber;
                return OptionsToReturn;
            }

            public Task FinalizeBlockAsync(BlockHeader header, byte[] blockHash, BlockProductionResult result)
            {
                FinalizeCalledWithHeader = header;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void Given_TheAuthorityNamesThisNode_When_CanProduceBlockIsAsked_Then_ItReturnsTrue()
        {
            var strategy = new ProducerAuthorityBlockProductionStrategy(
                new StaticProducerAuthority(ThisNodeId), ThisNodeId, new RecordingInnerStrategy());

            Assert.True(strategy.CanProduceBlock(1));
        }

        [Fact]
        public void Given_TheAuthorityNamesADifferentNode_When_CanProduceBlockIsAsked_Then_ItReturnsFalse()
        {
            var strategy = new ProducerAuthorityBlockProductionStrategy(
                new StaticProducerAuthority(OtherNodeId), ThisNodeId, new RecordingInnerStrategy());

            Assert.False(strategy.CanProduceBlock(1));
        }

        [Fact]
        public void Given_TheAuthorityNamesNobody_When_CanProduceBlockIsAsked_Then_ItReturnsFalse()
        {
            var strategy = new ProducerAuthorityBlockProductionStrategy(
                new StaticProducerAuthority(null), ThisNodeId, new RecordingInnerStrategy());

            Assert.False(strategy.CanProduceBlock(1));
        }

        [Fact]
        public void Given_AnInnerStrategyThatRefuses_When_TheAuthorityNamesThisNode_Then_TheAuthorityAloneDecidesAndItProduces()
        {
            var inner = new RecordingInnerStrategy { AnswerToCanProduceBlock = false };
            var strategy = new ProducerAuthorityBlockProductionStrategy(
                new StaticProducerAuthority(ThisNodeId), ThisNodeId, inner);

            Assert.True(strategy.CanProduceBlock(42));
            Assert.Equal(0, inner.CanProduceBlockCallCount);
        }

        [Fact]
        public void Given_AnyState_When_GetSigningDelayIsAsked_Then_ItReturnsZeroRegardlessOfInner()
        {
            var inner = new RecordingInnerStrategy();
            var strategy = new ProducerAuthorityBlockProductionStrategy(
                new StaticProducerAuthority(ThisNodeId), ThisNodeId, inner);

            var delay = strategy.GetSigningDelayAsync(1).GetAwaiter().GetResult();

            Assert.Equal(TimeSpan.Zero, delay);
        }

        [Fact]
        public void Given_AnyState_When_PrepareBlockOptionsIsAsked_Then_ItDelegatesToInner()
        {
            var inner = new RecordingInnerStrategy();
            var strategy = new ProducerAuthorityBlockProductionStrategy(
                new StaticProducerAuthority(ThisNodeId), ThisNodeId, inner);

            var options = strategy.PrepareBlockOptions(7, null);

            Assert.Same(inner.OptionsToReturn, options);
            Assert.Equal(7, inner.PrepareBlockOptionsCalledWithBlockNumber);
        }

        [Fact]
        public async Task Given_AnyState_When_FinalizeBlockAsyncIsCalled_Then_ItDelegatesToInner()
        {
            var inner = new RecordingInnerStrategy();
            var strategy = new ProducerAuthorityBlockProductionStrategy(
                new StaticProducerAuthority(ThisNodeId), ThisNodeId, inner);
            var header = new BlockHeader();

            await strategy.FinalizeBlockAsync(header, Array.Empty<byte>(), new BlockProductionResult());

            Assert.Same(header, inner.FinalizeCalledWithHeader);
        }

        [Fact]
        public void Given_AnAuthorityWhoseAnswerChangesBetweenCalls_When_CanProduceBlockIsAskedAgain_Then_ItFollowsTheNewAnswerWithoutCaching()
        {
            var authority = new SequenceProducerAuthority(OtherNodeId, ThisNodeId, OtherNodeId);
            var strategy = new ProducerAuthorityBlockProductionStrategy(authority, ThisNodeId, new RecordingInnerStrategy());

            Assert.False(strategy.CanProduceBlock(1));
            Assert.True(strategy.CanProduceBlock(2));
            Assert.False(strategy.CanProduceBlock(3));
        }

        [Fact]
        public void Given_AnAuthorityFileThatIsMissing_When_CurrentProducerIsAsked_Then_ItReturnsNull()
        {
            var path = NewTempFile();
            var authority = new FileProducerAuthority(path);

            Assert.Null(authority.CurrentProducer());
        }

        [Fact]
        public void Given_AnAuthorityFileThatIsEmpty_When_CurrentProducerIsAsked_Then_ItReturnsNull()
        {
            var path = NewTempFile();
            File.WriteAllText(path, string.Empty);
            var authority = new FileProducerAuthority(path);

            Assert.Null(authority.CurrentProducer());
        }

        [Fact]
        public void Given_AnAuthorityFileThatIsWhitespace_When_CurrentProducerIsAsked_Then_ItReturnsNull()
        {
            var path = NewTempFile();
            File.WriteAllText(path, "   \r\n\t  ");
            var authority = new FileProducerAuthority(path);

            Assert.Null(authority.CurrentProducer());
        }

        [Fact]
        public void Given_AnAuthorityFileThatIsUnreadable_When_CurrentProducerIsAsked_Then_ItReturnsNull()
        {
            var path = NewTempFile();
            File.WriteAllText(path, ThisNodeId);
            var authority = new FileProducerAuthority(path);

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.Null(authority.CurrentProducer());
            }
        }

        [Fact]
        public void Given_AnAuthorityFileNamingThisNodesIdWithSurroundingWhitespace_When_CurrentProducerIsAsked_Then_ItReturnsTheTrimmedId()
        {
            var path = NewTempFile();
            File.WriteAllText(path, "  " + ThisNodeId + "  \r\n");
            var authority = new FileProducerAuthority(path);

            Assert.Equal(ThisNodeId, authority.CurrentProducer());
        }

        [Fact]
        public void Given_AnAuthorityFileNamingThisNodeInDifferentCasing_When_CanProduceBlockIsAsked_Then_ItReturnsTrue()
        {
            var path = NewTempFile();
            File.WriteAllText(path, ThisNodeId.ToUpperInvariant());
            var strategy = new ProducerAuthorityBlockProductionStrategy(
                new FileProducerAuthority(path), ThisNodeId, new RecordingInnerStrategy());

            Assert.True(strategy.CanProduceBlock(1));
        }

        [Fact]
        public void Given_AnAuthorityFileNamingThisNodesId_When_ItIsAsked_Then_ItProduces()
        {
            var path = NewTempFile();
            File.WriteAllText(path, ThisNodeId);
            var strategy = new ProducerAuthorityBlockProductionStrategy(
                new FileProducerAuthority(path), ThisNodeId, new RecordingInnerStrategy());

            Assert.True(strategy.CanProduceBlock(1));
        }

        [Fact]
        public void Given_TheAuthorityFileIsRewrittenToNameThisNode_When_CanProduceBlockIsAskedAgain_Then_ItFollowsTheChangeWithoutRestart()
        {
            var path = NewTempFile();
            File.WriteAllText(path, OtherNodeId);
            var strategy = new ProducerAuthorityBlockProductionStrategy(
                new FileProducerAuthority(path), ThisNodeId, new RecordingInnerStrategy());

            Assert.False(strategy.CanProduceBlock(1));

            File.WriteAllText(path, ThisNodeId);

            Assert.True(strategy.CanProduceBlock(2));

            File.WriteAllText(path, OtherNodeId);

            Assert.False(strategy.CanProduceBlock(3));
        }
    }
}
