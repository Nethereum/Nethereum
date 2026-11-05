using System;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class WarmPrecompileSetTests
    {
        private static readonly EvmAddress NotAPrecompile = EvmAddress.FromHex("0x00000000000000000000000000000000000a0001");

        private static ExecutionStateService StateWarmedFor(HardforkName fork)
        {
            var state = new ExecutionStateService(new InMemoryStateReader(null));
            state.MarkPrecompilesAsWarm(DefaultHardforkConfigs.For(fork).Precompiles);
            return state;
        }

        private static EvmAddress PrecompileAddress(int number) =>
            EvmAddress.From(new[] { (byte)(number >> 24), (byte)(number >> 16), (byte)(number >> 8), (byte)number });

        [Theory]
        [InlineData(HardforkName.Istanbul)]
        [InlineData(HardforkName.Cancun)]
        [InlineData(HardforkName.Osaka)]
        public void Given_PrecompilesMarkedWarm_When_EveryAddressUpToTheSpecIsAsked_Then_ExactlyTheForksPrecompilesAreWarm(HardforkName fork)
        {
            var state = StateWarmedFor(fork);
            var precompiles = DefaultHardforkConfigs.For(fork).Precompiles;

            for (var number = 0; number < 0x200; number++)
                Assert.Equal(precompiles.CanHandle(number), state.AddressIsWarm(PrecompileAddress(number)));
        }

        [Fact]
        public void Given_PrecompilesMarkedWarm_When_AnAddressOutsideTheRegistryIsAsked_Then_ItIsCold()
        {
            var state = StateWarmedFor(HardforkName.Osaka);

            Assert.False(state.AddressIsWarm(NotAPrecompile));
            Assert.False(state.AddressIsWarm(EvmAddress.FromHex("0x0100000000000000000000000000000000000001")));
        }

        [Fact]
        public void Given_PrecompilesNeverMarkedWarm_When_APrecompileAddressIsAsked_Then_ItIsCold()
        {
            var state = new ExecutionStateService(new InMemoryStateReader(null));

            Assert.False(state.AddressIsWarm(PrecompileAddress(1)));
        }

        [Fact]
        public void Given_PrecompilesWarmedBeforeASnapshot_When_TheSnapshotWarmsAndRevertsAPrecompileAddress_Then_ThePrecompileStaysWarm()
        {
            var state = StateWarmedFor(HardforkName.Osaka);
            var snapshot = state.TakeSnapshot();

            state.MarkAddressAsWarm(PrecompileAddress(2));
            state.MarkAddressAsWarm(NotAPrecompile);
            state.RevertToSnapshot(snapshot);

            Assert.True(state.AddressIsWarm(PrecompileAddress(2)));
            Assert.False(state.AddressIsWarm(NotAPrecompile));
        }
    }
}
