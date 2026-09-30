using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Validation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    public class BundlerErrorCodesTests
    {
        [Theory]
        [InlineData(-32700, nameof(Erc7769ErrorCodes.ParseError))]
        [InlineData(-32600, nameof(Erc7769ErrorCodes.InvalidRequest))]
        [InlineData(-32601, nameof(Erc7769ErrorCodes.MethodNotFound))]
        [InlineData(-32602, nameof(Erc7769ErrorCodes.InvalidFields))]
        [InlineData(-32603, nameof(Erc7769ErrorCodes.InternalError))]
        [InlineData(-32500, nameof(Erc7769ErrorCodes.SimulateValidation))]
        [InlineData(-32501, nameof(Erc7769ErrorCodes.SimulatePaymasterValidation))]
        [InlineData(-32502, nameof(Erc7769ErrorCodes.OpcodeValidation))]
        [InlineData(-32503, nameof(Erc7769ErrorCodes.NotInTimeRange))]
        [InlineData(-32504, nameof(Erc7769ErrorCodes.Reputation))]
        [InlineData(-32505, nameof(Erc7769ErrorCodes.InsufficientStake))]
        [InlineData(-32506, nameof(Erc7769ErrorCodes.UnsupportedSignatureAggregator))]
        [InlineData(-32507, nameof(Erc7769ErrorCodes.InvalidSignature))]
        [InlineData(-32508, nameof(Erc7769ErrorCodes.PaymasterDepositTooLow))]
        [InlineData(-32521, nameof(Erc7769ErrorCodes.UserOperationReverted))]
        public void KnownErc7769Code_MatchesSpecValue(int expected, string constantName)
        {
            var actual = (int)typeof(Erc7769ErrorCodes).GetField(constantName)!.GetRawConstantValue()!;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void BundlerErrorCodes_MatchesErc7769ErrorCodes_ForEveryValue()
        {
            Assert.Equal(Erc7769ErrorCodes.ParseError, BundlerErrorCodes.ParseError);
            Assert.Equal(Erc7769ErrorCodes.InvalidRequest, BundlerErrorCodes.InvalidRequest);
            Assert.Equal(Erc7769ErrorCodes.MethodNotFound, BundlerErrorCodes.MethodNotFound);
            Assert.Equal(Erc7769ErrorCodes.InvalidFields, BundlerErrorCodes.InvalidFields);
            Assert.Equal(Erc7769ErrorCodes.InternalError, BundlerErrorCodes.InternalError);
            Assert.Equal(Erc7769ErrorCodes.SimulateValidation, BundlerErrorCodes.SimulateValidation);
            Assert.Equal(Erc7769ErrorCodes.SimulatePaymasterValidation, BundlerErrorCodes.SimulatePaymasterValidation);
            Assert.Equal(Erc7769ErrorCodes.OpcodeValidation, BundlerErrorCodes.OpcodeValidation);
            Assert.Equal(Erc7769ErrorCodes.NotInTimeRange, BundlerErrorCodes.NotInTimeRange);
            Assert.Equal(Erc7769ErrorCodes.Reputation, BundlerErrorCodes.Reputation);
            Assert.Equal(Erc7769ErrorCodes.InsufficientStake, BundlerErrorCodes.InsufficientStake);
            Assert.Equal(Erc7769ErrorCodes.UnsupportedSignatureAggregator, BundlerErrorCodes.UnsupportedSignatureAggregator);
            Assert.Equal(Erc7769ErrorCodes.InvalidSignature, BundlerErrorCodes.InvalidSignature);
            Assert.Equal(Erc7769ErrorCodes.PaymasterDepositTooLow, BundlerErrorCodes.PaymasterDepositTooLow);
            Assert.Equal(Erc7769ErrorCodes.UserOperationReverted, BundlerErrorCodes.UserOperationReverted);
        }
    }
}
