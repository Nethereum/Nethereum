using System;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

public class Permit2SettlementErrorMapperTests
{
    private static SmartContractCustomErrorRevertException Revert(Type errorType)
    {
        var selector = ABITypedRegistry.GetError(errorType).Sha3Signature;
        if (selector.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) selector = selector.Substring(2);
        var data = "0x" + selector.Substring(0, 8) + new string('0', 64);
        return new SmartContractCustomErrorRevertException(data);
    }

    [Theory]
    [InlineData(typeof(InvalidNonceError), "invalid_exact_evm_nonce_already_used")]
    [InlineData(typeof(SignatureExpiredError), "permit2_deadline_expired")]
    [InlineData(typeof(AllowanceExpiredError), "permit2_allowance_required")]
    [InlineData(typeof(InsufficientAllowanceError), "permit2_allowance_required")]
    [InlineData(typeof(InvalidAmountError), "permit2_amount_mismatch")]
    [InlineData(typeof(InvalidSignerError), "invalid_permit2_signature")]
    [InlineData(typeof(InvalidContractSignatureError), "invalid_permit2_signature")]
    [InlineData(typeof(InvalidSignatureError), "invalid_permit2_signature")]
    [InlineData(typeof(InvalidSignatureLengthError), "invalid_permit2_signature")]
    public void MapReason_DecodesPermit2CustomError_ToSpecificCode(Type errorType, string expected)
    {
        Assert.Equal(expected, Permit2SettlementErrorMapper.MapReason(Revert(errorType)));
    }

    [Theory]
    [InlineData(typeof(LengthMismatchError))]
    [InlineData(typeof(ExcessiveInvalidationError))]
    public void MapReason_ReturnsNull_ForUnmappedPermit2Error(Type errorType)
    {
        Assert.Null(Permit2SettlementErrorMapper.MapReason(Revert(errorType)));
    }

    [Fact]
    public void MapReason_ReturnsNull_ForNonPermit2Revert()
    {
        var notPermit2 = new SmartContractCustomErrorRevertException("0xdeadbeef" + new string('0', 64));
        Assert.Null(Permit2SettlementErrorMapper.MapReason(notPermit2));
    }
}
