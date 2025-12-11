using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

public class X402ErrorCodesTests
{
    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInsufficientFunds_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InsufficientFunds;
        Assert.Equal("insufficient_funds", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInvalidSignature_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InvalidSignature;
        Assert.Equal("invalid_exact_evm_payload_signature", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInvalidValidAfter_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InvalidValidAfter;
        Assert.Equal("invalid_exact_evm_payload_authorization_valid_after", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInvalidValidBefore_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InvalidValidBefore;
        Assert.Equal("invalid_exact_evm_payload_authorization_valid_before", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInvalidValue_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InvalidValue;
        Assert.Equal("invalid_exact_evm_payload_authorization_value", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingRecipientMismatch_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.RecipientMismatch;
        Assert.Equal("invalid_exact_evm_payload_recipient_mismatch", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInvalidNetwork_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InvalidNetwork;
        Assert.Equal("invalid_network", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInvalidPayload_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InvalidPayload;
        Assert.Equal("invalid_payload", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInvalidScheme_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InvalidScheme;
        Assert.Equal("invalid_scheme", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingUnsupportedScheme_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.UnsupportedScheme;
        Assert.Equal("unsupported_scheme", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingNonceAlreadyUsed_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.NonceAlreadyUsed;
        Assert.Equal("invalid_exact_evm_payload_authorization_nonce_used", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInvalidPaymentRequirements_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InvalidPaymentRequirements;
        Assert.Equal("invalid_payment_requirements", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInvalidX402Version_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InvalidX402Version;
        Assert.Equal("invalid_x402_version", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingInvalidTransactionState_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.InvalidTransactionState;
        Assert.Equal("invalid_transaction_state", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingUnexpectedVerifyError_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.UnexpectedVerifyError;
        Assert.Equal("unexpected_verify_error", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingUnexpectedSettleError_Then_CorrectValueIsReturned()
    {
        var errorCode = X402ErrorCodes.UnexpectedSettleError;
        Assert.Equal("unexpected_settle_error", errorCode);
    }

    [Fact]
    public void Given_X402ErrorCodes_When_CheckingAllCodes_Then_AllFollowSnakeCaseConvention()
    {
        var errorCodeType = typeof(X402ErrorCodes);
        var fields = errorCodeType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

        foreach (var field in fields)
        {
            var value = (string)field.GetValue(null)!;
            Assert.Matches("^[a-z0-9_]+$", value);
        }
    }

    [Fact]
    public void Given_X402ErrorCodes_When_AccessingConstants_Then_ValuesAreNotNull()
    {
        Assert.NotNull(X402ErrorCodes.InsufficientFunds);
        Assert.NotNull(X402ErrorCodes.InvalidSignature);
        Assert.NotNull(X402ErrorCodes.InvalidValidAfter);
        Assert.NotNull(X402ErrorCodes.InvalidValidBefore);
        Assert.NotNull(X402ErrorCodes.InvalidValue);
        Assert.NotNull(X402ErrorCodes.RecipientMismatch);
        Assert.NotNull(X402ErrorCodes.InvalidNetwork);
        Assert.NotNull(X402ErrorCodes.InvalidPayload);
        Assert.NotNull(X402ErrorCodes.InvalidScheme);
        Assert.NotNull(X402ErrorCodes.UnsupportedScheme);
        Assert.NotNull(X402ErrorCodes.NonceAlreadyUsed);
        Assert.NotNull(X402ErrorCodes.InvalidPaymentRequirements);
        Assert.NotNull(X402ErrorCodes.InvalidX402Version);
        Assert.NotNull(X402ErrorCodes.InvalidTransactionState);
        Assert.NotNull(X402ErrorCodes.UnexpectedVerifyError);
        Assert.NotNull(X402ErrorCodes.UnexpectedSettleError);
    }
}
