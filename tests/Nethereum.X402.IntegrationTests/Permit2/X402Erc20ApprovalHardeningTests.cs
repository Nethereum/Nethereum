using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

[Collection("X402 DevChain E2E")]
public class X402Erc20ApprovalHardeningTests : IClassFixture<X402DevChainFixture>, IAsyncLifetime
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private const string FacilitatorKey = X402DevChainFixture.FacilitatorKey;
    private const string RevertingToken = "0x00000000000000000000000000000000000A9901";
    private static readonly BigInteger GoodMaxFee = 1_000_000_000;
    private static readonly BigInteger MaxSponsoredGasCost = BigInteger.Parse("10000000000000000");

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _fundedPayer = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402Erc20ApprovalHardeningTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    public async Task InitializeAsync()
    {
        await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
        await _chain.Node.SetCodeAsync(RevertingToken, X402Permit2DevChain.RevertsOnApprove);
        await X402Permit2DevChain.ApprovePermit2Async(_chain.Node, _fundedPayer, ChainId, _chain.TokenAddress);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Rule", "S2")]
    public async Task Given_AnApprovalWhoseGasLimitIsBelowWhatTheApproveNeeds_When_Verifying_Then_Erc20ApprovalTxGasLimitTooLow()
    {
        var payer = EthECKey.GenerateKey();
        var spec = Spec(payer) with { Gas = 30_000 };

        var verify = await VerifyApprovalAsync(payer, spec);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxGasLimitTooLow, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "S2")]
    public async Task Given_AnApprovalWhoseGasLimitCoversTheApprove_When_Verifying_Then_TheGasLimitRuleAccepts()
    {
        var payer = EthECKey.GenerateKey();

        var verify = await VerifyApprovalAsync(payer, Spec(payer));

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "S2")]
    public async Task Given_AnApproveThatRevertsWhenSimulated_When_Verifying_Then_Erc20ApprovalTxSimulationFailed()
    {
        var payer = EthECKey.GenerateKey();
        var spec = new SignedApprovalSpec(ChainId, RevertingToken, 0, GoodMaxFee);
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(payer, spec));
        var payload = X402Permit2DevChain.Payload(payer, ChainId, RevertingToken, Erc20ApprovalTransactions.Extension(info));

        var verify = await SponsoringService().VerifyPaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxSimulationFailed, verify.InvalidReason);
    }

    [Theory]
    [Trait("Rule", "S2")]
    [InlineData("ffffffffffffffffffffffff")]
    [InlineData("000000000000000000000001")]
    [InlineData("800000000000000000000000")]
    public async Task Given_AnApproveWhoseSpenderWordHasNonZeroUpperBytes_When_Verifying_Then_Erc20ApprovalTxInvalidCalldata(string upperBytes)
    {
        var payer = EthECKey.GenerateKey();
        var data = "0x095ea7b3" + upperBytes + X402Permit2Addresses.Permit2.Substring(2).ToLowerInvariant() + new string('f', 64);

        var verify = await VerifyApprovalAsync(payer, Spec(payer) with { Data = data });

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxInvalidCalldata, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "S2")]
    public async Task Given_ASponsoredApprovalAndAPermitDeadlineBelowTheMinimum_When_Verifying_Then_Permit2DeadlineExpired()
    {
        var payer = await PayerWithTokensAsync();
        var payload = SponsoredPayload(payer, deadlineSecondsFromNow: 60);

        var verify = await SponsoringService().VerifyPaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.Permit2DeadlineExpired, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "S2")]
    public async Task Given_ASponsoredApprovalAndAPermitDeadlineBeyondTheMinimum_When_Verifying_Then_ItIsValid()
    {
        var payer = await PayerWithTokensAsync();
        var payload = SponsoredPayload(payer, deadlineSecondsFromNow: 600);

        var verify = await SponsoringService().VerifyPaymentAsync(payload, payload.Accepted);

        Assert.True(verify.IsValid, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "S2")]
    public async Task Given_ALowerConfiguredMinimum_When_ASponsoredPermitDeadlineIsAboveIt_Then_ItIsValid()
    {
        var payer = await PayerWithTokensAsync();
        var payload = SponsoredPayload(payer, deadlineSecondsFromNow: 60);
        var service = SponsoringService(new Erc20ApprovalGasSponsoringOptions
        {
            Enabled = true,
            MaxSponsoredGasCost = MaxSponsoredGasCost,
            MinimumPermitDeadlineSeconds = 30
        });

        var verify = await service.VerifyPaymentAsync(payload, payload.Accepted);

        Assert.True(verify.IsValid, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "S2")]
    public async Task Given_NoSponsoredApproval_When_APermitDeadlineIsBelowTheSponsoringMinimum_Then_ItIsStillValid()
    {
        var payload = X402Permit2DevChain.Payload(_fundedPayer, ChainId, _chain.TokenAddress, deadlineSecondsFromNow: 60);

        var verify = await SponsoringService().VerifyPaymentAsync(payload, payload.Accepted);

        Assert.True(verify.IsValid, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "S2")]
    public void Given_TheSponsoringOptions_When_Created_Then_TheMinimumPermitDeadlineDefaultsToOneHundredTwentySeconds()
    {
        Assert.Equal(120, new Erc20ApprovalGasSponsoringOptions().MinimumPermitDeadlineSeconds);
    }

    [Fact]
    [Trait("Rule", "S2")]
    public void Given_ANegativeMinimumPermitDeadline_When_Validated_Then_ItIsRejected()
    {
        var options = new Erc20ApprovalGasSponsoringOptions { MinimumPermitDeadlineSeconds = -1 };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    [Trait("Rule", "S3")]
    public async Task Given_ALegacyTransactionAsTheSignedApproval_When_Verifying_Then_Erc20ApprovalTxUnsupportedType()
    {
        var payer = EthECKey.GenerateKey();
        var spec = Spec(payer);
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.SignLegacy(payer, spec));
        var payload = X402Permit2DevChain.Payload(payer, ChainId, _chain.TokenAddress, Erc20ApprovalTransactions.Extension(info));

        var verify = await SponsoringService().VerifyPaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxUnsupportedType, verify.InvalidReason);
    }

    [Theory]
    [Trait("Rule", "S3")]
    [InlineData("2930")]
    [InlineData("7702")]
    [InlineData("4844")]
    public async Task Given_ATypedNon1559TransactionAsTheSignedApproval_When_Verifying_Then_Erc20ApprovalTxUnsupportedType(string type)
    {
        var payer = EthECKey.GenerateKey();
        var spec = Spec(payer);
        var signed = type switch
        {
            "2930" => Erc20ApprovalTransactions.Sign2930(payer, spec),
            "7702" => Erc20ApprovalTransactions.Sign7702(payer, spec),
            _ => Erc20ApprovalTransactions.Sign4844(payer, spec)
        };
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, signed);
        var payload = X402Permit2DevChain.Payload(payer, ChainId, _chain.TokenAddress, Erc20ApprovalTransactions.Extension(info));

        var verify = await SponsoringService().VerifyPaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxUnsupportedType, verify.InvalidReason);
    }

    [Theory]
    [Trait("Rule", "S7")]
    [InlineData("Permitted")]
    [InlineData("PermittedAmount")]
    [InlineData("PermittedToken")]
    [InlineData("Witness")]
    [InlineData("WitnessTo")]
    [InlineData("WitnessValidAfter")]
    [InlineData("Nonce")]
    [InlineData("Deadline")]
    [InlineData("Signature")]
    public async Task Given_APayloadMissingAField_When_Verifying_Then_InvalidPayloadWithoutAnErrorLog(string missing)
    {
        var logger = new ListLogger();
        var payload = X402Permit2DevChain.Payload(_fundedPayer, ChainId, _chain.TokenAddress);
        var permit2 = (Permit2SchemePayload)payload.Payload!;
        var auth = permit2.Permit2Authorization;
        switch (missing)
        {
            case "Permitted": auth.Permitted = null!; break;
            case "PermittedAmount": auth.Permitted.Amount = null!; break;
            case "PermittedToken": auth.Permitted.Token = null!; break;
            case "Witness": auth.Witness = null!; break;
            case "WitnessTo": auth.Witness.To = null!; break;
            case "WitnessValidAfter": auth.Witness.ValidAfter = null!; break;
            case "Nonce": auth.Nonce = null!; break;
            case "Deadline": auth.Deadline = null!; break;
            case "Signature": permit2.Signature = null!; break;
        }

        var verify = await ServiceWith(logger).VerifyPaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.InvalidPayload, verify.InvalidReason);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);
    }

    [Theory]
    [Trait("Rule", "S7")]
    [InlineData("Nonce")]
    [InlineData("Deadline")]
    [InlineData("Amount")]
    [InlineData("ValidAfter")]
    public async Task Given_APayloadWithANonNumericField_When_Verifying_Then_InvalidPayloadLoggedAtDebug(string field)
    {
        var logger = new ListLogger();
        var payload = X402Permit2DevChain.Payload(_fundedPayer, ChainId, _chain.TokenAddress);
        var auth = ((Permit2SchemePayload)payload.Payload!).Permit2Authorization;
        switch (field)
        {
            case "Nonce": auth.Nonce = "abc"; break;
            case "Deadline": auth.Deadline = "12x"; break;
            case "Amount": auth.Permitted.Amount = "1e6"; break;
            case "ValidAfter": auth.Witness.ValidAfter = "yesterday"; break;
        }

        var verify = await ServiceWith(logger).VerifyPaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.InvalidPayload, verify.InvalidReason);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.Exception is FormatException or OverflowException);
    }

    [Fact]
    [Trait("Rule", "S7")]
    public async Task Given_ACancelledToken_When_Verifying_Then_TheVerificationIsCancelledBeforeAnyRpc()
    {
        var rpc = new InterceptingRpcClient(_chain.Client);
        var service = new X402ExactPermit2Service(FacilitatorKey, new Dictionary<int, IClient> { [ChainId] = rpc });
        var payload = X402Permit2DevChain.Payload(_fundedPayer, ChainId, _chain.TokenAddress);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.VerifyPaymentAsync(payload, payload.Accepted, cancellation.Token));

        Assert.Empty(rpc.Methods);
    }

    [Fact]
    [Trait("Rule", "S7")]
    public async Task Given_ATokenCancelledBetweenRpcSteps_When_Verifying_Then_NoFurtherRpcIsSent()
    {
        using var cancellation = new CancellationTokenSource();
        var rpc = new InterceptingRpcClient(_chain.Client, method =>
        {
            if (method == "eth_call")
                cancellation.Cancel();
            return Task.CompletedTask;
        });
        var service = new X402ExactPermit2Service(FacilitatorKey, new Dictionary<int, IClient> { [ChainId] = rpc });
        var payload = X402Permit2DevChain.Payload(_fundedPayer, ChainId, _chain.TokenAddress);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.VerifyPaymentAsync(payload, payload.Accepted, cancellation.Token));

        Assert.DoesNotContain("eth_getCode", rpc.Methods);
    }

    [Theory]
    [Trait("Rule", "S8")]
    [InlineData("From")]
    [InlineData("Asset")]
    [InlineData("Spender")]
    [InlineData("Amount")]
    [InlineData("SignedTransaction")]
    [InlineData("Version")]
    public async Task Given_AnErc20ApprovalInfoFieldWithATrailingNewline_When_Verifying_Then_InvalidErc20ApprovalExtensionFormat(string field)
    {
        var payer = EthECKey.GenerateKey();
        var spec = Spec(payer);
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(payer, spec));
        switch (field)
        {
            case "From": info.From += "\n"; break;
            case "Asset": info.Asset += "\n"; break;
            case "Spender": info.Spender += "\n"; break;
            case "Amount": info.Amount += "\n"; break;
            case "SignedTransaction": info.SignedTransaction += "\n"; break;
            case "Version": info.Version += "\n"; break;
        }
        var payload = X402Permit2DevChain.Payload(payer, ChainId, _chain.TokenAddress, Erc20ApprovalTransactions.Extension(info));

        var verify = await SponsoringService().VerifyPaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.InvalidErc20ApprovalExtensionFormat, verify.InvalidReason);
    }

    [Theory]
    [Trait("Rule", "S8")]
    [InlineData("From")]
    [InlineData("Asset")]
    [InlineData("Spender")]
    [InlineData("Amount")]
    [InlineData("Nonce")]
    [InlineData("Deadline")]
    [InlineData("Signature")]
    [InlineData("Version")]
    public async Task Given_AnEip2612InfoFieldWithATrailingNewline_When_Verifying_Then_InvalidEip2612ExtensionFormat(string field)
    {
        var payer = EthECKey.GenerateKey();
        var info = new Eip2612GasSponsoringInfo
        {
            From = payer.GetPublicAddress(),
            Asset = _chain.TokenAddress,
            Spender = X402Permit2Addresses.Permit2,
            Amount = X402Permit2DevChain.Amount,
            Nonce = "0",
            Deadline = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600).ToString(),
            Signature = "0x" + new string('1', 130),
            Version = "1"
        };
        switch (field)
        {
            case "From": info.From += "\n"; break;
            case "Asset": info.Asset += "\n"; break;
            case "Spender": info.Spender += "\n"; break;
            case "Amount": info.Amount += "\n"; break;
            case "Nonce": info.Nonce += "\n"; break;
            case "Deadline": info.Deadline += "\n"; break;
            case "Signature": info.Signature += "\n"; break;
            case "Version": info.Version += "\n"; break;
        }
        var extensions = new Dictionary<string, object>
        {
            [X402ExtensionKeys.Eip2612GasSponsoring] = new Dictionary<string, object> { ["info"] = info }
        };
        var payload = X402Permit2DevChain.Payload(payer, ChainId, _chain.TokenAddress, extensions);

        var verify = await SponsoringService().VerifyPaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.InvalidEip2612ExtensionFormat, verify.InvalidReason);
    }

    private SignedApprovalSpec Spec(EthECKey payer) => new(ChainId, _chain.TokenAddress, 0, GoodMaxFee);

    private async Task<VerificationResponse> VerifyApprovalAsync(EthECKey payer, SignedApprovalSpec spec)
    {
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(payer, spec));
        var payload = X402Permit2DevChain.Payload(payer, ChainId, _chain.TokenAddress, Erc20ApprovalTransactions.Extension(info));
        return await SponsoringService().VerifyPaymentAsync(payload, payload.Accepted);
    }

    private PaymentPayload SponsoredPayload(EthECKey payer, long deadlineSecondsFromNow)
    {
        var spec = Spec(payer);
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(payer, spec));
        return X402Permit2DevChain.Payload(
            payer, ChainId, _chain.TokenAddress, Erc20ApprovalTransactions.Extension(info), deadlineSecondsFromNow);
    }

    private async Task<EthECKey> PayerWithTokensAsync()
    {
        var payer = EthECKey.GenerateKey();
        await X402Permit2DevChain.GiveTokensAsync(
            _chain.Node, X402DevChainFixture.PayerKey, ChainId, _chain.TokenAddress, payer.GetPublicAddress(), 2_000_000);
        return payer;
    }

    private X402ExactPermit2Service SponsoringService(Erc20ApprovalGasSponsoringOptions? options = null) =>
        new(FacilitatorKey, new Dictionary<int, IClient> { [ChainId] = _chain.Client },
            options ?? new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = MaxSponsoredGasCost });

    private X402ExactPermit2Service ServiceWith(ILogger logger) =>
        new(FacilitatorKey, new Dictionary<int, IClient> { [ChainId] = _chain.Client }, logger);
}
