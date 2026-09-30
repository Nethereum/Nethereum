using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.FullSync
{
    public class PushedBlockSourceDivergenceTests
    {
        private static byte[] Filled(byte v) => Enumerable.Repeat(v, 32).ToArray();

        private static NewBlockMessage MessageAt(long number, byte[] parentHash, byte seed) =>
            new NewBlockMessage
            {
                Header = new BlockHeader { BlockNumber = number, ParentHash = parentHash, Difficulty = seed },
                Transactions = new List<ISignedTransaction>(),
                Uncles = new List<BlockHeader>(),
            };

        private static async Task<ulong> HandOffOneBlockAsync(PushedBlockSource source, long number, byte[] parentHash, byte seed)
        {
            source.OnNewBlock(MessageAt(number, parentHash, seed));
            using var cts = new CancellationTokenSource();
            await foreach (var bundle in source.StreamAsync((ulong)number, cts.Token))
            {
                cts.Cancel();
                return (ulong)bundle.Header.BlockNumber;
            }
            return 0;
        }

        [Fact]
        public async Task Given_ACompetingBlockAtACommittedHeight_When_AForkChoiceIsRegistered_Then_ItReachesTheForkChoiceInsteadOfBeingSilentlyDropped()
        {
            var source = new PushedBlockSource();
            await HandOffOneBlockAsync(source, 10, Filled(9), seed: 1);

            Assert.Null(source.LastChainBreak);

            source.OnNewBlock(MessageAt(10, Filled(9), seed: 2), sourcePeerNodeId: "peer-B");

            Assert.NotNull(source.LastChainBreak);
            Assert.Equal(10UL, source.LastChainBreak.AtBlock);
            Assert.NotNull(source.LastChainBreak.IncomingHeader);
            Assert.Equal("peer-B", source.LastChainBreak.SourcePeerNodeId);
        }

        [Fact]
        public async Task Given_NoForkChoiceRegistered_When_ABlockArrivesAtACommittedHeight_Then_TheStreamYieldsBreakInsteadOfHangingForever()
        {
            var source = new PushedBlockSource();
            await HandOffOneBlockAsync(source, 10, Filled(9), seed: 1);

            var enumerated = new List<BlockBundle>();
            using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(10));
            var streamTask = Task.Run(async () =>
            {
                await foreach (var bundle in source.StreamAsync(11, cts.Token))
                {
                    enumerated.Add(bundle);
                }
            });

            await Task.Delay(200);
            source.OnNewBlock(MessageAt(10, Filled(9), seed: 2));

            await streamTask;

            Assert.Empty(enumerated);
            Assert.NotNull(source.LastChainBreak);
        }

        [Fact]
        public async Task Given_APeerRedeliveringTheSameAlreadyImportedBlock_When_ItArrivesFromAnotherConnection_Then_NoDivergenceIsSignalled()
        {
            var source = new PushedBlockSource();
            await HandOffOneBlockAsync(source, 10, Filled(9), seed: 1);

            source.OnNewBlock(MessageAt(10, Filled(9), seed: 1), sourcePeerNodeId: "peer-C");

            Assert.Null(source.LastChainBreak);
        }

        [Fact]
        public async Task Given_ANewStreamAsyncCall_When_APriorDivergenceWasSignalled_Then_ItStartsClean()
        {
            var source = new PushedBlockSource();
            await HandOffOneBlockAsync(source, 10, Filled(9), seed: 1);
            source.OnNewBlock(MessageAt(10, Filled(9), seed: 2));
            Assert.NotNull(source.LastChainBreak);

            using var cts = new CancellationTokenSource(System.TimeSpan.FromMilliseconds(200));
            try
            {
                await foreach (var _ in source.StreamAsync(11, cts.Token)) { }
            }
            catch (System.OperationCanceledException) { }

            Assert.Null(source.LastChainBreak);
        }
    }
}
