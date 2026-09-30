using System.Collections.Generic;
using System.Linq;
using Nethereum.AccountAbstraction.Bundler.Validation.ERC7562;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    /// <summary>
    /// ERC-7562 [OP-041]: <i>"Access to an address without deployed code is forbidden"</i> for
    /// <c>EXTCODE*</c> and <c>*CALL</c> opcodes. A precompile has no deployed code, so it is
    /// exempt - but only where it actually is a precompile, which is a per-fork question.
    ///
    /// <para>ERC-7562 [OP-062] then decides which precompiles a UserOperation may call:
    /// <i>"The core precompiles <c>0x1</c>-<c>0x11</c>."</i> Two different questions, two
    /// different sources, and a UserOperation must pass both.</para>
    /// </summary>
    public class ERC7562PrecompileExemptionTests
    {
        private const string BlsG1Add = "0x000000000000000000000000000000000000000b";
        private const string BlsMapFp2ToG2 = "0x0000000000000000000000000000000000000011";
        private const string OrdinaryAddress = "0x1234567890123456789012345678901234567890";

        private static readonly int[] PragueAndLater =
            Enumerable.Range(1, 0x11).ToArray();

        private static readonly int[] BeforePrague =
            Enumerable.Range(1, 0x0A).ToArray();

        private static ERC7562ValidationContext Context() =>
            new ERC7562ValidationContext
            {
                EntryPointAddress = "0x5ff137d4b0fdcd49dca30c7cf57e578a026d2789",
                Sender = new Erc4337Entity { Address = "0xSender", IsStaked = false },
                CurrentEntity = EntityType.Sender
            };

        private static IReadOnlyList<ERC7562Violation> CallWithNoCode(
            string target, IEnumerable<int> precompilesActiveAtThisFork)
        {
            var context = Context();
            var interceptor = new ERC7562TracingInterceptor(context, precompilesActiveAtThisFork);

            interceptor.OnCall("0xSender", target, 0, new byte[0], depth: 1, hasCode: false);

            return context.Violations;
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-041")]
        public void Given_AUserOperationCallingABlsPrecompile_When_ValidatedAtAForkThatHasIt_Then_ThereIsNoViolation()
        {
            Assert.Empty(CallWithNoCode(BlsG1Add, PragueAndLater));
            Assert.Empty(CallWithNoCode(BlsMapFp2ToG2, PragueAndLater));
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-041")]
        public void Given_AUserOperationCallingABlsPrecompile_When_ValidatedAtAForkWithoutIt_Then_ItIsAnOp041Violation()
        {
            var violations = CallWithNoCode(BlsG1Add, BeforePrague);

            Assert.Equal("OP-041", Assert.Single(violations).Rule);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-041")]
        public void Given_AnOrdinaryCodelessAddress_When_Called_Then_ItIsAnOp041Violation()
        {
            var violations = CallWithNoCode(OrdinaryAddress, PragueAndLater);

            Assert.Equal("OP-041", Assert.Single(violations).Rule);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-041")]
        public void Given_EveryPrecompileActiveAtTheFork_When_Called_Then_NoneIsAViolation()
        {
            for (var address = 1; address <= 0x11; address++)
            {
                var target = "0x" + address.ToString("x40");

                Assert.Empty(CallWithNoCode(target, PragueAndLater));
            }
        }
    }
}
