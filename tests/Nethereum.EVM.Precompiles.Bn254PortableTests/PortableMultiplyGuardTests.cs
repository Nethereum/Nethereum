using Xunit;

namespace Nethereum.EVM.Core.Tests.Bn254
{
    public class PortableMultiplyGuardTests
    {
        [Fact]
        public void Given_the_portable_build_When_the_sources_are_compiled_Then_the_forced_fallback_define_is_set()
        {
#if BN254_PORTABLE_MULTIPLY
            Assert.True(true);
#else
            Assert.Fail("BN254_PORTABLE_MULTIPLY is not defined, so the 32-bit-limb multiply is not under test");
#endif
        }
    }
}
