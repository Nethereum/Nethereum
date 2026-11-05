using System.Collections.Generic;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Precompiles;
using Xunit;

namespace Nethereum.EVM.UnitTests.Execution.Precompiles
{
    public class PrecompileRelocationResolverTests
    {
        private static readonly PrecompileRegistry Registry = DefaultPrecompileRegistries.OsakaBase();

        [Theory]
        [InlineData("0x0000000000000000000000000000000000000001", 1)]
        [InlineData("0x0000000000000000000000000000000000000006", 6)]
        [InlineData("0x000000000000000000000000000000000000000a", 10)]
        [InlineData("0x000000000000000000000000000000000000000A", 10)]
        [InlineData("0x0000000000000000000000000000000000000100", 0x100)]
        [InlineData("0000000000000000000000000000000000000004", 4)]
        public void Given_AnAddressWithAHandler_When_TheNaturalIndexIsResolved_Then_TheHandlerIndexIsReturned(string address, int expected)
        {
            var resolved = PrecompileRelocationResolver.TryResolve(null, Registry, address, out var index);

            Assert.True(resolved);
            Assert.Equal(expected, index);
        }

        [Theory]
        [InlineData("0x0000000000000000000000000000000000000000")]
        [InlineData("0x0000000000000000000000000000000000000200")]
        [InlineData("0x1000000000000000000000000000000000000004")]
        [InlineData("0x00000000000000000000000000000000ffffffff")]
        [InlineData("0x00000000000000000000000000000000000000zz")]
        [InlineData("0x00000000000000000000000000000000100000004")]
        [InlineData("")]
        public void Given_AnAddressWithoutAHandler_When_TheNaturalIndexIsResolved_Then_NothingIsResolved(string address)
        {
            var resolved = PrecompileRelocationResolver.TryResolve(null, Registry, address, out var index);

            Assert.False(resolved);
            Assert.True(index < 0 || !Registry.CanHandle(index));
        }

        [Fact]
        public void Given_ARelocatedAddress_When_ItIsResolved_Then_TheRelocationWinsOverTheNaturalIndex()
        {
            var relocations = new Dictionary<string, int> { ["0x0000000000000000000000000000000000000004"] = 6 };

            var resolved = PrecompileRelocationResolver.TryResolve(
                relocations, Registry, "0x0000000000000000000000000000000000000004", out var index);

            Assert.True(resolved);
            Assert.Equal(6, index);
        }

        [Fact]
        public void Given_AnAddressRelocatedAway_When_ItIsResolved_Then_ItIsNoLongerAPrecompile()
        {
            var relocations = new Dictionary<string, int> { ["0x0000000000000000000000000000000000000004"] = 0 };

            var resolved = PrecompileRelocationResolver.TryResolve(
                relocations, Registry, "0x0000000000000000000000000000000000000004", out _);

            Assert.False(resolved);
        }
    }
}
