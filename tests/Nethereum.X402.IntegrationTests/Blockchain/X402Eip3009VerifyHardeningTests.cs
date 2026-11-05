using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

public class X402Eip3009VerifyHardeningTests
{
    private const string Token = "0x5FbDB2315678afecb367f032d93F642f64180aa3";
    private const string FacilitatorKey = X402DevChainFixture.FacilitatorKey;

    [Theory]
    [Trait("Rule", "V10")]
    [InlineData("Signature")]
    [InlineData("From")]
    [InlineData("To")]
    [InlineData("Value")]
    [InlineData("ValidAfter")]
    [InlineData("ValidBefore")]
    [InlineData("Nonce")]
    [InlineData("Authorization")]
    public async Task Given_APayloadMissingAField_When_Verifying_Then_InvalidPayloadWithoutAnErrorLog(string missing)
    {
        var logger = new ListLogger();
        var rpc = new ScriptedTokenRpcClient();
        var (payload, requirements) = await PayloadAsync();
        var exact = (ExactSchemePayload)payload.Payload;
        switch (missing)
        {
            case "Signature": exact.Signature = null!; break;
            case "From": exact.Authorization.From = null!; break;
            case "To": exact.Authorization.To = null!; break;
            case "Value": exact.Authorization.Value = null!; break;
            case "ValidAfter": exact.Authorization.ValidAfter = null!; break;
            case "ValidBefore": exact.Authorization.ValidBefore = null!; break;
            case "Nonce": exact.Authorization.Nonce = null!; break;
            case "Authorization": exact.Authorization = null!; break;
        }

        var verify = await ServiceWith(rpc, logger).VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidPayload, verify.InvalidReason);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Empty(rpc.Methods);
    }

    [Theory]
    [Trait("Rule", "V10")]
    [InlineData("Value")]
    [InlineData("ValidAfter")]
    [InlineData("ValidBefore")]
    [InlineData("Amount")]
    public async Task Given_APayloadWithANonNumericField_When_Verifying_Then_InvalidPayloadLoggedAtDebug(string field)
    {
        var logger = new ListLogger();
        var rpc = new ScriptedTokenRpcClient();
        var (payload, requirements) = await PayloadAsync();
        var authorization = ((ExactSchemePayload)payload.Payload).Authorization;
        switch (field)
        {
            case "Value": authorization.Value = "1e6"; requirements.Amount = "1e6"; break;
            case "ValidAfter": authorization.ValidAfter = "yesterday"; break;
            case "ValidBefore": authorization.ValidBefore = "12x"; break;
            case "Amount": requirements.Amount = "abc"; break;
        }

        var verify = await ServiceWith(rpc, logger).VerifyPaymentAsync(payload, requirements);

        Assert.Equal(X402ErrorCodes.InvalidPayload, verify.InvalidReason);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Exception is FormatException or OverflowException);
        Assert.Empty(rpc.Methods);
    }

    [Theory]
    [Trait("Rule", "V9")]
    [InlineData("+1000000")]
    [InlineData(" 1000000")]
    [InlineData("1000000 ")]
    [InlineData("1000000\n")]
    [InlineData("-1")]
    [InlineData("0x10")]
    [InlineData("1_000")]
    [InlineData("١٠٠٠٠٠٠")]
    public async Task Given_AValueThatIsNotPlainAsciiDigits_When_Verifying_Then_InvalidPayload(string value)
    {
        var (payload, requirements) = await PayloadAsync();
        ((ExactSchemePayload)payload.Payload).Authorization.Value = value;

        var verify = await ServiceWith(new ScriptedTokenRpcClient(), new ListLogger()).VerifyPaymentAsync(payload, requirements);

        Assert.Equal(X402ErrorCodes.InvalidPayload, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V10")]
    public async Task Given_ACancelledToken_When_Verifying_Then_TheCancellationPropagatesBeforeAnyRpc()
    {
        var rpc = new ScriptedTokenRpcClient();
        var (payload, requirements) = await PayloadAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ServiceWith(rpc, new ListLogger()).VerifyPaymentAsync(payload, requirements, cancellation.Token));

        Assert.Empty(rpc.Methods);
    }

    private static async Task<(PaymentPayload Payload, PaymentRequirements Requirements)> PayloadAsync()
    {
        var requirements = X402TokenPayments.Requirements(Token, "1000000");
        return (await X402TokenPayments.SignedTransferAsync(Token, requirements), requirements);
    }

    private static X402TransferWithAuthorisation3009Service ServiceWith(IClient client, ILogger logger) =>
        new(FacilitatorKey, new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = client }, logger);
}
