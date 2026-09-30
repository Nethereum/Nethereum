using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Sequencer.ProducerAuthority;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.Sequencer.UnitTests
{
    public class InMemorySequencerArbiterTests
    {
        private sealed class MutableClock
        {
            public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            public DateTimeOffset Read() => Now;
        }

        private static InMemorySequencerArbiter NewArbiter(MutableClock clock, TimeSpan? ttl = null) =>
            new InMemorySequencerArbiter(ttl ?? TimeSpan.FromSeconds(30), clock.Read);

        [Fact]
        public async Task Given_NoHolder_When_ACandidateAcquires_Then_ItGetsAFreshToken()
        {
            var clock = new MutableClock();
            var arbiter = NewArbiter(clock);

            var grant = await arbiter.TryAcquireOrRenewAsync("A");

            Assert.NotNull(grant);
            Assert.Equal("A", grant!.NodeId);
            Assert.True(grant.FencingToken > 0);
        }

        [Fact]
        public async Task Given_AnActiveHolder_When_ADifferentCandidateTriesToAcquire_Then_ItIsRefused()
        {
            var clock = new MutableClock();
            var arbiter = NewArbiter(clock);
            await arbiter.TryAcquireOrRenewAsync("A");

            var grant = await arbiter.TryAcquireOrRenewAsync("B");

            Assert.Null(grant);
        }

        [Fact]
        public async Task Given_AnActiveHolder_When_TheSameHolderRenews_Then_TheTokenIsUnchangedButExpiryIsPushed()
        {
            var clock = new MutableClock();
            var arbiter = NewArbiter(clock);
            var first = await arbiter.TryAcquireOrRenewAsync("A");

            clock.Now = clock.Now.AddSeconds(5);
            var renewed = await arbiter.TryAcquireOrRenewAsync("A");

            Assert.Equal(first!.FencingToken, renewed!.FencingToken);
            Assert.True(renewed.ExpiresAtUtc > first.ExpiresAtUtc);
        }

        [Fact]
        public async Task Given_AHoldersLeaseHasExpired_When_ADifferentCandidateAcquires_Then_ItGetsAHigherToken()
        {
            var clock = new MutableClock();
            var arbiter = NewArbiter(clock, TimeSpan.FromSeconds(10));
            var first = await arbiter.TryAcquireOrRenewAsync("A");

            clock.Now = clock.Now.AddSeconds(11);
            var second = await arbiter.TryAcquireOrRenewAsync("B");

            Assert.NotNull(second);
            Assert.Equal("B", second!.NodeId);
            Assert.True(second.FencingToken > first!.FencingToken);
        }

        [Fact]
        public async Task Given_AHoldersLeaseHasExpired_When_TheSameHolderReacquires_Then_ItGetsAHigherToken()
        {
            var clock = new MutableClock();
            var arbiter = NewArbiter(clock, TimeSpan.FromSeconds(10));
            var first = await arbiter.TryAcquireOrRenewAsync("A");

            clock.Now = clock.Now.AddSeconds(11);
            var second = await arbiter.TryAcquireOrRenewAsync("A");

            Assert.True(second!.FencingToken > first!.FencingToken);
        }

        [Fact]
        public async Task Given_AHolder_When_ItIsForceRevoked_Then_TheNextAcquireByAnyoneGetsAFreshToken()
        {
            var clock = new MutableClock();
            var arbiter = NewArbiter(clock);
            var first = await arbiter.TryAcquireOrRenewAsync("A");

            arbiter.ForceRevoke("A");
            var second = await arbiter.TryAcquireOrRenewAsync("B");

            Assert.NotNull(second);
            Assert.Equal("B", second!.NodeId);
            Assert.True(second.FencingToken > first!.FencingToken);
        }

        [Fact]
        public async Task Given_AHolder_When_ItReleasesWithTheCorrectFencingToken_Then_TheLeaseBecomesAvailableImmediately()
        {
            var clock = new MutableClock();
            var arbiter = NewArbiter(clock);
            var grant = await arbiter.TryAcquireOrRenewAsync("A");

            await arbiter.ReleaseAsync("A", grant!.FencingToken);
            var second = await arbiter.TryAcquireOrRenewAsync("B");

            Assert.NotNull(second);
            Assert.Equal("B", second!.NodeId);
        }

        [Fact]
        public async Task Given_AReleaseCallWithAStaleFencingToken_When_Called_Then_TheActiveLeaseIsUnaffected()
        {
            var clock = new MutableClock();
            var arbiter = NewArbiter(clock);
            await arbiter.TryAcquireOrRenewAsync("A");

            await arbiter.ReleaseAsync("A", fencingToken: -1);
            var stillRefused = await arbiter.TryAcquireOrRenewAsync("B");

            Assert.Null(stillRefused);
        }

        [Fact]
        public async Task Given_ALeaseHasBeenGranted_When_CurrentAsyncIsAsked_Then_ItReportsTheHolderAndToken()
        {
            var clock = new MutableClock();
            var arbiter = NewArbiter(clock);
            var grant = await arbiter.TryAcquireOrRenewAsync("A");

            var status = await arbiter.CurrentAsync();

            Assert.Equal("A", status.NodeId);
            Assert.Equal(grant!.FencingToken, status.FencingToken);
            Assert.Equal(grant.ExpiresAtUtc, status.ExpiresAtUtc);
        }
    }

    public class ArbiterBackedProducerAuthorityTests
    {
        private sealed class MutableClock
        {
            public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            public DateTimeOffset Read() => Now;
        }

        private sealed class FlakyArbiter : ISequencerArbiter
        {
            private readonly ISequencerArbiter _inner;
            public bool FailNextPoll;

            public FlakyArbiter(ISequencerArbiter inner) => _inner = inner;

            public Task<LeaseGrant?> TryAcquireOrRenewAsync(string candidateNodeId, System.Threading.CancellationToken ct = default)
            {
                if (FailNextPoll)
                {
                    FailNextPoll = false;
                    throw new InvalidOperationException("simulated network partition");
                }
                return _inner.TryAcquireOrRenewAsync(candidateNodeId, ct);
            }

            public Task ReleaseAsync(string nodeId, long fencingToken, System.Threading.CancellationToken ct = default)
                => _inner.ReleaseAsync(nodeId, fencingToken, ct);

            public Task<SequencerStatus> CurrentAsync(System.Threading.CancellationToken ct = default)
                => _inner.CurrentAsync(ct);
        }

        [Fact]
        public async Task Given_AFreshGrant_When_CurrentProducerIsAsked_Then_ItNamesThisNode()
        {
            var clock = new MutableClock();
            var arbiter = new InMemorySequencerArbiter(TimeSpan.FromSeconds(30), clock.Read);
            var authority = new ArbiterBackedProducerAuthority(
                arbiter, "A", TimeSpan.FromSeconds(5), clock: clock.Read, startBackgroundLoop: false);

            await authority.PollOnceAsync();

            Assert.Equal("A", authority.CurrentProducer());
        }

        [Fact]
        public async Task Given_ArbiterFailsBriefly_When_TheCachedGrantHasNotYetLocallyExpired_Then_ItStillNamesThisNode()
        {
            var clock = new MutableClock();
            var inner = new InMemorySequencerArbiter(TimeSpan.FromSeconds(30), clock.Read);
            var flaky = new FlakyArbiter(inner);
            var authority = new ArbiterBackedProducerAuthority(
                flaky, "A", TimeSpan.FromSeconds(5), clock: clock.Read, startBackgroundLoop: false);
            await authority.PollOnceAsync();

            clock.Now = clock.Now.AddSeconds(2);
            flaky.FailNextPoll = true;
            var stillHeldDespiteTheFailedPoll = await authority.PollOnceAsync();

            Assert.True(stillHeldDespiteTheFailedPoll);
            Assert.Equal("A", authority.CurrentProducer());
        }

        [Fact]
        public async Task Given_TheArbiterStaysUnreachablePastTheCachedExpiry_When_CurrentProducerIsAsked_Then_ItReturnsNull()
        {
            var clock = new MutableClock();
            var inner = new InMemorySequencerArbiter(TimeSpan.FromSeconds(5), clock.Read);
            var flaky = new FlakyArbiter(inner);
            var authority = new ArbiterBackedProducerAuthority(
                flaky, "A", TimeSpan.FromSeconds(1), clock: clock.Read, startBackgroundLoop: false);
            await authority.PollOnceAsync();

            clock.Now = clock.Now.AddSeconds(6);

            Assert.Null(authority.CurrentProducer());
        }

        [Fact]
        public async Task Given_TheArbiterExplicitlyNamesADifferentHolder_When_CurrentProducerIsAskedAfterAPoll_Then_ItReturnsNullImmediately()
        {
            var clock = new MutableClock();
            var arbiter = new InMemorySequencerArbiter(TimeSpan.FromSeconds(30), clock.Read);
            var authorityA = new ArbiterBackedProducerAuthority(
                arbiter, "A", TimeSpan.FromSeconds(5), clock: clock.Read, startBackgroundLoop: false);
            await authorityA.PollOnceAsync();

            arbiter.ForceRevoke("A");
            var authorityB = new ArbiterBackedProducerAuthority(
                arbiter, "B", TimeSpan.FromSeconds(5), clock: clock.Read, startBackgroundLoop: false);
            await authorityB.PollOnceAsync();

            await authorityA.PollOnceAsync();

            Assert.Null(authorityA.CurrentProducer());
            Assert.Equal("B", authorityB.CurrentProducer());
        }

        [Fact]
        public void Given_NoPollHasEverSucceeded_When_CurrentProducerIsAsked_Then_ItReturnsNull()
        {
            var clock = new MutableClock();
            var arbiter = new InMemorySequencerArbiter(TimeSpan.FromSeconds(30), clock.Read);
            var authority = new ArbiterBackedProducerAuthority(
                arbiter, "A", TimeSpan.FromSeconds(5), clock: clock.Read, startBackgroundLoop: false);

            Assert.Null(authority.CurrentProducer());
        }
    }

    public class BlockProducerLeaseGateTests : IDisposable
    {
        private readonly SequencerTestFixture _fixture;
        private readonly AppChainCore _appChain;

        public BlockProducerLeaseGateTests()
        {
            _fixture = new SequencerTestFixture();
            _appChain = _fixture.CreateAppChain();
        }

        public void Dispose() => _fixture.Dispose();

        private ProducerAuthorityBlockProductionStrategy StrategyNaming(string? holder, string ourNodeId) =>
            new ProducerAuthorityBlockProductionStrategy(
                new StaticAuthority(holder),
                ourNodeId,
                new DefaultBlockProductionStrategy(new ChainConfig { ChainId = _appChain.Config.ChainId }));

        private sealed class StaticAuthority : IProducerAuthority
        {
            private readonly string? _holder;
            public StaticAuthority(string? holder) => _holder = holder;
            public string? CurrentProducer() => _holder;
        }

        [Fact]
        public async Task Given_TheStrategyNamesThisNodeAsHolder_When_ProduceBlockAsyncIsCalled_Then_ItSealsABlock()
        {
            var sequencer = new Sequencer(_appChain, new SequencerConfig(),
                blockProductionStrategy: StrategyNaming("A", "A"));
            await sequencer.StartAsync();

            var blockHash = await sequencer.ProduceBlockAsync();

            Assert.NotNull(blockHash);
            Assert.Equal(BigInteger.One, await sequencer.GetBlockNumberAsync());
            await sequencer.StopAsync();
        }

        [Fact]
        public async Task Given_TheStrategyNamesADifferentNodeAsHolder_When_ProduceBlockAsyncIsCalled_Then_ItThrowsAndSealsNoBlock()
        {
            var sequencer = new Sequencer(_appChain, new SequencerConfig(),
                blockProductionStrategy: StrategyNaming("B", "A"));
            await sequencer.StartAsync();

            await Assert.ThrowsAsync<SequencerLeaseNotHeldException>(() => sequencer.ProduceBlockAsync());

            Assert.Equal(BigInteger.Zero, await sequencer.GetBlockNumberAsync());
            await sequencer.StopAsync();
        }

        [Fact]
        public async Task Given_NoStrategyIsConfigured_When_ProduceBlockAsyncIsCalled_Then_ItAlwaysSeals()
        {
            var sequencer = new Sequencer(_appChain, new SequencerConfig(), blockProductionStrategy: null);
            await sequencer.StartAsync();

            var blockHash = await sequencer.ProduceBlockAsync();

            Assert.NotNull(blockHash);
            Assert.Equal(BigInteger.One, await sequencer.GetBlockNumberAsync());
            await sequencer.StopAsync();
        }

        [Fact]
        public async Task Given_TheHolderLosesTheLeaseBetweenBlocks_When_ItTriesToSealAgain_Then_TheGateRejectsAndTheHeightDoesNotAdvance()
        {
            var clock = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var arbiter = new InMemorySequencerArbiter(TimeSpan.FromSeconds(30), () => clock);
            var authority = new ArbiterBackedProducerAuthority(
                arbiter, "A", TimeSpan.FromSeconds(5), clock: () => clock, startBackgroundLoop: false);
            await authority.PollOnceAsync();

            var strategy = new ProducerAuthorityBlockProductionStrategy(
                authority, "A", new DefaultBlockProductionStrategy(new ChainConfig { ChainId = _appChain.Config.ChainId }));
            var sequencer = new Sequencer(_appChain, new SequencerConfig(), blockProductionStrategy: strategy);
            await sequencer.StartAsync();

            await sequencer.ProduceBlockAsync();
            Assert.Equal(BigInteger.One, await sequencer.GetBlockNumberAsync());

            arbiter.ForceRevoke("A");
            var otherAuthority = new ArbiterBackedProducerAuthority(
                arbiter, "B", TimeSpan.FromSeconds(5), clock: () => clock, startBackgroundLoop: false);
            await otherAuthority.PollOnceAsync();
            await authority.PollOnceAsync();

            await Assert.ThrowsAsync<SequencerLeaseNotHeldException>(() => sequencer.ProduceBlockAsync());
            Assert.Equal(BigInteger.One, await sequencer.GetBlockNumberAsync());

            await sequencer.StopAsync();
        }
    }
}
