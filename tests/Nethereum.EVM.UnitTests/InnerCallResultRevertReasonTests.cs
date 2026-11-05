using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class InnerCallResultRevertReasonTests
    {
        private const string ErrorOfNope = "08c379a0"
            + "0000000000000000000000000000000000000000000000000000000000000020"
            + "0000000000000000000000000000000000000000000000000000000000000004"
            + "6e6f706500000000000000000000000000000000000000000000000000000000";

        [Fact]
        public void Given_a_reverted_call_with_Error_string_output_When_the_reason_is_read_Then_it_is_decoded_from_the_output()
        {
            var result = new InnerCallResult { Reverted = true, Output = ErrorOfNope.HexToByteArray() };

            Assert.Equal("nope", result.RevertReason);
        }

        [Fact]
        public void Given_a_call_that_did_not_revert_When_the_reason_is_read_Then_it_is_null_even_with_output()
        {
            var result = new InnerCallResult { Reverted = false, Output = ErrorOfNope.HexToByteArray() };

            Assert.Null(result.RevertReason);
        }

        [Fact]
        public void Given_a_reverted_call_with_output_shorter_than_a_selector_When_the_reason_is_read_Then_it_is_null()
        {
            var result = new InnerCallResult { Reverted = true, Output = new byte[] { 1, 2, 3, 4 } };

            Assert.Null(result.RevertReason);
        }

        [Fact]
        public void Given_a_reason_assigned_explicitly_When_it_is_read_Then_the_assigned_value_wins_over_the_output()
        {
            var result = new InnerCallResult { Reverted = true, Output = ErrorOfNope.HexToByteArray(), RevertReason = "assigned" };

            Assert.Equal("assigned", result.RevertReason);
        }
    }
}
