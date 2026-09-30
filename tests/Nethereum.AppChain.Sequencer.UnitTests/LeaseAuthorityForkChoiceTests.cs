using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sequencer.ProducerAuthority;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.Sequencer.UnitTests
{
    public class LeaseAuthorityForkChoiceTests
    {
        private static BlockHeader CompetingHeader(long number, byte[] parentHash) =>
            new BlockHeader { BlockNumber = number, ParentHash = parentHash, Difficulty = 0 };

        private static byte[] Filled(byte v) => Enumerable.Repeat(v, 32).ToArray();

        [Fact]
        public async Task Given_ASingleSequencerDoubleSeal_When_OnlyOneAuthorsBranchHoldsTheCurrentFencingToken_Then_ThatBranchWins()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var arbiter = new InMemorySequencerArbiter(System.TimeSpan.FromSeconds(30));
            await arbiter.TryAcquireOrRenewAsync("peer-A");

            var forkChoice = new LeaseAuthorityForkChoice(arbiter, bundle.Blocks);
            var incomingHeader = CompetingHeader(10, Filled(9));
            var incomingHash = Filled(10);

            var verdict = await forkChoice.ShouldAdoptAsync(incomingHeader, incomingHash, sourcePeerNodeId: "peer-A", CancellationToken.None);

            Assert.Equal(ForkChoiceOutcome.AdoptIncoming, verdict.Outcome);
            Assert.Equal(9UL, verdict.CommonAncestor);
        }

        [Fact]
        public async Task Given_TwoBlocksWithNoInTurnOutOfTurnDistinction_When_DifficultyForkChoiceIsAskedInstead_Then_ItCannotDiscriminateBySourceIdentity()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var forkChoice = new DifficultyForkChoice(bundle.Blocks);
            var incomingHeader = CompetingHeader(10, Filled(9));
            var incomingHash = Filled(10);

            var verdict = await forkChoice.ShouldAdoptAsync(incomingHeader, incomingHash, CancellationToken.None);

            Assert.Equal(ForkChoiceOutcome.Undecidable, verdict.Outcome);
        }

        [Fact]
        public async Task Given_ASingleSequencerDoubleSeal_When_NeitherAuthorResolvesToTheArbitersCurrentToken_Then_TheVerdictIsUndecidableNotAGuess()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var arbiter = new InMemorySequencerArbiter(System.TimeSpan.FromSeconds(30));
            await arbiter.TryAcquireOrRenewAsync("peer-A");

            var forkChoice = new LeaseAuthorityForkChoice(arbiter, bundle.Blocks);
            var incomingHeader = CompetingHeader(10, Filled(9));
            var incomingHash = Filled(10);

            var verdict = await forkChoice.ShouldAdoptAsync(incomingHeader, incomingHash, sourcePeerNodeId: "peer-C", CancellationToken.None);

            Assert.Equal(ForkChoiceOutcome.Undecidable, verdict.Outcome);
            Assert.NotEqual(ForkChoiceOutcome.KeepLocal, verdict.Outcome);
        }

        [Fact]
        public async Task Given_ASingleSequencerDoubleSeal_When_TheSourcePeerIsUnknown_Then_TheVerdictIsUndecidableNotAGuess()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var arbiter = new InMemorySequencerArbiter(System.TimeSpan.FromSeconds(30));
            await arbiter.TryAcquireOrRenewAsync("peer-A");

            var forkChoice = new LeaseAuthorityForkChoice(arbiter, bundle.Blocks);
            var incomingHeader = CompetingHeader(10, Filled(9));
            var incomingHash = Filled(10);

            var verdict = await forkChoice.ShouldAdoptAsync(incomingHeader, incomingHash, CancellationToken.None);

            Assert.Equal(ForkChoiceOutcome.Undecidable, verdict.Outcome);
        }
    }
}
