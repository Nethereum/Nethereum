using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.MainnetChain.Bootstrap;
using Nethereum.Model;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class EngineForkchoiceHeadersTests
    {
        private static byte[] Hash(byte seed)
        {
            var hash = new byte[32];
            hash[0] = seed;
            return hash;
        }

        private static BlockHeader Header(ulong number, byte rootSeed) => new BlockHeader
        {
            BlockNumber = number,
            StateRoot = Hash(rootSeed),
            ReceiptHash = Hash((byte)(rootSeed + 1)),
            ParentHash = Hash(0),
            Timestamp = 1_700_000_000 + (long)number * 12
        };

        [Fact]
        public void Given_NoForkchoiceUpdatedYet_When_TheTipIsRead_Then_NoTipIsOfferedSoTheFollowerHasNothingToExecute()
        {
            var source = new TrustedHeaderCanonicalSource(new EngineForkchoiceHeaders(), useOptimistic: true);

            var tip = source.GetLatestAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Null(tip);
        }

        [Fact]
        public async Task Given_AForkchoiceUpdatedHead_When_TheTipIsRead_Then_TheHeadIsTheOptimisticTip()
        {
            var provider = new EngineForkchoiceHeaders();
            provider.Publish(new EngineForkchoiceHead(Header(100, 7), Hash(100)), finalized: null);
            var source = new TrustedHeaderCanonicalSource(provider, useOptimistic: true);

            var tip = await source.GetLatestAsync(CancellationToken.None);

            Assert.Equal(100UL, tip.BlockNumber);
            Assert.Equal(Hash(100), tip.BlockHash);
            Assert.Equal(Hash(7), tip.StateRoot);
        }

        [Fact]
        public async Task Given_AForkchoiceUpdatedFinalizedBlock_When_TheFinalizedTipIsRead_Then_TheFinalizedBlockIsServedAndTheHeadIsNot()
        {
            var provider = new EngineForkchoiceHeaders();
            provider.Publish(new EngineForkchoiceHead(Header(100, 7), Hash(100)), new EngineForkchoiceHead(Header(68, 3), Hash(68)));
            var finalizedSource = new TrustedHeaderCanonicalSource(provider, useOptimistic: false);
            var optimisticSource = new TrustedHeaderCanonicalSource(provider, useOptimistic: true);

            var finalized = await finalizedSource.GetLatestAsync(CancellationToken.None);
            var optimistic = await optimisticSource.GetLatestAsync(CancellationToken.None);

            Assert.Equal(68UL, finalized.BlockNumber);
            Assert.Equal(100UL, optimistic.BlockNumber);
        }

        [Fact]
        public async Task Given_ASecondForkchoiceUpdatedToASiblingHead_When_TheTipIsRead_Then_TheTipMovesToTheSibling()
        {
            var provider = new EngineForkchoiceHeaders();
            var source = new TrustedHeaderCanonicalSource(provider, useOptimistic: true);
            provider.Publish(new EngineForkchoiceHead(Header(100, 7), Hash(100)), finalized: null);
            provider.Publish(new EngineForkchoiceHead(Header(100, 9), Hash(101)), finalized: null);

            var tip = await source.GetLatestAsync(CancellationToken.None);

            Assert.Equal(Hash(101), tip.BlockHash);
        }

        private sealed class Scenario
        {
            public IBlockStore Blocks = new InMemoryBlockStore();
            public EnginePayloadStash Stash = new EnginePayloadStash();
            public int PeerCalls;
            public BlockHeader PeerAnswer;

            public EngineHeaderResolver Resolver() => new EngineHeaderResolver(Blocks, Stash, (hash, ct) =>
            {
                PeerCalls++;
                return Task.FromResult(PeerAnswer);
            });
        }

        [Fact]
        public async Task Given_AHeadHeaderHeldInTheNewPayloadStash_When_Resolved_Then_ItIsServedFromTheStashWithoutAskingPeers()
        {
            var scenario = new Scenario { PeerAnswer = Header(500, 1) };
            var stashed = Header(200, 5);
            scenario.Stash.Put(new BlockBundle(stashed, new System.Collections.Generic.List<ISignedTransaction>(), null, null, Hash(200)));

            var resolved = await scenario.Resolver().ResolveAsync(Hash(200), CancellationToken.None);

            Assert.Same(stashed, resolved);
            Assert.Equal(0, scenario.PeerCalls);
        }

        [Fact]
        public async Task Given_AHeadHeaderHeldNowhereLocally_When_Resolved_Then_ItIsFetchedFromThePeersByHash()
        {
            var peerHeader = Header(300, 6);
            var scenario = new Scenario { PeerAnswer = peerHeader };

            var resolved = await scenario.Resolver().ResolveAsync(Nethereum.CoreChain.BlockHashCalculator.ForHeader(peerHeader), CancellationToken.None);

            Assert.Same(peerHeader, resolved);
            Assert.Equal(1, scenario.PeerCalls);
        }

        [Fact]
        public async Task Given_AHeadHeaderHeldNowhereAndNoPeerHasIt_When_Resolved_Then_NothingIsReturned()
        {
            var scenario = new Scenario { PeerAnswer = null };

            var resolved = await scenario.Resolver().ResolveAsync(Hash(77), CancellationToken.None);

            Assert.Null(resolved);
        }
    }
}
